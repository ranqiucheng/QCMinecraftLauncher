using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Services
{
    /// <summary>整合包（Modrinth .mrpack）安装。</summary>
    public static class ModpackService
    {
        /// <summary>安装整合包。log 输出进度，返回安装后的版本 id。</summary>
        public static string Install(string projectTitle, string versionId, ModrinthFile file,
            string gameVersion, Action<string> log, Action<int, int> fileProgress, string iconUrl = null)
        {
            string gamedir = ConfigService.Current.GameDir;
            string safeName = SanitizeName(projectTitle);
            string packPath = Path.Combine(gamedir, "modpacks", safeName + ".mrpack");
            log("下载整合包 " + safeName + " ...");
            if (!DownloadService.DownloadFile(file.url, packPath, null, null, 180000))
                throw new Exception("整合包下载失败：" + (DownloadService.LastError ?? "未知错误") + " | URL: " + file.url);

            string tmpDir = Path.Combine(gamedir, "modpacks", "_tmp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmpDir);
            try
            {
                log("解压整合包...");
                ZipFile.ExtractToDirectory(packPath, tmpDir);

                string indexFile = Path.Combine(tmpDir, "modrinth.index.json");
                if (!File.Exists(indexFile)) throw new Exception("整合包缺少 modrinth.index.json");
                var index = Json.Deserialize<ModrinthIndex>(File.ReadAllText(indexFile));
                if (index == null) throw new Exception("整合包 index 解析失败");

                string versionIdName = safeName;
                string versionDir = Path.Combine(gamedir, "versions", versionIdName);
                Directory.CreateDirectory(versionDir);
                if (!string.IsNullOrEmpty(iconUrl))
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(versionDir, "modpack.json"),
                            "{\"title\":\"" + projectTitle.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"icon\":\"" + iconUrl + "\"}");
                    }
                    catch { }
                }

                // 下载 files
                int total = index.files != null ? index.files.Count : 0;
                int done = 0;
                if (index.files != null)
                {
                    foreach (var f in index.files)
                    {
                        if (f.downloads == null || f.downloads.Count == 0) continue;
                        string dest = Path.Combine(versionDir, f.path.Replace('/', Path.DirectorySeparatorChar));
                        DownloadService.DownloadFile(f.downloads[0], dest, f.hashes != null && f.hashes.ContainsKey("sha1") ? f.hashes["sha1"] : null, null, 120000);
                        done++;
                        if (done % 10 == 0 || done >= total) fileProgress(done, total);
                    }
                }
                log("整合包文件完成（" + total + " 项）");

                // 复制 overrides
                string overrides = Path.Combine(tmpDir, "overrides");
                if (Directory.Exists(overrides)) CopyDir(overrides, versionDir);

                // 处理依赖（Minecraft 版本 + loader）
                string mcVer = null, fabricVer = null, forgeVer = null;
                if (index.dependencies != null)
                {
                    index.dependencies.TryGetValue("minecraft", out mcVer);
                    index.dependencies.TryGetValue("fabric-loader", out fabricVer);
                    index.dependencies.TryGetValue("forge", out forgeVer);
                }
                if (string.IsNullOrEmpty(mcVer)) mcVer = gameVersion;

                if (!string.IsNullOrEmpty(fabricVer))
                {
                    log("安装 Fabric " + fabricVer + " ...");
                    try { FabricService.Install(mcVer, fabricVer, versionIdName, log); }
                    catch (Exception ex) { log("Fabric 安装失败：" + ex.Message); }
                }
                else if (!string.IsNullOrEmpty(forgeVer))
                {
                    log("该整合包需要 Forge " + forgeVer + "，目前需手动安装 Forge");
                }
                else
                {
                    log("该整合包为原版（无 loader），请确保已安装 " + mcVer);
                }

                return versionIdName;
            }
            finally
            {
                try { Directory.Delete(tmpDir, true); } catch { }
            }
        }

        private static void CopyDir(string src, string dest)
        {
            foreach (var dir in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(dir.Replace(src, dest));
            foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string target = file.Replace(src, dest);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
            }
        }

        private static string SanitizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "modpack";
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim();
        }
    }

    public class ModrinthIndex
    {
        public int formatVersion { get; set; }
        public string game { get; set; }
        public string versionId { get; set; }
        public string name { get; set; }
        public List<ModrinthIndexFile> files { get; set; }
        public Dictionary<string, string> dependencies { get; set; }
    }

    public class ModrinthIndexFile
    {
        public string path { get; set; }
        public List<string> downloads { get; set; }
        public Dictionary<string, string> hashes { get; set; }
    }
}