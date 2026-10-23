using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Services
{
    /// <summary>Modrinth 公开 API 封装（整合包/Mod/光影）。</summary>
    public static class ModrinthService
    {
        private const string BaseUrl = "https://api.modrinth.com/v2";

        /// <summary>搜索项目。kind: modpack / mod / shader / resourcepack</summary>
        public static List<ModrinthProject> Search(string query, string kind, string gameVersion = null, string category = null, string sortIndex = "relevance", int limit = 20)
        {
            var facets = new List<string>();
            facets.Add("[\"project_type:" + kind + "\"]");
            if (!string.IsNullOrEmpty(gameVersion) && gameVersion != "全部")
                facets.Add("[\"versions:" + gameVersion + "\"]");
            if (!string.IsNullOrEmpty(category) && category != "全部")
                facets.Add("[\"categories:" + category + "\"]");
            string facetsJson = "[" + string.Join(",", facets) + "]";
            string url = BaseUrl + "/search?query=" + Uri.EscapeDataString(query ?? "") + "&facets=" + Uri.EscapeDataString(facetsJson) + "&index=" + sortIndex + "&limit=" + limit;
            string json = Get(url);
            var result = Json.Deserialize<ModrinthSearchResult>(json);
            return result != null && result.hits != null ? result.hits : new List<ModrinthProject>();
        }

        /// <summary>获取项目版本列表。</summary>
        public static List<ModrinthVersion> GetVersions(string projectId)
        {
            string url = BaseUrl + "/project/" + Uri.EscapeDataString(projectId) + "/version";
            string json = Get(url);
            var result = Json.Deserialize<List<ModrinthVersion>>(json);
            return result ?? new List<ModrinthVersion>();
        }

        /// <summary>选择适合指定游戏版本的版本（找不到就取最新）。</summary>
        public static ModrinthVersion PickVersion(List<ModrinthVersion> versions, string gameVersion)
        {
            if (versions == null || versions.Count == 0) return null;
            if (!string.IsNullOrEmpty(gameVersion))
            {
                foreach (var v in versions)
                {
                    if (v.game_versions != null && Array.IndexOf(v.game_versions, gameVersion) >= 0)
                        return v;
                }
            }
            return versions[0];
        }

        public static ModrinthFile PickFile(ModrinthVersion version)
        {
            if (version == null || version.files == null || version.files.Count == 0) return null;
            foreach (var f in version.files)
            {
                if (f.primary) return f;
            }
            return version.files[0];
        }

        private static string Get(string url)
        {
            using (var wc = new WebClient())
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.UserAgent] = "MinecraftLauncher/1.0";
                return wc.DownloadString(url);
            }
        }
    }

    public class ModrinthSearchResult
    {
        public List<ModrinthProject> hits { get; set; }
    }

    public class ModrinthProject
    {
        public string project_id { get; set; }
        public string slug { get; set; }
        public string title { get; set; }
        public string description { get; set; }
        public string project_type { get; set; }
        public long downloads { get; set; }
        public string icon_url { get; set; }
        public string[] categories { get; set; }
        public string[] loaders { get; set; }
        public string[] game_versions { get; set; }
        public string date_modified { get; set; }
    }

    public class ModrinthVersion
    {
        public string id { get; set; }
        public string name { get; set; }
        public string version_number { get; set; }
        public string[] game_versions { get; set; }
        public string[] loaders { get; set; }
        public List<ModrinthFile> files { get; set; }
    }

    public class ModrinthFile
    {
        public string url { get; set; }
        public string filename { get; set; }
        public bool primary { get; set; }
    }
}
