using System;
using System.Collections.Generic;
using System.Reflection;

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
            string arguments = CommandBuilder.BuildArguments(profile);
            string command = CommandBuilder.BuildDisplayCommand(profile);
            Check(arguments.Contains("--ctx-size \"32768\""), "custom context argument is emitted");
            Check(arguments.Contains("--cache-type-k \"q8_0\" --cache-type-v \"q8_0\""), "custom KV cache arguments are emitted");
            Check(command.Contains("\"C:\\Models\\Example Model.gguf\""), "model paths with spaces are quoted");
            Check(!command.Contains("C:\\\\Models"), "Windows path separators are not doubled");
            Check(LlamaApiClient.LanBaseUrl(profile) == "http://server.local:8080", "published URL uses advertised host");
            CheckNormalizationDefaults();

            Console.WriteLine();
            Console.WriteLine(command);
            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "ALL OFFLINE TESTS PASSED" : failures + " TEST(S) FAILED");
            Environment.ExitCode = failures == 0 ? 0 : 1;
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
