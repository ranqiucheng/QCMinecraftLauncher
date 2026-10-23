using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using MinecraftLauncher.Models;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Services
{
    /// <summary>
    /// Microsoft 正版登录（旧 Client ID + 本地服务器授权码回调）。
    /// 用浏览器登录后自动回调到 http://localhost:端口，无需手动复制 code。
    /// </summary>
    public static class MicrosoftAuthService
    {
        private const string ClientId = "00000000402b5328";
        private const string Scope = "XboxLive.signin offline_access";

        public const int CallbackPort = 38291;
        private static string RedirectUri => "http://localhost:" + CallbackPort + "/";

        public static string BuildAuthUrl()
        {
            return "https://login.live.com/oauth20_authorize.srf"
                + "?client_id=" + Uri.EscapeDataString(ClientId)
                + "&response_type=code"
                + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri)
                + "&scope=" + Uri.EscapeDataString(Scope);
        }

        /// <summary>启动本地服务器并等待浏览器回调，返回授权码。</summary>
        public static string WaitForCode(int timeoutMs)
        {
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add(RedirectUri);
                listener.Start();
                try
                {
                    var task = listener.GetContextAsync();
                    if (!task.Wait(timeoutMs)) return null;
                    var ctx = task.Result;
                    string code = ctx.Request.QueryString["code"];
                    string err = ctx.Request.QueryString["error"];
                    try
                    {
                        byte[] buf = Encoding.UTF8.GetBytes(
                            err != null ? "授权失败：" + Uri.UnescapeDataString(err) : "登录成功！可以关闭此页面，回到启动器。");
                        ctx.Response.ContentType = "text/html; charset=utf-8";
                        ctx.Response.ContentLength64 = buf.Length;
                        ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                        ctx.Response.Close();
                    }
                    catch { }
                    return code;
                }
                finally
                {
                    try { listener.Stop(); } catch { }
                }
            }
        }

        public static string ExchangeCodeForToken(string code)
        {
            string body = "client_id=" + Uri.EscapeDataString(ClientId)
                + "&code=" + Uri.EscapeDataString(code.Trim())
                + "&grant_type=authorization_code"
                + "&redirect_uri=" + Uri.EscapeDataString(RedirectUri);
            string resp = PostForm("https://login.live.com/oauth20_token.srf", body);
            var t = Json.Deserialize<TokenResponse>(resp);
            if (t == null || string.IsNullOrEmpty(t.access_token))
                throw new Exception("授权码换 token 失败：" + resp);
            return t.access_token;
        }

        public static Account CompleteLogin(string accessToken, Action<string> log)
        {
            string xboxToken;
            AuthenticateXboxLive(accessToken, out xboxToken);
            log("Xbox Live 认证成功");

            string xstsToken, uhs;
            AuthorizeXsts(xboxToken, out xstsToken, out uhs);
            log("XSTS 认证成功");

            string mcToken = LoginMinecraft(xstsToken, uhs);
            log("Minecraft 认证成功");

            var acc = FetchProfile(mcToken);
            log("登录成功，欢迎 " + acc.Name);
            return acc;
        }

        private static void AuthenticateXboxLive(string accessToken, out string token)
        {
            var body = new
            {
                Properties = new { AuthMethod = "RWT", SiteName = "user.auth.xboxlive.com", RpsTicket = "d=" + accessToken },
                RelyingParty = "http://auth.xboxlive.com",
                TokenType = "JWT"
            };
            string resp = JsonPost("https://user.auth.xboxlive.com/user/authenticate", Json.Serialize(body));
            var r = Json.Deserialize<XboxAuthResponse>(resp);
            token = r.Token;
        }

        private static void AuthorizeXsts(string xboxToken, out string token, out string uhs)
        {
            var body = new
            {
                Properties = new { SandboxId = "RETAIL", UserTokens = new[] { xboxToken } },
                RelyingParty = "rp://api.minecraftservices.com/",
                TokenType = "JWT"
            };
            string resp = JsonPost("https://xsts.auth.xboxlive.com/xsts/authorize", Json.Serialize(body));
            var r = Json.Deserialize<XstsResponse>(resp);
            token = r.Token;
            uhs = r.DisplayClaims != null && r.DisplayClaims.xui != null && r.DisplayClaims.xui.Length > 0
                ? r.DisplayClaims.xui[0].uhs : "";
        }

        private static string LoginMinecraft(string xstsToken, string uhs)
        {
            var body = new { identityToken = "XBL3.0 x=" + uhs + ";" + xstsToken };
            string resp = JsonPost("https://api.minecraftservices.com/authentication/login_with_xbox", Json.Serialize(body));
            var r = Json.Deserialize<McTokenResponse>(resp);
            return r.access_token;
        }

        private static Account FetchProfile(string mcToken)
        {
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.Authorization] = "Bearer " + mcToken;
                string resp = wc.DownloadString("https://api.minecraftservices.com/minecraft/profile");
                var p = Json.Deserialize<ProfileResponse>(resp);
                return new Account
                {
                    Id = "msa:" + p.id,
                    Name = p.name,
                    Uuid = p.id,
                    AccessToken = mcToken,
                    Type = "microsoft"
                };
            }
        }

        private static string PostForm(string url, string body)
        {
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                return wc.UploadString(url, "POST", body);
            }
        }

        private static string JsonPost(string url, string json)
        {
            using (var wc = new WebClient())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                wc.Headers[HttpRequestHeader.Accept] = "application/json";
                return wc.UploadString(url, "POST", json);
            }
        }
    }

    // —— 响应模型 ——
    public class TokenResponse
    {
        public string token_type { get; set; }
        public string access_token { get; set; }
        public string refresh_token { get; set; }
        public int expires_in { get; set; }
    }

    public class XboxAuthResponse
    {
        public string Token { get; set; }
        public DisplayClaims DisplayClaims { get; set; }
    }

    public class XstsResponse
    {
        public string Token { get; set; }
        public DisplayClaims DisplayClaims { get; set; }
    }

    public class DisplayClaims
    {
        public Xui[] xui { get; set; }
    }

    public class Xui
    {
        public string uhs { get; set; }
        public string gtg { get; set; }
    }

    public class McTokenResponse
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
    }

    public class ProfileResponse
    {
        public string id { get; set; }
        public string name { get; set; }
    }
}