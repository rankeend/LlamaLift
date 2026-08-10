using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace LlamaServerManager
{
    public sealed class GpuProfile
    {
        public string Name { get; set; }
        public long MemoryBytes { get; set; }
        public string DriverVersion { get; set; }

        public override string ToString()
        {
            string memory = MemoryBytes > 0 ? " · " + FormatBytes(MemoryBytes) : string.Empty;
            return Name + memory;
        }

        private static string FormatBytes(long value)
        {
            return (value / 1024D / 1024D / 1024D).ToString("0.#", CultureInfo.InvariantCulture) + " GiB";
        }
    }

    public sealed class HardwareProfile
    {
        public string CpuName { get; set; }
        public int LogicalProcessors { get; set; }
        public long TotalMemoryBytes { get; set; }
        public List<GpuProfile> Gpus { get; set; }
        public string RecommendedBackend { get; set; }

        public HardwareProfile()
        {
            Gpus = new List<GpuProfile>();
            CpuName = "未知 CPU";
            RecommendedBackend = "CPU";
        }

        public long LargestGpuMemoryBytes
        {
            get
            {
                long maximum = 0L;
                foreach (GpuProfile gpu in Gpus) maximum = Math.Max(maximum, gpu.MemoryBytes);
                return maximum;
            }
        }

        public string Summary
        {
            get
            {
                string gpu = Gpus.Count == 0 ? "未检测到独立 GPU" : string.Join("；", Gpus.ConvertAll(delegate(GpuProfile item) { return item.ToString(); }).ToArray());
                return CpuName + " · " + LogicalProcessors + " 线程 · 内存 " + FormatBytes(TotalMemoryBytes) + "\n" + gpu + " · 推荐后端 " + RecommendedBackend;
            }
        }

        public static string FormatBytes(long value)
        {
            if (value <= 0) return "未知";
            return (value / 1024D / 1024D / 1024D).ToString("0.##", CultureInfo.InvariantCulture) + " GiB";
        }
    }

    public static class HardwareDetector
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private sealed class MemoryStatus
        {
            public uint Length = Convert.ToUInt32(Marshal.SizeOf(typeof(MemoryStatus)));
            public uint MemoryLoad;
            public ulong TotalPhysical;
            public ulong AvailablePhysical;
            public ulong TotalPageFile;
            public ulong AvailablePageFile;
            public ulong TotalVirtual;
            public ulong AvailableVirtual;
            public ulong AvailableExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus buffer);

        public static HardwareProfile Detect()
        {
            HardwareProfile result = new HardwareProfile();
            result.LogicalProcessors = Math.Max(1, Environment.ProcessorCount);
            result.CpuName = ReadCpuName();
            MemoryStatus memory = new MemoryStatus();
            if (GlobalMemoryStatusEx(memory) && memory.TotalPhysical <= long.MaxValue)
                result.TotalMemoryBytes = Convert.ToInt64(memory.TotalPhysical);

            AddNvidiaGpus(result.Gpus);
            AddWmiGpus(result.Gpus);
            result.RecommendedBackend = RecommendBackend(result.Gpus);
            return result;
        }

        private static string ReadCpuName()
        {
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    string value = Convert.ToString(key == null ? null : key.GetValue("ProcessorNameString"));
                    if (!string.IsNullOrWhiteSpace(value)) return Regex.Replace(value.Trim(), @"\s+", " ");
                }
            }
            catch { }
            return Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? "未知 CPU";
        }

        private static void AddNvidiaGpus(List<GpuProfile> target)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "nvidia-smi.exe";
                psi.Arguments = "--query-gpu=name,memory.total,driver_version --format=csv,noheader,nounits";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    if (!process.WaitForExit(4000)) { try { process.Kill(); } catch { } return; }
                    string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length < 2) continue;
                        long mib;
                        long.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out mib);
                        target.Add(new GpuProfile
                        {
                            Name = parts[0].Trim(),
                            MemoryBytes = mib * 1024L * 1024L,
                            DriverVersion = parts.Length > 2 ? parts[2].Trim() : string.Empty
                        });
                    }
                }
            }
            catch { }
        }

        private static void AddWmiGpus(List<GpuProfile> target)
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController"))
                using (ManagementObjectCollection results = searcher.Get())
                {
                    foreach (ManagementObject item in results)
                    {
                        string name = Convert.ToString(item["Name"]);
                        if (string.IsNullOrWhiteSpace(name) || IsVirtualDisplay(name) || ContainsGpu(target, name)) continue;
                        long memory = 0L;
                        try { memory = Convert.ToInt64(item["AdapterRAM"], CultureInfo.InvariantCulture); } catch { }
                        target.Add(new GpuProfile
                        {
                            Name = name.Trim(),
                            MemoryBytes = memory,
                            DriverVersion = Convert.ToString(item["DriverVersion"]) ?? string.Empty
                        });
                    }
                }
            }
            catch { }
        }

        private static bool ContainsGpu(List<GpuProfile> target, string name)
        {
            foreach (GpuProfile gpu in target)
            {
                if (gpu.Name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 && name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (string.Equals(gpu.Name, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool IsVirtualDisplay(string name)
        {
            string value = name.ToLowerInvariant();
            return value.IndexOf("virtual display", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("remote display", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("microsoft basic display", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("oray", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("todesk", StringComparison.Ordinal) >= 0 ||
                value.IndexOf("gameviewer", StringComparison.Ordinal) >= 0;
        }

        private static string RecommendBackend(List<GpuProfile> gpus)
        {
            foreach (GpuProfile gpu in gpus)
                if (gpu.Name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) return "CUDA 12";
            foreach (GpuProfile gpu in gpus)
                if (gpu.Name.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 || gpu.Name.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0) return "Vulkan";
            foreach (GpuProfile gpu in gpus)
                if (gpu.Name.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0) return "Vulkan";
            return "CPU";
        }
    }

    public sealed class GgufModelInfo
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public string Architecture { get; set; }
        public string Quantization { get; set; }
        public long FileSizeBytes { get; set; }
        public long ContextLength { get; set; }
        public long BlockCount { get; set; }
        public long EmbeddingLength { get; set; }
        public long HeadCount { get; set; }
        public long KvHeadCount { get; set; }
        public long FileType { get; set; }
    }

    public static class GgufMetadataReader
    {
        private const uint TypeUInt8 = 0;
        private const uint TypeInt8 = 1;
        private const uint TypeUInt16 = 2;
        private const uint TypeInt16 = 3;
        private const uint TypeUInt32 = 4;
        private const uint TypeInt32 = 5;
        private const uint TypeFloat32 = 6;
        private const uint TypeBool = 7;
        private const uint TypeString = 8;
        private const uint TypeArray = 9;
        private const uint TypeUInt64 = 10;
        private const uint TypeInt64 = 11;
        private const uint TypeFloat64 = 12;

        public static GgufModelInfo Read(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("找不到 GGUF 模型。", path);
            GgufModelInfo info = new GgufModelInfo();
            info.Path = path;
            info.FileSizeBytes = new FileInfo(path).Length;
            info.Name = System.IO.Path.GetFileNameWithoutExtension(path);
            info.Quantization = DetectQuantizationFromName(info.Name);

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8))
            {
                string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (magic != "GGUF") throw new InvalidDataException("所选文件不是有效的 GGUF 模型。");
                uint version = reader.ReadUInt32();
                if (version < 2 || version > 3) throw new InvalidDataException("暂不支持 GGUF v" + version + " 元数据。");
                reader.ReadUInt64(); // tensor count
                ulong metadataCount = reader.ReadUInt64();
                if (metadataCount > 1000000UL) throw new InvalidDataException("GGUF 元数据数量异常。");

                for (ulong i = 0; i < metadataCount; i++)
                {
                    string key = ReadString(reader, 1024 * 1024);
                    uint type = reader.ReadUInt32();
                    if (key.StartsWith("tokenizer.", StringComparison.Ordinal) && HasCoreMetadata(info)) break;
                    bool wanted = IsWantedKey(key);
                    object value = ReadValue(reader, type, wanted);
                    if (wanted) ApplyMetadata(info, key, value);
                }
            }
            if (string.IsNullOrWhiteSpace(info.Quantization)) info.Quantization = MapFileType(info.FileType);
            if (string.IsNullOrWhiteSpace(info.Architecture)) info.Architecture = "unknown";
            return info;
        }

        private static bool HasCoreMetadata(GgufModelInfo info)
        {
            return !string.IsNullOrWhiteSpace(info.Architecture) && info.ContextLength > 0 && info.BlockCount > 0 && info.EmbeddingLength > 0;
        }

        private static bool IsWantedKey(string key)
        {
            return key == "general.name" || key == "general.architecture" || key == "general.file_type" ||
                key.EndsWith(".context_length", StringComparison.Ordinal) ||
                key.EndsWith(".block_count", StringComparison.Ordinal) ||
                key.EndsWith(".embedding_length", StringComparison.Ordinal) ||
                key.EndsWith(".attention.head_count", StringComparison.Ordinal) ||
                key.EndsWith(".attention.head_count_kv", StringComparison.Ordinal);
        }

        private static void ApplyMetadata(GgufModelInfo info, string key, object value)
        {
            if (value == null) return;
            if (key == "general.name") info.Name = Convert.ToString(value, CultureInfo.InvariantCulture);
            else if (key == "general.architecture") info.Architecture = Convert.ToString(value, CultureInfo.InvariantCulture);
            else if (key == "general.file_type") info.FileType = ToInt64(value);
            else if (key.EndsWith(".context_length", StringComparison.Ordinal)) info.ContextLength = ToInt64(value);
            else if (key.EndsWith(".block_count", StringComparison.Ordinal)) info.BlockCount = ToInt64(value);
            else if (key.EndsWith(".embedding_length", StringComparison.Ordinal)) info.EmbeddingLength = ToInt64(value);
            else if (key.EndsWith(".attention.head_count_kv", StringComparison.Ordinal)) info.KvHeadCount = ToInt64(value);
            else if (key.EndsWith(".attention.head_count", StringComparison.Ordinal)) info.HeadCount = ToInt64(value);
        }

        private static object ReadValue(BinaryReader reader, uint type, bool keep)
        {
            if (type == TypeUInt8) return keep ? (object)reader.ReadByte() : Skip(reader, 1);
            if (type == TypeInt8) return keep ? (object)reader.ReadSByte() : Skip(reader, 1);
            if (type == TypeUInt16) return keep ? (object)reader.ReadUInt16() : Skip(reader, 2);
            if (type == TypeInt16) return keep ? (object)reader.ReadInt16() : Skip(reader, 2);
            if (type == TypeUInt32) return keep ? (object)reader.ReadUInt32() : Skip(reader, 4);
            if (type == TypeInt32) return keep ? (object)reader.ReadInt32() : Skip(reader, 4);
            if (type == TypeFloat32) return keep ? (object)reader.ReadSingle() : Skip(reader, 4);
            if (type == TypeBool) return keep ? (object)(reader.ReadByte() != 0) : Skip(reader, 1);
            if (type == TypeString) { string text = ReadString(reader, keep ? 16 * 1024 * 1024 : 0); return keep ? (object)text : null; }
            if (type == TypeUInt64) return keep ? (object)reader.ReadUInt64() : Skip(reader, 8);
            if (type == TypeInt64) return keep ? (object)reader.ReadInt64() : Skip(reader, 8);
            if (type == TypeFloat64) return keep ? (object)reader.ReadDouble() : Skip(reader, 8);
            if (type == TypeArray)
            {
                uint elementType = reader.ReadUInt32();
                ulong count = reader.ReadUInt64();
                if (count > 100000000UL) throw new InvalidDataException("GGUF 数组长度异常。");
                for (ulong i = 0; i < count; i++) ReadValue(reader, elementType, false);
                return null;
            }
            throw new InvalidDataException("未知 GGUF 元数据类型：" + type);
        }

        private static object Skip(BinaryReader reader, int bytes)
        {
            if (reader.BaseStream.Seek(bytes, SeekOrigin.Current) < 0) throw new EndOfStreamException();
            return null;
        }

        private static string ReadString(BinaryReader reader, int maximumBytes)
        {
            ulong length = reader.ReadUInt64();
            if (length > int.MaxValue) throw new InvalidDataException("GGUF 字符串长度异常。");
            int count = Convert.ToInt32(length);
            if (maximumBytes == 0)
            {
                reader.BaseStream.Seek(count, SeekOrigin.Current);
                return string.Empty;
            }
            if (count > maximumBytes) throw new InvalidDataException("GGUF 字符串超过安全读取上限。");
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count) throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }

        private static long ToInt64(object value)
        {
            try { return Convert.ToInt64(value, CultureInfo.InvariantCulture); } catch { return 0L; }
        }

        private static string DetectQuantizationFromName(string name)
        {
            Match match = Regex.Match(name ?? string.Empty, @"(?i)(IQ\d(?:_[A-Z0-9]+)+|Q\d(?:_[A-Z0-9]+)+|MXFP\d+|BF16|F16|F32)");
            return match.Success ? match.Value.ToUpperInvariant() : string.Empty;
        }

        private static string MapFileType(long value)
        {
            switch (value)
            {
                case 0: return "F32";
                case 1: return "F16";
                case 2: return "Q4_0";
                case 3: return "Q4_1";
                case 7: return "Q8_0";
                case 8: return "Q5_0";
                case 9: return "Q5_1";
                case 10: return "Q2_K";
                case 11: return "Q3_K_S";
                case 12: return "Q3_K_M";
                case 13: return "Q3_K_L";
                case 14: return "Q4_K_S";
                case 15: return "Q4_K_M";
                case 16: return "Q5_K_S";
                case 17: return "Q5_K_M";
                case 18: return "Q6_K";
                case 30: return "BF16";
                default: return value > 0 ? "file-type " + value : "未知";
            }
        }
    }

    public sealed class AdaptivePlan
    {
        public string Preset { get; set; }
        public int ContextSize { get; set; }
        public string GpuLayers { get; set; }
        public string CacheTypeK { get; set; }
        public string CacheTypeV { get; set; }
        public int FitTarget { get; set; }
        public int Threads { get; set; }
        public int BatchSize { get; set; }
        public int UbatchSize { get; set; }
        public string RecommendedModelQuantization { get; set; }
        public string Summary { get; set; }
        public List<string> Warnings { get; set; }

        public AdaptivePlan()
        {
            Warnings = new List<string>();
        }

        public void ApplyTo(ModelProfile profile)
        {
            profile.ContextSize = ContextSize;
            profile.GpuLayers = GpuLayers;
            profile.CacheTypeK = CacheTypeK;
            profile.CacheTypeV = CacheTypeV;
            profile.FitEnabled = true;
            profile.FitTarget = FitTarget;
            profile.FlashAttention = true;
            profile.Threads = Threads;
            profile.BatchSize = BatchSize;
            profile.UbatchSize = UbatchSize;
            profile.TuningPreset = Preset;
            profile.LastTuningSummary = Summary;
        }
    }

    public static class AdaptiveTuner
    {
        public static AdaptivePlan Recommend(HardwareProfile hardware, GgufModelInfo model, string preset)
        {
            if (hardware == null) throw new ArgumentNullException("hardware");
            if (model == null) throw new ArgumentNullException("model");
            string mode = NormalizePreset(preset);
            AdaptivePlan plan = new AdaptivePlan();
            plan.Preset = mode;

            bool fast = mode == "Fast";
            bool extreme = mode == "Extreme";
            plan.CacheTypeK = fast ? "q4_0" : (extreme ? "f16" : "q8_0");
            plan.CacheTypeV = fast ? "q4_0" : (extreme ? "f16" : "q8_0");
            plan.FitTarget = fast ? 2048 : (extreme ? 1024 : 1536);
            plan.BatchSize = fast ? 2048 : (extreme ? 512 : 1024);
            plan.UbatchSize = fast ? 512 : 256;
            plan.Threads = fast ? hardware.LogicalProcessors : Math.Max(1, hardware.LogicalProcessors - 2);

            long modelMaximum = model.ContextLength > 0 ? model.ContextLength : 32768L;
            long desired = fast ? 8192L : (extreme ? Math.Min(modelMaximum, 131072L) : 32768L);
            desired = Math.Min(desired, modelMaximum);
            double cacheBytes = CacheBytesPerToken(model, plan.CacheTypeK);
            double usage = fast ? 0.64D : (extreme ? 0.84D : 0.74D);
            double capacity = hardware.TotalMemoryBytes * usage + hardware.LargestGpuMemoryBytes * 0.65D;
            double weights = model.FileSizeBytes * 1.10D;
            double reserve = (fast ? 4D : (extreme ? 2D : 3D)) * 1024D * 1024D * 1024D;
            double cacheBudget = Math.Max(0D, capacity - weights - reserve);
            long memoryLimited = cacheBytes <= 0D ? desired : Convert.ToInt64(Math.Floor(cacheBudget / cacheBytes));
            long chosen = Math.Min(desired, memoryLimited);
            chosen = RoundContext(chosen);
            if (chosen < 2048L)
            {
                chosen = Math.Min(2048L, modelMaximum);
                plan.Warnings.Add("模型权重已接近可用内存上限；即使 2K 上下文也可能触发系统换页。建议使用更低量化或更小模型。");
            }
            else if (chosen < desired)
            {
                plan.Warnings.Add("上下文从目标值 " + desired + " 自动下调到 " + chosen + "，以预留系统和模型运行空间。");
            }
            plan.ContextSize = Convert.ToInt32(Math.Min(chosen, int.MaxValue));

            double estimatedKv = cacheBytes * plan.ContextSize;
            bool hasAccelerator = hardware.Gpus.Count > 0 && hardware.RecommendedBackend != "CPU";
            if (!hasAccelerator) plan.GpuLayers = "0";
            else if (hardware.LargestGpuMemoryBytes > (weights + estimatedKv) * 1.15D) plan.GpuLayers = "all";
            else plan.GpuLayers = "auto";

            if (extreme && cacheBudget < cacheBytes * Math.Max(8192, plan.ContextSize))
            {
                plan.CacheTypeK = "q8_0";
                plan.CacheTypeV = "q8_0";
                plan.Warnings.Add("极限档的 F16 KV Cache 超出安全预算，已自动回退为 Q8_0。");
            }

            plan.RecommendedModelQuantization = RecommendModelQuantization(mode, hardware, model);
            string currentQuant = string.IsNullOrWhiteSpace(model.Quantization) ? "未知" : model.Quantization;
            StringBuilder summary = new StringBuilder();
            summary.AppendLine("硬件：" + hardware.Summary.Replace("\n", "；"));
            summary.AppendLine("模型：" + model.Name + " · " + model.Architecture + " · " + currentQuant + " · " + HardwareProfile.FormatBytes(model.FileSizeBytes));
            summary.AppendLine("方案：" + DisplayPreset(mode) + " · 上下文 " + plan.ContextSize + " · GPU 层 " + plan.GpuLayers + " · KV " + plan.CacheTypeK + "/" + plan.CacheTypeV);
            summary.AppendLine("执行：线程 " + plan.Threads + " · batch " + plan.BatchSize + " · ubatch " + plan.UbatchSize + " · Fit 余量 " + plan.FitTarget + " MiB");
            summary.Append("模型量化建议：" + plan.RecommendedModelQuantization + "（当前 GGUF 权重量化不会被运行参数改变）");
            plan.Summary = summary.ToString();
            return plan;
        }

        private static double CacheBytesPerToken(GgufModelInfo model, string cacheType)
        {
            double layers = model.BlockCount > 0 ? model.BlockCount : 32D;
            double embedding = model.EmbeddingLength > 0 ? model.EmbeddingLength : 4096D;
            double ratio = model.HeadCount > 0 && model.KvHeadCount > 0 ? Math.Min(1D, model.KvHeadCount / (double)model.HeadCount) : 0.25D;
            double bytes = cacheType == "f16" || cacheType == "bf16" ? 2D : (cacheType == "q8_0" ? 1.0625D : 0.5625D);
            return layers * embedding * ratio * 2D * bytes;
        }

        private static long RoundContext(long value)
        {
            if (value < 2048L) return value;
            long[] levels = { 2048L, 4096L, 8192L, 16384L, 32768L, 65536L, 131072L, 262144L };
            long chosen = 2048L;
            foreach (long level in levels) if (level <= value) chosen = level;
            return chosen;
        }

        private static string RecommendModelQuantization(string mode, HardwareProfile hardware, GgufModelInfo model)
        {
            double capacity = hardware.TotalMemoryBytes + hardware.LargestGpuMemoryBytes * 0.75D;
            if (mode == "Fast") return "Q4_K_M（速度/占用优先）";
            if (mode == "Extreme") return capacity > model.FileSizeBytes * 2.5D ? "Q8_0 或 BF16（精度优先）" : "Q6_K（当前容量下的高质量选择）";
            return capacity > model.FileSizeBytes * 1.8D ? "Q6_K（质量与占用平衡）" : "Q5_K_M（更稳妥的平衡选择）";
        }

        private static string NormalizePreset(string value)
        {
            if (string.Equals(value, "Fast", StringComparison.OrdinalIgnoreCase) || value == "快速") return "Fast";
            if (string.Equals(value, "Extreme", StringComparison.OrdinalIgnoreCase) || value == "极限") return "Extreme";
            return "Balanced";
        }

        public static string DisplayPreset(string value)
        {
            string normalized = NormalizePreset(value);
            return normalized == "Fast" ? "快速" : (normalized == "Extreme" ? "极限" : "均衡");
        }
    }
}
