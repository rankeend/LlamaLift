using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text;
using System.IO.Compression;

namespace LlamaServerManager
{
    internal static class SmokeTests
    {
        private static int failures;

        private static void Main()
        {
            ModelProfile generic = ModelProfile.CreateGenericProfile();
            Check(string.IsNullOrWhiteSpace(generic.ServerExecutable), "generic profile has no fixed backend path");
            Check(string.IsNullOrWhiteSpace(generic.ModelPath), "generic profile has no fixed model path");
            Check(generic.Host == "127.0.0.1", "generic profile is local-only by default");
            Check(generic.AdvertisedHost == "127.0.0.1", "generic advertised host is safe by default");
            Check(generic.ContextSize == 8192, "generic context default is conservative");
            Check(generic.CacheTypeK == "f16" && generic.CacheTypeV == "f16", "generic KV cache favors compatibility");
            Check(generic.Parallel == 1, "generic single active request");
            Check(generic.BatchSize == 2048 && generic.UbatchSize == 512, "generic batch defaults match llama.cpp defaults");
            Check(LlamaApiClient.LocalBaseUrl(generic) == "http://127.0.0.1:8080", "generic local probe URL");
            Check(LlamaApiClient.LanBaseUrl(generic) == "http://127.0.0.1:8080", "generic published URL");

            ModelProfile profile = ModelProfile.CreateGenericProfile();
            profile.ServerExecutable = @"C:\llama.cpp\llama-server.exe";
            profile.ModelPath = @"C:\Models\Example Model.gguf";
            profile.Alias = "example-model";
            profile.Host = "0.0.0.0";
            profile.AdvertisedHost = "server.local";
            profile.ContextSize = 32768;
            profile.CacheTypeK = "q8_0";
            profile.CacheTypeV = "q8_0";
            profile.Threads = 12;
            profile.BatchSize = 1024;
            profile.UbatchSize = 256;
            string arguments = CommandBuilder.BuildArguments(profile);
            string command = CommandBuilder.BuildDisplayCommand(profile);
            Check(arguments.Contains("--ctx-size \"32768\""), "custom context argument is emitted");
            Check(arguments.Contains("--cache-type-k \"q8_0\" --cache-type-v \"q8_0\""), "custom KV cache arguments are emitted");
            Check(arguments.Contains("--threads \"12\" --batch-size \"1024\" --ubatch-size \"256\""), "adaptive execution arguments are emitted");
            Check(command.Contains("\"C:\\Models\\Example Model.gguf\""), "model paths with spaces are quoted");
            Check(!command.Contains("C:\\\\Models"), "Windows path separators are not doubled");
            Check(LlamaApiClient.LanBaseUrl(profile) == "http://server.local:8080", "published URL uses advertised host");
            CheckNormalizationDefaults();
            CheckAdaptivePlans();
            CheckGgufMetadataReader();
            CheckZipTraversalDefense();
            if (string.Equals(Environment.GetEnvironmentVariable("LLAMA_MANAGER_NETWORK_TEST"), "1", StringComparison.Ordinal))
                CheckRuntimeCatalog();

            Console.WriteLine();
            Console.WriteLine(command);
            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "ALL OFFLINE TESTS PASSED" : failures + " TEST(S) FAILED");
            Environment.ExitCode = failures == 0 ? 0 : 1;
        }

        private static void CheckAdaptivePlans()
        {
            HardwareProfile hardware = new HardwareProfile();
            hardware.CpuName = "Test CPU";
            hardware.LogicalProcessors = 16;
            hardware.TotalMemoryBytes = 64L * 1024L * 1024L * 1024L;
            hardware.RecommendedBackend = "CUDA 12";
            hardware.Gpus.Add(new GpuProfile { Name = "Test GPU", MemoryBytes = 24L * 1024L * 1024L * 1024L });

            GgufModelInfo model = new GgufModelInfo();
            model.Name = "Example 14B";
            model.Architecture = "qwen";
            model.Quantization = "Q4_K_M";
            model.FileSizeBytes = 9L * 1024L * 1024L * 1024L;
            model.ContextLength = 131072;
            model.BlockCount = 48;
            model.EmbeddingLength = 5120;
            model.HeadCount = 40;
            model.KvHeadCount = 8;

            AdaptivePlan fast = AdaptiveTuner.Recommend(hardware, model, "Fast");
            AdaptivePlan balanced = AdaptiveTuner.Recommend(hardware, model, "Balanced");
            AdaptivePlan extreme = AdaptiveTuner.Recommend(hardware, model, "Extreme");
            Check(fast.ContextSize <= balanced.ContextSize && balanced.ContextSize <= extreme.ContextSize, "adaptive presets increase context deliberately");
            Check(fast.CacheTypeK == "q4_0" && balanced.CacheTypeK == "q8_0", "adaptive presets select KV cache quantization");
            Check(fast.GpuLayers == "all" || fast.GpuLayers == "auto", "GPU hardware receives accelerated layer policy");
            Check(!string.IsNullOrWhiteSpace(extreme.RecommendedModelQuantization), "adaptive plan explains model quantization recommendation");

            ModelProfile applied = ModelProfile.CreateGenericProfile();
            balanced.ApplyTo(applied);
            Check(applied.ContextSize == balanced.ContextSize && applied.TuningPreset == "Balanced", "adaptive plan applies to model profile");
        }

        private static void CheckRuntimeCatalog()
        {
            Console.WriteLine();
            HardwareProfile hardware = HardwareDetector.Detect();
            Check(hardware.LogicalProcessors > 0 && hardware.TotalMemoryBytes > 0, "local hardware detector reads CPU and physical memory");
            Console.WriteLine(hardware.Summary);
            Console.WriteLine();
            Console.WriteLine("Querying official llama.cpp releases...");
            List<LlamaReleaseAsset> assets = LlamaReleaseClient.GetWindowsAssetsAsync().Result;
            Check(assets.Count > 0, "official catalog exposes Windows x64 assets");
            bool valid = true;
            foreach (LlamaReleaseAsset asset in assets)
            {
                if (asset.Downloads.Count == 0 || string.IsNullOrWhiteSpace(asset.ReleaseTag) || string.IsNullOrWhiteSpace(asset.Backend)) valid = false;
                foreach (RuntimeDownload part in asset.Downloads)
                    if (!part.Url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)) valid = false;
            }
            Check(valid, "official runtime assets have trusted download metadata");
            if (assets.Count > 0) Console.WriteLine("Latest recognized runtime: " + assets[0]);
        }

        private static void CheckGgufMetadataReader()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "synthetic-q4_k_m.gguf");
            try
            {
                using (BinaryWriter writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None), Encoding.UTF8))
                {
                    writer.Write(Encoding.ASCII.GetBytes("GGUF"));
                    writer.Write((uint)3);
                    writer.Write((ulong)0);
                    writer.Write((ulong)8);
                    WriteGgufString(writer, "general.architecture"); writer.Write((uint)8); WriteGgufString(writer, "qwen2");
                    WriteGgufString(writer, "general.name"); writer.Write((uint)8); WriteGgufString(writer, "Synthetic Qwen");
                    WriteGgufString(writer, "general.file_type"); writer.Write((uint)4); writer.Write((uint)15);
                    WriteGgufString(writer, "qwen2.context_length"); writer.Write((uint)4); writer.Write((uint)32768);
                    WriteGgufString(writer, "qwen2.block_count"); writer.Write((uint)4); writer.Write((uint)48);
                    WriteGgufString(writer, "qwen2.embedding_length"); writer.Write((uint)4); writer.Write((uint)5120);
                    WriteGgufString(writer, "qwen2.attention.head_count"); writer.Write((uint)4); writer.Write((uint)40);
                    WriteGgufString(writer, "qwen2.attention.head_count_kv"); writer.Write((uint)4); writer.Write((uint)8);
                }
                GgufModelInfo parsed = GgufMetadataReader.Read(path);
                Check(parsed.Architecture == "qwen2" && parsed.ContextLength == 32768, "GGUF reader parses architecture and context");
                Check(parsed.BlockCount == 48 && parsed.EmbeddingLength == 5120, "GGUF reader parses model dimensions");
                Check(parsed.HeadCount == 40 && parsed.KvHeadCount == 8, "GGUF reader parses grouped-query attention metadata");
                Check(parsed.Quantization == "Q4_K_M", "GGUF reader maps model quantization");
            }
            finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
        }

        private static void WriteGgufString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write((ulong)bytes.Length);
            writer.Write(bytes);
        }

        private static void CheckZipTraversalDefense()
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string archivePath = Path.Combine(root, "malicious-runtime.zip");
            string destination = Path.Combine(root, "runtime-extract-test");
            string escaped = Path.Combine(root, "escape.txt");
            try
            {
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                if (File.Exists(archivePath)) File.Delete(archivePath);
                if (File.Exists(escaped)) File.Delete(escaped);
                Directory.CreateDirectory(destination);
                using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = archive.CreateEntry("../escape.txt");
                    using (StreamWriter writer = new StreamWriter(entry.Open())) writer.Write("blocked");
                }

                MethodInfo extract = typeof(RuntimeInstaller).GetMethod("ExtractSafe", BindingFlags.NonPublic | BindingFlags.Static);
                bool blocked = false;
                try { extract.Invoke(null, new object[] { archivePath, destination }); }
                catch (TargetInvocationException ex) { blocked = ex.InnerException is InvalidDataException; }
                Check(blocked && !File.Exists(escaped), "runtime ZIP traversal is blocked");
            }
            finally
            {
                try { if (Directory.Exists(destination)) Directory.Delete(destination, true); } catch { }
                try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch { }
                try { if (File.Exists(escaped)) File.Delete(escaped); } catch { }
            }
        }

        private static void CheckNormalizationDefaults()
        {
            ModelProfile damaged = new ModelProfile();
            damaged.Name = string.Empty;
            damaged.Host = string.Empty;
            damaged.AdvertisedHost = string.Empty;
            damaged.Port = 0;
            damaged.Parallel = 0;
            damaged.GpuLayers = string.Empty;
            damaged.CacheTypeK = string.Empty;
            damaged.CacheTypeV = string.Empty;

            AppConfig config = new AppConfig();
            config.Profiles.Add(damaged);
            config.SelectedProfileId = damaged.Id;

            MethodInfo normalize = typeof(ConfigStore).GetMethod("Normalize", BindingFlags.NonPublic | BindingFlags.Static);
            normalize.Invoke(null, new object[] { config });

            Check(damaged.Name == "未命名模型", "blank profile name is normalized");
            Check(damaged.Host == "127.0.0.1" && damaged.AdvertisedHost == "127.0.0.1", "blank hosts recover to local-only defaults");
            Check(damaged.Port == 8080 && damaged.Parallel == 1, "invalid numeric values are normalized");
            Check(damaged.GpuLayers == "auto", "blank GPU layer setting is normalized");
            Check(damaged.CacheTypeK == "q8_0" && damaged.CacheTypeV == "q8_0", "blank KV cache settings are normalized");
        }

        private static void Check(bool condition, string name)
        {
            if (condition)
            {
                Console.WriteLine("[PASS] " + name);
            }
            else
            {
                failures++;
                Console.WriteLine("[FAIL] " + name);
            }
        }
    }
}
