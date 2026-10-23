using System;
using System.Collections.Generic;
using System.IO;
using MinecraftLauncher.Models;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Services
{
    /// <summary>解析版本 JSON，处理 inheritsFrom 继承合并（Forge/Fabric/整合包版本）。</summary>
    public static class VersionResolver
    {
        public static VersionInfo Resolve(string versionId)
        {
            return ResolveRecursive(versionId, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        private static VersionInfo ResolveRecursive(string id, HashSet<string> visited)
        {
            if (!visited.Add(id))
                throw new Exception("版本继承出现循环：" + id);

            string gamedir = ConfigService.Current.GameDir;
            string jsonPath = Path.Combine(gamedir, "versions", id, id + ".json");
            if (!File.Exists(jsonPath))
                throw new Exception("版本文件不存在：" + jsonPath);

            var info = Json.Deserialize<VersionInfo>(File.ReadAllText(jsonPath));
            if (info == null) throw new Exception("版本解析失败：" + id);

            if (string.IsNullOrEmpty(info.inheritsFrom))
                return info;

            var parent = ResolveRecursive(info.inheritsFrom, visited);
            return Merge(parent, info);
        }

        private static VersionInfo Merge(VersionInfo parent, VersionInfo child)
        {
            return new VersionInfo
            {
                id = child.id,
                type = !string.IsNullOrEmpty(child.type) ? child.type : parent.type,
                mainClass = !string.IsNullOrEmpty(child.mainClass) ? child.mainClass : parent.mainClass,
                assets = !string.IsNullOrEmpty(child.assets) ? child.assets : parent.assets,
                minecraftArguments = !string.IsNullOrEmpty(child.minecraftArguments) ? child.minecraftArguments : parent.minecraftArguments,
                inheritsFrom = child.inheritsFrom,
                jar = !string.IsNullOrEmpty(child.jar) ? child.jar : parent.jar,
                assetIndex = child.assetIndex != null ? child.assetIndex : parent.assetIndex,
                downloads = child.downloads != null ? child.downloads : parent.downloads,
                javaVersion = child.javaVersion != null ? child.javaVersion : parent.javaVersion,
                arguments = MergeArguments(parent.arguments, child.arguments),
                libraries = MergeLibraries(parent.libraries, child.libraries)
            };
        }

        private static ArgumentSet MergeArguments(ArgumentSet p, ArgumentSet c)
        {
            if (p == null) return c;
            if (c == null) return p;
            return new ArgumentSet
            {
                jvm = Concat(p.jvm, c.jvm),
                game = Concat(p.game, c.game)
            };
        }

        private static object[] Concat(object[] a, object[] b)
        {
            if (a == null) return b;
            if (b == null) return a;
            var list = new List<object>(a);
            list.AddRange(b);
            return list.ToArray();
        }

        private static Library[] MergeLibraries(Library[] p, Library[] c)
        {
            if (p == null) return c;
            if (c == null) return p;
            var list = new List<Library>();
            var seen = new HashSet<string>();
            foreach (var lib in p)
            {
                if (lib == null) continue;
                string key = LibKey(lib);
                if (seen.Add(key)) list.Add(lib);
            }
            foreach (var lib in c)
            {
                if (lib == null) continue;
                string key = LibKey(lib);
                if (seen.Add(key)) list.Add(lib);
            }
            return list.ToArray();
        }

        private static string LibKey(Library lib)
        {
            if (!string.IsNullOrEmpty(lib.name)) return lib.name;
            if (lib.downloads != null && lib.downloads.artifact != null && !string.IsNullOrEmpty(lib.downloads.artifact.path))
                return lib.downloads.artifact.path;
            return "";
        }
    }
}