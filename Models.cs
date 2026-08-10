using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace LlamaServerManager
{
    internal static class AppVersion
    {
        public const string ProductVersion = "0.1.0";
        public const string DisplayVersion = "v0.1.0-internal";
    }

    public sealed class AppConfig
    {
        public int SchemaVersion { get; set; }
        public string SelectedProfileId { get; set; }
        public List<ModelProfile> Profiles { get; set; }
        public string ThemeMode { get; set; }
        public string AccentName { get; set; }
        public bool FirstRunCompleted { get; set; }

        public AppConfig()
        {
            SchemaVersion = 2;
            SelectedProfileId = string.Empty;
            Profiles = new List<ModelProfile>();
            ThemeMode = "System";
            AccentName = "Emerald";
            FirstRunCompleted = false;
        }
    }

    public sealed class ModelProfile
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ServerExecutable { get; set; }
        public string ModelPath { get; set; }
        public string MmprojPath { get; set; }
        public string Alias { get; set; }
        public string ApiKeyFile { get; set; }
        public string Host { get; set; }
        public string AdvertisedHost { get; set; }
        public int Port { get; set; }
        public int ContextSize { get; set; }
        public int Parallel { get; set; }
        public string GpuLayers { get; set; }
        public bool FitEnabled { get; set; }
        public int FitTarget { get; set; }
        public bool FlashAttention { get; set; }
        public string CacheTypeK { get; set; }
        public string CacheTypeV { get; set; }
        public int ImageMinTokens { get; set; }
        public bool Jinja { get; set; }
        public bool DisableWebUi { get; set; }
        public bool NoMmap { get; set; }
        public bool Mlock { get; set; }
        public string Reasoning { get; set; }
        public string ExtraArguments { get; set; }

        public ModelProfile()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "我的 llama.cpp 服务";
            ServerExecutable = string.Empty;
            ModelPath = string.Empty;
            MmprojPath = string.Empty;
            Alias = "local-model";
            ApiKeyFile = string.Empty;
            Host = "127.0.0.1";
            AdvertisedHost = "127.0.0.1";
            Port = 8080;
            ContextSize = 8192;
            Parallel = 1;
            GpuLayers = "auto";
            FitEnabled = true;
            FitTarget = 1024;
            FlashAttention = true;
            CacheTypeK = "f16";
            CacheTypeV = "f16";
            ImageMinTokens = 0;
            Jinja = true;
            DisableWebUi = false;
            NoMmap = false;
            Mlock = false;
            Reasoning = string.Empty;
            ExtraArguments = string.Empty;
        }

        public static ModelProfile CreateGenericProfile()
        {
            return new ModelProfile();
        }

        public ModelProfile CloneAs(string newName)
        {
            ModelProfile copy = (ModelProfile)MemberwiseClone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = newName;
            return copy;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public static class ConfigStore
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static bool IsPortable
        {
            get { return File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.flag")); }
        }

        public static string DataDirectory
        {
            get
            {
                if (IsPortable) return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlamaServerManager");
            }
        }

        public static string LogDirectory
        {
            get { return Path.Combine(DataDirectory, "logs"); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(DataDirectory, "settings.json"); }
        }

        public static AppConfig Load()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(LogDirectory);

            if (!File.Exists(ConfigPath))
            {
                AppConfig initial = CreateInitialConfig();
                Save(initial);
                return initial;
            }

            try
            {
                string json = File.ReadAllText(ConfigPath, Encoding.UTF8);
                AppConfig config = Serializer.Deserialize<AppConfig>(json);
                if (config == null || config.Profiles == null || config.Profiles.Count == 0)
                {
                    config = CreateInitialConfig();
                    Save(config);
                }
                Normalize(config);
                return config;
            }
            catch
            {
                string backup = ConfigPath + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                try { File.Copy(ConfigPath, backup, true); } catch { }
                AppConfig fallback = CreateInitialConfig();
                Save(fallback);
                return fallback;
            }
        }

        public static void Save(AppConfig config)
        {
            Directory.CreateDirectory(DataDirectory);
            Normalize(config);
            string json = Serializer.Serialize(config);
            string temp = ConfigPath + ".tmp";
            File.WriteAllText(temp, PrettyJson(json), new UTF8Encoding(false));

            if (File.Exists(ConfigPath))
            {
                string backup = ConfigPath + ".bak";
                try
                {
                    File.Replace(temp, ConfigPath, backup, true);
                    return;
                }
                catch
                {
                    try { File.Delete(ConfigPath); } catch { }
                }
            }

            File.Move(temp, ConfigPath);
        }

        private static AppConfig CreateInitialConfig()
        {
            AppConfig config = new AppConfig();
            ModelProfile profile = ModelProfile.CreateGenericProfile();
            config.Profiles.Add(profile);
            config.SelectedProfileId = profile.Id;
            return config;
        }

        private static void Normalize(AppConfig config)
        {
            config.SchemaVersion = 2;
            if (string.IsNullOrWhiteSpace(config.ThemeMode)) config.ThemeMode = "System";
            if (string.IsNullOrWhiteSpace(config.AccentName)) config.AccentName = "Emerald";
            if (config.Profiles == null)
            {
                config.Profiles = new List<ModelProfile>();
            }

            foreach (ModelProfile profile in config.Profiles)
            {
                if (string.IsNullOrWhiteSpace(profile.Id)) profile.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(profile.Name)) profile.Name = "未命名模型";
                if (string.IsNullOrWhiteSpace(profile.Host)) profile.Host = "127.0.0.1";
                if (string.IsNullOrWhiteSpace(profile.AdvertisedHost))
                {
                    profile.AdvertisedHost = profile.Host == "0.0.0.0" || profile.Host == "::"
                        ? NetworkHelper.GetPreferredLanIPv4()
                        : profile.Host;
                }
                if (profile.Port <= 0) profile.Port = 8080;
                if (profile.ContextSize < 0) profile.ContextSize = 8192;
                if (profile.Parallel <= 0) profile.Parallel = 1;
                if (string.IsNullOrWhiteSpace(profile.GpuLayers)) profile.GpuLayers = "auto";
                if (string.IsNullOrWhiteSpace(profile.CacheTypeK)) profile.CacheTypeK = "q8_0";
                if (string.IsNullOrWhiteSpace(profile.CacheTypeV)) profile.CacheTypeV = "q8_0";
            }

            bool selectedExists = false;
            foreach (ModelProfile profile in config.Profiles)
            {
                if (profile.Id == config.SelectedProfileId)
                {
                    selectedExists = true;
                    break;
                }
            }
            if (!selectedExists && config.Profiles.Count > 0)
            {
                config.SelectedProfileId = config.Profiles[0].Id;
            }
        }

        private static string PrettyJson(string compact)
        {
            StringBuilder output = new StringBuilder();
            bool quoted = false;
            bool escaped = false;
            int indent = 0;

            for (int i = 0; i < compact.Length; i++)
            {
                char ch = compact[i];
                if (quoted)
                {
                    output.Append(ch);
                    if (escaped) escaped = false;
                    else if (ch == '\\') escaped = true;
                    else if (ch == '"') quoted = false;
                    continue;
                }

                if (ch == '"')
                {
                    quoted = true;
                    output.Append(ch);
                }
                else if (ch == '{' || ch == '[')
                {
                    output.Append(ch);
                    output.AppendLine();
                    indent++;
                    output.Append(new string(' ', indent * 2));
                }
                else if (ch == '}' || ch == ']')
                {
                    output.AppendLine();
                    indent--;
                    output.Append(new string(' ', indent * 2));
                    output.Append(ch);
                }
                else if (ch == ',')
                {
                    output.Append(ch);
                    output.AppendLine();
                    output.Append(new string(' ', indent * 2));
                }
                else if (ch == ':')
                {
                    output.Append(": ");
                }
                else if (!char.IsWhiteSpace(ch))
                {
                    output.Append(ch);
                }
            }
            return output.ToString();
        }
    }
}
