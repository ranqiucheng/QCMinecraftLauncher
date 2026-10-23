using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using MinecraftLauncher.Models;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Services
{
    /// <summary>Fabric 版本安装（用 Fabric 官方 API 生成版本 JSON）。</summary>
    public static class FabricService
    {
        /// <summary>安装 Fabric 版本到 versions/<name>/。</summary>
        public static void Install(string gameVersion, string loaderVersion, string name, Action<string> log)
        {
            string gamedir = ConfigService.Current.GameDir;
            string versionDir = Path.Combine(gamedir, "versions", name);
            Directory.CreateDirectory(versionDir);

            string url = "https://meta.fabricmc.net/v2/versions/loader/"
                + Uri.EscapeDataString(gameVersion) + "/" + Uri.EscapeDataString(loaderVersion) + "/profile/json";
            string json = DownloadString(url);
            File.WriteAllText(Path.Combine(versionDir, name + ".json"), json);

            var info = Json.Deserialize<VersionInfo>(json);
            if (info != null && info.libraries != null)
            {
                foreach (var lib in info.libraries)
                {
                    if (lib == null) continue;
                    var artifact = lib.downloads != null ? lib.downloads.artifact : null;
                    string rel = null;
                    if (artifact != null && !string.IsNullOrEmpty(artifact.path)) rel = artifact.path;
                    else if (!string.IsNullOrEmpty(lib.name)) rel = VersionHelper.MavenPath(lib.name);
                    if (string.IsNullOrEmpty(rel)) continue;

                    string dest = Path.Combine(gamedir, "libraries", rel);
                    if (File.Exists(dest)) continue;
                    string libUrl = artifact != null && !string.IsNullOrEmpty(artifact.url) ? artifact.url : "https://maven.fabricmc.net/" + rel;
                    DownloadService.DownloadFile(libUrl, dest, artifact != null ? artifact.sha1 : null);
                }
            }
            log("Fabric " + gameVersion + " / " + loaderVersion + " 安装完成");
        }

        private static string DownloadString(string url)
        {
            using (var wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.UserAgent] = "MinecraftLauncher/1.0";
                return wc.DownloadString(url);
            }
        }
    }
}