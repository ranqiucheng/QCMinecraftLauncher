using System;
using System.Collections.Generic;
using System.IO;
using MinecraftLauncher.Models;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Services
{
    /// <summary>负责版本清单与版本信息的获取。</summary>
    public static class VersionService
    {
        public const string OfficialManifest = "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json";
        public const string BmclManifest = "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json";

        public static string ManifestUrl =>
            ConfigService.Current.DownloadSource == 1 ? BmclManifest : OfficialManifest;

        public static VersionManifest FetchManifest()
        {
            string json = DownloadService.DownloadString(ManifestUrl);
            return Json.Deserialize<VersionManifest>(json);
        }

        public static VersionInfo FetchVersionInfo(VersionEntry entry)
        {
            string json = DownloadService.DownloadString(entry.url);
            return Json.Deserialize<VersionInfo>(json);
        }

        /// <summary>获取本地已安装的版本 id 列表。</summary>
        public static List<string> GetInstalledVersions()
        {
            var list = new List<string>();
            string vdir = Path.Combine(ConfigService.Current.GameDir, "versions");
            if (Directory.Exists(vdir))
            {
                foreach (var d in Directory.GetDirectories(vdir))
                {
                    string name = Path.GetFileName(d);
                    if (File.Exists(Path.Combine(d, name + ".json")))
                        list.Add(name);
                }
            }
            return list;
        }
    }
}