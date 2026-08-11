using System;
using System.Collections.Generic;
using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
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
            RestrictToCurrentUser(path);
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
            byte[] bytes = new byte[24];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create()) generator.GetBytes(bytes);
            StringBuilder text = new StringBuilder("llift_");
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

        private static void RestrictToCurrentUser(string path)
        {
            try
            {
                SecurityIdentifier user = WindowsIdentity.GetCurrent().User;
                if (user == null) return;
                FileSecurity security = new FileSecurity();
                security.SetOwner(user);
                security.SetAccessRuleProtection(true, false);
                security.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.FullControl, AccessControlType.Allow));
                File.SetAccessControl(path, security);
            }
            catch
            {
                // FAT/exFAT and some portable locations do not support Windows ACLs.
            }
        }
    }
}
