using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MinecraftLauncher.Models;
using MinecraftLauncher.Services;
using MinecraftLauncher.Util;

namespace MinecraftLauncher.Views
{
    /// <summary>启动页：文件夹信息 + 已安装版本卡片列表，点击启动。</summary>
    public partial class HomePage : UserControl
    {
        private readonly ObservableCollection<InstalledVersionItem> _items = new ObservableCollection<InstalledVersionItem>();
        private Account _account;

        public HomePage()
        {
            InitializeComponent();
            VersionItems.ItemsSource = _items;
            _account = ConfigService.Current.Account;
            if (_account == null) _account = AuthService.CreateOfflineAccount("Player");
            UpdateAccountUi();
            UpdateFolderUi();
            RefreshVersions();
        }

        private void UpdateAccountUi()
        {
            AccountText.Text = _account.Name;
            AccountSubText.Text = _account.IsOffline ? "离线模式（点击设置用户名）" : "Microsoft 正版";
        }

        private void UpdateFolderUi()
        {
            string gamedir = ConfigService.Current.GameDir;
            try { FolderName.Text = new DirectoryInfo(gamedir).Name; }
            catch { FolderName.Text = gamedir; }
            FolderPath.Text = gamedir;
        }

        public void RefreshVersions()
        {
            _items.Clear();
            foreach (var id in VersionService.GetInstalledVersions())
            {
                string loader = DetectLoader(id);
                string iconUrl = ReadModpackIcon(id);
                _items.Add(new InstalledVersionItem { Id = id, Name = id, Info = BuildInfo(id), LoaderType = loader, IconUrl = iconUrl });
            }
            EmptyText.Visibility = _items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private string BuildInfo(string id)
        {
            string type = ReadVersionType(id);
            if (type == "release") return "正式版 " + id;
            if (type == "snapshot") return "预览版 " + id;
            return type + " " + id;
        }

        private string ReadVersionType(string id)
        {
            try
            {
                string p = Path.Combine(ConfigService.Current.GameDir, "versions", id, id + ".json");
                if (File.Exists(p))
                {
                    var info = Json.Deserialize<VersionInfo>(File.ReadAllText(p));
                    if (info != null && !string.IsNullOrEmpty(info.type)) return info.type;
                }
            }
            catch { }
            return "release";
        }

        private static string ReadModpackIcon(string id)
        {
            try
            {
                string vdir = Path.Combine(ConfigService.Current.GameDir, "versions", id);
                string p = Path.Combine(vdir, "modpack.json");
                if (File.Exists(p))
                {
                    var o = Json.Deserialize<ModpackMeta>(File.ReadAllText(p));
                    if (o != null && !string.IsNullOrEmpty(o.icon)) return o.icon;
                }
                // 已有整合包：从 modrinth.index.json 读名称，去 Modrinth 搜图标
                string indexPath = Path.Combine(vdir, "modrinth.index.json");
                if (File.Exists(indexPath))
                {
                    var idx = Json.Deserialize<ModrinthIndex>(File.ReadAllText(indexPath));
                    if (idx != null && !string.IsNullOrEmpty(idx.name))
                    {
                        var hits = ModrinthService.Search(idx.name, "modpack", null, null, "relevance", 5);
                        if (hits != null)
                        {
                            foreach (var h in hits)
                            {
                                if (h.title != null && h.title.Equals(idx.name, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(h.icon_url))
                                {
                                    try { File.WriteAllText(p, "{\"title\":\"" + h.title.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"icon\":\"" + h.icon_url + "\"}"); } catch { }
                                    return h.icon_url;
                                }
                            }
                            if (hits.Count > 0 && !string.IsNullOrEmpty(hits[0].icon_url) && hits[0].title != null)
                            {
                                try { File.WriteAllText(p, "{\"title\":\"" + hits[0].title.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"icon\":\"" + hits[0].icon_url + "\"}"); } catch { }
                                return hits[0].icon_url;
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private static string DetectLoader(string id)
        {
            try
            {
                string gamedir = ConfigService.Current.GameDir;
                string vdir = Path.Combine(gamedir, "versions", id);
                if (File.Exists(Path.Combine(vdir, "modrinth.index.json"))) return "modpack";
                string lp = id.ToLowerInvariant();
                if (lp.Contains("modpack") || lp.Contains("整合包")) return "modpack";
                if (lp.Contains("neoforge")) return "neoforge";
                if (lp.Contains("forge")) return "forge";
                if (lp.Contains("quilt")) return "quilt";
                if (lp.Contains("fabric")) return "fabric";
                string jsonPath = Path.Combine(vdir, id + ".json");
                if (File.Exists(jsonPath))
                {
                    string txt = File.ReadAllText(jsonPath).ToLowerInvariant();
                    if (txt.Contains("net.fabricmc")) return "fabric";
                    if (txt.Contains("neoforged")) return "neoforge";
                    if (txt.Contains("fml") || txt.Contains("forge")) return "forge";
                }
            }
            catch { }
            return "vanilla";
        }

        private void DeleteVersion_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn == null ? null : btn.Tag as InstalledVersionItem;
            if (item == null) return;
            var result = MessageBox.Show(
                "确定要删除版本「" + item.Name + "」吗？\n\n这会删除该版本的所有文件（including mods、存档等版本隔离目录），不可恢复！",
                "删除版本", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
            try
            {
                string vdir = Path.Combine(ConfigService.Current.GameDir, "versions", item.Id);
                if (Directory.Exists(vdir)) Directory.Delete(vdir, true);
                RefreshVersions();
            }
            catch (Exception ex)
            {
                MessageBox.Show("删除失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshVersions();
            UpdateFolderUi();
        }

        private void SwitchFolder_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择 Minecraft 游戏目录（.minecraft 文件夹）";
                dlg.ShowNewFolderButton = false;
                if (!string.IsNullOrEmpty(ConfigService.Current.GameDir))
                    dlg.SelectedPath = ConfigService.Current.GameDir;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ConfigService.Current.GameDir = dlg.SelectedPath;
                    ConfigService.Save();
                    UpdateFolderUi();
                    RefreshVersions();
                }
            }
        }

        private void LaunchItem_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var item = btn == null ? null : btn.Tag as InstalledVersionItem;
            if (item != null) LaunchVersion(item.Id);
        }

        private void LaunchVersion(string id)
        {
            AppendLog("=== 正在启动 " + id + " ===");
            var entry = new VersionEntry { id = id, type = "release" };
            Task.Run(() =>
            {
                try
                {
                    var proc = LaunchService.Launch(entry, _account, msg => Dispatcher.Invoke(() => AppendLog(msg)));
                    Dispatcher.Invoke(() => AppendLog("游戏进程已启动，PID=" + proc.Id));
                    // 监控进程：如果 8 秒内就退出且非 0，说明启动失败
                    var exited = proc.WaitForExit(8000);
                    if (exited)
                    {
                        int code = proc.ExitCode;
                        Dispatcher.Invoke(() =>
                        {
                            AppendLog("游戏进程已退出，退出码 " + code + "（请看上方日志）");
                            if (code != 0)
                                MessageBox.Show("游戏启动失败（退出码 " + code + "），请查看启动页日志。\n常见原因：Java 版本不匹配、libraries 缺失。",
                                    "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() => AppendLog("游戏运行中..."));
                    }
                    if (ConfigService.Current.CloseAfterLaunch)
                        Dispatcher.Invoke(() => Application.Current.Shutdown());
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        AppendLog("启动失败：" + ex.Message);
                        MessageBox.Show("启动失败：" + ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        private void AppendLog(string msg)
        {
            LogBox.AppendText(msg + Environment.NewLine);
            LogBox.ScrollToEnd();
            try
            {
                string logPath = Path.Combine(ConfigService.Current.GameDir, "launch.log");
                File.AppendAllText(logPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + Environment.NewLine);
            }
            catch { }
        }

        private void OpenLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logPath = Path.Combine(ConfigService.Current.GameDir, "launch.log");
                if (!File.Exists(logPath)) { MessageBox.Show("还没有日志，请先启动一次游戏", "提示"); return; }
                System.Diagnostics.Process.Start("notepad.exe", "\"" + logPath + "\"");
            }
            catch (Exception ex) { MessageBox.Show("打开日志失败：" + ex.Message); }
        }

        private void AccountArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string name = InputDialog.Show("设置用户名", "请输入离线用户名：", _account.Name);
            if (!string.IsNullOrWhiteSpace(name))
            {
                _account = AuthService.CreateOfflineAccount(name.Trim());
                ConfigService.Current.Account = _account;
                ConfigService.Save();
                UpdateAccountUi();
            }
        }
    }

    /// <summary>启动页的一个版本卡片。</summary>
    public class ModpackMeta
    {
        public string title { get; set; }
        public string icon { get; set; }
    }

    public class InstalledVersionItem : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void Raise(string n) { if (PropertyChanged != null) PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(n)); }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Info { get; set; }
        public string LoaderType { get; set; } = "vanilla";
        public string IconUrl { get; set; }
        private System.Windows.Media.ImageSource _iconImage;
        public System.Windows.Media.ImageSource IconImage
        {
            get
            {
                if (_iconImage != null) return _iconImage;
                _iconImage = LoadIcon();
                return _iconImage;
            }
            set { _iconImage = value; Raise("IconImage"); }
        }
        public void NotifyIcon() { Raise("IconImage"); }
        private System.Windows.Media.ImageSource LoadIcon()
        {
            {
                // 原逻辑（网络图标优先，否则本地）
                if (!string.IsNullOrEmpty(IconUrl))
                {
                    try
                    {
                        string url = IconUrl;
                        if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                            url = "https://wsrv.nl/?url=" + Uri.EscapeDataString(url) + "&output=png";
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(url, UriKind.Absolute);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        return bmp;
                    }
                    catch { }
                }
                // 回退到本地类型图标
                string asset = LoaderType == "forge" ? "forge" : LoaderType == "fabric" ? "fabric"
                    : LoaderType == "modpack" ? "modpack" : LoaderType == "neoforge" ? "neoforge"
                    : LoaderType == "quilt" ? "quilt" : "vanilla";
                try
                {
                    var fallback = new System.Windows.Media.Imaging.BitmapImage();
                    fallback.BeginInit();
                    fallback.UriSource = new Uri("pack://application:,,,/Assets/loaders/" + asset + ".png");
                    fallback.EndInit();
                    return fallback;
                }
                catch { return null; }
            }
        }
        public string LoaderLabel
        {
            get
            {
                switch (LoaderType)
                {
                    case "modpack": return "P";
                    case "forge": return "F";
                    case "fabric": return "f";
                    case "quilt": return "Q";
                    case "neoforge": return "N";
                    default: return "V";
                }
            }
        }
        public string LoaderColor
        {
            get
            {
                switch (LoaderType)
                {
                    case "modpack": return "#8B5CF6";
                    case "forge": return "#E67E22";
                    case "fabric": return "#3FA9F5";
                    case "quilt": return "#C084FC";
                    case "neoforge": return "#F97316";
                    default: return "#7CB442";
                }
            }
        }
    }
}
