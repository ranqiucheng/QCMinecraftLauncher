using System;
using System.Security.Cryptography;
using System.Text;
using MinecraftLauncher.Models;

namespace MinecraftLauncher.Services
{
    /// <summary>登录服务：先实现离线登录，正版登录后续补充。</summary>
    public static class AuthService
    {
        public static Account CreateOfflineAccount(string name)
        {
            return new Account
            {
                Id = "offline:" + name,
                Name = name,
                Uuid = OfflineUuid(name),
                AccessToken = "0",
                Type = "offline"
            };
        }

        /// <summary>Minecraft 离线模式 UUID：MD5("OfflinePlayer:" + name) 并设置版本/变体位。</summary>
        public static string OfflineUuid(string name)
        {
            string input = "OfflinePlayer:" + name;
            byte[] hash;
            using (var md5 = MD5.Create())
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            hash[6] = (byte)((hash[6] & 0x0F) | 0x30);  // version 3
            hash[8] = (byte)((hash[8] & 0x3F) | 0x80);  // IETF variant
            string hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return hex.Substring(0, 8) + "-" + hex.Substring(8, 4) + "-" + hex.Substring(12, 4) + "-"
                 + hex.Substring(16, 4) + "-" + hex.Substring(20, 12);
        }
    }
}