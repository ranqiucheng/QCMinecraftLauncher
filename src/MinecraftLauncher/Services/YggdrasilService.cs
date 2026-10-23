using System;
using System.IO;
using System.Net;
using System.Text;
using MinecraftLauncher.Models;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Services
{
    /// <summary>
    /// 第三方认证（Yggdrasil API，如 LittleSkin）。
    /// </summary>
    public static class YggdrasilService
    {
        /// <summary>登录第三方认证服务器，返回账号。</summary>
        public static Account Login(string server, string username, string password)
        {
            string baseUrl = NormalizeServer(server);
            string url = baseUrl.TrimEnd('/') + "/authserver/authenticate";
            var body = new
            {
                username = username,
                password = password,
                agent = new { name = "Minecraft", version = 1 },
                requestUser = true
            };
            string resp = JsonPost(url, Json.Serialize(body));
            var r = Json.Deserialize<YggAuthResponse>(resp);
            if (r == null || string.IsNullOrEmpty(r.accessToken))
                throw new Exception("认证失败：" + resp);
            if (r.selectedProfile == null)
                throw new Exception("该账号没有可用的游戏角色（需先在皮肤站创建角色）");
            return new Account
            {
                Id = "ygg:" + r.selectedProfile.id,
                Name = r.selectedProfile.name,
                Uuid = r.selectedProfile.id,
                AccessToken = r.accessToken,
                Type = "yggdrasil"
            };
        }

        /// <summary>规范化服务器地址（补全为 authlib-injector 的 API 地址）。</summary>
        public static string NormalizeServer(string server)
        {
            if (string.IsNullOrWhiteSpace(server)) return "https://littleskin.cn/api/yggdrasil";
            string s = server.Trim();
            if (s.StartsWith("https://littleskin.cn", StringComparison.OrdinalIgnoreCase) && !s.Contains("/api/yggdrasil"))
                s = "https://littleskin.cn/api/yggdrasil";
            return s.TrimEnd('/');
        }

        /// <summary>确保 authlib-injector.jar 存在于游戏目录，返回路径。</summary>
        public static string EnsureAuthlibInjector(string gameDir)
        {
            string path = Path.Combine(gameDir, "authlib-injector.jar");
            if (File.Exists(path)) return path;
            string[] urls = {
                "https://bmclapi2.bangbang93.com/maven/org/yushijinhun/authlib-injector/1.2.7/authlib-injector-1.2.7.jar",
                "https://github.com/yushijinhun/authlib-injector/releases/download/v1.2.7/authlib-injector-1.2.7.jar"
            };
            foreach (var u in urls)
            {
                if (DownloadService.DownloadFile(u, path)) return path;
            }
            return null;
        }

        private static string JsonPost(string url, string json)
        {
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                wc.Headers[HttpRequestHeader.Accept] = "application/json";
                wc.Headers[HttpRequestHeader.UserAgent] = "MinecraftLauncher/1.0";
                return wc.UploadString(url, "POST", json);
            }
        }
    }

    public class YggAuthResponse
    {
        public string accessToken { get; set; }
        public string clientToken { get; set; }
        public YggProfile selectedProfile { get; set; }
        public YggProfile[] availableProfiles { get; set; }
    }

    public class YggProfile
    {
        public string id { get; set; }
        public string name { get; set; }
    }
}