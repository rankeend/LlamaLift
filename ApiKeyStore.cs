using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;

namespace LlamaServerManager
{
    public sealed class ManagedApiKeyFile
    {
        public string Name { get; set; }
        public string FilePath { get; set; }
        public int KeyCount { get; set; }
        public string MaskedPreview { get; set; }

        public ManagedApiKeyFile()
        {
            Name = string.Empty;
            FilePath = string.Empty;
            MaskedPreview = string.Empty;
        }

        public override string ToString()
        {
            return Name + "  ·  " + KeyCount + " 个 Key  ·  " + MaskedPreview;
        }
    }

    public static class ApiKeyFileSupport
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetShortPathName(string longPath, StringBuilder shortPath, uint bufferLength);

        public static bool TryOpenForRead(string path, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return true;
            try
            {
                string full = Path.GetFullPath(path);
                if (!File.Exists(full))
                {
                    error = "文件不存在：" + full;
                    return false;
                }
                using (FileStream stream = new FileStream(full, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    if (stream.CanRead) return true;
                }
                error = "文件无法读取：" + full;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static string FindReadableReplacement(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath)) return string.Empty;
            string fileName;
            try { fileName = Path.GetFileName(configuredPath); }
            catch { return string.Empty; }
            if (string.IsNullOrWhiteSpace(fileName)) return string.Empty;

            List<string> directories = new List<string>();
            directories.Add(ConfigStore.ApiKeyDirectory);
            directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlamaLift", "api-keys"));
            directories.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LlamaServerManager", "api-keys"));
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string directory in directories)
            {
                try
                {
                    string candidate = Path.GetFullPath(Path.Combine(directory, fileName));
                    if (!visited.Add(candidate) || string.Equals(candidate, Path.GetFullPath(configuredPath), StringComparison.OrdinalIgnoreCase)) continue;
                    string error;
                    if (TryOpenForRead(candidate, out error)) return candidate;
                }
                catch { }
            }
            return string.Empty;
        }

        public static string PrepareForLaunch(string sourcePath, out bool bridged)
        {
            bridged = false;
            if (string.IsNullOrWhiteSpace(sourcePath)) return string.Empty;
            string full = Path.GetFullPath(sourcePath);
            string readError;
            if (!TryOpenForRead(full, out readError))
                throw new IOException("API Key 文件存在但无法读取。请在“API Key 管理”中重新选择或新建密钥。详情：" + readError);
            if (IsAscii(full)) return full;

            string runtimeDirectory = Path.Combine(Path.GetTempPath(), "LlamaLift", "runtime-keys");
            Directory.CreateDirectory(runtimeDirectory);
            CleanupStaleCopies(runtimeDirectory);
            FileInfo info = new FileInfo(full);
            string identity = full.ToUpperInvariant() + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            string target = Path.Combine(runtimeDirectory, "key-" + Hash(identity).Substring(0, 24) + ".txt");
            File.Copy(full, target, true);
            File.SetLastWriteTimeUtc(target, DateTime.UtcNow);

            string compatible = ToShortPath(target);
            if (!IsAscii(compatible))
                throw new IOException("当前 llama-server 无法可靠读取含非 ASCII 字符的 API Key 路径，且系统未能创建兼容短路径。请把密钥文件放到纯英文目录后重试。");
            if (!TryOpenForRead(compatible, out readError))
                throw new IOException("API Key 运行时兼容副本无法读取：" + readError);
            bridged = !string.Equals(full, compatible, StringComparison.OrdinalIgnoreCase);
            return compatible;
        }

        public static bool IsAscii(string value)
        {
            if (value == null) return true;
            foreach (char ch in value) if (ch > 127) return false;
            return true;
        }

        public static void ReleaseRuntimeCopy(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            try
            {
                string rootDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "LlamaLift", "runtime-keys"))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string root = rootDirectory + Path.DirectorySeparatorChar;
                string shortRoot = ToShortPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(path);
                string name = Path.GetFileName(full);
                if ((!full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !full.StartsWith(shortRoot, StringComparison.OrdinalIgnoreCase)) ||
                    !name.StartsWith("key-", StringComparison.OrdinalIgnoreCase) ||
                    !name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) return;
                if (File.Exists(full)) File.Delete(full);
            }
            catch { }
        }

        private static string ToShortPath(string path)
        {
            try
            {
                StringBuilder buffer = new StringBuilder(1024);
                uint length = GetShortPathName(path, buffer, (uint)buffer.Capacity);
                if (length > 0 && length < buffer.Capacity) return buffer.ToString();
            }
            catch { }
            return path;
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                StringBuilder result = new StringBuilder(digest.Length * 2);
                foreach (byte item in digest) result.Append(item.ToString("x2"));
                return result.ToString();
            }
        }

        private static void CleanupStaleCopies(string directory)
        {
            try
            {
                foreach (string path in Directory.GetFiles(directory, "key-*.txt", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddDays(-7)) File.Delete(path);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }

    public sealed class ApiKeyStore
    {
        private readonly string directory;

        public string DirectoryPath { get { return directory; } }

        public ApiKeyStore(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath)) throw new ArgumentException("API Key 目录不能为空。", "directoryPath");
            directory = Path.GetFullPath(directoryPath);
            Directory.CreateDirectory(directory);
        }

        public List<ManagedApiKeyFile> List()
        {
            List<ManagedApiKeyFile> result = new List<ManagedApiKeyFile>();
            Directory.CreateDirectory(directory);
            foreach (string path in Directory.GetFiles(directory, "*.txt", SearchOption.TopDirectoryOnly))
            {
                try { result.Add(Describe(path)); }
                catch { }
            }
            result.Sort(delegate(ManagedApiKeyFile left, ManagedApiKeyFile right)
            {
                return StringComparer.CurrentCultureIgnoreCase.Compare(left.Name, right.Name);
            });
            return result;
        }

        public ManagedApiKeyFile Save(string name, string content)
        {
            string safeName = SanitizeName(name);
            List<string> keys = NormalizeKeys(content);
            if (keys.Count == 0) throw new InvalidOperationException("至少需要输入一个非空 API Key。");
            string path = ResolveManagedPath(safeName + ".txt");
            File.WriteAllLines(path, keys.ToArray(), new UTF8Encoding(false));
            return Describe(path);
        }

        public ManagedApiKeyFile Import(string sourcePath, string name)
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                throw new FileNotFoundException("找不到要导入的 API Key 文件。", sourcePath);
            string content = File.ReadAllText(sourcePath, Encoding.UTF8);
            string targetName = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(sourcePath) : name;
            return Save(targetName, content);
        }

        public string Read(string path)
        {
            string managed = ResolveExistingManagedPath(path);
            return File.ReadAllText(managed, Encoding.UTF8);
        }

        public void Delete(string path)
        {
            string managed = ResolveExistingManagedPath(path);
            File.Delete(managed);
        }

        public ManagedApiKeyFile Describe(string path)
        {
            string managed = ResolveExistingManagedPath(path);
            string[] lines = File.ReadAllLines(managed, Encoding.UTF8);
            int count = 0;
            string preview = "未读取到密钥";
            foreach (string line in lines)
            {
                string key = line.Trim();
                if (key.Length == 0) continue;
                count++;
                if (count == 1) preview = Mask(key);
            }
            return new ManagedApiKeyFile
            {
                Name = Path.GetFileNameWithoutExtension(managed),
                FilePath = managed,
                KeyCount = count,
                MaskedPreview = preview
            };
        }

        public static string GenerateKey()
        {
            byte[] bytes = new byte[32];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
            StringBuilder text = new StringBuilder("sk-llamalift-", 13 + bytes.Length * 2);
            foreach (byte value in bytes) text.Append(value.ToString("x2"));
            return text.ToString();
        }

        private string ResolveManagedPath(string fileName)
        {
            string path = Path.GetFullPath(Path.Combine(directory, fileName));
            string prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("API Key 文件路径超出了托管目录。");
            return path;
        }

        private string ResolveExistingManagedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("没有选择 API Key 文件。", "path");
            string full = Path.GetFullPath(path);
            string prefix = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("只能管理应用数据目录中的 API Key 文件。");
            if (!File.Exists(full)) throw new FileNotFoundException("API Key 文件不存在。", full);
            return full;
        }

        private static string SanitizeName(string value)
        {
            string name = string.IsNullOrWhiteSpace(value) ? "api-key" : value.Trim();
            foreach (char invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '-');
            name = name.Trim(' ', '.');
            if (name.Length == 0) name = "api-key";
            if (name.Length > 80) name = name.Substring(0, 80).TrimEnd();
            return name;
        }

        private static List<string> NormalizeKeys(string content)
        {
            List<string> result = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            string normalized = (content ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            foreach (string raw in normalized.Split(new char[] { '\n' }, StringSplitOptions.None))
            {
                string key = raw.Trim();
                if (key.Length == 0 || !seen.Add(key)) continue;
                if (key.IndexOfAny(new char[] { ' ', '\t' }) >= 0)
                    throw new InvalidOperationException("API Key 不能包含空格或制表符；每行只放一个 Key。");
                result.Add(key);
            }
            return result;
        }

        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value)) return "空";
            if (value.Length <= 4) return new string('•', value.Length);
            int visible = Math.Min(4, value.Length);
            return new string('•', Math.Min(12, value.Length - visible)) + value.Substring(value.Length - visible);
        }

    }
}
