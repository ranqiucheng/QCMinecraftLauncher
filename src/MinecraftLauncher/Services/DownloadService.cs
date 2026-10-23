using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace MinecraftLauncher.Services
{
    internal class TimeoutWebClient : WebClient
    {
        private readonly int _timeout;
        public TimeoutWebClient(int timeoutMs) { _timeout = timeoutMs; }
        protected override WebRequest GetWebRequest(Uri address)
        {
            var req = base.GetWebRequest(address);
            if (req is HttpWebRequest)
            {
                ((HttpWebRequest)req).Timeout = _timeout;
                ((HttpWebRequest)req).ReadWriteTimeout = _timeout;
            }
            return req;
        }
    }

    public static class DownloadService
    {
        static DownloadService()
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192;
            }
            catch { }
        }

        public static string LastError { get; private set; }

        public static string Mirror(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (ConfigService.Current.DownloadSource != 1) return url;
            return url
                .Replace("https://launcher.mojang.com", "https://bmclapi2.bangbang93.com")
                .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com")
                .Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com")
                .Replace("https://piston-data.mojang.com", "https://bmclapi2.bangbang93.com")
                .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven")
                .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets");
        }

        public static string DownloadString(string url)
        {
            using (var wc = new TimeoutWebClient(15000))
            {
                wc.Encoding = Encoding.UTF8;
                wc.Headers[HttpRequestHeader.UserAgent] = "MinecraftLauncher/1.0";
                return wc.DownloadString(Mirror(url));
            }
        }

        public static bool DownloadFile(string url, string destPath, string expectedSha1 = null, Action<long, long> progress = null, int timeoutMs = 30000)
        {
            Exception lastEx = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    string dir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    string tmp = destPath + ".tmp";
                    using (var wc = new TimeoutWebClient(timeoutMs))
                    {
                        wc.Headers[HttpRequestHeader.UserAgent] = "MinecraftLauncher/1.0";
                        if (progress != null)
                            wc.DownloadProgressChanged += (s, e) => progress(e.TotalBytesToReceive, e.BytesReceived);
                        wc.DownloadFile(Mirror(url), tmp);
                    }

                if (expectedSha1 != null)
                {
                    string actual = Sha1(tmp);
                    if (!string.Equals(actual, expectedSha1, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(tmp); } catch { }
                        return false;
                    }
                }

                    if (File.Exists(destPath)) File.Delete(destPath);
                    File.Move(tmp, destPath);
                    return true;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    try { if (File.Exists(destPath + ".tmp")) File.Delete(destPath + ".tmp"); } catch { }
                    if (attempt < 3) System.Threading.Thread.Sleep(1500);
                }
            }
            LastError = lastEx != null ? lastEx.Message : "未知错误";
            return false;
        }

        public static string Sha1(string path)
        {
            using (var fs = File.OpenRead(path))
            using (var sha = SHA1.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}