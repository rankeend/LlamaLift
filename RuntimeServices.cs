using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Web.Script.Serialization;

namespace LlamaServerManager
{
    public sealed class LlamaServerCandidate
    {
        public string ExecutablePath { get; set; }
        public string Source { get; set; }
        public int Score { get; set; }
        public DateTime LastWriteTimeUtc { get; set; }

        public string InstallDirectory
        {
            get
            {
                try { return Path.GetDirectoryName(ExecutablePath) ?? string.Empty; }
                catch { return string.Empty; }
            }
        }
    }

    public static class LlamaServerLocator
    {
        private const int MaximumDirectoriesPerRoot = 1200;
        private const int MaximumDepth = 6;

        public static List<LlamaServerCandidate> FindCandidates(AppConfig config)
        {
            return FindCandidates(config, null, true);
        }

        internal static List<LlamaServerCandidate> FindCandidates(AppConfig config, IEnumerable<string> extraRoots, bool includeSystemLocations)
        {
            Dictionary<string, LlamaServerCandidate> candidates = new Dictionary<string, LlamaServerCandidate>(StringComparer.OrdinalIgnoreCase);
            if (config != null)
            {
                if (config.InstalledRuntimes != null)
                    foreach (InstalledRuntime runtime in config.InstalledRuntimes)
                        AddCandidate(candidates, runtime == null ? null : runtime.ServerExecutable, "LlamaLift 已登记运行时", 100);
                if (config.Profiles != null)
                    foreach (ModelProfile profile in config.Profiles)
                        AddCandidate(candidates, profile == null ? null : profile.ServerExecutable, "现有模型配置", 95);
            }

            if (extraRoots != null)
                foreach (string root in extraRoots) SearchRoot(candidates, root, "指定目录", 88);

            if (includeSystemLocations)
            {
                AddPathCandidates(candidates);
                SearchRoot(candidates, ConfigStore.RuntimeDirectory, "LlamaLift 运行时目录", 92);
                SearchRoot(candidates, AppDomain.CurrentDomain.BaseDirectory, "应用程序目录", 84);
                foreach (SearchLocation location in CommonLocations())
                    SearchRoot(candidates, location.Path, location.Source, location.Score);
            }

            List<LlamaServerCandidate> result = candidates.Values.ToList();
            result.Sort(delegate(LlamaServerCandidate left, LlamaServerCandidate right)
            {
                int score = right.Score.CompareTo(left.Score);
                if (score != 0) return score;
                return right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
            });
            return result;
        }

        private static void AddPathCandidates(Dictionary<string, LlamaServerCandidate> candidates)
        {
            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string raw in pathValue.Split(Path.PathSeparator))
            {
                string directory = raw == null ? string.Empty : raw.Trim().Trim('"');
                if (directory.Length == 0) continue;
                AddCandidate(candidates, Path.Combine(directory, "llama-server.exe"), "系统 PATH", 94);
            }
        }

        private static IEnumerable<SearchLocation> CommonLocations()
        {
            List<SearchLocation> roots = new List<SearchLocation>();
            string user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            AddNamedRoots(roots, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "桌面常用目录", 78);
            AddNamedRoots(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "文档常用目录", 76);
            AddNamedRoots(roots, string.IsNullOrWhiteSpace(user) ? string.Empty : Path.Combine(user, "Downloads"), "下载常用目录", 77);
            AddNamedRoots(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Program Files", 74);
            AddNamedRoots(roots, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Program Files (x86)", 72);
            AddNamedRoots(roots, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"), "用户程序目录", 80);

            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    string root = drive.RootDirectory.FullName;
                    string[] names = { "llama.cpp", "llama-cpp", "llama", "LLM\\llama.cpp", "AI\\llama.cpp", "tools\\llama.cpp" };
                    foreach (string name in names) roots.Add(new SearchLocation(Path.Combine(root, name), "磁盘常用安装目录", 82));
                }
            }
            catch { }
            return roots;
        }

        private static void AddNamedRoots(List<SearchLocation> roots, string parent, string source, int score)
        {
            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent)) return;
            string[] names = { "llama.cpp", "llama-cpp", "llama", "LlamaLift" };
            foreach (string name in names) roots.Add(new SearchLocation(Path.Combine(parent, name), source, score));
            try
            {
                foreach (string directory in Directory.GetDirectories(parent, "*llama*", SearchOption.TopDirectoryOnly))
                    roots.Add(new SearchLocation(directory, source, score));
            }
            catch { }
        }

        private static void SearchRoot(Dictionary<string, LlamaServerCandidate> candidates, string root, string source, int score)
        {
            if (string.IsNullOrWhiteSpace(root)) return;
            if (File.Exists(root))
            {
                AddCandidate(candidates, root, source, score);
                return;
            }
            if (!Directory.Exists(root)) return;

            Queue<SearchDirectory> pending = new Queue<SearchDirectory>();
            pending.Enqueue(new SearchDirectory(root, 0));
            int visited = 0;
            while (pending.Count > 0 && visited++ < MaximumDirectoriesPerRoot)
            {
                SearchDirectory current = pending.Dequeue();
                try
                {
                    AddCandidate(candidates, Path.Combine(current.Path, "llama-server.exe"), source, score);
                    if (current.Depth >= MaximumDepth) continue;
                    foreach (string child in Directory.GetDirectories(current.Path))
                    {
                        try
                        {
                            FileAttributes attributes = File.GetAttributes(child);
                            if ((attributes & FileAttributes.ReparsePoint) != 0) continue;
                        }
                        catch { continue; }
                        pending.Enqueue(new SearchDirectory(child, current.Depth + 1));
                    }
                }
                catch { }
            }
        }

        private static void AddCandidate(Dictionary<string, LlamaServerCandidate> candidates, string path, string source, int score)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                string full = Path.GetFullPath(path.Trim().Trim('"'));
                if (!File.Exists(full) || !string.Equals(Path.GetFileName(full), "llama-server.exe", StringComparison.OrdinalIgnoreCase)) return;
                LlamaServerCandidate existing;
                if (candidates.TryGetValue(full, out existing))
                {
                    if (score > existing.Score) { existing.Score = score; existing.Source = source; }
                    return;
                }
                candidates[full] = new LlamaServerCandidate
                {
                    ExecutablePath = full,
                    Source = source,
                    Score = score,
                    LastWriteTimeUtc = File.GetLastWriteTimeUtc(full)
                };
            }
            catch { }
        }

        private sealed class SearchDirectory
        {
            public string Path { get; private set; }
            public int Depth { get; private set; }
            public SearchDirectory(string path, int depth) { Path = path; Depth = depth; }
        }

        private sealed class SearchLocation
        {
            public string Path { get; private set; }
            public string Source { get; private set; }
            public int Score { get; private set; }
            public SearchLocation(string path, string source, int score) { Path = path; Source = source; Score = score; }
        }
    }

    public sealed class RuntimeDownload
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public long Size { get; set; }
        public string Digest { get; set; }
    }

    public sealed class LlamaReleaseAsset
    {
        public string ReleaseTag { get; set; }
        public string PublishedAt { get; set; }
        public string Backend { get; set; }
        public string Architecture { get; set; }
        public List<RuntimeDownload> Downloads { get; set; }

        public LlamaReleaseAsset()
        {
            Downloads = new List<RuntimeDownload>();
            Architecture = "x64";
        }

        public long TotalSize
        {
            get { return Downloads == null ? 0L : Downloads.Sum(delegate(RuntimeDownload item) { return item.Size; }); }
        }

        public string MainAssetName
        {
            get { return Downloads == null || Downloads.Count == 0 ? string.Empty : Downloads[0].Name; }
        }

        public override string ToString()
        {
            double mib = TotalSize / 1024D / 1024D;
            return ReleaseTag + " · " + Backend + " · " + Architecture + " · " + mib.ToString("0", CultureInfo.InvariantCulture) + " MiB";
        }
    }

    internal sealed class GitHubReleaseDto
    {
        public string tag_name { get; set; }
        public string published_at { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public List<GitHubAssetDto> assets { get; set; }
    }

    internal sealed class GitHubAssetDto
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
        public string digest { get; set; }
    }

    public static class LlamaReleaseClient
    {
        private const string ReleasesUrl = "https://api.github.com/repos/ggml-org/llama.cpp/releases?per_page=8";

        public static Task<List<LlamaReleaseAsset>> GetWindowsAssetsAsync()
        {
            return Task.Factory.StartNew<List<LlamaReleaseAsset>>(delegate { return GetWindowsAssets(); });
        }

        private static List<LlamaReleaseAsset> GetWindowsAssets()
        {
            Exception last = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try { return GetWindowsAssetsOnce(); }
                catch (WebException ex)
                {
                    last = ex;
                    if (attempt < 3) Thread.Sleep(500 * attempt);
                }
                catch (IOException ex)
                {
                    last = ex;
                    if (attempt < 3) Thread.Sleep(500 * attempt);
                }
            }
            throw new InvalidOperationException("连续 3 次连接 llama.cpp 官方 Release 失败，请检查网络或稍后重试。", last);
        }

        private static List<LlamaReleaseAsset> GetWindowsAssetsOnce()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ReleasesUrl);
            request.Method = "GET";
            request.UserAgent = "LlamaLift/" + AppVersion.ProductVersion;
            request.Accept = "application/vnd.github+json";
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;

            string json;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                json = reader.ReadToEnd();
            }

            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            List<GitHubReleaseDto> releases = serializer.Deserialize<List<GitHubReleaseDto>>(json) ?? new List<GitHubReleaseDto>();
            List<LlamaReleaseAsset> result = new List<LlamaReleaseAsset>();

            foreach (GitHubReleaseDto release in releases)
            {
                if (release == null || release.draft || release.prerelease || release.assets == null) continue;
                foreach (GitHubAssetDto item in release.assets)
                {
                    if (!IsSupportedMainAsset(item)) continue;
                    LlamaReleaseAsset asset = new LlamaReleaseAsset();
                    asset.ReleaseTag = release.tag_name ?? string.Empty;
                    asset.PublishedAt = release.published_at ?? string.Empty;
                    asset.Backend = DetectBackend(item.name);
                    asset.Architecture = "x64";
                    asset.Downloads.Add(ToDownload(item));

                    string cudaVersion = DetectCudaVersion(item.name);
                    if (!string.IsNullOrWhiteSpace(cudaVersion))
                    {
                        GitHubAssetDto companion = release.assets.FirstOrDefault(delegate(GitHubAssetDto candidate)
                        {
                            return candidate != null && candidate.name != null &&
                                candidate.name.StartsWith("cudart-", StringComparison.OrdinalIgnoreCase) &&
                                candidate.name.IndexOf(cudaVersion, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                candidate.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
                        });
                        if (companion != null) asset.Downloads.Add(ToDownload(companion));
                    }
                    result.Add(asset);
                }
            }

            return result.OrderByDescending(delegate(LlamaReleaseAsset item) { return ParseBuildNumber(item.ReleaseTag); })
                .ThenBy(delegate(LlamaReleaseAsset item) { return BackendOrder(item.Backend); })
                .ToList();
        }

        private static bool IsSupportedMainAsset(GitHubAssetDto item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.name) || string.IsNullOrWhiteSpace(item.browser_download_url)) return false;
            string name = item.name.ToLowerInvariant();
            if (!name.EndsWith(".zip", StringComparison.Ordinal)) return false;
            if (!name.StartsWith("llama-", StringComparison.Ordinal) || name.StartsWith("llama-server-", StringComparison.Ordinal)) return false;
            if (name.IndexOf("-bin-win-", StringComparison.Ordinal) < 0) return false;
            if (name.IndexOf("arm64", StringComparison.Ordinal) >= 0) return false;
            return name.IndexOf("x64", StringComparison.Ordinal) >= 0;
        }

        private static RuntimeDownload ToDownload(GitHubAssetDto item)
        {
            return new RuntimeDownload
            {
                Name = item.name ?? string.Empty,
                Url = item.browser_download_url ?? string.Empty,
                Size = item.size,
                Digest = item.digest ?? string.Empty
            };
        }

        private static string DetectBackend(string name)
        {
            string lower = (name ?? string.Empty).ToLowerInvariant();
            if (lower.IndexOf("cuda-13", StringComparison.Ordinal) >= 0) return "CUDA 13";
            if (lower.IndexOf("cuda-12", StringComparison.Ordinal) >= 0) return "CUDA 12";
            if (lower.IndexOf("vulkan", StringComparison.Ordinal) >= 0) return "Vulkan";
            if (lower.IndexOf("sycl", StringComparison.Ordinal) >= 0) return "SYCL";
            if (lower.IndexOf("hip", StringComparison.Ordinal) >= 0) return "HIP";
            if (lower.IndexOf("cpu", StringComparison.Ordinal) >= 0) return "CPU";
            return "Windows x64";
        }

        private static string DetectCudaVersion(string name)
        {
            string lower = (name ?? string.Empty).ToLowerInvariant();
            int start = lower.IndexOf("cuda-", StringComparison.Ordinal);
            if (start < 0) return string.Empty;
            int end = lower.IndexOf("-x64", start, StringComparison.Ordinal);
            return end <= start ? string.Empty : lower.Substring(start, end - start);
        }

        private static int ParseBuildNumber(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return 0;
            int value;
            return int.TryParse(tag.TrimStart('b', 'B'), out value) ? value : 0;
        }

        private static int BackendOrder(string backend)
        {
            if (backend == "CUDA 12") return 0;
            if (backend == "CUDA 13") return 1;
            if (backend == "Vulkan") return 2;
            if (backend == "HIP") return 3;
            if (backend == "SYCL") return 4;
            if (backend == "CPU") return 5;
            return 9;
        }
    }

    public static class RuntimeInstaller
    {
        public static Task<InstalledRuntime> InstallAsync(LlamaReleaseAsset asset, IProgress<int> progress)
        {
            if (asset == null) throw new ArgumentNullException("asset");
            return Task.Factory.StartNew<InstalledRuntime>(delegate { return Install(asset, progress); });
        }

        private static InstalledRuntime Install(LlamaReleaseAsset asset, IProgress<int> progress)
        {
            if (asset.Downloads == null || asset.Downloads.Count == 0) throw new InvalidOperationException("所选 Release 没有可下载文件。");
            Directory.CreateDirectory(ConfigStore.RuntimeDirectory);
            string tag = SafeSegment(asset.ReleaseTag);
            string backend = SafeSegment(asset.Backend);
            string tagDirectory = Path.Combine(ConfigStore.RuntimeDirectory, tag);
            string finalDirectory = Path.Combine(tagDirectory, backend);
            string existing = FindServerExecutable(finalDirectory);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                Report(progress, 100);
                return CreateRecord(asset, finalDirectory, existing, string.Empty);
            }

            Directory.CreateDirectory(tagDirectory);
            string stagingRoot = Path.Combine(ConfigStore.RuntimeDirectory, ".installing-" + Guid.NewGuid().ToString("N"));
            string contentDirectory = Path.Combine(stagingRoot, "content");
            Directory.CreateDirectory(contentDirectory);
            string mainHash = string.Empty;
            try
            {
                long totalBytes = Math.Max(1L, asset.TotalSize);
                long completedBytes = 0L;
                for (int i = 0; i < asset.Downloads.Count; i++)
                {
                    RuntimeDownload part = asset.Downloads[i];
                    ValidateDownloadUrl(part.Url);
                    string archivePath = Path.Combine(stagingRoot, SafeSegment(part.Name));
                    long before = completedBytes;
                    DownloadWithRetry(part.Url, archivePath, delegate(long received)
                    {
                        int value = Convert.ToInt32(Math.Min(88L, ((before + received) * 88L) / totalBytes));
                        Report(progress, value);
                    });
                    string hash = ComputeSha256(archivePath);
                    VerifyDigest(part, hash);
                    if (i == 0) mainHash = hash;
                    completedBytes += Math.Max(part.Size, new FileInfo(archivePath).Length);
                    ExtractSafe(archivePath, contentDirectory);
                    File.Delete(archivePath);
                    Report(progress, 90 + ((i + 1) * 5 / asset.Downloads.Count));
                }

                string stagedServer = FindServerExecutable(contentDirectory);
                if (string.IsNullOrWhiteSpace(stagedServer))
                    throw new InvalidDataException("官方压缩包解压后未找到 llama-server.exe，安装已取消。");

                if (Directory.Exists(finalDirectory))
                    finalDirectory = finalDirectory + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
                string relativeServer = stagedServer.Substring(contentDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                Directory.Move(contentDirectory, finalDirectory);
                string finalServer = Path.Combine(finalDirectory, relativeServer);
                Report(progress, 100);
                return CreateRecord(asset, finalDirectory, finalServer, mainHash);
            }
            finally
            {
                try { if (Directory.Exists(stagingRoot)) Directory.Delete(stagingRoot, true); } catch { }
            }
        }

        private static InstalledRuntime CreateRecord(LlamaReleaseAsset asset, string directory, string server, string hash)
        {
            return new InstalledRuntime
            {
                ReleaseTag = asset.ReleaseTag,
                Backend = asset.Backend,
                AssetName = asset.MainAssetName,
                InstallDirectory = directory,
                ServerExecutable = server,
                InstalledAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                SourceUrl = asset.Downloads[0].Url,
                Sha256 = hash
            };
        }

        private static void DownloadWithRetry(string url, string destination, Action<long> progress)
        {
            Exception last = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    if (File.Exists(destination)) File.Delete(destination);
                    Download(url, destination, progress);
                    return;
                }
                catch (WebException ex)
                {
                    last = ex;
                    if (attempt < 3) Thread.Sleep(800 * attempt);
                }
                catch (IOException ex)
                {
                    last = ex;
                    if (attempt < 3) Thread.Sleep(800 * attempt);
                }
            }
            throw new InvalidOperationException("连续 3 次下载失败，请检查网络后重试。", last);
        }

        private static void Download(string url, string destination, Action<long> progress)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "LlamaLift/" + AppVersion.ProductVersion;
            request.AllowAutoRedirect = true;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Timeout = 30000;
            request.ReadWriteTimeout = 30000;
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream input = response.GetResponseStream())
            using (FileStream output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] buffer = new byte[1024 * 128];
                long received = 0L;
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                    received += read;
                    if (progress != null) progress(received);
                }
                output.Flush(true);
            }
        }

        private static void ExtractSafe(string archivePath, string destination)
        {
            string root = Path.GetFullPath(destination).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (ZipArchive archive = ZipFile.OpenRead(archivePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(destination, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("压缩包包含越界路径，已拒绝解压：" + entry.FullName);
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None))
                        input.CopyTo(output);
                }
            }
        }

        private static void VerifyDigest(RuntimeDownload part, string actual)
        {
            if (string.IsNullOrWhiteSpace(part.Digest)) return;
            string expected = part.Digest.Trim();
            int colon = expected.IndexOf(':');
            if (colon >= 0)
            {
                string algorithm = expected.Substring(0, colon);
                if (!algorithm.Equals("sha256", StringComparison.OrdinalIgnoreCase)) return;
                expected = expected.Substring(colon + 1);
            }
            if (!expected.Equals(actual, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("下载文件 SHA-256 校验失败：" + part.Name);
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream input = File.OpenRead(path))
            {
                byte[] hash = sha.ComputeHash(input);
                StringBuilder text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash) text.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return text.ToString();
            }
        }

        private static string FindServerExecutable(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return string.Empty;
            string[] matches = Directory.GetFiles(directory, "llama-server.exe", SearchOption.AllDirectories);
            return matches.Length == 0 ? string.Empty : matches[0];
        }

        private static void ValidateDownloadUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidDataException("下载地址不是有效的 HTTPS URL。");
            string host = uri.Host.ToLowerInvariant();
            if (host != "github.com" && !host.EndsWith(".github.com", StringComparison.Ordinal) &&
                !host.EndsWith(".githubusercontent.com", StringComparison.Ordinal))
                throw new InvalidDataException("拒绝从非 GitHub 域名下载运行时：" + host);
        }

        private static string SafeSegment(string value)
        {
            string result = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) result = result.Replace(invalid, '-');
            return result.Replace(' ', '-');
        }

        private static void Report(IProgress<int> progress, int value)
        {
            if (progress != null) progress.Report(Math.Max(0, Math.Min(100, value)));
        }
    }
}
