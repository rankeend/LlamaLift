using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace LlamaServerManager
{
    public enum CommandDiagnosticSeverity
    {
        Information,
        Warning,
        Error
    }

    public sealed class CommandDiagnosticIssue
    {
        public string Code { get; set; }
        public CommandDiagnosticSeverity Severity { get; set; }
        public string Message { get; set; }
        public string Suggestion { get; set; }

        public CommandDiagnosticIssue()
        {
            Code = string.Empty;
            Message = string.Empty;
            Suggestion = string.Empty;
        }
    }

    public sealed class CommandPreflightResult
    {
        public CommandParseResult ParseResult { get; set; }
        public List<CommandDiagnosticIssue> Issues { get; private set; }

        public int ErrorCount { get { return Count(CommandDiagnosticSeverity.Error); } }
        public int WarningCount { get { return Count(CommandDiagnosticSeverity.Warning); } }
        public bool CanLikelyRun { get { return ErrorCount == 0; } }

        public CommandPreflightResult()
        {
            Issues = new List<CommandDiagnosticIssue>();
        }

        public string StatusText
        {
            get
            {
                if (ErrorCount > 0) return "存在较高启动失败风险";
                if (WarningCount > 0) return "基本可运行，但有项目需要确认";
                return "静态预检通过，参数具备正常启动条件";
            }
        }

        public string BuildReviewText(int maximumIssues)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine(StatusText + "。");
            text.AppendLine("发现 " + ErrorCount + " 个错误风险、" + WarningCount + " 个提醒。");
            text.AppendLine();
            int count = Math.Min(Math.Max(1, maximumIssues), Issues.Count);
            for (int i = 0; i < count; i++)
            {
                CommandDiagnosticIssue issue = Issues[i];
                string marker = issue.Severity == CommandDiagnosticSeverity.Error ? "错误风险" :
                    issue.Severity == CommandDiagnosticSeverity.Warning ? "提醒" : "信息";
                text.AppendLine("[" + marker + "] " + issue.Message);
                if (!string.IsNullOrWhiteSpace(issue.Suggestion)) text.AppendLine("修改建议：" + issue.Suggestion);
                text.AppendLine();
            }
            if (Issues.Count > count) text.AppendLine("另有 " + (Issues.Count - count) + " 项提示，请在参数工作台中继续检查。");
            text.AppendLine("这是保存前静态预检，不会实际加载模型；显存不足、模型损坏等问题仍需启动后才能确认。");
            return text.ToString().TrimEnd();
        }

        private int Count(CommandDiagnosticSeverity severity)
        {
            int count = 0;
            foreach (CommandDiagnosticIssue issue in Issues)
                if (issue.Severity == severity) count++;
            return count;
        }
    }

    public static class CommandPreflightValidator
    {
        private static readonly HashSet<string> KnownCacheTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "f32", "f16", "bf16", "q8_0", "q4_0", "q4_1", "iq4_nl", "q5_0", "q5_1",
            "turbo2", "turbo3", "turbo4"
        };

        public static CommandPreflightResult Validate(string command, ModelProfile baseline, bool inspectEnvironment)
        {
            CommandPreflightResult result = new CommandPreflightResult();
            result.ParseResult = CommandParser.Parse(command, baseline);

            foreach (string error in result.ParseResult.Errors)
                Add(result, "parse", CommandDiagnosticSeverity.Error, error, SuggestForParseError(error));
            foreach (string warning in result.ParseResult.Warnings)
                Add(result, "parse-warning", CommandDiagnosticSeverity.Warning, warning,
                    "核对重复项和未识别参数；未知参数会按原文保留，但可能不受当前 llama.cpp 版本支持。");

            if (Regex.IsMatch(command ?? string.Empty, @"(?i)--(?:api-key|token|password)(?:\s|=)"))
                Add(result, "inline-secret", CommandDiagnosticSeverity.Warning, "命令中可能包含明文密钥或密码，启动日志会自动脱敏，但配置文件仍需保存原文。",
                    "优先使用“API Key 管理”生成本地密钥文件，并改用 --api-key-file，避免在命令中直接写入凭据。");

            ModelProfile profile = result.ParseResult.Profile ?? (baseline == null ? ModelProfile.CreateGenericProfile() : baseline.Clone());
            string executable;
            string arguments;
            if (!CommandParser.TrySplitExecutableAndArguments(command, profile.ServerExecutable, out executable, out arguments))
            {
                Add(result, "executable", CommandDiagnosticSeverity.Error, "无法从命令中确定 llama-server 可执行文件。",
                    "把 llama-server.exe 的完整路径放在命令开头；路径包含空格时使用双引号。也可以只输入参数，但需先在模型配置中选择程序。"
                );
            }
            else
            {
                if (!executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    Add(result, "executable-extension", CommandDiagnosticSeverity.Warning, "命令开头不像 Windows 可执行文件：" + executable,
                        "确认这里指向 llama-server.exe，而不是目录、压缩包或模型文件。");
                if (inspectEnvironment && !File.Exists(executable))
                    Add(result, "executable-missing", CommandDiagnosticSeverity.Error, "找不到 llama-server.exe：" + executable,
                        "重新选择已安装的 llama-server.exe，或先在“运行环境”页面安装对应版本。");
            }

            if (string.IsNullOrWhiteSpace(profile.ModelPath))
                Add(result, "model-empty", CommandDiagnosticSeverity.Error, "命令中没有可用的模型路径。", "添加 --model \"模型.gguf\"，或先在简易配置中选择主模型。");
            else
            {
                if (!profile.ModelPath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                    Add(result, "model-extension", CommandDiagnosticSeverity.Warning, "主模型文件不是常见的 .gguf 文件：" + profile.ModelPath,
                        "确认所选文件为 llama.cpp 可读取的 GGUF 模型，而不是分片索引、压缩包或其他格式。");
                if (inspectEnvironment && !File.Exists(profile.ModelPath))
                    Add(result, "model-missing", CommandDiagnosticSeverity.Error, "找不到模型文件：" + profile.ModelPath,
                        "检查盘符、文件名和引号；移动模型后需要重新选择路径。");
            }

            if (!string.IsNullOrWhiteSpace(profile.MmprojPath))
            {
                if (!profile.MmprojPath.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                    Add(result, "mmproj-extension", CommandDiagnosticSeverity.Warning, "视觉投影文件不是 .gguf：" + profile.MmprojPath,
                        "视觉模型通常使用 mmproj-*.gguf；纯文本模型可移除 --mmproj。");
                if (inspectEnvironment && !File.Exists(profile.MmprojPath))
                    Add(result, "mmproj-missing", CommandDiagnosticSeverity.Error, "找不到视觉模型文件：" + profile.MmprojPath,
                        "重新选择与主模型匹配的 mmproj 文件，或删除 --mmproj 参数。");
            }

            ValidateApiKey(result, profile.ApiKeyFile, inspectEnvironment);
            ValidateNumericAndMemoryHints(result, profile);

            if (inspectEnvironment && profile.Port >= 1 && profile.Port <= 65535 && IsPortInUse(profile.Port))
                Add(result, "port-in-use", CommandDiagnosticSeverity.Warning, "端口 " + profile.Port + " 当前已被其他进程占用。",
                    "关闭原有 llama-server，或把 --port 改为未占用的端口；如果正是当前服务，可忽略此提醒。");

            if (IsLanBinding(profile.Host) && string.IsNullOrWhiteSpace(profile.ApiKeyFile))
                Add(result, "lan-without-key", CommandDiagnosticSeverity.Warning, "监听地址会向局域网开放，但没有配置 API Key。",
                    "在“API Key 管理”中创建或选择密钥文件；仅本机使用时把 --host 改为 127.0.0.1。");

            return result;
        }

        private static void ValidateApiKey(CommandPreflightResult result, string path, bool inspectEnvironment)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            if (inspectEnvironment && !File.Exists(path))
            {
                Add(result, "api-key-missing", CommandDiagnosticSeverity.Error, "找不到 API Key 文件：" + path,
                    "从“API Key 管理”重新选择托管密钥，或移除 --api-key-file 关闭鉴权。");
                return;
            }
            if (!inspectEnvironment) return;
            try
            {
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                int usable = 0;
                foreach (string line in lines) if (!string.IsNullOrWhiteSpace(line)) usable++;
                if (usable == 0)
                    Add(result, "api-key-empty", CommandDiagnosticSeverity.Error, "API Key 文件为空：" + path,
                        "至少保存一个非空密钥，或移除 --api-key-file。");
            }
            catch (Exception ex)
            {
                Add(result, "api-key-unreadable", CommandDiagnosticSeverity.Error, "无法读取 API Key 文件：" + ex.Message,
                    "检查文件权限和编码，或使用管理器重新导入密钥文件。");
            }
        }

        private static void ValidateNumericAndMemoryHints(CommandPreflightResult result, ModelProfile profile)
        {
            int gpuLayers;
            if (!string.Equals(profile.GpuLayers, "auto", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(profile.GpuLayers, "all", StringComparison.OrdinalIgnoreCase) &&
                (!Int32.TryParse(profile.GpuLayers, NumberStyles.Integer, CultureInfo.InvariantCulture, out gpuLayers) || gpuLayers < 0))
                Add(result, "gpu-layers", CommandDiagnosticSeverity.Error, "GPU 层参数无效：" + profile.GpuLayers,
                    "使用 auto、all 或不小于 0 的整数，例如 --n-gpu-layers 60。");

            if (!KnownCacheTypes.Contains(profile.CacheTypeK))
                Add(result, "cache-k", CommandDiagnosticSeverity.Warning, "KV Cache K 类型可能不受支持：" + profile.CacheTypeK,
                    "以当前 llama-server --help 输出为准；turbo2/turbo3/turbo4 仅适用于明确支持 TurboQuant 的分支。");
            if (!KnownCacheTypes.Contains(profile.CacheTypeV))
                Add(result, "cache-v", CommandDiagnosticSeverity.Warning, "KV Cache V 类型可能不受支持：" + profile.CacheTypeV,
                    "以当前 llama-server --help 输出为准；turbo2/turbo3/turbo4 仅适用于明确支持 TurboQuant 的分支。");

            if (profile.ContextSize > 262144)
                Add(result, "large-context", CommandDiagnosticSeverity.Warning, "上下文长度非常大（" + profile.ContextSize + "），可能导致显存或内存不足。",
                    "先用 32768 或 65536 验证模型，再逐步提高；也可使用 q8_0/q4_0 KV Cache。");
            if (profile.ContextSize > 0 && profile.Parallel > 1 && profile.ContextSize / profile.Parallel < 1024)
                Add(result, "parallel-context", CommandDiagnosticSeverity.Warning, "当前上下文与并发组合可能使每个请求可用上下文过小。",
                    "降低 --parallel，或提高 --ctx-size，建议每个并发槽至少预留 1024 tokens。");
            if (profile.UbatchSize > profile.BatchSize)
                Add(result, "batch-order", CommandDiagnosticSeverity.Error, "--ubatch-size 不能大于 --batch-size。",
                    "降低 ubatch，或提高 batch；兼容起点可使用 --batch-size 2048 --ubatch-size 512。");
            if (profile.ImageMinTokens > 0 && string.IsNullOrWhiteSpace(profile.MmprojPath))
                Add(result, "image-without-mmproj", CommandDiagnosticSeverity.Warning, "设置了图片 tokens，但没有配置视觉投影模型。",
                    "选择匹配的 mmproj-*.gguf，或把 --image-min-tokens 设为 0。");
        }

        private static string SuggestForParseError(string error)
        {
            if (error.IndexOf("双引号", StringComparison.OrdinalIgnoreCase) >= 0)
                return "补齐路径或参数值末尾的双引号；Windows 路径包含空格时必须成对使用双引号。";
            if (error.IndexOf("缺少参数值", StringComparison.OrdinalIgnoreCase) >= 0)
                return "在该参数后补充值，或使用 --参数=值 的写法。";
            if (error.IndexOf("ubatch", StringComparison.OrdinalIgnoreCase) >= 0)
                return "让 --ubatch-size 小于或等于 --batch-size。";
            return "检查参数拼写、允许范围和 on/off 布尔值；可先从简易表单重新生成，再逐项修改。";
        }

        private static bool IsPortInUse(int port)
        {
            try
            {
                foreach (IPEndPoint endpoint in IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners())
                    if (endpoint.Port == port) return true;
            }
            catch { }
            return false;
        }

        private static bool IsLanBinding(string host)
        {
            return string.Equals(host, "0.0.0.0", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, "::", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, "[::]", StringComparison.OrdinalIgnoreCase);
        }

        private static void Add(CommandPreflightResult result, string code, CommandDiagnosticSeverity severity, string message, string suggestion)
        {
            foreach (CommandDiagnosticIssue existing in result.Issues)
                if (existing.Code == code && existing.Message == message) return;
            result.Issues.Add(new CommandDiagnosticIssue
            {
                Code = code,
                Severity = severity,
                Message = message,
                Suggestion = suggestion
            });
        }
    }
}
