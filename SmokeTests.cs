using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Text;
using System.IO.Compression;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LlamaServerManager
{
    internal static class SmokeTests
    {
        private static int failures;

        private static void Main(string[] args)
        {
            if (Array.IndexOf(args, "--fake-stay-alive") >= 0)
            {
                Thread.Sleep(60000);
                return;
            }
            ModelProfile generic = ModelProfile.CreateGenericProfile();
            Check(string.IsNullOrWhiteSpace(generic.ServerExecutable), "generic profile has no fixed backend path");
            Check(string.IsNullOrWhiteSpace(generic.ModelPath), "generic profile has no fixed model path");
            Check(generic.Host == "127.0.0.1", "generic profile is local-only by default");
            Check(generic.AdvertisedHost == "127.0.0.1", "generic advertised host is safe by default");
            Check(generic.ContextSize == 8192, "generic context default is conservative");
            Check(generic.CacheTypeK == "f16" && generic.CacheTypeV == "f16", "generic KV cache favors compatibility");
            Check(generic.Parallel == 1, "generic single active request");
            Check(generic.BatchSize == 2048 && generic.UbatchSize == 512, "generic batch defaults match llama.cpp defaults");
            Check(generic.EnableMetrics, "generic profile enables local performance metrics");
            Check(generic.ApiProtocol == ApiProtocolMode.Responses, "generic profile defaults to the Responses protocol");
            Check(LlamaApiClient.LocalBaseUrl(generic) == "http://127.0.0.1:8080", "generic local probe URL");
            Check(LlamaApiClient.LanBaseUrl(generic) == "http://127.0.0.1:8080", "generic published URL");
            CheckApiProtocols();
            CheckApiProtocolLoopback();

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
            Check(arguments.Contains("--metrics"), "llama.cpp Prometheus metrics are enabled by default");
            Check(command.Contains("\"C:\\Models\\Example Model.gguf\""), "model paths with spaces are quoted");
            Check(!command.Contains("C:\\\\Models"), "Windows path separators are not doubled");
            Check(LlamaApiClient.LanBaseUrl(profile) == "http://server.local:8080", "published URL uses advertised host");
            CheckCommandEditing();
            CheckCommandPreflight();
            CheckApiKeyStore();
            CheckApiKeyLaunchCompatibility();
            CheckLlamaServerLocator();
            CheckParameterPresets();
            CheckNormalizationDefaults();
            CheckConfigurationMigration();
            CheckAdaptivePlans();
            CheckProcessLifecycle();
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

        private static void CheckCommandEditing()
        {
            ModelProfile baseline = ModelProfile.CreateGenericProfile();
            baseline.ServerExecutable = @"C:\llama.cpp\llama-server.exe";
            string edited = "\"C:\\llama.cpp\\llama-server.exe\" -m \"D:\\Models\\Qwen Test.gguf\" -c 32768 -ngl 60 " +
                "--port=9090 --parallel 2 -ctk q8_0 -ctv q4_0 --fit off --flash-attn on --no-webui --metrics --rope-scaling yarn";
            CommandParseResult parsed = CommandParser.Parse(edited, baseline);
            Check(parsed.Success, "editable command accepts aliases, equals syntax, quotes and booleans");
            Check(parsed.Profile.ModelPath == @"D:\Models\Qwen Test.gguf" && parsed.Profile.ContextSize == 32768, "editable command synchronizes model and context fields");
            Check(parsed.Profile.Port == 9090 && parsed.Profile.Parallel == 2 && parsed.Profile.GpuLayers == "60", "editable command synchronizes network and acceleration fields");
            Check(!parsed.Profile.FitEnabled && parsed.Profile.FlashAttention && parsed.Profile.DisableWebUi && parsed.Profile.EnableMetrics, "editable command synchronizes switches");
            Check(parsed.Profile.ExtraArguments.Contains("--rope-scaling yarn") && parsed.UnknownCount == 2, "unknown command arguments are preserved");

            CommandParseResult invalid = CommandParser.Parse("llama-server.exe --port 70000 --ctx-size nope", baseline);
            Check(!invalid.Success && invalid.Errors.Count == 2, "invalid command values block synchronization with inline errors");

            string roundTrip = CommandBuilder.BuildDisplayCommand(parsed.Profile);
            CommandParseResult reparsed = CommandParser.Parse(roundTrip, baseline);
            Check(reparsed.Success && reparsed.Profile.ContextSize == 32768 && reparsed.Profile.ExtraArguments.Contains("--rope-scaling"), "generated command round-trips through the parser");

            parsed.Profile.UseCustomCommand = true;
            parsed.Profile.CustomCommand = edited;
            Check(CommandBuilder.BuildDisplayCommand(parsed.Profile) == edited, "saved custom command is preserved verbatim for display");
            Check(CommandBuilder.BuildLaunchExecutable(parsed.Profile) == @"C:\llama.cpp\llama-server.exe", "custom command executable is split safely");
            Check(CommandBuilder.BuildLaunchArguments(parsed.Profile).Contains("--port=9090"), "custom command arguments are used for launch");
            parsed.Profile.CustomCommand += " --api-key super-secret-value";
            Check(!CommandBuilder.BuildSafeDisplayCommand(parsed.Profile).Contains("super-secret-value"), "inline secrets are redacted from launch logs");
        }

        private static void CheckApiProtocols()
        {
            ModelProfile profile = ModelProfile.CreateGenericProfile();
            profile.Alias = "qwen-local";

            ApiProtocolRequest responses = LlamaApiClient.BuildProtocolTestRequest(profile, ApiProtocolMode.Responses);
            Check(responses.RelativePath == "/v1/responses" && responses.AuthenticationHeader == "Authorization" &&
                responses.Json.Contains("\"input\"") && responses.Json.Contains("\"max_output_tokens\""),
                "Responses protocol uses the native route, Bearer authentication and Responses payload");

            ApiProtocolRequest chat = LlamaApiClient.BuildProtocolTestRequest(profile, ApiProtocolMode.ChatCompletions);
            Check(chat.RelativePath == "/v1/chat/completions" && chat.AuthenticationHeader == "Authorization" &&
                chat.Json.Contains("\"messages\"") && chat.Json.Contains("\"max_tokens\""),
                "Chat Completions protocol uses the chat route and OpenAI payload");

            profile.ApiProtocol = ApiProtocolMode.AnthropicMessages;
            ApiProtocolRequest anthropic = LlamaApiClient.BuildProtocolTestRequest(profile, profile.ApiProtocol);
            Check(anthropic.RelativePath == "/v1/messages" && anthropic.AuthenticationHeader == "x-api-key" &&
                anthropic.Json.Contains("\"messages\"") && !anthropic.Json.Contains("max_output_tokens"),
                "Anthropic Messages protocol uses the messages route and x-api-key authentication");
            Check(LlamaApiClient.ProtocolClientBaseUrl(profile) == "http://127.0.0.1:8080" &&
                LlamaApiClient.ProtocolEndpointUrl(profile) == "http://127.0.0.1:8080/v1/messages",
                "Anthropic clients receive the correct base URL and endpoint URL");

            profile.ApiProtocol = ApiProtocolMode.ChatCompletions;
            Check(LlamaApiClient.ProtocolClientBaseUrl(profile) == "http://127.0.0.1:8080/v1" &&
                LlamaApiClient.ProtocolEndpointUrl(profile) == "http://127.0.0.1:8080/v1/chat/completions",
                "OpenAI-compatible clients receive the /v1 base URL");
            Check(ApiProtocolMode.Normalize("unknown") == ApiProtocolMode.Responses && ApiProtocolMode.Values().Length == 3,
                "invalid protocol values recover safely and exactly three protocols are offered");
        }

        private static void CheckApiProtocolLoopback()
        {
            string keyPath = Path.Combine(Path.GetTempPath(), "llamalift-protocol-key-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(keyPath, "sk-llamalift-test-only", new UTF8Encoding(false));
            try
            {
                foreach (string protocol in ApiProtocolMode.Values())
                {
                    TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
                    listener.Start();
                    int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    string requestLine = string.Empty;
                    string authorization = string.Empty;
                    string anthropicKey = string.Empty;
                    string requestBody = string.Empty;
                    string workerError = string.Empty;
                    Thread worker = new Thread(delegate()
                    {
                        try
                        {
                            using (TcpClient client = listener.AcceptTcpClient())
                            using (NetworkStream stream = client.GetStream())
                            {
                                client.ReceiveTimeout = 10000;
                                List<byte> headerBuffer = new List<byte>();
                                int matched = 0;
                                byte[] headerTerminator = new byte[] { 13, 10, 13, 10 };
                                while (matched < headerTerminator.Length)
                                {
                                    int next = stream.ReadByte();
                                    if (next < 0) throw new EndOfStreamException("HTTP request headers ended unexpectedly.");
                                    headerBuffer.Add((byte)next);
                                    matched = next == headerTerminator[matched] ? matched + 1 : (next == headerTerminator[0] ? 1 : 0);
                                    if (headerBuffer.Count > 32768) throw new InvalidDataException("HTTP request headers are too large.");
                                }
                                string[] headerLines = Encoding.ASCII.GetString(headerBuffer.ToArray()).Split(new string[] { "\r\n" }, StringSplitOptions.None);
                                requestLine = headerLines.Length == 0 ? string.Empty : headerLines[0];
                                int contentLength = 0;
                                for (int headerIndex = 1; headerIndex < headerLines.Length; headerIndex++)
                                {
                                    string header = headerLines[headerIndex];
                                    if (header.Length == 0) break;
                                    int colon = header.IndexOf(':');
                                    if (colon <= 0) continue;
                                    string name = header.Substring(0, colon).Trim();
                                    string value = header.Substring(colon + 1).Trim();
                                    if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)) Int32.TryParse(value, out contentLength);
                                    else if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)) authorization = value;
                                    else if (name.Equals("x-api-key", StringComparison.OrdinalIgnoreCase)) anthropicKey = value;
                                }
                                byte[] continueBytes = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
                                stream.Write(continueBytes, 0, continueBytes.Length);
                                stream.Flush();
                                byte[] body = new byte[Math.Max(0, contentLength)];
                                int offset = 0;
                                while (offset < body.Length)
                                {
                                    int read = stream.Read(body, offset, body.Length - offset);
                                    if (read <= 0) break;
                                    offset += read;
                                }
                                requestBody = Encoding.UTF8.GetString(body, 0, offset);
                                byte[] payload = Encoding.UTF8.GetBytes("{\"ok\":true}");
                                string responseHeaders = "HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + payload.Length + "\r\nConnection: close\r\n\r\n";
                                byte[] headerBytes = Encoding.ASCII.GetBytes(responseHeaders);
                                stream.Write(headerBytes, 0, headerBytes.Length);
                                stream.Write(payload, 0, payload.Length);
                                stream.Flush();
                            }
                        }
                        catch (Exception ex) { workerError = ex.Message; }
                    });
                    worker.IsBackground = true;
                    worker.Start();

                    try
                    {
                        ModelProfile profile = ModelProfile.CreateGenericProfile();
                        profile.Port = port;
                        profile.Alias = "loopback-model";
                        profile.ApiKeyFile = keyPath;
                        ApiCheckResult result = LlamaApiClient.TestProtocolAsync(profile, protocol).GetAwaiter().GetResult();
                        bool completed = worker.Join(12000);
                        string expectedPath = ApiProtocolMode.EndpointPath(protocol);
                        bool authMatches = protocol == ApiProtocolMode.AnthropicMessages
                            ? anthropicKey == "sk-llamalift-test-only" && string.IsNullOrEmpty(authorization)
                            : authorization == "Bearer sk-llamalift-test-only" && string.IsNullOrEmpty(anthropicKey);
                        string diagnostic = " [summary=" + result.Summary + ", completed=" + completed + ", worker=" + workerError +
                            ", request=" + requestLine + "]";
                        Check(result.Success && completed && workerError.Length == 0 && requestLine.StartsWith("POST " + expectedPath + " ", StringComparison.Ordinal),
                            ApiProtocolMode.DisplayName(protocol) + " completes a real loopback HTTP request on the correct route" + diagnostic);
                        Check(authMatches && requestBody.Contains("\"model\":\"loopback-model\""),
                            ApiProtocolMode.DisplayName(protocol) + " sends the expected authentication header and JSON body" + diagnostic);
                    }
                    finally { listener.Stop(); }
                }
            }
            finally { try { File.Delete(keyPath); } catch { } }
        }

        private static void CheckCommandPreflight()
        {
            ModelProfile baseline = ModelProfile.CreateGenericProfile();
            baseline.ServerExecutable = @"C:\llama.cpp\llama-server.exe";
            baseline.ModelPath = @"D:\Models\Qwen.gguf";
            CommandPreflightResult valid = CommandPreflightValidator.Validate(
                "\"C:\\llama.cpp\\llama-server.exe\" --model \"D:\\Models\\Qwen.gguf\" --port 8080 --ctx-size 32768 --parallel 1 --n-gpu-layers auto --cache-type-k f16 --cache-type-v f16",
                baseline, false);
            Check(valid.ErrorCount == 0 && valid.CanLikelyRun, "custom command preflight accepts a coherent command");

            CommandPreflightResult invalid = CommandPreflightValidator.Validate(
                "llama-server.exe --model model.gguf --port 70000 --ctx-size 524288 --n-gpu-layers nonsense",
                baseline, false);
            Check(invalid.ErrorCount >= 2 && invalid.WarningCount >= 1, "custom command preflight reports invalid values and resource risks");
            Check(invalid.BuildReviewText(5).Contains("修改建议"), "custom command preflight provides actionable suggestions");

            CommandPreflightResult lan = CommandPreflightValidator.Validate(
                "llama-server.exe --model model.gguf --host 0.0.0.0 --port 8080", baseline, false);
            Check(lan.WarningCount > 0 && lan.BuildReviewText(5).Contains("API Key"), "custom command preflight warns about unauthenticated LAN access");

            CommandPreflightResult inlineSecret = CommandPreflightValidator.Validate(
                "llama-server.exe --model model.gguf --api-key super-secret-value", baseline, false);
            Check(inlineSecret.BuildReviewText(8).Contains("--api-key-file") && !inlineSecret.BuildReviewText(8).Contains("super-secret-value"),
                "custom command preflight recommends managed key files without echoing secrets");
        }

        private static void CheckApiKeyStore()
        {
            string root = Path.Combine(Path.GetTempPath(), "LlamaServerManager-tests-" + Guid.NewGuid().ToString("N"));
            string outside = root + "-outside.txt";
            Directory.CreateDirectory(root);
            try
            {
                ApiKeyStore store = new ApiKeyStore(root);
                ManagedApiKeyFile saved = store.Save("测试 Key", "sk-first\r\nsk-second\r\nsk-first\r\n");
                Check(saved.KeyCount == 2, "API Key manager stores one unique key per line");
                Check(!saved.MaskedPreview.Contains("sk-first") && saved.MaskedPreview.EndsWith("irst"), "API Key list exposes only a masked preview");
                Check(store.List().Count == 1 && store.Read(saved.FilePath).Contains("sk-second"), "API Key manager lists and reads managed files");
                string generated = ApiKeyStore.GenerateKey();
                Check(generated.StartsWith("sk-llamalift-") && generated.Length == 77,
                    "API Key manager generates sk-llamalift keys with 32 random bytes");
                Check(System.Text.RegularExpressions.Regex.IsMatch(generated, @"^sk-llamalift-[0-9a-f]{64}$"),
                    "API Key manager emits a client-compatible lowercase hexadecimal key");
                File.WriteAllText(outside, "must-stay");
                bool traversalBlocked = false;
                try { store.Delete(outside); }
                catch (InvalidOperationException) { traversalBlocked = true; }
                Check(traversalBlocked && File.Exists(outside), "API Key manager blocks deletion outside its managed directory");
                store.Delete(saved.FilePath);
                Check(store.List().Count == 0, "API Key manager deletes only selected managed files");
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
                if (File.Exists(outside)) File.Delete(outside);
            }
        }

        private static void CheckApiKeyLaunchCompatibility()
        {
            string root = Path.Combine(Path.GetTempPath(), "llamalift-key-兼容性-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(root);
                string source = Path.Combine(root, "密钥.txt");
                File.WriteAllText(source, "sk-test-only", new UTF8Encoding(false));
                bool bridged;
                string launchPath = ApiKeyFileSupport.PrepareForLaunch(source, out bridged);
                Check(bridged && File.Exists(launchPath) && ApiKeyFileSupport.IsAscii(launchPath),
                    "non-ASCII API Key paths are bridged to a readable ASCII launch path");
                Check(File.ReadAllText(launchPath, Encoding.UTF8) == "sk-test-only",
                    "API Key launch bridge preserves file contents");
                string rewritten = ServerProcessManager.ReplaceApiKeyFileArgument(
                    "--model model.gguf --api-key-file \"" + source + "\" --port 8080", launchPath);
                Check(rewritten.Contains(launchPath) && !rewritten.Contains(source),
                    "generated and custom launch arguments use the compatibility path");
                string readError;
                Check(ApiKeyFileSupport.TryOpenForRead(source, out readError),
                    "API Key validation performs a real read-open check");
                ApiKeyFileSupport.ReleaseRuntimeCopy(launchPath);
                Check(!File.Exists(launchPath), "temporary API Key bridge is removed after the server session");
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
        }

        private static void CheckParameterPresets()
        {
            List<ParameterPreset> presets = ParameterPreset.CreateDefaults();
            Check(presets.Count == 3 && presets[0].Name.StartsWith("预设 1") && presets[2].Name.StartsWith("预设 3"), "three named parameter preset slots are created");
            ModelProfile target = ModelProfile.CreateGenericProfile();
            target.ModelPath = @"D:\Models\Keep.gguf";
            target.Host = "0.0.0.0";
            target.Port = 8123;
            presets[2].ApplyTo(target);
            Check(presets[0].ContextSize == 32768 && presets[1].ContextSize == 65536 && presets[2].ContextSize == 131072,
                "built-in parameter presets provide practical 32K, 64K and 128K context targets");
            Check(target.ContextSize == 131072 && target.BatchSize == 4096, "parameter preset applies performance values");
            Check(target.ModelPath == @"D:\Models\Keep.gguf" && target.Host == "0.0.0.0" && target.Port == 8123, "parameter preset does not overwrite identity or network settings");
            target.ContextSize = 24576;
            presets[0].Capture(target);
            Check(presets[0].ContextSize == 24576, "parameter preset captures customized values");

            target.ExtraArguments = "--cache-reuse 25";
            presets[1].ExtraArguments = "--cache-reuse 256";
            string preserved = target.ExtraArguments;
            presets[1].ApplyTo(target);
            target.ExtraArguments = ModelProfile.MergeExtraArguments(preserved, target.ExtraArguments);
            Check(target.ExtraArguments.Contains("--cache-reuse 25") && target.ExtraArguments.Contains("--cache-reuse 256"),
                "preset conversion preserves prefix-colliding unknown arguments without substring loss");
        }

        private static void CheckLlamaServerLocator()
        {
            string root = Path.Combine(Path.GetTempPath(), "llamalift-locator-" + Guid.NewGuid().ToString("N"));
            string bin = Path.Combine(root, "turboquant", "build", "bin");
            string executable = Path.Combine(bin, "llama-server.exe");
            try
            {
                Directory.CreateDirectory(bin);
                File.WriteAllText(executable, "test-only");
                AppConfig config = new AppConfig();
                config.Profiles.Clear();
                config.InstalledRuntimes.Clear();
                config.InstalledRuntimes.Add(new InstalledRuntime { ServerExecutable = executable, InstallDirectory = bin });
                List<LlamaServerCandidate> found = LlamaServerLocator.FindCandidates(config, new string[] { root }, false);
                Check(found.Count == 1 && string.Equals(found[0].ExecutablePath, executable, StringComparison.OrdinalIgnoreCase),
                    "llama-server locator finds nested runtime binaries and deduplicates candidates");
                Check(found[0].Source == "LlamaLift 已登记运行时" && found[0].InstallDirectory == bin,
                    "llama-server locator prioritizes registered runtimes and reports the install directory");
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
            }
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

            ICollection<string> turboTypes = new List<string>(RuntimeCapabilityDetector.StandardCacheTypes()) { "turbo2", "turbo3", "turbo4" };
            AdaptivePlan turboExtreme = AdaptiveTuner.Recommend(hardware, model, "Extreme", turboTypes);
            Check(turboExtreme.CacheTypeK == "turbo3" && turboExtreme.CacheTypeV == "turbo3", "TurboQuant runtime receives a TurboQuant-aware extreme plan");
            Check(turboExtreme.ContextSize >= 131072, "extreme adaptive plan targets at least 128K when model and memory allow");

            AdaptivePlan turbo3Only = AdaptiveTuner.Recommend(hardware, model, "Extreme", new List<string> { "f16", "turbo3" });
            Check(turbo3Only.CacheTypeK == "turbo3" && turbo3Only.CacheTypeV == "turbo3", "runtime exposing only turbo3 receives a supported KV plan");
            AdaptivePlan turbo4Only = AdaptiveTuner.Recommend(hardware, model, "Fast", new List<string> { "f16", "turbo4" });
            Check(turbo4Only.CacheTypeK == "turbo4" && turbo4Only.CacheTypeV == "turbo4", "runtime exposing only turbo4 never receives unsupported q4_0");
            AdaptivePlan minimal = AdaptiveTuner.Recommend(hardware, model, "Balanced", new List<string> { "f16" });
            Check(minimal.CacheTypeK == "f16" && minimal.CacheTypeV == "f16", "adaptive KV selection is closed over the runtime capability set");
        }

        private static void CheckConfigurationMigration()
        {
            string legacyJson = "{\"SchemaVersion\":3,\"SelectedProfileId\":\"muse\",\"Profiles\":[{\"Id\":\"muse\",\"Name\":\"Muse profile\",\"ContextSize\":262144,\"CacheTypeK\":\"turbo3\",\"CacheTypeV\":\"turbo3\",\"ModelPath\":\"D:\\\\Models\\\\Muse.gguf\"}]}";
            AppConfig migrated = ConfigStore.DeserializeAndNormalize(legacyJson);
            ModelProfile muse = migrated.Profiles[0];
            Check(muse.Name == "Muse profile" && muse.ContextSize == 262144, "migration preserves existing profile identity and 256K context");
            Check(muse.CacheTypeK == "turbo3" && muse.CacheTypeV == "turbo3", "migration preserves TurboQuant cache types");
            Check(muse.ApiProtocol == ApiProtocolMode.Responses, "legacy profiles migrate to the Responses protocol without data loss");

            AppConfig defaults = new AppConfig();
            defaults.SchemaVersion = 6;
            ParameterPreset oldFast = new ParameterPreset { Name = "预设 1 · 快速", ContextSize = 4096, CacheTypeK = "q4_0", CacheTypeV = "q4_0", BatchSize = 2048, UbatchSize = 512 };
            ParameterPreset customized = new ParameterPreset { Name = "我的长上下文", ContextSize = 262144, CacheTypeK = "turbo3", CacheTypeV = "turbo3", BatchSize = 2048, UbatchSize = 512 };
            defaults.ParameterPresets = new List<ParameterPreset> { oldFast, customized };
            defaults.SelectedParameterPresetId = oldFast.Id;
            ConfigStore.NormalizeForTesting(defaults);
            Check(oldFast.ContextSize == 32768 && oldFast.BuiltInKey == "Fast", "untouched legacy built-in preset is safely upgraded");
            Check(customized.ContextSize == 262144 && customized.CacheTypeK == "turbo3", "customized legacy preset is never overwritten by migration");

            AppConfig current = new AppConfig();
            current.Profiles.Clear();
            ModelProfile currentProfile = ModelProfile.CreateGenericProfile();
            currentProfile.Id = "same";
            currentProfile.Name = "Muse profile";
            currentProfile.ModelPath = @"D:\new.gguf";
            currentProfile.ContextSize = 65536;
            current.Profiles.Add(currentProfile);
            current.SelectedProfileId = currentProfile.Id;
            AppConfig legacy = new AppConfig();
            legacy.Profiles.Clear();
            ModelProfile legacyProfile = ModelProfile.CreateGenericProfile();
            legacyProfile.Id = "same";
            legacyProfile.Name = "Muse profile";
            legacyProfile.ModelPath = @"D:\old.gguf";
            legacyProfile.ContextSize = 262144;
            legacyProfile.CacheTypeK = "turbo3";
            legacyProfile.CacheTypeV = "turbo3";
            legacy.Profiles.Add(legacyProfile);
            legacy.SelectedProfileId = legacyProfile.Id;
            ConfigStore.MergeLegacyForTesting(current, legacy);
            Check(current.Profiles.Count == 2 && current.Profiles[0].ModelPath == @"D:\new.gguf" && current.SelectedProfileId == "same",
                "legacy merge never overwrites the current branded profile");
            Check(current.Profiles[1].Name.Contains("旧版导入") && current.Profiles[1].ContextSize == 262144,
                "conflicting legacy profile is preserved as a separately identifiable copy");

            AppConfig portCurrent = new AppConfig();
            portCurrent.Profiles.Clear();
            ModelProfile portCurrentProfile = ModelProfile.CreateGenericProfile();
            portCurrentProfile.Id = "port-diff";
            portCurrentProfile.ModelPath = @"D:\same.gguf";
            portCurrentProfile.Port = 8080;
            portCurrent.Profiles.Add(portCurrentProfile);
            AppConfig portLegacy = new AppConfig();
            portLegacy.Profiles.Clear();
            ModelProfile portLegacyProfile = portCurrentProfile.Clone();
            portLegacyProfile.Port = 9999;
            portLegacyProfile.ExtraArguments = "--rope-scaling yarn";
            portLegacy.Profiles.Add(portLegacyProfile);
            ConfigStore.MergeLegacyForTesting(portCurrent, portLegacy);
            Check(portCurrent.Profiles.Count == 2 && portCurrent.Profiles[1].Port == 9999 && portCurrent.Profiles[1].ExtraArguments.Contains("rope-scaling"),
                "migration compares network and advanced fields before declaring profiles equivalent");

            AppConfig presetCurrent = new AppConfig();
            presetCurrent.ParameterPresets.Clear();
            ParameterPreset currentPreset = new ParameterPreset { Id = "preset-diff", Name = "兼容预设", Jinja = true, EnableMetrics = true };
            presetCurrent.ParameterPresets.Add(currentPreset);
            AppConfig presetLegacy = new AppConfig();
            presetLegacy.ParameterPresets.Clear();
            ParameterPreset legacyPreset = currentPreset.CloneAs("兼容预设");
            legacyPreset.Id = "preset-diff";
            legacyPreset.Jinja = false;
            presetLegacy.ParameterPresets.Add(legacyPreset);
            ConfigStore.MergeLegacyForTesting(presetCurrent, presetLegacy);
            Check(presetCurrent.ParameterPresets.Count == 2 && !presetCurrent.ParameterPresets[1].Jinja,
                "migration preserves presets that differ only in advanced switches");

            ModelProfile customCommand = ModelProfile.CreateGenericProfile();
            customCommand.UseCustomCommand = true;
            customCommand.CustomCommand = "llama-server.exe --ctx-size 8192 --rope-scaling yarn";
            customCommand.ExtraArguments = "--rope-scaling yarn";
            customCommand.ContextSize = 131072;
            customCommand.ExtraArguments = ModelProfile.MergeExtraArguments(customCommand.ExtraArguments, "--cache-reuse 256");
            bool converted = customCommand.SwitchToGeneratedCommand();
            string generated = CommandBuilder.BuildDisplayCommand(customCommand);
            Check(converted && generated.Contains("--ctx-size \"131072\"") && generated.Contains("--rope-scaling yarn") && generated.Contains("--cache-reuse 256"),
                "performance synchronization replaces stale custom values while preserving and merging unknown arguments");
        }

        private static void CheckProcessLifecycle()
        {
            ServerProcessManager manager = new ServerProcessManager();
            try
            {
                ModelProfile profile = ModelProfile.CreateGenericProfile();
                profile.ServerExecutable = Assembly.GetExecutingAssembly().Location;
                profile.ModelPath = Assembly.GetExecutingAssembly().Location;
                profile.ExtraArguments = "--fake-stay-alive";
                manager.Start(profile);
                Thread.Sleep(500);
                Check(manager.IsRunning, "long-loading process remains managed without a startup kill timer");
                Stopwatch call = Stopwatch.StartNew();
                System.Threading.Tasks.Task<bool>[] concurrentStops = new System.Threading.Tasks.Task<bool>[8];
                for (int i = 0; i < concurrentStops.Length; i++) concurrentStops[i] = manager.StopAsync(5000);
                Check(call.ElapsedMilliseconds < 500, "stop request does not block the caller thread");
                System.Threading.Tasks.Task.WaitAll(concurrentStops);
                bool allStopped = true;
                foreach (System.Threading.Tasks.Task<bool> stop in concurrentStops) allStopped &= stop.Result;
                Check(allStopped && !manager.IsRunning, "concurrent stop requests share one successful lifecycle result");

                int upEvents = 0;
                int downEvents = 0;
                manager.RunningChanged += delegate(bool running, int pid)
                {
                    if (running) Interlocked.Increment(ref upEvents); else Interlocked.Increment(ref downEvents);
                };
                manager.LogReceived += delegate(string message, bool error) { throw new InvalidOperationException("subscriber fault injection"); };
                manager.Start(profile);
                Thread.Sleep(300);
                Check(manager.IsRunning && upEvents == 1, "subscriber failures cannot orphan or hide a started process");
                Check(manager.StopAsync(5000).Result && downEvents == 1, "one process session emits exactly one stopped transition");

                bool lifecycleStressPassed = true;
                for (int cycle = 0; cycle < 12; cycle++)
                {
                    manager.Start(profile);
                    System.Threading.Tasks.Task<bool>[] stops = new System.Threading.Tasks.Task<bool>[16];
                    for (int i = 0; i < stops.Length; i++) stops[i] = manager.StopAsync(5000);
                    System.Threading.Tasks.Task.WaitAll(stops);
                    foreach (System.Threading.Tasks.Task<bool> stop in stops) lifecycleStressPassed &= stop.Result;
                    lifecycleStressPassed &= !manager.IsRunning;
                }
                Thread.Sleep(100);
                Check(lifecycleStressPassed && upEvents == downEvents, "12 rounds x 16 concurrent stops leave no stale session transition");
            }
            finally { manager.Dispose(); }

            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Check(!NetworkHelper.WaitForTcpPortReleaseAsync(port, 400).Result, "port-release gate blocks restart while the old listener remains");
            listener.Stop();
            Check(NetworkHelper.WaitForTcpPortReleaseAsync(port, 2000).Result, "port-release gate allows restart after listener cleanup");
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
            damaged.ApiProtocol = "unsupported";

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
            Check(damaged.ApiProtocol == ApiProtocolMode.Responses, "invalid API protocol values normalize to Responses");
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
