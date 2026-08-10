using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
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
            args.Add(profile.FlashAttention ? "--flash-attn on" : "--flash-attn off");
            Add(args, "--cache-type-k", profile.CacheTypeK);
            Add(args, "--cache-type-v", profile.CacheTypeV);

            if (profile.ImageMinTokens > 0)
            {
                Add(args, "--image-min-tokens", profile.ImageMinTokens.ToString());
            }
            if (profile.Jinja) args.Add("--jinja");
            if (profile.DisableWebUi) args.Add("--no-webui");
            if (profile.NoMmap) args.Add("--no-mmap");
            if (profile.Mlock) args.Add("--mlock");
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
            return Quote(profile.ServerExecutable) + " " + BuildArguments(profile);
        }

        public static List<string> ValidateForStart(ModelProfile profile)
        {
            List<string> errors = new List<string>();
            if (string.IsNullOrWhiteSpace(profile.ServerExecutable) || !File.Exists(profile.ServerExecutable))
                errors.Add("找不到 llama-server.exe：" + profile.ServerExecutable);
            if (string.IsNullOrWhiteSpace(profile.ModelPath) || !File.Exists(profile.ModelPath))
                errors.Add("找不到模型文件：" + profile.ModelPath);
            if (!string.IsNullOrWhiteSpace(profile.MmprojPath) && !File.Exists(profile.MmprojPath))
                errors.Add("找不到 mmproj 文件：" + profile.MmprojPath);
            if (!string.IsNullOrWhiteSpace(profile.ApiKeyFile) && !File.Exists(profile.ApiKeyFile))
                errors.Add("找不到 API Key 文件：" + profile.ApiKeyFile);
            if (profile.Port < 1 || profile.Port > 65535)
                errors.Add("端口必须在 1 到 65535 之间。");
            if (profile.ContextSize < 0)
                errors.Add("上下文长度不能小于 0；0 表示使用模型默认值。");
            if (profile.Parallel < 1)
                errors.Add("并发数必须大于 0。");
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
        private bool expectedStop;

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

        public void Start(ModelProfile profile)
        {
            lock (sync)
            {
                if (process != null && !HasExited(process))
                    throw new InvalidOperationException("llama-server 已经在运行。");

                expectedStop = false;
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = profile.ServerExecutable;
                psi.Arguments = CommandBuilder.BuildArguments(profile);
                psi.WorkingDirectory = Path.GetDirectoryName(profile.ServerExecutable);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                process = new Process();
                process.StartInfo = psi;
                process.EnableRaisingEvents = true;
                process.OutputDataReceived += OnOutputDataReceived;
                process.ErrorDataReceived += OnErrorDataReceived;
                process.Exited += OnExited;

                Emit("准备启动：" + CommandBuilder.BuildDisplayCommand(profile), false);
                if (!process.Start())
                    throw new InvalidOperationException("无法启动 llama-server 进程。");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                Emit("进程已启动，PID " + process.Id, false);
                RaiseRunningChanged(true, process.Id);
            }
        }

        public void Stop()
        {
            Process current;
            lock (sync)
            {
                current = process;
                expectedStop = true;
            }

            if (current == null || HasExited(current))
            {
                RaiseRunningChanged(false, 0);
                return;
            }

            int pid = 0;
            try { pid = current.Id; } catch { }
            Emit("正在停止 PID " + pid + "……", false);

            try
            {
                current.Kill();
                if (!current.WaitForExit(10000))
                    Emit("停止进程超时，请在任务管理器确认。", true);
            }
            catch (Exception ex)
            {
                Emit("停止失败：" + ex.Message, true);
            }
            finally
            {
                RaiseRunningChanged(false, 0);
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
            if (e.Data != null) Emit(e.Data, false);
        }

        private void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null) Emit(e.Data, IsErrorLine(e.Data));
        }

        private void OnExited(object sender, EventArgs e)
        {
            int code = -1;
            try { code = ((Process)sender).ExitCode; } catch { }
            Emit((expectedStop ? "进程已停止" : "进程意外退出") + "，退出代码 " + code, !expectedStop && code != 0);
            RaiseRunningChanged(false, 0);
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
            if (handler != null) handler(message, error);
        }

        private void RaiseRunningChanged(bool running, int pid)
        {
            Action<bool, int> handler = RunningChanged;
            if (handler != null) handler(running, pid);
        }

        public void Dispose()
        {
            Process current;
            lock (sync) { current = process; }
            if (current != null)
            {
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
            return Task.Factory.StartNew(delegate
            {
                string key;
                ApiCheckResult keyError = ReadApiKey(profile, out key);
                if (keyError != null) return keyError;

                Dictionary<string, object> body = new Dictionary<string, object>();
                body["model"] = profile.Alias;
                body["input"] = "只需简短回复：Responses API 测试成功";
                body["max_output_tokens"] = 256;
                body["stream"] = false;

                return Send(
                    "POST",
                    LocalBaseUrl(profile) + "/v1/responses",
                    Serializer.Serialize(body),
                    key,
                    180000);
            });
        }

        public static Task<ApiCheckResult> TestChatCompletionsAsync(ModelProfile profile)
        {
            return Task.Factory.StartNew(delegate
            {
                string key;
                ApiCheckResult keyError = ReadApiKey(profile, out key);
                if (keyError != null) return keyError;

                Dictionary<string, object> userMessage = new Dictionary<string, object>();
                userMessage["role"] = "user";
                userMessage["content"] = "只需简短回复：Chat Completions API 测试成功";

                Dictionary<string, object> body = new Dictionary<string, object>();
                body["model"] = profile.Alias;
                body["messages"] = new object[] { userMessage };
                body["max_tokens"] = 256;
                body["stream"] = false;

                return Send(
                    "POST",
                    LocalBaseUrl(profile) + "/v1/chat/completions",
                    Serializer.Serialize(body),
                    key,
                    180000);
            });
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

            string[] lines = File.ReadAllLines(profile.ApiKeyFile, Encoding.UTF8);
            foreach (string line in lines)
            {
                string candidate = line.Trim();
                if (candidate.Length == 0 || candidate.StartsWith("#", StringComparison.Ordinal)) continue;
                key = candidate;
                break;
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
            return "http://127.0.0.1:" + profile.Port;
        }

        public static string LanBaseUrl(ModelProfile profile)
        {
            string host = profile.AdvertisedHost;
            if (string.IsNullOrWhiteSpace(host))
            {
                host = profile.Host;
                if (host == "0.0.0.0" || host == "::") host = NetworkHelper.GetPreferredLanIPv4();
            }
            return "http://" + host + ":" + profile.Port;
        }

        private static ApiCheckResult Send(string method, string url, string json, string bearerKey, int timeout)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Timeout = timeout;
            request.ReadWriteTimeout = timeout;
            request.Accept = "application/json";
            request.UserAgent = "LlamaServerManager/" + AppVersion.ProductVersion;

            if (!string.IsNullOrWhiteSpace(bearerKey))
                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + bearerKey;

            if (json != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                request.ContentType = "application/json; charset=utf-8";
                request.ContentLength = bytes.Length;
                using (Stream stream = request.GetRequestStream())
                    stream.Write(bytes, 0, bytes.Length);
            }

            try
            {
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
