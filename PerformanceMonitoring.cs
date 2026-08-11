using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace LlamaServerManager
{
    public sealed class SystemPerformanceSample
    {
        public DateTime Timestamp { get; set; }
        public double CpuUsage { get; set; }
        public double ProcessCpuUsage { get; set; }
        public ulong MemoryTotalBytes { get; set; }
        public ulong MemoryUsedBytes { get; set; }
        public ulong MemoryAvailableBytes { get; set; }
        public long ProcessWorkingSetBytes { get; set; }
        public long ProcessPrivateBytes { get; set; }
        public int ProcessThreads { get; set; }
        public int ProcessHandles { get; set; }
        public double GpuUsage { get; set; }
        public double ProcessGpuUsage { get; set; }
        public double GpuDedicatedBytes { get; set; }
        public double GpuSharedBytes { get; set; }
        public double ProcessGpuDedicatedBytes { get; set; }
        public double DiskReadBytesPerSecond { get; set; }
        public double DiskWriteBytesPerSecond { get; set; }
        public double ProcessReadBytesPerSecond { get; set; }
        public double ProcessWriteBytesPerSecond { get; set; }
        public double NetworkReceiveBytesPerSecond { get; set; }
        public double NetworkSendBytesPerSecond { get; set; }
    }

    public sealed class SystemPerformanceMonitor : IDisposable
    {
        private readonly object sync = new object();
        private readonly List<NamedCounter> gpuEngineCounters = new List<NamedCounter>();
        private readonly List<NamedCounter> gpuMemoryCounters = new List<NamedCounter>();
        private readonly List<NamedCounter> gpuProcessMemoryCounters = new List<NamedCounter>();
        private readonly List<PerformanceCounter> networkReceiveCounters = new List<PerformanceCounter>();
        private readonly List<PerformanceCounter> networkSendCounters = new List<PerformanceCounter>();
        private PerformanceCounter diskReadCounter;
        private PerformanceCounter diskWriteCounter;
        private ulong previousIdle;
        private ulong previousKernel;
        private ulong previousUser;
        private DateTime previousProcessAtUtc;
        private TimeSpan previousProcessCpu;
        private ulong previousProcessRead;
        private ulong previousProcessWrite;
        private bool hasCpuBaseline;
        private bool hasProcessBaseline;
        private DateTime countersRefreshedUtc = DateTime.MinValue;

        public string CpuName { get; private set; }
        public string GpuName { get; private set; }
        public string GpuDriverVersion { get; private set; }
        public ulong GpuMemoryTotalBytes { get; private set; }
        public int LogicalProcessors { get; private set; }
        public int CpuMaxClockMhz { get; private set; }

        public SystemPerformanceMonitor()
        {
            CpuName = "Windows CPU";
            GpuName = "未检测到可读 GPU 计数器";
            GpuDriverVersion = "未知";
            LogicalProcessors = Math.Max(1, Environment.ProcessorCount);
            ReadHardwareIdentity();
        }

        public SystemPerformanceSample Sample(int processId)
        {
            lock (sync)
            {
                if ((DateTime.UtcNow - countersRefreshedUtc).TotalSeconds > 30D)
                    RefreshPerformanceCounters();

                SystemPerformanceSample sample = new SystemPerformanceSample();
                sample.Timestamp = DateTime.Now;
                sample.CpuUsage = ReadCpuUsage();
                ReadMemory(sample);
                ReadGpu(sample, processId);
                ReadDiskAndNetwork(sample);
                ReadProcess(sample, processId);
                return sample;
            }
        }

        private void ReadHardwareIdentity()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string name = Convert.ToString(item["Name"]).Trim();
                        if (!string.IsNullOrWhiteSpace(name)) CpuName = name;
                        int count;
                        if (Int32.TryParse(Convert.ToString(item["NumberOfLogicalProcessors"]), out count) && count > 0)
                            LogicalProcessors = count;
                        Int32.TryParse(Convert.ToString(item["MaxClockSpeed"]), out count);
                        if (count > 0) CpuMaxClockMhz = count;
                        break;
                    }
                }
            }
            catch { }

            try
            {
                List<string> names = new List<string>();
                ulong largestMemory = 0;
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT Name,AdapterRAM,DriverVersion FROM Win32_VideoController"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string name = Convert.ToString(item["Name"]).Trim();
                        if (!string.IsNullOrWhiteSpace(name)) names.Add(name);
                        string driver = Convert.ToString(item["DriverVersion"]).Trim();
                        if (!string.IsNullOrWhiteSpace(driver)) GpuDriverVersion = driver;
                        ulong memory;
                        if (UInt64.TryParse(Convert.ToString(item["AdapterRAM"]), out memory) && memory > largestMemory)
                            largestMemory = memory;
                    }
                }
                if (names.Count > 0) GpuName = string.Join(" · ", names.ToArray());
                GpuMemoryTotalBytes = largestMemory;
            }
            catch { }
        }

        private void RefreshPerformanceCounters()
        {
            DisposeNamedCounters(gpuEngineCounters);
            DisposeNamedCounters(gpuMemoryCounters);
            DisposeNamedCounters(gpuProcessMemoryCounters);
            DisposeCounters(networkReceiveCounters);
            DisposeCounters(networkSendCounters);
            DisposeCounter(ref diskReadCounter);
            DisposeCounter(ref diskWriteCounter);

            try
            {
                if (PerformanceCounterCategory.Exists("PhysicalDisk"))
                {
                    diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total", true);
                    diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total", true);
                    diskReadCounter.NextValue();
                    diskWriteCounter.NextValue();
                }
            }
            catch { DisposeCounter(ref diskReadCounter); DisposeCounter(ref diskWriteCounter); }

            try
            {
                if (PerformanceCounterCategory.Exists("Network Interface"))
                {
                    PerformanceCounterCategory category = new PerformanceCounterCategory("Network Interface");
                    foreach (string instance in category.GetInstanceNames())
                    {
                        try
                        {
                            PerformanceCounter receive = new PerformanceCounter("Network Interface", "Bytes Received/sec", instance, true);
                            PerformanceCounter send = new PerformanceCounter("Network Interface", "Bytes Sent/sec", instance, true);
                            receive.NextValue();
                            send.NextValue();
                            networkReceiveCounters.Add(receive);
                            networkSendCounters.Add(send);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            countersRefreshedUtc = DateTime.UtcNow;
        }

        private static void AddNamedCounters(string categoryName, string counterName, List<NamedCounter> target)
        {
            try
            {
                if (!PerformanceCounterCategory.Exists(categoryName)) return;
                PerformanceCounterCategory category = new PerformanceCounterCategory(categoryName);
                foreach (string instance in category.GetInstanceNames())
                {
                    try
                    {
                        PerformanceCounter counter = new PerformanceCounter(categoryName, counterName, instance, true);
                        counter.NextValue();
                        target.Add(new NamedCounter(instance + "|" + counterName, counter));
                    }
                    catch { }
                }
            }
            catch { }
        }

        private double ReadCpuUsage()
        {
            ulong idle;
            ulong kernel;
            ulong user;
            if (!GetSystemTimes(out idle, out kernel, out user)) return 0D;
            double value = 0D;
            if (hasCpuBaseline)
            {
                ulong idleDelta = idle - previousIdle;
                ulong kernelDelta = kernel - previousKernel;
                ulong userDelta = user - previousUser;
                ulong total = kernelDelta + userDelta;
                if (total > 0) value = Math.Max(0D, Math.Min(100D, (total - idleDelta) * 100D / total));
            }
            previousIdle = idle;
            previousKernel = kernel;
            previousUser = user;
            hasCpuBaseline = true;
            return value;
        }

        private static void ReadMemory(SystemPerformanceSample sample)
        {
            MEMORYSTATUSEX state = new MEMORYSTATUSEX();
            state.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (!GlobalMemoryStatusEx(ref state)) return;
            sample.MemoryTotalBytes = state.ullTotalPhys;
            sample.MemoryAvailableBytes = state.ullAvailPhys;
            sample.MemoryUsedBytes = state.ullTotalPhys >= state.ullAvailPhys ? state.ullTotalPhys - state.ullAvailPhys : 0;
        }

        private void ReadGpu(SystemPerformanceSample sample, int processId)
        {
            Dictionary<string, double> engines = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            string pidToken = processId > 0 ? "pid_" + processId.ToString(CultureInfo.InvariantCulture) + "_" : string.Empty;
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name,UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string name = Convert.ToString(item["Name"]);
                        double value = ReadDouble(item["UtilizationPercentage"]);
                        string key = GpuEngineKey(name);
                        double existing;
                        engines.TryGetValue(key, out existing);
                        engines[key] = existing + value;
                        if (pidToken.Length > 0 && name.IndexOf(pidToken, StringComparison.OrdinalIgnoreCase) >= 0)
                            sample.ProcessGpuUsage += value;
                    }
                }
            }
            catch { }
            foreach (double engine in engines.Values) sample.GpuUsage = Math.Max(sample.GpuUsage, engine);
            sample.GpuUsage = Math.Max(0D, Math.Min(100D, sample.GpuUsage));
            sample.ProcessGpuUsage = Math.Max(0D, Math.Min(100D, sample.ProcessGpuUsage));

            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT DedicatedUsage,SharedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        sample.GpuDedicatedBytes += ReadDouble(item["DedicatedUsage"]);
                        sample.GpuSharedBytes += ReadDouble(item["SharedUsage"]);
                    }
                }
            }
            catch { }
            if (pidToken.Length == 0) return;
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT Name,DedicatedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUProcessMemory"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string name = Convert.ToString(item["Name"]);
                        if (name.IndexOf(pidToken, StringComparison.OrdinalIgnoreCase) >= 0)
                            sample.ProcessGpuDedicatedBytes += ReadDouble(item["DedicatedUsage"]);
                    }
                }
            }
            catch { }
        }

        private static double ReadDouble(object value)
        {
            double parsed;
            return value != null && Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed)
                ? Math.Max(0D, parsed)
                : 0D;
        }

        private static string GpuEngineKey(string instance)
        {
            int index = instance.IndexOf("_luid_", StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? instance.Substring(index) : instance;
        }

        private void ReadDiskAndNetwork(SystemPerformanceSample sample)
        {
            try { if (diskReadCounter != null) sample.DiskReadBytesPerSecond = Math.Max(0D, diskReadCounter.NextValue()); } catch { }
            try { if (diskWriteCounter != null) sample.DiskWriteBytesPerSecond = Math.Max(0D, diskWriteCounter.NextValue()); } catch { }
            foreach (PerformanceCounter counter in networkReceiveCounters)
                try { sample.NetworkReceiveBytesPerSecond += Math.Max(0D, counter.NextValue()); } catch { }
            foreach (PerformanceCounter counter in networkSendCounters)
                try { sample.NetworkSendBytesPerSecond += Math.Max(0D, counter.NextValue()); } catch { }
        }

        private void ReadProcess(SystemPerformanceSample sample, int processId)
        {
            if (processId <= 0)
            {
                hasProcessBaseline = false;
                return;
            }
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    process.Refresh();
                    sample.ProcessWorkingSetBytes = process.WorkingSet64;
                    sample.ProcessPrivateBytes = process.PrivateMemorySize64;
                    sample.ProcessThreads = process.Threads.Count;
                    sample.ProcessHandles = process.HandleCount;

                    DateTime now = DateTime.UtcNow;
                    double elapsedSeconds = hasProcessBaseline ? Math.Max(0.001D, (now - previousProcessAtUtc).TotalSeconds) : 0D;
                    TimeSpan cpu = process.TotalProcessorTime;
                    if (hasProcessBaseline)
                    {
                        double elapsedMs = (now - previousProcessAtUtc).TotalMilliseconds;
                        double cpuMs = (cpu - previousProcessCpu).TotalMilliseconds;
                        if (elapsedMs > 0D)
                            sample.ProcessCpuUsage = Math.Max(0D, Math.Min(100D, cpuMs * 100D / elapsedMs / Math.Max(1, LogicalProcessors)));
                    }
                    previousProcessAtUtc = now;
                    previousProcessCpu = cpu;

                    IO_COUNTERS io;
                    if (GetProcessIoCounters(process.Handle, out io))
                    {
                        if (hasProcessBaseline)
                        {
                            sample.ProcessReadBytesPerSecond = (io.ReadTransferCount - previousProcessRead) / elapsedSeconds;
                            sample.ProcessWriteBytesPerSecond = (io.WriteTransferCount - previousProcessWrite) / elapsedSeconds;
                        }
                        previousProcessRead = io.ReadTransferCount;
                        previousProcessWrite = io.WriteTransferCount;
                    }
                    hasProcessBaseline = true;
                }
            }
            catch { hasProcessBaseline = false; }
        }

        public void Dispose()
        {
            lock (sync)
            {
                DisposeNamedCounters(gpuEngineCounters);
                DisposeNamedCounters(gpuMemoryCounters);
                DisposeNamedCounters(gpuProcessMemoryCounters);
                DisposeCounters(networkReceiveCounters);
                DisposeCounters(networkSendCounters);
                DisposeCounter(ref diskReadCounter);
                DisposeCounter(ref diskWriteCounter);
            }
        }

        private static void DisposeNamedCounters(List<NamedCounter> counters)
        {
            foreach (NamedCounter counter in counters) try { counter.Counter.Dispose(); } catch { }
            counters.Clear();
        }

        private static void DisposeCounters(List<PerformanceCounter> counters)
        {
            foreach (PerformanceCounter counter in counters) try { counter.Dispose(); } catch { }
            counters.Clear();
        }

        private static void DisposeCounter(ref PerformanceCounter counter)
        {
            if (counter != null) try { counter.Dispose(); } catch { }
            counter = null;
        }

        private sealed class NamedCounter
        {
            public string Name { get; private set; }
            public PerformanceCounter Counter { get; private set; }
            public NamedCounter(string name, PerformanceCounter counter) { Name = name; Counter = counter; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX buffer);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessIoCounters(IntPtr processHandle, out IO_COUNTERS counters);
    }

    public sealed class LlamaPerformanceSample
    {
        public DateTime Timestamp { get; set; }
        public bool ServerReachable { get; set; }
        public bool MetricsAvailable { get; set; }
        public bool SlotsAvailable { get; set; }
        public string Status { get; set; }
        public double PromptTokensPerSecond { get; set; }
        public double GenerationTokensPerSecond { get; set; }
        public double PromptTokensTotal { get; set; }
        public double GeneratedTokensTotal { get; set; }
        public double PromptSecondsTotal { get; set; }
        public double GeneratedSecondsTotal { get; set; }
        public int RequestsProcessing { get; set; }
        public int RequestsDeferred { get; set; }
        public int SlotsTotal { get; set; }
        public int SlotsActive { get; set; }
        public int SlotsSpeculative { get; set; }
        public double ContextUsagePercent { get; set; }
        public int ContextTokensUsed { get; set; }
        public int ContextTokensTotal { get; set; }
        public int ContextHighWatermark { get; set; }

        public LlamaPerformanceSample()
        {
            Status = "等待 llama-server";
            Timestamp = DateTime.Now;
        }
    }

    public sealed class LlamaMetricsClient
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
        private static readonly Regex MetricLine = new Regex(@"^(?<name>llamacpp:[a-z0-9_]+)(?:\{[^}]*\})?\s+(?<value>[-+0-9.eE]+)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public LlamaPerformanceSample Sample(ModelProfile profile)
        {
            LlamaPerformanceSample sample = new LlamaPerformanceSample();
            if (profile == null) return sample;
            string baseUrl = LlamaApiClient.LocalBaseUrl(profile);
            string key = ReadFirstKey(profile.ApiKeyFile);

            string metrics;
            int metricsStatus;
            if (TryGet(baseUrl + "/metrics", key, out metrics, out metricsStatus))
            {
                sample.ServerReachable = true;
                sample.MetricsAvailable = true;
                ParseMetrics(metrics, sample);
            }
            else if (metricsStatus > 0)
            {
                sample.ServerReachable = true;
            }

            string slots;
            int slotsStatus;
            if (TryGet(baseUrl + "/slots", key, out slots, out slotsStatus))
            {
                sample.ServerReachable = true;
                sample.SlotsAvailable = true;
                ParseSlots(slots, sample);
            }
            else if (slotsStatus > 0)
            {
                sample.ServerReachable = true;
            }

            if (!sample.ServerReachable) sample.Status = "服务离线或尚在加载";
            else if (sample.MetricsAvailable && sample.SlotsAvailable) sample.Status = "完整指标在线";
            else if (sample.SlotsAvailable) sample.Status = "槽位在线 · 启用性能指标可查看更多";
            else if (sample.MetricsAvailable) sample.Status = "基础指标在线 · 槽位接口不可用";
            else sample.Status = "服务在线 · 监测接口未启用";
            return sample;
        }

        private static void ParseMetrics(string text, LlamaPerformanceSample sample)
        {
            using (StringReader reader = new StringReader(text ?? string.Empty))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    Match match = MetricLine.Match(line.Trim());
                    if (!match.Success) continue;
                    double value;
                    if (!Double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) continue;
                    string name = match.Groups["name"].Value.ToLowerInvariant();
                    if (name == "llamacpp:prompt_tokens_total") sample.PromptTokensTotal += value;
                    else if (name == "llamacpp:prompt_seconds_total") sample.PromptSecondsTotal += value;
                    else if (name == "llamacpp:prompt_tokens_seconds") sample.PromptTokensPerSecond = Math.Max(sample.PromptTokensPerSecond, value);
                    else if (name == "llamacpp:tokens_predicted_total") sample.GeneratedTokensTotal += value;
                    else if (name == "llamacpp:tokens_predicted_seconds_total") sample.GeneratedSecondsTotal += value;
                    else if (name == "llamacpp:predicted_tokens_seconds") sample.GenerationTokensPerSecond = Math.Max(sample.GenerationTokensPerSecond, value);
                    else if (name == "llamacpp:requests_processing") sample.RequestsProcessing += Convert.ToInt32(Math.Max(0D, value));
                    else if (name == "llamacpp:requests_deferred") sample.RequestsDeferred += Convert.ToInt32(Math.Max(0D, value));
                    else if (name == "llamacpp:n_tokens_max") sample.ContextHighWatermark = Math.Max(sample.ContextHighWatermark, Convert.ToInt32(Math.Max(0D, value)));
                }
            }
        }

        private static void ParseSlots(string json, LlamaPerformanceSample sample)
        {
            object root;
            try { root = Serializer.DeserializeObject(json); }
            catch { return; }
            IEnumerable slots = root as IEnumerable;
            if (slots == null || root is string) return;
            foreach (object value in slots)
            {
                IDictionary<string, object> slot = value as IDictionary<string, object>;
                if (slot == null) continue;
                sample.SlotsTotal++;
                if (GetBoolean(slot, "is_processing")) sample.SlotsActive++;
                if (GetBoolean(slot, "speculative")) sample.SlotsSpeculative++;
                int context = GetInteger(slot, "n_ctx", "n_ctx_slot");
                int used = GetInteger(slot, "n_past", "n_tokens", "n_prompt_tokens_processed");
                if (context > 0) sample.ContextTokensTotal += context;
                if (used > 0) sample.ContextTokensUsed += Math.Min(context > 0 ? context : used, used);
                IDictionary<string, object> timings = GetDictionary(slot, "timings");
                if (timings != null)
                {
                    sample.PromptTokensPerSecond = Math.Max(sample.PromptTokensPerSecond, GetDouble(timings, "prompt_per_second"));
                    sample.GenerationTokensPerSecond = Math.Max(sample.GenerationTokensPerSecond, GetDouble(timings, "predicted_per_second"));
                }
            }
            if (sample.SlotsTotal > 0 && sample.RequestsProcessing == 0) sample.RequestsProcessing = sample.SlotsActive;
            if (sample.ContextTokensTotal > 0)
                sample.ContextUsagePercent = Math.Max(0D, Math.Min(100D, sample.ContextTokensUsed * 100D / sample.ContextTokensTotal));
        }

        private static IDictionary<string, object> GetDictionary(IDictionary<string, object> source, string key)
        {
            object value;
            return source.TryGetValue(key, out value) ? value as IDictionary<string, object> : null;
        }

        private static bool GetBoolean(IDictionary<string, object> source, string key)
        {
            object value;
            if (!source.TryGetValue(key, out value) || value == null) return false;
            bool parsed;
            return Boolean.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out parsed) && parsed;
        }

        private static int GetInteger(IDictionary<string, object> source, params string[] keys)
        {
            foreach (string key in keys)
            {
                object value;
                int parsed;
                if (source.TryGetValue(key, out value) && Int32.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                    return parsed;
            }
            return 0;
        }

        private static double GetDouble(IDictionary<string, object> source, string key)
        {
            object value;
            double parsed;
            return source.TryGetValue(key, out value) && Double.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed) ? parsed : 0D;
        }

        private static bool TryGet(string url, string key, out string body, out int statusCode)
        {
            body = string.Empty;
            statusCode = 0;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.Timeout = 400;
                request.ReadWriteTimeout = 400;
                request.Proxy = null;
                request.UserAgent = "LlamaLift/" + AppVersion.ProductVersion;
                if (!string.IsNullOrWhiteSpace(key))
                {
                    request.Headers[HttpRequestHeader.Authorization] = "Bearer " + key;
                    request.Headers["x-api-key"] = key;
                }
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    statusCode = (int)response.StatusCode;
                    body = reader.ReadToEnd();
                    return statusCode >= 200 && statusCode < 300;
                }
            }
            catch (WebException ex)
            {
                HttpWebResponse response = ex.Response as HttpWebResponse;
                if (response != null) statusCode = (int)response.StatusCode;
                return false;
            }
            catch { return false; }
        }

        private static string ReadFirstKey(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return string.Empty;
            try
            {
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                    if (!string.IsNullOrWhiteSpace(line)) return line.Trim();
            }
            catch { }
            return string.Empty;
        }
    }

    public sealed class RealtimeMetricChart : Control
    {
        private readonly List<ChartPoint> points = new List<ChartPoint>();
        private readonly ToolTip tooltip = new ToolTip();
        private Color accent = Color.FromArgb(0, 122, 255);
        private Color text = Color.FromArgb(29, 29, 31);
        private Color muted = Color.FromArgb(110, 110, 115);
        private Color grid = Color.FromArgb(229, 229, 234);
        private Color surface = Color.White;
        private int hoverIndex = -1;

        public string ChartTitle { get; set; }
        public string Unit { get; set; }
        public double FixedMaximum { get; set; }
        public int Capacity { get; set; }
        public bool Paused { get; set; }

        public RealtimeMetricChart()
        {
            ChartTitle = "实时指标";
            Unit = string.Empty;
            Capacity = 90;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            BackColor = Color.Transparent;
            MinimumSize = new Size(180, 120);
            TabStop = true;
            AccessibleRole = AccessibleRole.Chart;
            MouseMove += ChartMouseMove;
            MouseLeave += delegate { hoverIndex = -1; tooltip.Hide(this); Invalidate(); };
        }

        public void ApplyPalette(ThemePalette palette)
        {
            if (palette == null) return;
            accent = palette.Accent;
            text = palette.Text;
            muted = palette.Muted;
            grid = ThemeService.Mix(palette.Surface, palette.Border, palette.IsDark ? 0.58F : 0.72F);
            surface = palette.Surface;
            Invalidate();
        }

        public void AddValue(double value)
        {
            if (Paused || Double.IsNaN(value) || Double.IsInfinity(value)) return;
            points.Add(new ChartPoint(DateTime.Now, Math.Max(0D, value)));
            while (points.Count > Math.Max(12, Capacity)) points.RemoveAt(0);
            AccessibleName = ChartTitle;
            AccessibleDescription = ChartTitle + " 当前 " + FormatValue(value) + Unit;
            Invalidate();
        }

        public void ClearValues()
        {
            points.Clear();
            hoverIndex = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = ClientRectangle;
            if (bounds.Width < 80 || bounds.Height < 70) return;
            using (SolidBrush background = new SolidBrush(surface)) g.FillRectangle(background, bounds);
            if (Focused)
            {
                Rectangle focus = Rectangle.Inflate(bounds, -3, -3);
                ControlPaint.DrawFocusRectangle(g, focus, accent, surface);
            }

            using (Font titleFont = new Font("Microsoft YaHei UI", 9.5F, FontStyle.Bold, GraphicsUnit.Point))
            using (SolidBrush titleBrush = new SolidBrush(text))
                g.DrawString(ChartTitle, titleFont, titleBrush, 16F, 12F);

            double current = points.Count == 0 ? 0D : points[points.Count - 1].Value;
            string currentText = points.Count == 0 ? "等待数据" : FormatValue(current) + Unit;
            SizeF currentSize;
            using (Font metricFont = new Font("Segoe UI", 14F, FontStyle.Bold, GraphicsUnit.Point))
            using (SolidBrush metricBrush = new SolidBrush(points.Count == 0 ? muted : text))
            {
                currentSize = g.MeasureString(currentText, metricFont);
                g.DrawString(currentText, metricFont, metricBrush, Math.Max(16F, bounds.Width - currentSize.Width - 14F), 7F);
            }

            RectangleF plot = new RectangleF(16F, 50F, bounds.Width - 32F, bounds.Height - 72F);
            using (Pen gridPen = new Pen(grid, 1F))
            {
                gridPen.DashStyle = DashStyle.Dot;
                for (int i = 0; i <= 3; i++)
                {
                    float y = plot.Top + plot.Height * i / 3F;
                    g.DrawLine(gridPen, plot.Left, y, plot.Right, y);
                }
            }
            if (points.Count < 2)
            {
                using (SolidBrush empty = new SolidBrush(muted)) g.DrawString("采样后将在这里显示最近 90 秒趋势", Font, empty, plot.Left, plot.Top + 14F);
                return;
            }

            double maximum = FixedMaximum > 0D ? FixedMaximum : AutoMaximum();
            PointF[] pathPoints = new PointF[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                float x = plot.Left + plot.Width * i / Math.Max(1, points.Count - 1);
                float y = plot.Bottom - plot.Height * Convert.ToSingle(Math.Min(maximum, points[i].Value) / maximum);
                pathPoints[i] = new PointF(x, y);
            }
            using (GraphicsPath fillPath = new GraphicsPath())
            {
                fillPath.AddLines(pathPoints);
                fillPath.AddLine(pathPoints[pathPoints.Length - 1], new PointF(plot.Right, plot.Bottom));
                fillPath.AddLine(new PointF(plot.Right, plot.Bottom), new PointF(plot.Left, plot.Bottom));
                fillPath.CloseFigure();
                Color top = Color.FromArgb(surface.R, surface.G, surface.B);
                using (LinearGradientBrush fill = new LinearGradientBrush(plot, Color.FromArgb(72, accent), Color.FromArgb(0, top), LinearGradientMode.Vertical))
                    g.FillPath(fill, fillPath);
            }
            using (Pen line = new Pen(accent, 2F))
            {
                line.StartCap = LineCap.Round;
                line.EndCap = LineCap.Round;
                line.LineJoin = LineJoin.Round;
                g.DrawLines(line, pathPoints);
            }
            if (hoverIndex >= 0 && hoverIndex < pathPoints.Length)
            {
                PointF point = pathPoints[hoverIndex];
                using (Pen hover = new Pen(Color.FromArgb(120, muted), 1F)) { hover.DashStyle = DashStyle.Dash; g.DrawLine(hover, point.X, plot.Top, point.X, plot.Bottom); }
                using (SolidBrush dot = new SolidBrush(surface)) g.FillEllipse(dot, point.X - 4F, point.Y - 4F, 8F, 8F);
                using (Pen ring = new Pen(accent, 2F)) g.DrawEllipse(ring, point.X - 4F, point.Y - 4F, 8F, 8F);
            }

            string range = "0" + Unit + "  —  " + FormatValue(maximum) + Unit;
            using (SolidBrush hint = new SolidBrush(muted)) g.DrawString(range, new Font("Segoe UI", 7.5F), hint, plot.Left, plot.Bottom + 5F);
        }

        private double AutoMaximum()
        {
            double maximum = 1D;
            foreach (ChartPoint point in points) maximum = Math.Max(maximum, point.Value);
            if (maximum <= 10D) return Math.Ceiling(maximum * 1.2D * 10D) / 10D;
            double magnitude = Math.Pow(10D, Math.Floor(Math.Log10(maximum)));
            return Math.Ceiling(maximum * 1.12D / magnitude) * magnitude;
        }

        private void ChartMouseMove(object sender, MouseEventArgs e)
        {
            if (points.Count < 2 || Width <= 32) return;
            int index = Convert.ToInt32(Math.Round((e.X - 16D) * (points.Count - 1D) / Math.Max(1D, Width - 32D)));
            index = Math.Max(0, Math.Min(points.Count - 1, index));
            if (index == hoverIndex) return;
            hoverIndex = index;
            ShowTooltip(index, e.X + 10, e.Y - 28);
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (points.Count == 0) return;
            if (e.KeyCode == Keys.Left) hoverIndex = Math.Max(0, hoverIndex < 0 ? points.Count - 1 : hoverIndex - 1);
            else if (e.KeyCode == Keys.Right) hoverIndex = Math.Min(points.Count - 1, hoverIndex < 0 ? 0 : hoverIndex + 1);
            else if (e.KeyCode == Keys.Home) hoverIndex = 0;
            else if (e.KeyCode == Keys.End) hoverIndex = points.Count - 1;
            else return;
            int x = 16 + Convert.ToInt32((Width - 32D) * hoverIndex / Math.Max(1D, points.Count - 1D));
            ShowTooltip(hoverIndex, x + 10, 28);
            e.Handled = true;
            Invalidate();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            tooltip.Hide(this);
            hoverIndex = -1;
            Invalidate();
        }

        private void ShowTooltip(int index, int x, int y)
        {
            if (index < 0 || index >= points.Count) return;
            ChartPoint point = points[index];
            tooltip.Show(point.Timestamp.ToString("HH:mm:ss") + "  " + FormatValue(point.Value) + Unit, this,
                Math.Min(Math.Max(8, Width - 120), Math.Max(8, x)), Math.Max(8, y), 1800);
        }

        private static string FormatValue(double value)
        {
            if (value >= 1000D) return value.ToString("0", CultureInfo.InvariantCulture);
            if (value >= 100D) return value.ToString("0.0", CultureInfo.InvariantCulture);
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tooltip.Dispose();
            base.Dispose(disposing);
        }

        private sealed class ChartPoint
        {
            public DateTime Timestamp { get; private set; }
            public double Value { get; private set; }
            public ChartPoint(DateTime timestamp, double value) { Timestamp = timestamp; Value = value; }
        }
    }
}
