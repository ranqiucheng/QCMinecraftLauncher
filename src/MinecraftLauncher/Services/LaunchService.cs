using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using MinecraftLauncher.Models;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Services
{
    /// <summary>负责拼接启动参数并拉起 java 进程。</summary>
    public static class LaunchService
    {
        public static Process Launch(VersionEntry entry, Account account, Action<string> log)
        {
            string gamedir = ConfigService.Current.GameDir;
            string versionDir = Path.Combine(gamedir, "versions", entry.id);
            string jsonPath = Path.Combine(versionDir, entry.id + ".json");
            if (!File.Exists(jsonPath))
                throw new Exception("版本 " + entry.id + " 尚未安装，请先安装");

            var info = VersionResolver.Resolve(entry.id);
            if (info == null) throw new Exception("版本信息解析失败");

            // natives 目录
            string nativesDir = Path.Combine(versionDir, "natives");
            if (Directory.Exists(nativesDir)) { try { Directory.Delete(nativesDir, true); } catch { } }
            Directory.CreateDirectory(nativesDir);

            // classpath 与 natives 解压
            var classpath = new List<string>();
            var modulePath = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (info.libraries != null)
            {
                foreach (var lib in info.libraries)
                {
                    if (lib == null || !VersionHelper.LibraryApplies(lib)) continue;

                    string artifactPath = null;
                    var artifact = lib.downloads != null ? lib.downloads.artifact : null;
                    if (artifact != null && !string.IsNullOrEmpty(artifact.path))
                        artifactPath = artifact.path;
                    else if (!string.IsNullOrEmpty(lib.name))
                        artifactPath = VersionHelper.MavenPath(lib.name);

                    if (!string.IsNullOrEmpty(artifactPath))
                    {
                        string p = Path.Combine(gamedir, "libraries", artifactPath);
                        if (!seenPaths.Add(Path.GetFullPath(p))) continue;
                        if (File.Exists(p))
                        {
                            // 全部进 classpath（module path 完全交给版本 json 的 -p）
                            classpath.Add(p);
                        }
                    }

                    var native = VersionHelper.GetNatives(lib);
                    if (native != null && !string.IsNullOrEmpty(native.path))
                    {
                        string np = Path.Combine(gamedir, "libraries", native.path);
                        if (File.Exists(np)) ExtractNatives(np, nativesDir);
                    }
                }
            }

            string jarId = !string.IsNullOrEmpty(info.jar) ? info.jar : entry.id;
            string clientJar = Path.Combine(gamedir, "versions", jarId, jarId + ".jar");
            if (File.Exists(clientJar) && seenPaths.Add(Path.GetFullPath(clientJar))) classpath.Add(clientJar);
            string classpathStr = string.Join(";", classpath);

            string assetsRoot = Path.Combine(gamedir, "assets");
            var vars = new Dictionary<string, string>
            {
                {"natives_directory", nativesDir},
                {"classpath", classpathStr},
                {"library_directory", Path.Combine(gamedir, "libraries")},
                {"classpath_separator", ";"},
                {"launcher_name", "MinecraftLauncher"},
                {"launcher_version", "1.0"},
                {"version_name", info.id ?? entry.id},
                {"game_directory", ConfigService.GetVersionGameDir(info.id ?? entry.id)},
                {"assets_root", assetsRoot},
                {"assets_index_name", info.assetIndex != null ? info.assetIndex.id : (info.assets ?? "")},
                {"auth_player_name", account != null ? account.Name : "Player"},
                {"auth_uuid", account != null ? account.Uuid : ""},
                {"auth_access_token", account != null ? account.AccessToken : "0"},
                {"clientid", ""},
                {"auth_xuid", ""},
                {"user_type", "legacy"},
                {"version_type", info.type ?? "release"},
                {"user_properties", "{}"},
                {"game_assets", assetsRoot},
                {"resolution_width", "854"},
                {"resolution_height", "480"},
                {"auth_session", account != null ? (account.AccessToken + ":" + account.Uuid + ":" + account.Name) : "0:0:0"}
            };

            List<string> jvmArgs;
            if (info.arguments != null && info.arguments.jvm != null)
                jvmArgs = ParseArgs(info.arguments.jvm);
            else
                jvmArgs = new List<string> { "-Djava.library.path=${natives_directory}", "-cp", "${classpath}" };

            // launchwrapper（旧 Forge/OptiFine）在 Java 17+ 需要 --add-opens
            bool isLaunchWrapper = info.mainClass != null && (info.mainClass.Contains("launchwrapper") || info.mainClass.Contains("bootstraplauncher") || info.mainClass.Contains("cpw.mods") || info.mainClass.Contains("forge"));
            if (isLaunchWrapper)
            {
                string[] opens = {
                    "--add-opens", "java.base/java.lang.invoke=ALL-UNNAMED",
                    "--add-opens", "java.base/java.lang.reflect=ALL-UNNAMED",
                    "--add-opens", "java.base/java.lang=ALL-UNNAMED",
                    "--add-opens", "java.base/java.util=ALL-UNNAMED",
                    "--add-opens", "java.base/java.util.jar=ALL-UNNAMED",
                    "--add-opens", "java.base/java.nio=ALL-UNNAMED",
                    "--add-opens", "java.base/sun.nio.ch=ALL-UNNAMED",
                    "--add-opens", "java.base/java.net=ALL-UNNAMED",
                    "--add-opens", "java.base/java.io=ALL-UNNAMED"
                };
                for (int i = 0; i < opens.Length; i += 2)
                {
                    jvmArgs.Add(opens[i]);
                    jvmArgs.Add(opens[i + 1]);
                }
                // Forge 1.17+ 的 module path（json 里没有 -p 时才手动加）
                bool jsonHasModulePath = jvmArgs.Any(a => a == "-p" || a == "--module-path");
                if (!jsonHasModulePath && modulePath.Count > 0)
                {
                    jvmArgs.Insert(0, "--add-modules");
                    jvmArgs.Insert(1, "ALL-MODULE-PATH");
                    jvmArgs.Insert(0, "--module-path");
                    jvmArgs.Insert(1, string.Join(";", modulePath));
                    log("已添加 --module-path（" + modulePath.Count + " 个 Forge 模块）");
                }
                // Forge 兼容参数（PCL 同款）
                jvmArgs.Add("-Dfml.ignoreInvalidMinecraftCertificates=True");
                jvmArgs.Add("-Dfml.ignorePatchDiscrepancies=True");
                jvmArgs.Add("-Djdk.lang.Process.allowAmbiguousCommands=True");
                jvmArgs.Add("-XX:-OmitStackTraceInFastThrow");
                log("已为 launchwrapper 添加 --add-opens 参数");
            }

            jvmArgs = ApplyMemory(jvmArgs);

            // 第三方认证（authlib-injector）
            if (account != null && account.Type == "yggdrasil")
            {
                string inj = YggdrasilService.EnsureAuthlibInjector(gamedir);
                if (!string.IsNullOrEmpty(inj))
                {
                    jvmArgs.Insert(0, "-javaagent:" + inj + "=" + YggdrasilService.NormalizeServer(ConfigService.Current.YggdrasilServer));
                }
                else
                {
                    log("警告：未能下载 authlib-injector.jar，第三方登录可能失败");
                }
            }

            List<string> gameArgs;
            if (info.arguments != null && info.arguments.game != null)
                gameArgs = ParseArgs(info.arguments.game);
            else if (!string.IsNullOrEmpty(info.minecraftArguments))
                gameArgs = info.minecraftArguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            else
                gameArgs = new List<string>();

            jvmArgs = jvmArgs.Select(a => ReplaceVars(a, vars)).ToList();
            gameArgs = gameArgs.Select(a => ReplaceVars(a, vars)).ToList();

            string mainClass = info.mainClass;
            if (string.IsNullOrEmpty(mainClass)) mainClass = "net.minecraft.client.main.Main";

            var all = new List<string>();
            all.AddRange(jvmArgs);
            all.Add(mainClass);
            all.AddRange(gameArgs);

            string javaPath = JavaService.ResolveJava(info.javaVersion, log);
            log("使用 Java: " + javaPath);

            string fullArgs = string.Join(" ", all.Select(QuoteArg));
            try { File.WriteAllText(Path.Combine(gamedir, "launch-cmd.txt"), fullArgs); } catch { }
            string logArgs = fullArgs.Length > 2500 ? fullArgs.Substring(0, 2500) + " ..." : fullArgs;
            log("执行: " + javaPath + " " + logArgs);

            var psi = new ProcessStartInfo
            {
                FileName = javaPath,
                Arguments = fullArgs,
                WorkingDirectory = gamedir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log(e.Data); };
            proc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) log(e.Data); };
            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            return proc;
        }

        private static string QuoteArg(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "\"\"";
            if (arg.IndexOf(' ') >= 0 || arg.IndexOf('\t') >= 0)
                return "\"" + arg + "\"";
            return arg;
        }

        private static bool HasModuleInfo(string jarPath)
        {
            try
            {
                using (var fs = File.OpenRead(jarPath))
                using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Read))
                {
                    foreach (var e in zip.Entries)
                    {
                        if (e.FullName == "module-info.class") return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static void ExtractNatives(string zipPath, string destDir)
        {
            try
            {
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in zip.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) continue;
                        if (entry.FullName.StartsWith("META-INF", StringComparison.OrdinalIgnoreCase)) continue;
                        string dest = Path.Combine(destDir, entry.FullName);
                        string d = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                        entry.ExtractToFile(dest, true);
                    }
                }
            }
            catch { }
        }

        private static List<string> ApplyMemory(List<string> args)
        {
            int max = ConfigService.Current.MaxMemoryMb;
            if (max <= 0) max = 4096;
            args = args.Where(a => !a.StartsWith("-Xmx") && !a.StartsWith("-Xms")).ToList();
            args.Insert(0, "-Xmx" + max + "M");
            args.Insert(0, "-Xms256M");
            return args;
        }

        private static string ReplaceVars(string s, Dictionary<string, string> vars)
        {
            if (string.IsNullOrEmpty(s)) return s;
            foreach (var kv in vars)
                s = s.Replace("${" + kv.Key + "}", kv.Value);
            return s;
        }

        private static List<string> ParseArgs(object[] args)
        {
            var result = new List<string>();
            if (args == null) return result;
            foreach (var a in args)
            {
                if (a is string s) { result.Add(s); continue; }
                var d = a as Dictionary<string, object>;
                if (d == null) continue;
                if (!RulesAllow(d)) continue;
                if (d.ContainsKey("value"))
                {
                    var v = d["value"];
                    if (v is string vs) result.Add(vs);
                    else if (v is IEnumerable ie)
                        foreach (var item in ie)
                            if (item != null) result.Add(item.ToString());
                }
            }
            return result;
        }

        private static bool RulesAllow(Dictionary<string, object> d)
        {
            if (!d.ContainsKey("rules")) return true;
            var rules = d["rules"] as object[];
            if (rules == null) return true;
            bool allow = false;
            foreach (var r in rules)
            {
                var rd = r as Dictionary<string, object>;
                if (rd == null) continue;
                string action = rd.ContainsKey("action") ? rd["action"] as string : null;
                if (RuleMatches(rd))
                {
                    if (action == "allow") allow = true;
                    else if (action == "disallow") return false;
                }
            }
            return allow;
        }

        private static bool RuleMatches(Dictionary<string, object> rule)
        {
            if (rule.ContainsKey("os"))
            {
                var os = rule["os"] as Dictionary<string, object>;
                if (os != null && os.ContainsKey("name"))
                {
                    string name = os["name"] as string;
                    if (!string.IsNullOrEmpty(name) && name != "windows") return false;
                }
            }
            // 不支持 demo / 自定义分辨率等特性，带 features 的规则一律不匹配
            if (rule.ContainsKey("features"))
                return false;
            return true;
        }
    }
}
