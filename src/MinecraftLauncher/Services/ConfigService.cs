using System;
using System.IO;
using MinecraftLauncher.Util;
using MinecraftLauncher.Models;

namespace MinecraftLauncher.Services
{
    /// <summary>启动器配置。</summary>
    public class Config
    {
        public string GameDir { get; set; }
        public int MaxMemoryMb { get; set; }
        public string JavaPath { get; set; }
        public int DownloadSource { get; set; }     // 0=官方 1=BMCLAPI
        public string SelectedAccountId { get; set; }
        public bool ShowSnapshot { get; set; }
        public string PlayerName { get; set; }
        public Account Account { get; set; }
        public bool VersionIsolation { get; set; }  // 版本隔离：每个版本独立的 mods/存档等目录
        public bool CloseAfterLaunch { get; set; }   // 启动游戏后是否关闭启动器
        public bool DarkTheme { get; set; } = true;  // 深色主题
        public int ThemeColor { get; set; } = 1;     // 主题色：0蓝 1紫 2绿 3红 4橙 5青 6粉
        public string MsClientId { get; set; } = "";   // Microsoft OAuth Client ID（自行注册）
        public string YggdrasilServer { get; set; } = "https://littleskin.cn/api/yggdrasil"; // 第三方认证服务器
        public bool AutoJava { get; set; } = true;   // 自动选择 Java
        public bool AutoMemory { get; set; } = true; // 自动管理内存

        public Config()
        {
            MaxMemoryMb = 4096;
            DownloadSource = 1;
            ShowSnapshot = false;
            PlayerName = "Player";
            VersionIsolation = true;
            CloseAfterLaunch = false;
        }
    }

    /// <summary>配置的加载与保存。</summary>
    public static class ConfigService
    {
        private static readonly string ConfigPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher.json");

        public static Config Current { get; private set; }

        public static string DefaultGameDir => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");

        static ConfigService()
        {
            Current = Load();
        }

        private static Config Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var c = Json.Deserialize<Config>(File.ReadAllText(ConfigPath));
                    if (c != null) return c;
                }
            }
            catch { /* 配置损坏时回退到默认值 */ }
            var cfg = new Config { GameDir = DefaultGameDir, Account = AuthService.CreateOfflineAccount("Player") };
            return cfg;
        }

        /// <summary>获取某版本实际的"游戏目录"：版本隔离时指向 versions/版本名。</summary>
        public static string GetVersionGameDir(string versionId)
        {
            string gamedir = Current.GameDir;
            if (Current.VersionIsolation && !string.IsNullOrEmpty(versionId))
                return Path.Combine(gamedir, "versions", versionId);
            return gamedir;
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(ConfigPath, Json.Serialize(Current));
            }
            catch { }
        }
    }
}