using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace LlamaServerManager
{
    internal static class AppVersion
    {
        public const string ProductVersion = "1.1.0";
        public const string DisplayVersion = "v1.1.0-preview";
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
        public bool LegacyImportCompleted { get; set; }

        public AppConfig()
        {
            SchemaVersion = 9;
            SelectedProfileId = string.Empty;
            Profiles = new List<ModelProfile>();
            ThemeMode = "Light";
            AccentName = "Blue";
            FirstRunCompleted = false;
            InstalledRuntimes = new List<InstalledRuntime>();
            ParameterPresets = ParameterPreset.CreateDefaults();
            SelectedParameterPresetId = ParameterPresets[1].Id;
            LegacyImportCompleted = false;
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
        public string ApiProtocol { get; set; }
        public string ChatTemplate { get; set; }
        public string ChatTemplateFile { get; set; }
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
            ApiProtocol = ApiProtocolMode.Responses;
            ChatTemplate = string.Empty;
            ChatTemplateFile = string.Empty;
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

        public bool SwitchToGeneratedCommand()
        {
            bool converted = UseCustomCommand;
            UseCustomCommand = false;
            CustomCommand = string.Empty;
            LastCommandValidationSummary = string.Empty;
            LastCommandValidatedAtUtc = string.Empty;
            return converted;
        }

        public static string MergeExtraArguments(string preserved, string incoming)
        {
            string first = (preserved ?? string.Empty).Trim();
            string second = (incoming ?? string.Empty).Trim();
            if (first.Length == 0) return second;
            if (second.Length == 0) return first;
            if (string.Equals(first, second, StringComparison.Ordinal)) return first;
            // Unknown arguments are deliberately lossless. Substring checks are unsafe here:
            // "--cache-reuse 25" is a substring of "--cache-reuse 256", but the two
            // command fragments are not equivalent. Keeping both is safer than silently
            // discarding a user-supplied value; llama-server's preflight reports conflicts.
            return first + " " + second;
        }

        public void CopyCommandSettingsFrom(ModelProfile source)
        {
            if (source == null) return;
            ServerExecutable = source.ServerExecutable;
            ModelPath = source.ModelPath;
            MmprojPath = source.MmprojPath;
            Alias = source.Alias;
            ApiKeyFile = source.ApiKeyFile;
            ChatTemplate = source.ChatTemplate;
            ChatTemplateFile = source.ChatTemplateFile;
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
            TuningPreset = source.TuningPreset;
            LastTuningSummary = source.LastTuningSummary;
        }

        public override string ToString()
        {
            return Name;
        }
    }

    public sealed class ParameterPreset
    {
        public string Id { get; set; }
        public string BuiltInKey { get; set; }
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
            BuiltInKey = string.Empty;
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

        public ParameterPreset CloneAs(string newName)
        {
            ParameterPreset copy = (ParameterPreset)MemberwiseClone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.BuiltInKey = string.Empty;
            copy.Name = newName;
            return copy;
        }

        public static List<ParameterPreset> CreateDefaults()
        {
            ModelProfile fast = ModelProfile.CreateGenericProfile();
            fast.ContextSize = 32768;
            fast.CacheTypeK = "q4_0";
            fast.CacheTypeV = "q4_0";
            fast.BatchSize = 2048;
            fast.UbatchSize = 512;

            ModelProfile balanced = ModelProfile.CreateGenericProfile();
            balanced.ContextSize = 65536;
            balanced.CacheTypeK = "q8_0";
            balanced.CacheTypeV = "q8_0";

            ModelProfile extreme = ModelProfile.CreateGenericProfile();
            extreme.ContextSize = 131072;
            extreme.CacheTypeK = "f16";
            extreme.CacheTypeV = "f16";
            extreme.BatchSize = 4096;
            extreme.UbatchSize = 1024;

            ParameterPreset first = new ParameterPreset();
            first.BuiltInKey = "Fast";
            first.Name = "预设 1 · 快速";
            first.Capture(fast);
            ParameterPreset second = new ParameterPreset();
            second.BuiltInKey = "Balanced";
            second.Name = "预设 2 · 均衡";
            second.Capture(balanced);
            ParameterPreset third = new ParameterPreset();
            third.BuiltInKey = "Extreme";
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
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlamaLift");
            }
        }

        private static string LegacyConfigPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LlamaServerManager", "settings.json");
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
                if (!IsPortable && File.Exists(LegacyConfigPath))
                {
                    try
                    {
                        AppConfig imported = DeserializeAndNormalize(File.ReadAllText(LegacyConfigPath, Encoding.UTF8));
                        imported.LegacyImportCompleted = true;
                        Save(imported);
                        return imported;
                    }
                    catch
                    {
                        // Leave the legacy file untouched and fall back to a new configuration.
                    }
                }
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
                if (!IsPortable && !config.LegacyImportCompleted && File.Exists(LegacyConfigPath))
                {
                    try
                    {
                        AppConfig legacy = DeserializeAndNormalize(File.ReadAllText(LegacyConfigPath, Encoding.UTF8));
                        MergeLegacyConfiguration(config, legacy);
                        config.LegacyImportCompleted = true;
                        Save(config);
                    }
                    catch
                    {
                        // A malformed legacy file must never invalidate the current LlamaLift configuration.
                    }
                }
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

        internal static AppConfig DeserializeAndNormalize(string json)
        {
            AppConfig config = Serializer.Deserialize<AppConfig>(json);
            if (config == null) throw new InvalidDataException("配置内容为空。");
            Normalize(config);
            return config;
        }

        internal static void NormalizeForTesting(AppConfig config)
        {
            Normalize(config);
        }

        internal static void MergeLegacyForTesting(AppConfig target, AppConfig legacy)
        {
            MergeLegacyConfiguration(target, legacy);
        }

        private static void MergeLegacyConfiguration(AppConfig target, AppConfig legacy)
        {
            if (target == null || legacy == null) return;
            if (target.Profiles.Count == 1 && IsBlankProfile(target.Profiles[0]) && legacy.Profiles.Count > 0)
            {
                target.Profiles.Clear();
                target.SelectedProfileId = legacy.SelectedProfileId;
            }
            foreach (ModelProfile source in legacy.Profiles)
            {
                int match = -1;
                for (int i = 0; i < target.Profiles.Count; i++)
                {
                    ModelProfile candidate = target.Profiles[i];
                    if ((!string.IsNullOrWhiteSpace(source.Id) && candidate.Id == source.Id) ||
                        (string.Equals(candidate.Name, source.Name, StringComparison.OrdinalIgnoreCase) &&
                         string.Equals(candidate.ModelPath, source.ModelPath, StringComparison.OrdinalIgnoreCase)))
                    {
                        match = i;
                        break;
                    }
                }
                if (match < 0) target.Profiles.Add(source);
                else if (!ProfilesEquivalent(target.Profiles[match], source))
                {
                    ModelProfile imported = source.CloneAs(UniqueImportedName(target.Profiles, source.Name));
                    target.Profiles.Add(imported);
                }
            }
            foreach (InstalledRuntime source in legacy.InstalledRuntimes)
            {
                bool exists = false;
                foreach (InstalledRuntime candidate in target.InstalledRuntimes)
                    if (candidate.Id == source.Id || string.Equals(candidate.ServerExecutable, source.ServerExecutable, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
                if (!exists) target.InstalledRuntimes.Add(source);
            }
            foreach (ParameterPreset source in legacy.ParameterPresets)
            {
                ParameterPreset match = null;
                foreach (ParameterPreset candidate in target.ParameterPresets)
                    if (candidate.Id == source.Id ||
                        (!string.IsNullOrWhiteSpace(source.BuiltInKey) && candidate.BuiltInKey == source.BuiltInKey) ||
                        string.Equals(candidate.Name, source.Name, StringComparison.OrdinalIgnoreCase)) { match = candidate; break; }
                if (match == null) target.ParameterPresets.Add(source);
                else if (!PresetsEquivalent(match, source))
                    target.ParameterPresets.Add(source.CloneAs(source.Name + "（旧版导入）"));
            }
            bool selectedExists = false;
            foreach (ModelProfile profile in target.Profiles)
                if (profile.Id == target.SelectedProfileId) { selectedExists = true; break; }
            if (!selectedExists) target.SelectedProfileId = legacy.SelectedProfileId;
            Normalize(target);
        }

        private static bool IsBlankProfile(ModelProfile profile)
        {
            return profile != null && string.IsNullOrWhiteSpace(profile.ServerExecutable) &&
                string.IsNullOrWhiteSpace(profile.ModelPath) && string.IsNullOrWhiteSpace(profile.MmprojPath);
        }

        private static bool ProfilesEquivalent(ModelProfile left, ModelProfile right)
        {
            if (left == null || right == null) return left == right;
            return string.Equals(left.ServerExecutable, right.ServerExecutable, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.ModelPath, right.ModelPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.MmprojPath, right.MmprojPath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.Alias, right.Alias, StringComparison.Ordinal) &&
                string.Equals(left.ApiKeyFile, right.ApiKeyFile, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(ApiProtocolMode.Normalize(left.ApiProtocol), ApiProtocolMode.Normalize(right.ApiProtocol), StringComparison.Ordinal) &&
                string.Equals(left.ChatTemplate, right.ChatTemplate, StringComparison.Ordinal) &&
                string.Equals(left.ChatTemplateFile, right.ChatTemplateFile, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.AdvertisedHost, right.AdvertisedHost, StringComparison.OrdinalIgnoreCase) &&
                left.Port == right.Port &&
                left.ContextSize == right.ContextSize && left.Parallel == right.Parallel &&
                string.Equals(left.GpuLayers, right.GpuLayers, StringComparison.OrdinalIgnoreCase) &&
                left.FitEnabled == right.FitEnabled && left.FitTarget == right.FitTarget &&
                left.FlashAttention == right.FlashAttention &&
                string.Equals(left.CacheTypeK, right.CacheTypeK, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.CacheTypeV, right.CacheTypeV, StringComparison.OrdinalIgnoreCase) &&
                left.ImageMinTokens == right.ImageMinTokens && left.Jinja == right.Jinja &&
                left.DisableWebUi == right.DisableWebUi && left.NoMmap == right.NoMmap && left.Mlock == right.Mlock &&
                left.EnableMetrics == right.EnableMetrics && string.Equals(left.Reasoning, right.Reasoning, StringComparison.Ordinal) &&
                string.Equals(left.ExtraArguments, right.ExtraArguments, StringComparison.Ordinal) &&
                left.Threads == right.Threads && left.BatchSize == right.BatchSize && left.UbatchSize == right.UbatchSize &&
                string.Equals(left.TuningPreset, right.TuningPreset, StringComparison.OrdinalIgnoreCase) &&
                left.UseCustomCommand == right.UseCustomCommand &&
                string.Equals(left.CustomCommand, right.CustomCommand, StringComparison.Ordinal);
        }

        private static string UniqueImportedName(List<ModelProfile> profiles, string name)
        {
            string baseName = (string.IsNullOrWhiteSpace(name) ? "未命名模型" : name) + "（旧版导入）";
            string candidate = baseName;
            int suffix = 2;
            while (ContainsProfileName(profiles, candidate)) candidate = baseName + " " + suffix++;
            return candidate;
        }

        private static bool PresetsEquivalent(ParameterPreset left, ParameterPreset right)
        {
            if (left == null || right == null) return left == right;
            return left.ContextSize == right.ContextSize && left.Parallel == right.Parallel &&
                string.Equals(left.GpuLayers, right.GpuLayers, StringComparison.OrdinalIgnoreCase) &&
                left.FitEnabled == right.FitEnabled && left.FitTarget == right.FitTarget &&
                left.FlashAttention == right.FlashAttention &&
                string.Equals(left.CacheTypeK, right.CacheTypeK, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.CacheTypeV, right.CacheTypeV, StringComparison.OrdinalIgnoreCase) &&
                left.ImageMinTokens == right.ImageMinTokens && left.Jinja == right.Jinja &&
                left.DisableWebUi == right.DisableWebUi && left.NoMmap == right.NoMmap && left.Mlock == right.Mlock &&
                left.EnableMetrics == right.EnableMetrics && string.Equals(left.Reasoning, right.Reasoning, StringComparison.Ordinal) &&
                left.Threads == right.Threads && left.BatchSize == right.BatchSize && left.UbatchSize == right.UbatchSize &&
                string.Equals(left.ExtraArguments, right.ExtraArguments, StringComparison.Ordinal);
        }

        private static bool ContainsProfileName(List<ModelProfile> profiles, string name)
        {
            foreach (ModelProfile profile in profiles)
                if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static void Normalize(AppConfig config)
        {
            int previousSchema = config.SchemaVersion;
            config.SchemaVersion = 9;
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
            if (previousSchema < 7) UpgradeLegacyBuiltInPresets(config.ParameterPresets);

            foreach (ParameterPreset preset in config.ParameterPresets)
            {
                if (string.IsNullOrWhiteSpace(preset.Id)) preset.Id = Guid.NewGuid().ToString("N");
                if (preset.BuiltInKey == null) preset.BuiltInKey = string.Empty;
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
                profile.ApiProtocol = ApiProtocolMode.Normalize(profile.ApiProtocol);
                if (profile.ChatTemplate == null) profile.ChatTemplate = string.Empty;
                if (profile.ChatTemplateFile == null) profile.ChatTemplateFile = string.Empty;
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

        private static void UpgradeLegacyBuiltInPresets(List<ParameterPreset> presets)
        {
            foreach (ParameterPreset preset in presets)
            {
                if (preset == null || !string.IsNullOrWhiteSpace(preset.BuiltInKey)) continue;
                if (preset.Name == "预设 1 · 快速" && preset.ContextSize == 4096 &&
                    string.Equals(preset.CacheTypeK, "q4_0", StringComparison.OrdinalIgnoreCase) && preset.BatchSize == 2048)
                {
                    preset.BuiltInKey = "Fast";
                    preset.ContextSize = 32768;
                }
                else if (preset.Name == "预设 2 · 均衡" && preset.ContextSize == 8192 &&
                    string.Equals(preset.CacheTypeK, "q8_0", StringComparison.OrdinalIgnoreCase))
                {
                    preset.BuiltInKey = "Balanced";
                    preset.ContextSize = 65536;
                }
                else if (preset.Name == "预设 3 · 极限" && preset.ContextSize == 32768 &&
                    string.Equals(preset.CacheTypeK, "f16", StringComparison.OrdinalIgnoreCase) && preset.BatchSize == 4096)
                {
                    preset.BuiltInKey = "Extreme";
                    preset.ContextSize = 131072;
                }
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
