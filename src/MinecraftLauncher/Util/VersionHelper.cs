using System;
using MinecraftLauncher.Models;

namespace MinecraftLauncher.Util
{
    /// <summary>版本相关共享逻辑（安装与启动共用）。</summary>
    public static class VersionHelper
    {
        public static bool LibraryApplies(Library lib)
        {
            if (lib.rules == null || lib.rules.Length == 0) return true;
            bool allow = false;
            foreach (var rule in lib.rules)
            {
                bool osMatch = rule.os == null || OsMatches(rule.os);
                if (osMatch)
                {
                    if (rule.action == "allow") allow = true;
                    else if (rule.action == "disallow") return false;
                }
            }
            return allow;
        }

        public static bool OsMatches(RuleOs os)
        {
            if (!string.IsNullOrEmpty(os.name) && os.name != "windows") return false;
            return true;
        }

        public static DownloadItem GetNatives(Library lib)
        {
            var c = lib.downloads != null ? lib.downloads.classifiers : null;
            if (c == null) return null;
            bool is64 = Environment.Is64BitOperatingSystem;
            DownloadItem item;
            if (is64 && c.TryGetValue("natives-windows-64", out item)) return item;
            if (is64 && c.TryGetValue("natives-windows", out item)) return item;
            if (!is64 && c.TryGetValue("natives-windows-32", out item)) return item;
            if (!is64 && c.TryGetValue("natives-windows", out item)) return item;
            return null;
        }

        /// <summary>
        /// 由 maven 坐标（group:artifact:version[:classifier]）推导库的相对路径。
        /// 用于 PCL 等启动器生成的、只有 name 没有 downloads.artifact.path 的库。
        /// </summary>
        public static string MavenPath(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string[] parts = name.Split(':');
            if (parts.Length < 3) return null;
            string group = parts[0].Replace('.', '/');
            string artifact = parts[1];
            string version = parts[2];
            string classifier = parts.Length > 3 ? parts[3] : null;
            string fileName = classifier == null
                ? artifact + "-" + version + ".jar"
                : artifact + "-" + version + "-" + classifier + ".jar";
            return group + "/" + artifact + "/" + version + "/" + fileName;
        }
    }
}