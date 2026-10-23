using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using MinecraftLauncher.Models;

namespace MinecraftLauncher.Services
{
    /// <summary>Java 运行时检测。</summary>
    public static class JavaService
    {
        public static string ResolveJava(JavaVersionInfo javaVersion, Action<string> log)
        {
            string cfg = ConfigService.Current.JavaPath;
            if (!string.IsNullOrEmpty(cfg) && File.Exists(cfg))
            {
                if (log != null) log("使用指定 Java: " + cfg);
                return cfg;
            }

            int required = javaVersion != null && javaVersion.majorVersion > 0 ? javaVersion.majorVersion : 8;
            var candidates = CollectCandidates();

            // 先收集所有有效 Java（去重 + 主版本）
            var javas = new List<JavaInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in candidates)
            {
                if (!File.Exists(c)) continue;
                string full;
                try { full = Path.GetFullPath(c); } catch { continue; }
                if (!seen.Add(full)) continue;
                int major = GetJavaMajor(full);
                if (major > 0) javas.Add(new JavaInfo { Path = full, Major = major });
            }
            if (javas.Count == 0)
                throw new Exception("未找到任何 Java 运行时，请在设置中指定 Java 路径");

            // 1) 精确匹配（同主版本）
            JavaInfo exact = javas.Find(j => j.Major == required);
            if (exact != null)
            {
                if (log != null) log("使用 Java " + exact.Major + ": " + exact.Path);
                return exact.Path;
            }

            // 2) Java 8 特殊处理：8 与 8 兼容（1.8.x），其他版本不兼容
            if (required <= 8)
            {
                JavaInfo j8 = javas.Find(j => j.Major == 8);
                if (j8 != null) { if (log != null) log("使用 Java 8: " + j8.Path); return j8.Path; }
                // 没有 Java 8，只能用高版本（可能不兼容）
                javas.Sort((a, b) => a.Major.CompareTo(b.Major));
                if (log != null) log("警告：该版本需要 Java 8，但只找到 Java " + javas[0].Major + "，可能不兼容");
                return javas[0].Path;
            }

            // 3) 高版本兼容（>= required 里选最接近的）
            javas.Sort((a, b) => a.Major.CompareTo(b.Major));
            JavaInfo compatible = javas.Find(j => j.Major >= required);
            if (compatible != null)
            {
                if (log != null) log("使用 Java " + compatible.Major + ": " + compatible.Path);
                return compatible.Path;
            }

            // 4) 兜底：最高版本
            JavaInfo highest = javas[javas.Count - 1];
            if (log != null) log("警告：未找到满足 Java " + required + "+ 的运行时，使用 Java " + highest.Major);
            return highest.Path;
        }

        /// <summary>自动检测系统中所有可用的 Java 运行时。</summary>
        public static List<JavaInfo> DetectAll()
        {
            var result = new List<JavaInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in CollectCandidates())
            {
                if (!File.Exists(c)) continue;
                string full;
                try { full = Path.GetFullPath(c); } catch { continue; }
                if (!seen.Add(full)) continue;
                int major = GetJavaMajor(full);
                if (major <= 0) continue;
                result.Add(new JavaInfo { Path = full, Major = major });
            }
            result.Sort((a, b) => b.Major.CompareTo(a.Major));
            return result;
        }

        private static List<string> CollectCandidates()
        {
            var list = new List<string>();
            string jh = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(jh)) list.Add(Path.Combine(jh, "bin", "java.exe"));

            string pathVar = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathVar))
            {
                foreach (var dir in pathVar.Split(';'))
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    list.Add(Path.Combine(dir.Trim(), "java.exe"));
                }
            }

            // 常见安装目录
            foreach (var root in new[]
            {
                @"C:\Program Files\Java",
                @"C:\Program Files (x86)\Java",
                @"C:\Program Files\Eclipse Adoptium",
                @"C:\Program Files\Microsoft",
                @"C:\Program Files\Zulu",
                @"C:\Program Files\Amazon Corretto",
                @"C:\Program Files\BellSoft",
                @"C:\Program Files\Semeru",
                @"C:\Program Files\GraalVM",
                @"C:\Program Files\RedHat",
                @"C:\Program Files\SapMachine"
            })
            {
                if (Directory.Exists(root))
                    foreach (var d in Directory.GetDirectories(root))
                        list.Add(Path.Combine(d, "bin", "java.exe"));
            }

            // 所有盘符下的常见 Java 目录（如 A:\java、D:\java）
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable) continue;
                    foreach (var name in new[] { "java", "Java", "jdk", "JDK", "jre", "JRE", "jdk8", "jdk17", "jdk21" })
                    {
                        string root = Path.Combine(drive.Name, name);
                        if (!Directory.Exists(root)) continue;
                        // 直接 bin/java.exe
                        list.Add(Path.Combine(root, "bin", "java.exe"));
                        // 子目录（如 A:\java\jdk-21\bin\java.exe）
                        try
                        {
                            foreach (var d in Directory.GetDirectories(root))
                                list.Add(Path.Combine(d, "bin", "java.exe"));
                        }
                        catch { }
                    }
                    // Programs 目录（用户级安装）
                    string localPrograms = Path.Combine(drive.Name, "Programs");
                    if (Directory.Exists(localPrograms))
                    {
                        try
                        {
                            foreach (var d in Directory.GetDirectories(localPrograms))
                                list.Add(Path.Combine(d, "bin", "java.exe"));
                        }
                        catch { }
                    }
                }
                catch { }
            }

            // 游戏目录的 runtime（MC 自带 Java，如 .minecraft\runtime）
            try
            {
                string gamedir = ConfigService.Current.GameDir;
                if (!string.IsNullOrEmpty(gamedir))
                {
                    string runtime = Path.Combine(gamedir, "runtime");
                    if (Directory.Exists(runtime))
                    {
                        foreach (var sub in Directory.GetDirectories(runtime))
                        {
                            string exe = FindJavaExe(sub);
                            if (exe != null) list.Add(exe);
                        }
                    }
                }
            }
            catch { }
            return list;
        }

        private static string FindJavaExe(string root)
        {
            try
            {
                string direct = Path.Combine(root, "bin", "java.exe");
                if (File.Exists(direct)) return direct;
                foreach (var d in Directory.GetDirectories(root))
                {
                    string e = Path.Combine(d, "bin", "java.exe");
                    if (File.Exists(e)) return e;
                }
            }
            catch { }
            return null;
        }

        private static int GetJavaMajor(string javaExe)
        {
            try
            {
                var psi = new ProcessStartInfo(javaExe, "-version")
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                var p = Process.Start(psi);
                if (p == null) return 0;
                string output = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                var m = Regex.Match(output, "\"(\\d+)(?:\\.(\\d+))?");
                if (m.Success)
                {
                    int major = int.Parse(m.Groups[1].Value);
                    int minor = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                    return major == 1 ? minor : major;
                }
            }
            catch { }
            return 0;
        }
    }

    public class JavaInfo
    {
        public string Path { get; set; }
        public int Major { get; set; }
        public string Display { get { return "Java " + Major + "  (" + Path + ")"; } }
    }
}