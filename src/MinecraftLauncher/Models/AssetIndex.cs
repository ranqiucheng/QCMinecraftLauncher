using System.Collections.Generic;

namespace MinecraftLauncher.Models
{
    /// <summary>资源索引文件（assets/indexes/*.json）结构。</summary>
    public class AssetIndexObjects
    {
        public Dictionary<string, AssetObject> objects { get; set; }
    }

    public class AssetObject
    {
        public string hash { get; set; }
        public int size { get; set; }
    }
}