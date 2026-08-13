using System;

namespace LlamaServerManager
{
    public static class ApiProtocolMode
    {
        public const string Responses = "Responses";
        public const string ChatCompletions = "ChatCompletions";
        public const string AnthropicMessages = "AnthropicMessages";

        private static readonly string[] values = new string[]
        {
            Responses,
            ChatCompletions,
            AnthropicMessages
        };

        public static string[] Values()
        {
            return (string[])values.Clone();
        }

        public static string Normalize(string value)
        {
            string candidate = (value ?? string.Empty).Trim();
            if (candidate.Equals(ChatCompletions, StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("Chat Completions", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("chat", StringComparison.OrdinalIgnoreCase))
                return ChatCompletions;
            if (candidate.Equals(AnthropicMessages, StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("Anthropic Messages", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("anthropic", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("messages", StringComparison.OrdinalIgnoreCase))
                return AnthropicMessages;
            return Responses;
        }

        public static string DisplayName(string value)
        {
            switch (Normalize(value))
            {
                case ChatCompletions: return "Chat Completions";
                case AnthropicMessages: return "Anthropic Messages";
                default: return "Responses（原生）";
            }
        }

        public static string FromDisplayName(string value)
        {
            return Normalize(value);
        }

        public static string EndpointPath(string value)
        {
            switch (Normalize(value))
            {
                case ChatCompletions: return "/v1/chat/completions";
                case AnthropicMessages: return "/v1/messages";
                default: return "/v1/responses";
            }
        }

        public static string ClientBasePath(string value)
        {
            return Normalize(value) == AnthropicMessages ? string.Empty : "/v1";
        }

        public static string AuthenticationLabel(string value)
        {
            return Normalize(value) == AnthropicMessages ? "x-api-key" : "Authorization: Bearer";
        }

        public static string Description(string value)
        {
            switch (Normalize(value))
            {
                case ChatCompletions:
                    return "兼容范围最广，使用 /v1/chat/completions 与 Bearer Key。";
                case AnthropicMessages:
                    return "供 Claude/Anthropic 客户端使用，调用 /v1/messages 并发送 x-api-key。";
                default:
                    return "适合 Codex/Zcode 等新客户端，使用 /v1/responses 与 Bearer Key。";
            }
        }
    }
}
