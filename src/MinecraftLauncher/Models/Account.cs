namespace MinecraftLauncher.Models
{
    /// <summary>登录账号。</summary>
    public class Account
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Uuid { get; set; }
        public string AccessToken { get; set; }
        public string Type { get; set; }   // offline / microsoft

        public bool IsOffline => Type == "offline";
    }
}