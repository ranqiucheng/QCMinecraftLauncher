using System;
using System.ComponentModel;

namespace MinecraftLauncher.Services
{
    /// <summary>全局下载中心：任何页面都能看到当前下载进度。</summary>
    public static class DownloadCenter
    {
        public static event PropertyChangedEventHandler Changed;

        private static string _name = "";
        private static string _status = "";
        private static int _done;
        private static int _total;
        private static bool _active;

        public static bool IsActive { get { return _active; } }
        public static string Name { get { return _name; } }
        public static string Status { get { return _status; } }
        public static int Done { get { return _done; } }
        public static int Total { get { return _total; } }
        public static double Percent { get { return _total > 0 ? Math.Min(100.0, _done * 100.0 / _total) : 0; } }

        public static void Start(string name, int total)
        {
            _name = name; _total = total; _done = 0; _status = "准备中..."; _active = true;
            Raise("IsActive");
        }

        public static void Update(int done, int total, string status)
        {
            _done = done; _total = total;
            if (!string.IsNullOrEmpty(status)) _status = status;
            Raise("Percent"); Raise("Done"); Raise("Status");
        }

        public static void SetStatus(string status)
        {
            _status = status;
            Raise("Status");
        }

        public static void Cancel()
        {
            _status = "已取消"; _active = false; _done = 0; _total = 0;
            Raise("IsActive");
        }

        public static void Finish(string message)
        {
            _status = message; _active = false;
            Raise("IsActive");
        }

        private static void Raise(string prop)
        {
            if (Changed != null) Changed(null, new PropertyChangedEventArgs(prop));
        }
    }
}