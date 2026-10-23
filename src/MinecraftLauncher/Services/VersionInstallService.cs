using System;
using System.IO;
using MinecraftLauncher.Models;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Services
{
    /// <summary>把某个版本完整安装到游戏目录。</summary>
    public static class VersionInstallService
    {
        /// <summary>安装版本。log 输出阶段信息，fileProgress(done, total) 报告文件进度。阻塞式，应在后台线程调用。</summary>
        public static void Install(VersionEntry entry, Action<string> log, Action<int, int> fileProgress)
        {
            string gamedir = ConfigService.Current.GameDir;
            string versionDir = Path.Combine(gamedir, "versions", entry.id);
            Directory.CreateDirectory(versionDir);

            log("下载版本信息 " + entry.id + " ...");
            string jsonPath = Path.Combine(versionDir, entry.id + ".json");
            string versionJson = DownloadService.DownloadString(entry.url);
            File.WriteAllText(jsonPath, versionJson);
            var info = Json.Deserialize<VersionInfo>(versionJson);
            if (info == null) throw new Exception("版本信息解析失败");

            string clientUrl = info.downloads != null && info.downloads.client != null ? info.downloads.client.url : null;
            if (!string.IsNullOrEmpty(clientUrl))
            {
                log("下载客户端 jar ...");
                string clientPath = Path.Combine(versionDir, entry.id + ".jar");
                if (!File.Exists(clientPath))
                    DownloadService.DownloadFile(clientUrl, clientPath, info.downloads.client.sha1);
            }
            else
            {
                log("提示：该版本客户端 jar 需特殊处理，可能为远古版本");
            }

            int libCount = 0;
            if (info.libraries != null)
            {
                foreach (var lib in info.libraries)
                {
                    if (lib == null) continue;
                    if (!VersionHelper.LibraryApplies(lib)) continue;

                    string libRelPath = null;
                    var artifact = lib.downloads != null ? lib.downloads.artifact : null;
                    if (artifact != null && !string.IsNullOrEmpty(artifact.path))
                        libRelPath = artifact.path;
                    else if (!string.IsNullOrEmpty(lib.name))
                        libRelPath = VersionHelper.MavenPath(lib.name);
                    if (!string.IsNullOrEmpty(libRelPath))
                    {
                        string libPath = Path.Combine(gamedir, "libraries", libRelPath);
                        if (!File.Exists(libPath))
                        {
                            string url = artifact != null && !string.IsNullOrEmpty(artifact.url) ? artifact.url : null;
                            if (string.IsNullOrEmpty(url))
                                url = "https://libraries.minecraft.net/" + libRelPath;
                            DownloadService.DownloadFile(url, libPath, artifact != null ? artifact.sha1 : null);
                        }
                        libCount++;
                    }

                    var native = VersionHelper.GetNatives(lib);
                    if (native != null && !string.IsNullOrEmpty(native.path))
                    {
                        string natPath = Path.Combine(gamedir, "libraries", native.path);
                        if (!File.Exists(natPath))
                        {
                            string url = native.url;
                            if (string.IsNullOrEmpty(url))
                                url = "https://libraries.minecraft.net/" + native.path;
                            DownloadService.DownloadFile(url, natPath, native.sha1);
                        }
                        libCount++;
                    }
                }
            }
            log("依赖库处理完成（" + libCount + " 项）");

            if (info.assetIndex == null)
            {
                log("该版本无资源索引（远古版本），跳过资源下载");
                return;
            }

            log("下载资源索引 ...");
            string idxDir = Path.Combine(gamedir, "assets", "indexes");
            Directory.CreateDirectory(idxDir);
            string idxPath = Path.Combine(idxDir, info.assetIndex.id + ".json");
            if (!File.Exists(idxPath))
            {
                string idxUrl = info.assetIndex.url;
                if (string.IsNullOrEmpty(idxUrl))
                    idxUrl = "https://piston-meta.mojang.com/v1/packages/" + info.assetIndex.id + ".json";
                DownloadService.DownloadFile(idxUrl, idxPath, info.assetIndex.sha1);
            }

            log("下载资源文件 ...");
            var assetIndex = Json.Deserialize<AssetIndexObjects>(File.ReadAllText(idxPath));
            int total = assetIndex != null && assetIndex.objects != null ? assetIndex.objects.Count : 0;
            int done = 0;
            if (assetIndex != null && assetIndex.objects != null)
            {
                foreach (var kv in assetIndex.objects)
                {
                    string hash = kv.Value.hash;
                    if (!string.IsNullOrEmpty(hash) && hash.Length >= 2)
                    {
                        string sub = hash.Substring(0, 2);
                        string objPath = Path.Combine(gamedir, "assets", "objects", sub, hash);
                        if (!File.Exists(objPath))
                        {
                            string url = "https://resources.download.minecraft.net/" + sub + "/" + hash;
                            DownloadService.DownloadFile(url, objPath, hash);
                        }
                    }
                    done++;
                    fileProgress(done, total);
                }
            }
            log("资源文件完成（" + total + " 项）");
        }
    }
}
