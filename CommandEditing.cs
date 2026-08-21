using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LlamaServerManager
{
    public sealed class CommandParseResult
    {
        public ModelProfile Profile { get; set; }
        public List<string> Errors { get; private set; }
        public List<string> Warnings { get; private set; }
        public int RecognizedCount { get; set; }
        public int UnknownCount { get; set; }

        public bool Success
        {
            get { return Errors.Count == 0; }
        }

        public CommandParseResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }
    }

    public static class CommandParser
    {
        public static CommandParseResult Parse(string command, ModelProfile baseline)
        {
            CommandParseResult result = new CommandParseResult();
            result.Profile = baseline == null ? ModelProfile.CreateGenericProfile() : baseline.Clone();
            List<string> tokens = Tokenize(command, result.Errors);
            if (tokens.Count == 0)
            {
                result.Errors.Add("启动命令不能为空。");
                return result;
            }

            int index = 0;
            if (!LooksLikeOption(tokens[0]))
            {
                result.Profile.ServerExecutable = tokens[0];
                result.RecognizedCount++;
                index = 1;
            }

            List<string> unknown = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (index < tokens.Count)
            {
                string raw = tokens[index];
                if (raw == "--")
                {
                    index++;
                    while (index < tokens.Count) unknown.Add(tokens[index++]);
                    break;
                }

                string inlineValue = null;
                string flag = raw;
                int equals = raw.IndexOf('=');
                if (equals > 0)
                {
                    flag = raw.Substring(0, equals);
                    inlineValue = raw.Substring(equals + 1);
                }

                string canonical = NormalizeFlag(flag);
                if (canonical == null)
                {
                    unknown.Add(raw);
                    if (LooksLikeOption(raw) && index + 1 < tokens.Count &&
                        (!LooksLikeOption(tokens[index + 1]) || IsNegativeNumber(tokens[index + 1])))
                    {
                        unknown.Add(tokens[++index]);
                    }
                    index++;
                    continue;
                }

                if (!seen.Add(canonical)) result.Warnings.Add(canonical + " 重复出现，已采用最后一个值。");
                result.RecognizedCount++;

                if (IsPresenceFlag(canonical))
                {
                    bool enabled = true;
                    if (inlineValue != null && !TryParseBoolean(canonical, inlineValue, result, out enabled))
                    {
                        index++;
                        continue;
                    }
                    ApplyPresenceFlag(result.Profile, canonical, enabled);
                    index++;
                    continue;
                }

                string value = inlineValue;
                if (value == null)
                {
                    if (index + 1 >= tokens.Count ||
                        (LooksLikeOption(tokens[index + 1]) && !IsNegativeNumber(tokens[index + 1])))
                    {
                        result.Errors.Add(canonical + " 缺少参数值。");
                        index++;
                        continue;
                    }
                    value = tokens[++index];
                }

                ApplyValue(result.Profile, canonical, value, result);
                index++;
            }

            result.UnknownCount = unknown.Count;
            result.Profile.ExtraArguments = FormatTokens(unknown);
            if (result.Profile.UbatchSize > result.Profile.BatchSize)
                result.Errors.Add("--ubatch-size 不能大于 --batch-size。");
            if (unknown.Count > 0)
                result.Warnings.Add("有 " + unknown.Count + " 个未映射项，已保留到“自定义参数”，不会丢失。");
            return result;
        }

        public static List<string> Tokenize(string command, List<string> errors)
        {
            List<string> tokens = new List<string>();
            if (string.IsNullOrWhiteSpace(command)) return tokens;
            StringBuilder current = new StringBuilder();
            bool quoted = false;
            bool tokenStarted = false;

            for (int i = 0; i < command.Length; i++)
            {
                char ch = command[i];
                if (ch == '\\' && i + 1 < command.Length && command[i + 1] == '"')
                {
                    current.Append('"');
                    tokenStarted = true;
                    i++;
                }
                else if (ch == '"')
                {
                    quoted = !quoted;
                    tokenStarted = true;
                }
                else if (Char.IsWhiteSpace(ch) && !quoted)
                {
                    if (tokenStarted)
                    {
                        tokens.Add(current.ToString());
                        current.Length = 0;
                        tokenStarted = false;
                    }
                }
                else
                {
                    current.Append(ch);
                    tokenStarted = true;
                }
            }

            if (quoted && errors != null) errors.Add("命令中存在未闭合的双引号。");
            if (tokenStarted) tokens.Add(current.ToString());
            return tokens;
        }

        public static bool TrySplitExecutableAndArguments(string command, string fallbackExecutable, out string executable, out string arguments)
        {
            executable = fallbackExecutable ?? string.Empty;
            arguments = string.Empty;
            if (string.IsNullOrWhiteSpace(command)) return false;

            string value = command.Trim();
            if (LooksLikeOption(value))
            {
                arguments = value;
                return !string.IsNullOrWhiteSpace(executable);
            }

            if (value[0] == '"')
            {
                StringBuilder path = new StringBuilder();
                int index = 1;
                bool closed = false;
                while (index < value.Length)
                {
                    char ch = value[index];
                    if (ch == '\\' && index + 1 < value.Length && value[index + 1] == '"')
                    {
                        path.Append('"');
                        index += 2;
                        continue;
                    }
                    if (ch == '"')
                    {
                        closed = true;
                        index++;
                        break;
                    }
                    path.Append(ch);
                    index++;
                }
                if (!closed) return false;
                executable = path.ToString();
                arguments = index < value.Length ? value.Substring(index).TrimStart() : string.Empty;
                return !string.IsNullOrWhiteSpace(executable);
            }

            int separator = 0;
            while (separator < value.Length && !Char.IsWhiteSpace(value[separator])) separator++;
            executable = value.Substring(0, separator);
            arguments = separator < value.Length ? value.Substring(separator).TrimStart() : string.Empty;
            return !string.IsNullOrWhiteSpace(executable);
        }

        private static void ApplyValue(ModelProfile profile, string flag, string value, CommandParseResult result)
        {
            int parsed;
            bool enabled;
            switch (flag)
            {
                case "--host": profile.Host = value; break;
                case "--model": profile.ModelPath = value; break;
                case "--mmproj": profile.MmprojPath = value; break;
                case "--alias": profile.Alias = value; break;
                case "--api-key-file": profile.ApiKeyFile = value; break;
                case "--chat-template":
                    profile.ChatTemplate = value;
                    profile.ChatTemplateFile = string.Empty;
                    break;
                case "--chat-template-file":
                    profile.ChatTemplateFile = value;
                    profile.ChatTemplate = string.Empty;
                    break;
                case "--n-gpu-layers": profile.GpuLayers = value; break;
                case "--cache-type-k": profile.CacheTypeK = value; break;
                case "--cache-type-v": profile.CacheTypeV = value; break;
                case "--reasoning": profile.Reasoning = value; break;
                case "--port":
                    if (TryParseInteger(flag, value, 1, 65535, result, out parsed)) profile.Port = parsed;
                    break;
                case "--ctx-size":
                    if (TryParseInteger(flag, value, 0, 1048576, result, out parsed)) profile.ContextSize = parsed;
                    break;
                case "--parallel":
                    if (TryParseInteger(flag, value, 1, 128, result, out parsed)) profile.Parallel = parsed;
                    break;
                case "--threads":
                    if (TryParseInteger(flag, value, 0, 512, result, out parsed)) profile.Threads = parsed;
                    break;
                case "--batch-size":
                    if (TryParseInteger(flag, value, 1, 65536, result, out parsed)) profile.BatchSize = parsed;
                    break;
                case "--ubatch-size":
                    if (TryParseInteger(flag, value, 1, 65536, result, out parsed)) profile.UbatchSize = parsed;
                    break;
                case "--fit-target":
                    if (TryParseInteger(flag, value, 0, 1048576, result, out parsed)) profile.FitTarget = parsed;
                    break;
                case "--image-min-tokens":
                    if (TryParseInteger(flag, value, 0, 1048576, result, out parsed)) profile.ImageMinTokens = parsed;
                    break;
                case "--fit":
                    if (TryParseBoolean(flag, value, result, out enabled)) profile.FitEnabled = enabled;
                    break;
                case "--flash-attn":
                    if (string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase))
                    {
                        profile.FlashAttention = true;
                        result.Warnings.Add("--flash-attn auto 已映射为开启；当前简易表单只支持开/关。");
                    }
                    else if (TryParseBoolean(flag, value, result, out enabled)) profile.FlashAttention = enabled;
                    break;
            }
        }

        private static bool TryParseInteger(string flag, string value, int minimum, int maximum, CommandParseResult result, out int parsed)
        {
            if (!Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) || parsed < minimum || parsed > maximum)
            {
                result.Errors.Add(flag + " 的值“" + value + "”无效，允许范围为 " + minimum + " 到 " + maximum + "。");
                return false;
            }
            return true;
        }

        private static bool TryParseBoolean(string flag, string value, CommandParseResult result, out bool enabled)
        {
            if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1")
            {
                enabled = true;
                return true;
            }
            if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) || value == "0")
            {
                enabled = false;
                return true;
            }
            enabled = false;
            result.Errors.Add(flag + " 的值“" + value + "”无效，请使用 on/off、true/false 或 1/0。");
            return false;
        }

        private static void ApplyPresenceFlag(ModelProfile profile, string flag, bool enabled)
        {
            if (flag == "--jinja") profile.Jinja = enabled;
            else if (flag == "--no-webui") profile.DisableWebUi = enabled;
            else if (flag == "--no-mmap") profile.NoMmap = enabled;
            else if (flag == "--mlock") profile.Mlock = enabled;
            else if (flag == "--metrics") profile.EnableMetrics = enabled;
        }

        private static bool IsPresenceFlag(string flag)
        {
            return flag == "--jinja" || flag == "--no-webui" || flag == "--no-mmap" || flag == "--mlock" || flag == "--metrics";
        }

        private static string NormalizeFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) return null;
            string value = flag.ToLowerInvariant();
            switch (value)
            {
                case "--host": return "--host";
                case "--port": return "--port";
                case "-m": case "--model": return "--model";
                case "--mmproj": return "--mmproj";
                case "--alias": return "--alias";
                case "--api-key-file": return "--api-key-file";
                case "--chat-template": return "--chat-template";
                case "--chat-template-file": return "--chat-template-file";
                case "-ngl": case "--gpu-layers": case "--n-gpu-layers": return "--n-gpu-layers";
                case "--fit": return "--fit";
                case "--fit-target": return "--fit-target";
                case "-c": case "--ctx-size": return "--ctx-size";
                case "-np": case "--parallel": return "--parallel";
                case "-t": case "--threads": return "--threads";
                case "-b": case "--batch-size": return "--batch-size";
                case "-ub": case "--ubatch-size": return "--ubatch-size";
                case "-fa": case "--flash-attn": return "--flash-attn";
                case "-ctk": case "--cache-type-k": return "--cache-type-k";
                case "-ctv": case "--cache-type-v": return "--cache-type-v";
                case "--image-min-tokens": return "--image-min-tokens";
                case "--jinja": return "--jinja";
                case "--no-webui": return "--no-webui";
                case "--no-mmap": return "--no-mmap";
                case "--mlock": return "--mlock";
                case "--metrics": return "--metrics";
                case "--reasoning": return "--reasoning";
                default: return null;
            }
        }

        private static bool LooksLikeOption(string value)
        {
            return !string.IsNullOrEmpty(value) && value.Length > 1 && value[0] == '-';
        }

        private static bool IsNegativeNumber(string value)
        {
            int parsed;
            return Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) && parsed < 0;
        }

        private static string FormatTokens(List<string> tokens)
        {
            List<string> formatted = new List<string>();
            foreach (string token in tokens)
            {
                if (token.Length == 0 || token.IndexOfAny(new char[] { ' ', '\t', '\r', '\n', '"' }) >= 0)
                    formatted.Add(CommandBuilder.Quote(token));
                else
                    formatted.Add(token);
            }
            return string.Join(" ", formatted.ToArray());
        }
    }
}
