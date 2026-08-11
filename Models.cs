using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace LlamaServerManager
{
    internal static class AppVersion
    {
        public const string ProductVersion = "0.3.0";
        public const string DisplayVersion = "v0.3.0-dev";
    }

    public sealed class AppConfig
    {
        public int SchemaVersion { get; set; }
        public string SelectedProfileId { get; set; }
        public List<ModelProfile> Profiles { get; set; }
        public string ThemeMode { get; set; }
        public string AccentName { get; set; }
        public bool FirstRunCompleted { get; set; }
        public List<InstalledRuntime> InstalledRuntimes { get; set; }
        public List<ParameterPreset> ParameterPresets { get; set; }
        public string SelectedParameterPresetId { get; set; }

        public AppConfig()
        {
            SchemaVersion = 6;
            SelectedProfileId = string.Empty;
            Profiles = new List<ModelProfile>();
            ThemeMode = "Light";
            AccentName = "Blue";
            FirstRunCompleted = false;
            InstalledRuntimes = new List<InstalledRuntime>();
            ParameterPresets = ParameterPreset.CreateDefaults();
            SelectedParameterPresetId = ParameterPresets[1].Id;
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
        public bool EnableMetrics { get; set; }
        public string Reasoning { get; set; }
        public string ExtraArguments { get; set; }
        public int Threads { get; set; }
        public int BatchSize { get; set; }
        public int UbatchSize { get; set; }
        public string TuningPreset { get; set; }
        public string LastTuningSummary { get; set; }
        public bool UseCustomCommand { get; set; }
        public string CustomCommand { get; set; }
        public string LastCommandValidationSummary { get; set; }
        public string LastCommandValidatedAtUtc { get; set; }

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
            EnableMetrics = true;
            Reasoning = string.Empty;
            ExtraArguments = string.Empty;
            Threads = 0;
            BatchSize = 2048;
            UbatchSize = 512;
            TuningPreset = "Balanced";
            LastTuningSummary = string.Empty;
            UseCustomCommand = false;
            CustomCommand = string.Empty;
            LastCommandValidationSummary = string.Empty;
            LastCommandValidatedAtUtc = string.Empty;
        }

        public static ModelProfile CreateGenericProfile()
        {
            return new ModelProfile();
        }

        public ModelProfile CloneAs(string newName)
        {
            ModelProfile copy = Clone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = newName;
            return copy;
        }

        public ModelProfile Clone()
        {
            return (ModelProfile)MemberwiseClone();
        }

        public void CopyCommandSettingsFrom(ModelProfile source)
        {
            if (source == null) return;
            ServerExecutable = source.ServerExecutable;
            ModelPath = source.ModelPath;
            MmprojPath = source.MmprojPath;
            Alias = source.Alias;
            ApiKeyFile = source.ApiKeyFile;
            Host = source.Host;
            Port = source.Port;
            ContextSize = source.ContextSize;
            Parallel = source.Parallel;
            GpuLayers = source.GpuLayers;
            FitEnabled = source.FitEnabled;
            FitTarget = source.FitTarget;
            FlashAttention = source.FlashAttention;
            CacheTypeK = source.CacheTypeK;
            CacheTypeV = source.CacheTypeV;
            ImageMinTokens = source.ImageMinTokens;
            Jinja = source.Jinja;
            DisableWebUi = source.DisableWebUi;
            NoMmap = source.NoMmap;
            Mlock = source.Mlock;
            EnableMetrics = source.EnableMetrics;
            Reasoning = source.Reasoning;
            ExtraArguments = source.ExtraArguments;
            Threads = source.Threads;
            BatchSize = source.BatchSize;
            UbatchSize = source.UbatchSize;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public sealed class ParameterPreset
    {
        public string Id { get; set; }
        public string Name { get; set; }
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
        public bool EnableMetrics { get; set; }
        public string Reasoning { get; set; }
        public string ExtraArguments { get; set; }
        public int Threads { get; set; }
        public int BatchSize { get; set; }
        public int UbatchSize { get; set; }

        public ParameterPreset()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "未命名预设";
            ContextSize = 8192;
            Parallel = 1;
            GpuLayers = "auto";
            FitEnabled = true;
            FitTarget = 1024;
            FlashAttention = true;
            CacheTypeK = "f16";
            CacheTypeV = "f16";
            Jinja = true;
            EnableMetrics = true;
            BatchSize = 2048;
            UbatchSize = 512;
            Reasoning = string.Empty;
            ExtraArguments = string.Empty;
        }

        public void Capture(ModelProfile profile)
        {
            if (profile == null) return;
            ContextSize = profile.ContextSize;
            Parallel = profile.Parallel;
            GpuLayers = profile.GpuLayers;
            FitEnabled = profile.FitEnabled;
            FitTarget = profile.FitTarget;
            FlashAttention = profile.FlashAttention;
            CacheTypeK = profile.CacheTypeK;
            CacheTypeV = profile.CacheTypeV;
            ImageMinTokens = profile.ImageMinTokens;
            Jinja = profile.Jinja;
            DisableWebUi = profile.DisableWebUi;
            NoMmap = profile.NoMmap;
            Mlock = profile.Mlock;
            EnableMetrics = profile.EnableMetrics;
            Reasoning = profile.Reasoning;
            ExtraArguments = profile.ExtraArguments;
            Threads = profile.Threads;
            BatchSize = profile.BatchSize;
            UbatchSize = profile.UbatchSize;
        }

        public void ApplyTo(ModelProfile profile)
        {
            if (profile == null) return;
            profile.ContextSize = ContextSize;
            profile.Parallel = Parallel;
            profile.GpuLayers = GpuLayers;
            profile.FitEnabled = FitEnabled;
            profile.FitTarget = FitTarget;
            profile.FlashAttention = FlashAttention;
            profile.CacheTypeK = CacheTypeK;
            profile.CacheTypeV = CacheTypeV;
            profile.ImageMinTokens = ImageMinTokens;
            profile.Jinja = Jinja;
            profile.DisableWebUi = DisableWebUi;
            profile.NoMmap = NoMmap;
            profile.Mlock = Mlock;
            profile.EnableMetrics = EnableMetrics;
            profile.Reasoning = Reasoning;
            profile.ExtraArguments = ExtraArguments;
            profile.Threads = Threads;
            profile.BatchSize = BatchSize;
            profile.UbatchSize = UbatchSize;
        }

        public static List<ParameterPreset> CreateDefaults()
        {
            ModelProfile fast = ModelProfile.CreateGenericProfile();
            fast.ContextSize = 4096;
            fast.CacheTypeK = "q4_0";
            fast.CacheTypeV = "q4_0";
            fast.BatchSize = 2048;
            fast.UbatchSize = 512;

            ModelProfile balanced = ModelProfile.CreateGenericProfile();
            balanced.ContextSize = 8192;
            balanced.CacheTypeK = "q8_0";
            balanced.CacheTypeV = "q8_0";

            ModelProfile extreme = ModelProfile.CreateGenericProfile();
            extreme.ContextSize = 32768;
            extreme.CacheTypeK = "f16";
            extreme.CacheTypeV = "f16";
            extreme.BatchSize = 4096;
            extreme.UbatchSize = 1024;

            ParameterPreset first = new ParameterPreset();
            first.Name = "预设 1 · 快速";
            first.Capture(fast);
            ParameterPreset second = new ParameterPreset();
            second.Name = "预设 2 · 均衡";
            second.Capture(balanced);
            ParameterPreset third = new ParameterPreset();
            third.Name = "预设 3 · 极限";
            third.Capture(extreme);
            return new List<ParameterPreset> { first, second, third };
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public sealed class InstalledRuntime
    {
        public string Id { get; set; }
        public string ReleaseTag { get; set; }
        public string Backend { get; set; }
        public string AssetName { get; set; }
        public string InstallDirectory { get; set; }
        public string ServerExecutable { get; set; }
        public string InstalledAtUtc { get; set; }
        public string SourceUrl { get; set; }
        public string Sha256 { get; set; }

        public InstalledRuntime()
        {
            Id = Guid.NewGuid().ToString("N");
            ReleaseTag = string.Empty;
            Backend = string.Empty;
            AssetName = string.Empty;
            InstallDirectory = string.Empty;
            ServerExecutable = string.Empty;
            InstalledAtUtc = string.Empty;
            SourceUrl = string.Empty;
            Sha256 = string.Empty;
        }

        public override string ToString()
        {
            string state = File.Exists(ServerExecutable) ? string.Empty : " · 文件缺失";
            return ReleaseTag + " · " + Backend + state;
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
                string branded = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlamaLift");
                string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlamaServerManager");
                return Directory.Exists(branded) || !Directory.Exists(legacy) ? branded : legacy;
            }
        }

        public static string LogDirectory
        {
            get { return Path.Combine(DataDirectory, "logs"); }
        }

        public static string RuntimeDirectory
        {
            get { return Path.Combine(DataDirectory, "runtimes"); }
        }

        public static string ApiKeyDirectory
        {
            get { return Path.Combine(DataDirectory, "api-keys"); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(DataDirectory, "settings.json"); }
        }

        public static AppConfig Load()
        {
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(LogDirectory);
            Directory.CreateDirectory(RuntimeDirectory);
            Directory.CreateDirectory(ApiKeyDirectory);

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
            int previousSchema = config.SchemaVersion;
            config.SchemaVersion = 6;
            if (string.IsNullOrWhiteSpace(config.ThemeMode)) config.ThemeMode = "System";
            if (string.IsNullOrWhiteSpace(config.AccentName)) config.AccentName = "Blue";
            if (config.Profiles == null)
            {
                config.Profiles = new List<ModelProfile>();
            }
            if (config.InstalledRuntimes == null)
            {
                config.InstalledRuntimes = new List<InstalledRuntime>();
            }
            if (config.ParameterPresets == null || config.ParameterPresets.Count == 0)
            {
                config.ParameterPresets = ParameterPreset.CreateDefaults();
            }

            foreach (ParameterPreset preset in config.ParameterPresets)
            {
                if (string.IsNullOrWhiteSpace(preset.Id)) preset.Id = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(preset.Name)) preset.Name = "未命名预设";
                if (preset.ContextSize < 0) preset.ContextSize = 8192;
                if (preset.Parallel <= 0) preset.Parallel = 1;
                if (string.IsNullOrWhiteSpace(preset.GpuLayers)) preset.GpuLayers = "auto";
                if (string.IsNullOrWhiteSpace(preset.CacheTypeK)) preset.CacheTypeK = "f16";
                if (string.IsNullOrWhiteSpace(preset.CacheTypeV)) preset.CacheTypeV = "f16";
                if (preset.BatchSize <= 0) preset.BatchSize = 2048;
                if (preset.UbatchSize <= 0) preset.UbatchSize = 512;
                if (preset.UbatchSize > preset.BatchSize) preset.UbatchSize = preset.BatchSize;
                if (previousSchema < 6) preset.EnableMetrics = true;
            }
            bool selectedPresetExists = false;
            foreach (ParameterPreset preset in config.ParameterPresets)
                if (preset.Id == config.SelectedParameterPresetId) { selectedPresetExists = true; break; }
            if (!selectedPresetExists) config.SelectedParameterPresetId = config.ParameterPresets[0].Id;

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
                if (profile.Threads < 0) profile.Threads = 0;
                if (profile.BatchSize <= 0) profile.BatchSize = 2048;
                if (profile.UbatchSize <= 0) profile.UbatchSize = 512;
                if (profile.UbatchSize > profile.BatchSize) profile.UbatchSize = profile.BatchSize;
                if (string.IsNullOrWhiteSpace(profile.TuningPreset)) profile.TuningPreset = "Balanced";
                if (profile.CustomCommand == null) profile.CustomCommand = string.Empty;
                if (profile.LastCommandValidationSummary == null) profile.LastCommandValidationSummary = string.Empty;
                if (profile.LastCommandValidatedAtUtc == null) profile.LastCommandValidatedAtUtc = string.Empty;
                if (previousSchema < 6) profile.EnableMetrics = true;
            }

            foreach (InstalledRuntime runtime in config.InstalledRuntimes)
            {
                if (string.IsNullOrWhiteSpace(runtime.Id)) runtime.Id = Guid.NewGuid().ToString("N");
                if (runtime.ReleaseTag == null) runtime.ReleaseTag = string.Empty;
                if (runtime.Backend == null) runtime.Backend = string.Empty;
                if (runtime.AssetName == null) runtime.AssetName = string.Empty;
                if (runtime.InstallDirectory == null) runtime.InstallDirectory = string.Empty;
                if (runtime.ServerExecutable == null) runtime.ServerExecutable = string.Empty;
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
