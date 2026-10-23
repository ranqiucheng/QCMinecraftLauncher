using System;
using System.Collections.Generic;

namespace MinecraftLauncher.Models
{
    // 与 Mojang 官方 JSON 字段名保持精确一致（JavaScriptSerializer 区分大小写）。

    /// <summary>version_manifest_v2.json 顶层结构。</summary>
    public class VersionManifest
    {
        public LatestInfo latest { get; set; }
        public VersionEntry[] versions { get; set; }
    }

    public class LatestInfo
    {
        public string release { get; set; }
        public string snapshot { get; set; }
    }

    /// <summary>版本清单中的单个版本条目。</summary>
    public class VersionEntry
    {
        public string id { get; set; }
        public string type { get; set; }
        public string url { get; set; }
        public string time { get; set; }
        public string releaseTime { get; set; }
    }

    /// <summary>单个版本详细 JSON（version.json）的核心结构。</summary>
    public class VersionInfo
    {
        public string id { get; set; }
        public string type { get; set; }
        public string mainClass { get; set; }
        public string assets { get; set; }
        public string minecraftArguments { get; set; }
        public string inheritsFrom { get; set; }
        public string jar { get; set; }
        public AssetIndex assetIndex { get; set; }
        public DownloadsInfo downloads { get; set; }
        public Library[] libraries { get; set; }
        public ArgumentSet arguments { get; set; }
        public JavaVersionInfo javaVersion { get; set; }
    }

    public class AssetIndex
    {
        public string id { get; set; }
        public string sha1 { get; set; }
        public int size { get; set; }
        public int totalSize { get; set; }
        public string url { get; set; }
    }

    public class DownloadsInfo
    {
        public DownloadItem client { get; set; }
        public DownloadItem client_mappings { get; set; }
        public DownloadItem server { get; set; }
        public DownloadItem windows_server { get; set; }
    }

    public class DownloadItem
    {
        public string path { get; set; }
        public string sha1 { get; set; }
        public int size { get; set; }
        public string url { get; set; }
    }

    public class Library
    {
        public string name { get; set; }
        public LibraryDownloads downloads { get; set; }
        public LibraryRule[] rules { get; set; }
        public string[] extract { get; set; }
    }

    public class LibraryDownloads
    {
        public DownloadItem artifact { get; set; }
        public Dictionary<string, DownloadItem> classifiers { get; set; }
    }

    public class LibraryRule
    {
        public string action { get; set; }
        public RuleOs os { get; set; }
    }

    public class RuleOs
    {
        public string name { get; set; }
        public string version { get; set; }
        public string arch { get; set; }
    }

    public class ArgumentSet
    {
        public object[] game { get; set; }
        public object[] jvm { get; set; }
    }

    public class JavaVersionInfo
    {
        public string component { get; set; }
        public int majorVersion { get; set; }
    }
}