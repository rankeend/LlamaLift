using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace LlamaServerManager
{
    public static class CommandBuilder
    {
        public static string BuildArguments(ModelProfile profile)
        {
            List<string> args = new List<string>();
            Add(args, "--host", profile.Host);
            Add(args, "--port", profile.Port.ToString());
            Add(args, "--model", profile.ModelPath);

            if (!string.IsNullOrWhiteSpace(profile.MmprojPath))
            {
                Add(args, "--mmproj", profile.MmprojPath);
            }
            if (!string.IsNullOrWhiteSpace(profile.Alias))
            {
                Add(args, "--alias", profile.Alias);
            }
            if (!string.IsNullOrWhiteSpace(profile.ApiKeyFile))
            {
                Add(args, "--api-key-file", profile.ApiKeyFile);
            }

            Add(args, "--n-gpu-layers", profile.GpuLayers);
            args.Add(profile.FitEnabled ? "--fit on" : "--fit off");
            if (profile.FitEnabled && profile.FitTarget > 0)
            {
                Add(args, "--fit-target", profile.FitTarget.ToString());
            }
            Add(args, "--ctx-size", profile.ContextSize.ToString());
            Add(args, "--parallel", profile.Parallel.ToString());
            if (profile.Threads > 0) Add(args, "--threads", profile.Threads.ToString());
            if (profile.BatchSize > 0) Add(args, "--batch-size", profile.BatchSize.ToString());
            if (profile.UbatchSize > 0) Add(args, "--ubatch-size", profile.UbatchSize.ToString());
            args.Add(profile.FlashAttention ? "--flash-attn on" : "--flash-attn off");
            Add(args, "--cache-type-k", profile.CacheTypeK);
            Add(args, "--cache-type-v", profile.CacheTypeV);

            if (profile.ImageMinTokens > 0)
            {
                Add(args, "--image-min-tokens", profile.ImageMinTokens.ToString());
            }
            bool hasChatTemplate = !string.IsNullOrWhiteSpace(profile.ChatTemplate) ||
                !string.IsNullOrWhiteSpace(profile.ChatTemplateFile);
            if (profile.Jinja || hasChatTemplate) args.Add("--jinja");
            if (!string.IsNullOrWhiteSpace(profile.ChatTemplateFile))
                Add(args, "--chat-template-file", profile.ChatTemplateFile);
            else if (!string.IsNullOrWhiteSpace(profile.ChatTemplate))
                Add(args, "--chat-template", profile.ChatTemplate);
            if (profile.DisableWebUi) args.Add("--no-webui");
            if (profile.NoMmap) args.Add("--no-mmap");
            if (profile.Mlock) args.Add("--mlock");
            if (profile.EnableMetrics) args.Add("--metrics");
            if (!string.IsNullOrWhiteSpace(profile.Reasoning))
            {
                Add(args, "--reasoning", profile.Reasoning);
            }
            if (!string.IsNullOrWhiteSpace(profile.ExtraArguments))
            {
                args.Add(profile.ExtraArguments.Trim());
            }

            return string.Join(" ", args.ToArray());
        }

        public static string BuildDisplayCommand(ModelProfile profile)
        {
            if (profile != null && profile.UseCustomCommand)
                return (profile.CustomCommand ?? string.Empty).Trim();
            string executable = string.IsNullOrWhiteSpace(profile.ServerExecutable) ? "llama-server.exe" : Quote(profile.ServerExecutable);
            return executable + " " + BuildArguments(profile);
        }

        public static string BuildSafeDisplayCommand(ModelProfile profile)
        {
            string command = BuildDisplayCommand(profile);
            command = Regex.Replace(command,
                "(?i)(--(?:api-key|token|password)\\s+)(?:\"[^\"]*\"|\\S+)", "$1\"***\"");
            command = Regex.Replace(command,
                "(?i)(--(?:api-key|token|password)=)(?:\"[^\"]*\"|\\S+)", "$1\"***\"");
            return command;
        }

        public static string BuildLaunchExecutable(ModelProfile profile)
        {
            if (profile == null) return string.Empty;
            string executable;
            string arguments;
            if (profile.UseCustomCommand)
            {
                if (CommandParser.TrySplitExecutableAndArguments(profile.CustomCommand, profile.ServerExecutable, out executable, out arguments))
                    return executable;
                return string.Empty;
            }
            return profile.ServerExecutable;
        }

        public static string BuildLaunchArguments(ModelProfile profile)
        {
            if (profile == null) return string.Empty;
            string executable;
            string arguments;
            if (profile.UseCustomCommand)
            {
                if (CommandParser.TrySplitExecutableAndArguments(profile.CustomCommand, profile.ServerExecutable, out executable, out arguments))
                    return arguments;
                return profile.CustomCommand ?? string.Empty;
            }
            return BuildArguments(profile);
        }

        public static List<string> ValidateForStart(ModelProfile profile)
        {
            List<string> errors = new List<string>();
            string launchExecutable = BuildLaunchExecutable(profile);
            if (string.IsNullOrWhiteSpace(launchExecutable) || !File.Exists(launchExecutable))
                errors.Add("找不到 llama-server.exe：" + launchExecutable);
            if (string.IsNullOrWhiteSpace(profile.ModelPath) || !File.Exists(profile.ModelPath))
                errors.Add("找不到模型文件：" + profile.ModelPath);
            if (!string.IsNullOrWhiteSpace(profile.MmprojPath) && !File.Exists(profile.MmprojPath))
                errors.Add("找不到 mmproj 文件：" + profile.MmprojPath);
            if (!string.IsNullOrWhiteSpace(profile.ChatTemplateFile) && !File.Exists(profile.ChatTemplateFile))
                errors.Add("找不到聊天模板文件：" + profile.ChatTemplateFile);
            if (!string.IsNullOrWhiteSpace(profile.ApiKeyFile))
            {
                string apiKeyError;
                if (!ApiKeyFileSupport.TryOpenForRead(profile.ApiKeyFile, out apiKeyError))
                    errors.Add("API Key 文件无法读取：" + apiKeyError + "。请在“API Key 管理”中重新选择或新建密钥。");
            }
            if (profile.Port < 1 || profile.Port > 65535)
                errors.Add("端口必须在 1 到 65535 之间。");
            if (profile.ContextSize < 0)
                errors.Add("上下文长度不能小于 0；0 表示使用模型默认值。");
            if (profile.Parallel < 1)
                errors.Add("并发数必须大于 0。");
            if (profile.Threads < 0)
                errors.Add("CPU 线程数不能小于 0；0 表示由 llama.cpp 自动选择。");
            if (profile.BatchSize < 1 || profile.UbatchSize < 1 || profile.UbatchSize > profile.BatchSize)
                errors.Add("批处理参数无效：ubatch 必须大于 0 且不能超过 batch。");
            if (profile.UseCustomCommand)
            {
                CommandPreflightResult preflight = CommandPreflightValidator.Validate(profile.CustomCommand, profile, true);
                foreach (CommandDiagnosticIssue issue in preflight.Issues)
                    if (issue.Severity == CommandDiagnosticSeverity.Error && !errors.Contains(issue.Message)) errors.Add(issue.Message);
            }
            return errors;
        }

        private static void Add(List<string> args, string flag, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            args.Add(flag + " " + Quote(value));
        }

        public static string Quote(string value)
        {
            if (value == null) return "\"\"";
            // Windows paths use backslashes literally; only embedded quotes need escaping.
            string escaped = value.Replace("\"", "\\\"");
            return "\"" + escaped + "\"";
        }
    }

    public sealed class ServerProcessManager : IDisposable
    {
        private Process process;
        private readonly object sync = new object();
        private readonly object completionSync = new object();
        private readonly Queue<string> recentOutput = new Queue<string>();
        private bool expectedStop;
        private bool stopping;
        private bool starting;
        private Task<bool> stopTask;
        private string activeRuntimeApiKeyPath = string.Empty;

        public event Action<string, bool> LogReceived;
        public event Action<bool, int> RunningChanged;

        public bool IsRunning
        {
            get
            {
                lock (sync)
                {
                    return process != null && !HasExited(process);
                }
            }
        }

        public int ProcessId
        {
            get
            {
                lock (sync)
                {
                    if (process == null || HasExited(process)) return 0;
                    try { return process.Id; } catch { return 0; }
                }
            }
        }

        public bool IsStopping
        {
            get { lock (sync) return stopping; }
        }

        public void Start(ModelProfile profile)
        {
            Process created;
            bool apiKeyBridged = false;
            string launchApiKeyPath = string.Empty;
            string launchExecutable = CommandBuilder.BuildLaunchExecutable(profile);
            string launchArguments = CommandBuilder.BuildLaunchArguments(profile);
            if (!string.IsNullOrWhiteSpace(profile.ApiKeyFile))
            {
                launchApiKeyPath = ApiKeyFileSupport.PrepareForLaunch(profile.ApiKeyFile, out apiKeyBridged);
                launchArguments = ReplaceApiKeyFileArgument(launchArguments, launchApiKeyPath);
            }
            lock (sync)
            {
                if (stopping || starting) throw new InvalidOperationException("llama-server 正在切换状态，请等待进程和端口释放。");
                if (process != null && !HasExited(process))
                    throw new InvalidOperationException("llama-server 已经在运行。");

                if (process != null)
                {
                    try { process.Dispose(); } catch { }
                    process = null;
                }

                starting = true;
                expectedStop = false;
                stopTask = null;
                recentOutput.Clear();
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = launchExecutable;
                psi.Arguments = launchArguments;
                string workingDirectory = Path.GetDirectoryName(psi.FileName);
                psi.WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? AppDomain.CurrentDomain.BaseDirectory : workingDirectory;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                created = new Process();
                created.StartInfo = psi;
                created.EnableRaisingEvents = true;
                created.OutputDataReceived += OnOutputDataReceived;
                created.ErrorDataReceived += OnErrorDataReceived;
                created.Exited += OnExited;
                process = created;
                activeRuntimeApiKeyPath = apiKeyBridged ? launchApiKeyPath : string.Empty;
            }

            bool started = false;
            Emit("准备启动：" + CommandBuilder.BuildSafeDisplayCommand(profile), false);
            if (apiKeyBridged) Emit("API Key 位于含非 ASCII 字符的目录，已使用仅供本次启动的兼容路径；密钥内容不会写入日志。", false);
            try
            {
                if (!created.Start()) throw new InvalidOperationException("无法启动 llama-server 进程。");
                started = true;
                created.BeginOutputReadLine();
                created.BeginErrorReadLine();
                int pid = created.Id;
                lock (sync)
                {
                    if (!ReferenceEquals(process, created) || HasExited(created))
                        throw new InvalidOperationException("llama-server 在初始化完成前退出。");
                    starting = false;
                }
                Emit("进程已启动，PID " + pid + "；正在加载模型，管理器不会因加载时间较长而自动停止。", false);
                RaiseRunningChanged(true, pid);
            }
            catch
            {
                created.Exited -= OnExited;
                created.OutputDataReceived -= OnOutputDataReceived;
                created.ErrorDataReceived -= OnErrorDataReceived;
                if (started && !HasExited(created))
                {
                    try { created.Kill(); } catch { }
                    try { created.WaitForExit(5000); } catch { }
                }
                lock (sync)
                {
                    if (ReferenceEquals(process, created)) process = null;
                    if (apiKeyBridged && string.Equals(activeRuntimeApiKeyPath, launchApiKeyPath, StringComparison.OrdinalIgnoreCase))
                        activeRuntimeApiKeyPath = string.Empty;
                    starting = false;
                    stopping = false;
                }
                try { created.Dispose(); } catch { }
                if (apiKeyBridged) ApiKeyFileSupport.ReleaseRuntimeCopy(launchApiKeyPath);
                throw;
            }
        }

        internal static string ReplaceApiKeyFileArgument(string arguments, string replacementPath)
        {
            if (string.IsNullOrWhiteSpace(arguments) || string.IsNullOrWhiteSpace(replacementPath)) return arguments ?? string.Empty;
            List<string> errors = new List<string>();
            List<string> tokens = CommandParser.Tokenize(arguments, errors);
            if (errors.Count > 0) return arguments;
            bool replaced = false;
            for (int i = 0; i < tokens.Count; i++)
            {
                if (string.Equals(tokens[i], "--api-key-file", StringComparison.OrdinalIgnoreCase) && i + 1 < tokens.Count)
                {
                    tokens[i + 1] = replacementPath;
                    replaced = true;
                    i++;
                }
                else if (tokens[i].StartsWith("--api-key-file=", StringComparison.OrdinalIgnoreCase))
                {
                    tokens[i] = "--api-key-file=" + replacementPath;
                    replaced = true;
                }
            }
            if (!replaced) return arguments;
            List<string> rebuilt = new List<string>();
            foreach (string token in tokens)
                rebuilt.Add(token.IndexOfAny(new char[] { ' ', '\t', '"' }) >= 0 ? CommandBuilder.Quote(token) : token);
            return string.Join(" ", rebuilt.ToArray());
        }

        public void Stop()
        {
            StopAsync(15000).GetAwaiter().GetResult();
        }

        public Task<bool> StopAsync(int timeoutMilliseconds)
        {
            lock (sync)
            {
                if (stopTask != null && !stopTask.IsCompleted) return stopTask;
                if (process == null)
                {
                    stopping = false;
                    return Task.FromResult(true);
                }
                Process target = process;
                expectedStop = true;
                stopping = true;
                stopTask = Task.Run<bool>(delegate { return StopCore(target, Math.Max(1000, timeoutMilliseconds)); });
                return stopTask;
            }
        }

        private bool StopCore(Process current, int timeoutMilliseconds)
        {
            if (HasExited(current))
            {
                CompleteExit(current);
                return true;
            }

            int pid = 0;
            try { pid = current.Id; } catch { }
            Emit("正在停止 PID " + pid + "……", false);

            try
            {
                current.Kill();
                Stopwatch wait = Stopwatch.StartNew();
                while (wait.ElapsedMilliseconds < timeoutMilliseconds)
                {
                    lock (sync)
                        if (!ReferenceEquals(process, current)) return true;
                    if (HasExited(current)) break;
                    Thread.Sleep(25);
                }
                lock (sync)
                {
                    if (!ReferenceEquals(process, current)) return true;
                }
                if (!HasExited(current))
                {
                    Emit("停止进程超时：旧进程可能仍持有端口或显存。已禁止立即重启，请在任务管理器确认 PID " + pid + "。", true);
                    return false;
                }
                try { current.WaitForExit(); } catch { }
                CompleteExit(current);
                lock (sync) return !ReferenceEquals(process, current);
            }
            catch (Exception ex)
            {
                lock (sync)
                    if (!ReferenceEquals(process, current)) return true;
                Emit("停止失败：" + ex.Message, true);
                return false;
            }
        }

        public Task<string> ProbeBackendAsync(string executable)
        {
            return Task.Factory.StartNew(delegate
            {
                if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                    return "找不到 llama-server.exe：" + executable;

                StringBuilder output = new StringBuilder();
                output.AppendLine(RunProbe(executable, "--version"));
                output.AppendLine(RunProbe(executable, "--list-devices"));
                return output.ToString().Trim();
            });
        }

        private static string RunProbe(string executable, string arguments)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = executable;
            psi.Arguments = arguments;
            psi.WorkingDirectory = Path.GetDirectoryName(executable);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;

            using (Process probe = Process.Start(psi))
            {
                string stdout = probe.StandardOutput.ReadToEnd();
                string stderr = probe.StandardError.ReadToEnd();
                if (!probe.WaitForExit(15000))
                {
                    try { probe.Kill(); } catch { }
                    return arguments + " 检测超时。";
                }
                return (stdout + Environment.NewLine + stderr).Trim();
            }
        }

        private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && IsCurrentProcess(sender as Process)) { RememberOutput(e.Data); Emit(e.Data, false); }
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && IsCurrentProcess(sender as Process)) { RememberOutput(e.Data); Emit(e.Data, IsErrorLine(e.Data)); }
        }

        private bool IsCurrentProcess(Process candidate)
        {
            lock (sync) return candidate != null && ReferenceEquals(process, candidate);
        }

        private void OnExited(object sender, EventArgs e)
        {
            CompleteExit(sender as Process);
        }

        private void CompleteExit(Process exited)
        {
            if (exited == null) return;
            string runtimeKeyToRelease = string.Empty;
            lock (completionSync)
            {
                bool wasExpected;
                int code = -1;
                string diagnosis = string.Empty;
                lock (sync)
                {
                    if (!ReferenceEquals(process, exited) || !HasExited(exited)) return;
                    stopping = true;
                    starting = false;
                    wasExpected = expectedStop;
                    try { code = exited.ExitCode; } catch { }
                    if (!wasExpected) diagnosis = DiagnoseOutputSnapshot(recentOutput.ToArray());
                }
                Emit((wasExpected ? "进程已停止" : "进程意外退出") + "，退出代码 " + code, !wasExpected && code != 0);
                if (!string.IsNullOrWhiteSpace(diagnosis)) Emit("启动失败诊断：" + diagnosis, true);
                RaiseRunningChanged(false, 0);

                lock (sync)
                {
                    if (!ReferenceEquals(process, exited)) return;
                    process = null;
                    runtimeKeyToRelease = activeRuntimeApiKeyPath;
                    activeRuntimeApiKeyPath = string.Empty;
                    stopping = false;
                    exited.OutputDataReceived -= OnOutputDataReceived;
                    exited.ErrorDataReceived -= OnErrorDataReceived;
                    exited.Exited -= OnExited;
                }
                try { exited.Dispose(); } catch { }
                ApiKeyFileSupport.ReleaseRuntimeCopy(runtimeKeyToRelease);
            }
        }

        private void RememberOutput(string line)
        {
            lock (sync)
            {
                recentOutput.Enqueue(line);
                while (recentOutput.Count > 120) recentOutput.Dequeue();
            }
        }

        private static string DiagnoseOutputSnapshot(string[] lines)
        {
            string text = string.Join("\n", lines ?? new string[0]).ToLowerInvariant();
            if (text.Contains("api-key-file") && (text.Contains("failed to open") || text.Contains("cannot open") || text.Contains("access denied")))
                return "API Key 文件无法读取。请在“API Key 管理”中重新选择或新建密钥；若路径含中文或特殊字符，LlamaLift 会在启动时自动使用兼容路径。";
            if (text.Contains("out of memory") || text.Contains("cuda error") || text.Contains("failed to allocate") || text.Contains("alloc failed"))
                return "显存或内存不足。请降低上下文、并发、GPU 层或 KV Cache 精度后重试。";
            if (text.Contains("unknown argument") || text.Contains("invalid argument") || text.Contains("unrecognized option"))
                return "当前 llama-server 不支持某个启动参数。请在参数工作台运行预检，或用该运行时的 --help 核对参数。";
            if (text.Contains("address already in use") || text.Contains("bind") && text.Contains("failed"))
                return "端口绑定失败。旧进程或其他服务仍在占用端口，请等待释放或更换端口。";
            if (text.Contains("failed to load model") || text.Contains("error loading model") || text.Contains("failed to open"))
                return "模型加载失败。请检查 GGUF 完整性、模型路径、分片文件和运行时兼容性。";
            return "llama-server 在模型就绪前退出。请查看退出前最后几行日志；管理器不会因加载时间过长主动终止进程。";
        }

        private static bool IsErrorLine(string line)
        {
            string value = line.ToLowerInvariant();
            return value.Contains(" error") || value.Contains("exception") || value.Contains("failed") || value.Contains("fatal");
        }

        private static bool HasExited(Process value)
        {
            try { return value.HasExited; } catch { return true; }
        }

        private void Emit(string message, bool error)
        {
            Action<string, bool> handler = LogReceived;
            if (handler == null) return;
            foreach (Action<string, bool> subscriber in handler.GetInvocationList())
                try { subscriber(message, error); } catch { }
        }

        private void RaiseRunningChanged(bool running, int pid)
        {
            Action<bool, int> handler = RunningChanged;
            if (handler == null) return;
            foreach (Action<bool, int> subscriber in handler.GetInvocationList())
                try { subscriber(running, pid); } catch { }
        }

        public void Dispose()
        {
            Process current;
            lock (sync) { current = process; }
            if (current != null)
            {
                if (!HasExited(current))
                {
                    try { current.Kill(); } catch { }
                    try { current.WaitForExit(3000); } catch { }
                }
                try { current.Dispose(); } catch { }
            }
        }
    }

    public sealed class ApiCheckResult
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string Summary { get; set; }
        public string Body { get; set; }

        public ApiCheckResult()
        {
            Summary = string.Empty;
            Body = string.Empty;
        }
    }

    internal sealed class ApiProtocolRequest
    {
        public string Protocol { get; set; }
        public string RelativePath { get; set; }
        public string AuthenticationHeader { get; set; }
        public string Json { get; set; }
    }

    public static class LlamaApiClient
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static Task<ApiCheckResult> CheckHealthAsync(ModelProfile profile)
        {
            return Task.Factory.StartNew(delegate
            {
                return Send("GET", LocalBaseUrl(profile) + "/health", null, null, 5000);
            });
        }

        public static Task<ApiCheckResult> CheckModelsAsync(ModelProfile profile)
        {
            return Task.Factory.StartNew(delegate
            {
                return Send("GET", LocalBaseUrl(profile) + "/v1/models", null, null, 10000);
            });
        }

        public static Task<ApiCheckResult> TestResponsesAsync(ModelProfile profile)
        {
            return TestProtocolAsync(profile, ApiProtocolMode.Responses);
        }

        public static Task<ApiCheckResult> TestChatCompletionsAsync(ModelProfile profile)
        {
            return TestProtocolAsync(profile, ApiProtocolMode.ChatCompletions);
        }

        public static Task<ApiCheckResult> TestAnthropicMessagesAsync(ModelProfile profile)
        {
            return TestProtocolAsync(profile, ApiProtocolMode.AnthropicMessages);
        }

        public static Task<ApiCheckResult> TestSelectedProtocolAsync(ModelProfile profile)
        {
            return TestProtocolAsync(profile, profile == null ? ApiProtocolMode.Responses : profile.ApiProtocol);
        }

        public static Task<ApiCheckResult> TestProtocolAsync(ModelProfile profile, string protocol)
        {
            return Task.Factory.StartNew(delegate
            {
                if (profile == null) return new ApiCheckResult { Success = false, Summary = "没有可测试的模型配置。" };
                string key;
                ApiCheckResult keyError = ReadApiKey(profile, out key);
                if (keyError != null) return keyError;
                ApiProtocolRequest request = BuildProtocolTestRequest(profile, protocol);
                return Send(
                    "POST",
                    LocalBaseUrl(profile) + request.RelativePath,
                    request.Json,
                    key,
                    180000,
                    request.AuthenticationHeader);
            });
        }

        internal static ApiProtocolRequest BuildProtocolTestRequest(ModelProfile profile, string protocol)
        {
            string normalized = ApiProtocolMode.Normalize(protocol);
            Dictionary<string, object> body = new Dictionary<string, object>();
            body["model"] = string.IsNullOrWhiteSpace(profile.Alias) ? "local-model" : profile.Alias;
            body["stream"] = false;

            if (normalized == ApiProtocolMode.Responses)
            {
                body["input"] = "只需简短回复：Responses API 测试成功";
                body["max_output_tokens"] = 8;
            }
            else
            {
                Dictionary<string, object> userMessage = new Dictionary<string, object>();
                userMessage["role"] = "user";
                userMessage["content"] = normalized == ApiProtocolMode.AnthropicMessages
                    ? "只需简短回复：Anthropic Messages API 测试成功"
                    : "只需简短回复：Chat Completions API 测试成功";
                body["messages"] = new object[] { userMessage };
                body["max_tokens"] = 8;
            }

            return new ApiProtocolRequest
            {
                Protocol = normalized,
                RelativePath = ApiProtocolMode.EndpointPath(normalized),
                AuthenticationHeader = normalized == ApiProtocolMode.AnthropicMessages ? "x-api-key" : "Authorization",
                Json = Serializer.Serialize(body)
            };
        }

        private static ApiCheckResult ReadApiKey(ModelProfile profile, out string key)
        {
            key = string.Empty;
            if (string.IsNullOrWhiteSpace(profile.ApiKeyFile))
            {
                return null;
            }
            if (!File.Exists(profile.ApiKeyFile))
            {
                return new ApiCheckResult
                {
                    Success = false,
                    Summary = "无法测试鉴权：API Key 文件不存在。"
                };
            }

            try
            {
                string[] lines = File.ReadAllLines(profile.ApiKeyFile, Encoding.UTF8);
                foreach (string line in lines)
                {
                    string candidate = line.Trim();
                    if (candidate.Length == 0 || candidate.StartsWith("#", StringComparison.Ordinal)) continue;
                    key = candidate;
                    break;
                }
            }
            catch (Exception ex)
            {
                return new ApiCheckResult
                {
                    Success = false,
                    Summary = "无法读取 API Key 文件：" + ex.Message
                };
            }
            if (string.IsNullOrWhiteSpace(key))
            {
                return new ApiCheckResult
                {
                    Success = false,
                    Summary = "API Key 文件为空。"
                };
            }
            return null;
        }

        public static string LocalBaseUrl(ModelProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            return "http://127.0.0.1:" + profile.Port;
        }

        public static string LanBaseUrl(ModelProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            string host = profile.AdvertisedHost;
            if (string.IsNullOrWhiteSpace(host))
            {
                host = profile.Host;
                if (host == "0.0.0.0" || host == "::") host = NetworkHelper.GetPreferredLanIPv4();
            }
            return "http://" + host + ":" + profile.Port;
        }

        public static string ProtocolClientBaseUrl(ModelProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            return LanBaseUrl(profile) + ApiProtocolMode.ClientBasePath(profile.ApiProtocol);
        }

        public static string ProtocolEndpointUrl(ModelProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            return LanBaseUrl(profile) + ApiProtocolMode.EndpointPath(profile.ApiProtocol);
        }

        private static ApiCheckResult Send(string method, string url, string json, string apiKey, int timeout, string authenticationHeader = "Authorization")
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                request.Timeout = timeout;
                request.ReadWriteTimeout = timeout;
                request.Accept = "application/json";
                request.UserAgent = "LlamaLift/" + AppVersion.ProductVersion;
                request.Proxy = null;
                request.KeepAlive = false;
                request.ServicePoint.Expect100Continue = false;

                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    if (string.Equals(authenticationHeader, "x-api-key", StringComparison.OrdinalIgnoreCase))
                        request.Headers["x-api-key"] = apiKey;
                    else
                        request.Headers[HttpRequestHeader.Authorization] = "Bearer " + apiKey;
                }

                if (json != null)
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(json);
                    request.ContentType = "application/json; charset=utf-8";
                    request.ContentLength = bytes.Length;
                    using (Stream stream = request.GetRequestStream())
                        stream.Write(bytes, 0, bytes.Length);
                }

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    string responseBody = ReadBody(response);
                    return new ApiCheckResult
                    {
                        Success = (int)response.StatusCode >= 200 && (int)response.StatusCode < 300,
                        StatusCode = (int)response.StatusCode,
                        Summary = "HTTP " + (int)response.StatusCode + " " + response.StatusDescription,
                        Body = responseBody
                    };
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    using (response)
                    {
                        return new ApiCheckResult
                        {
                            Success = false,
                            StatusCode = (int)response.StatusCode,
                            Summary = "HTTP " + (int)response.StatusCode + " " + response.StatusDescription,
                            Body = ReadBody(response)
                        };
                    }
                }
                return new ApiCheckResult
                {
                    Success = false,
                    StatusCode = 0,
                    Summary = ex.Status == WebExceptionStatus.Timeout ? "请求超时" : ex.Message,
                    Body = string.Empty
                };
            }
            catch (Exception ex)
            {
                return new ApiCheckResult
                {
                    Success = false,
                    StatusCode = 0,
                    Summary = ex.Message,
                    Body = string.Empty
                };
            }
        }

        private static string ReadBody(HttpWebResponse response)
        {
            Stream stream = response.GetResponseStream();
            if (stream == null) return string.Empty;
            using (stream)
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }
    }

    public static class NetworkHelper
    {
        public static bool IsTcpPortInUse(int port)
        {
            if (port < 1 || port > 65535) return false;
            try
            {
                foreach (IPEndPoint endpoint in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                    if (endpoint.Port == port) return true;
            }
            catch { }
            return false;
        }

        public static Task<bool> WaitForTcpPortReleaseAsync(int port, int timeoutMilliseconds)
        {
            return Task.Factory.StartNew<bool>(delegate
            {
                Stopwatch timer = Stopwatch.StartNew();
                while (timer.ElapsedMilliseconds < timeoutMilliseconds)
                {
                    if (!IsTcpPortInUse(port)) return true;
                    Thread.Sleep(200);
                }
                return !IsTcpPortInUse(port);
            });
        }

        public static string GetPreferredLanIPv4()
        {
            string fallback = string.Empty;
            try
            {
                NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface adapter in interfaces)
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback || adapter.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (address.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string value = address.Address.ToString();
                        if (value.StartsWith("169.254.", StringComparison.Ordinal)) continue;
                        if (IsPrivateIPv4(value)) return value;
                        if (string.IsNullOrWhiteSpace(fallback)) fallback = value;
                    }
                }
            }
            catch { }
            return string.IsNullOrWhiteSpace(fallback) ? "127.0.0.1" : fallback;
        }

        private static bool IsPrivateIPv4(string value)
        {
            if (value.StartsWith("10.", StringComparison.Ordinal)) return true;
            if (value.StartsWith("192.168.", StringComparison.Ordinal)) return true;
            if (!value.StartsWith("172.", StringComparison.Ordinal)) return false;
            string[] parts = value.Split('.');
            int second;
            return parts.Length == 4 && Int32.TryParse(parts[1], out second) && second >= 16 && second <= 31;
        }
    }
}
