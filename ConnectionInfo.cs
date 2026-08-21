using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace LlamaServerManager
{
    public sealed class ConnectionInfoSnapshot
    {
        public string ProviderId { get; set; }
        public string ApiProtocol { get; set; }
        public string ApiAddress { get; set; }
        public string ApiKey { get; set; }
        public bool HasApiKey { get; set; }
        public string ModelFullName { get; set; }
        public string MaximumContext { get; set; }

        public ConnectionInfoSnapshot()
        {
            ProviderId = string.Empty;
            ApiProtocol = string.Empty;
            ApiAddress = string.Empty;
            ApiKey = string.Empty;
            ModelFullName = string.Empty;
            MaximumContext = string.Empty;
        }

        public static ConnectionInfoSnapshot Create(ModelProfile profile)
        {
            if (profile == null) throw new ArgumentNullException("profile");
            ConnectionInfoSnapshot result = new ConnectionInfoSnapshot();
            result.ProviderId = BuildProviderId(profile);
            result.ApiProtocol = ApiProtocolMode.DisplayName(profile.ApiProtocol);
            result.ApiAddress = LlamaApiClient.ProtocolClientBaseUrl(profile);
            result.ModelFullName = ModelDisplayName(profile);

            string key;
            string keyError;
            result.HasApiKey = ApiKeyFileSupport.TryReadFirstKey(profile.ApiKeyFile, out key, out keyError) &&
                !string.IsNullOrWhiteSpace(key);
            result.ApiKey = result.HasApiKey ? key :
                string.IsNullOrWhiteSpace(profile.ApiKeyFile) ? "未配置（当前服务无 API Key）" : "无法读取 API Key";

            long modelMaximum = 0L;
            try
            {
                if (!string.IsNullOrWhiteSpace(profile.ModelPath) && File.Exists(profile.ModelPath))
                    modelMaximum = GgufMetadataReader.Read(profile.ModelPath).ContextLength;
            }
            catch { }
            result.MaximumContext = modelMaximum > 0
                ? modelMaximum.ToString("N0", CultureInfo.InvariantCulture) + " tokens"
                : profile.ContextSize > 0
                    ? "配置为 " + profile.ContextSize.ToString("N0", CultureInfo.InvariantCulture) + " tokens（模型上限未知）"
                    : "模型上限未知";
            return result;
        }

        public static string BuildProviderId(ModelProfile profile)
        {
            string source = profile == null ? string.Empty : profile.Alias;
            StringBuilder slug = new StringBuilder();
            bool separatorPending = false;
            foreach (char ch in (source ?? string.Empty).ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
                {
                    if (separatorPending && slug.Length > 0) slug.Append('-');
                    slug.Append(ch);
                    separatorPending = false;
                }
                else separatorPending = slug.Length > 0;
            }
            string value = slug.ToString().Trim('-');
            if (value.Length == 0)
            {
                string id = profile == null ? string.Empty : profile.Id;
                value = !string.IsNullOrWhiteSpace(id)
                    ? "profile-" + id.Substring(0, Math.Min(8, id.Length)).ToLowerInvariant()
                    : "local-model";
            }
            return "llamalift-" + value;
        }

        private static string ModelDisplayName(ModelProfile profile)
        {
            try
            {
                string fileName = Path.GetFileName(profile.ModelPath);
                if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
            }
            catch { }
            return string.IsNullOrWhiteSpace(profile.Alias) ? "未命名模型" : profile.Alias;
        }
    }
}
