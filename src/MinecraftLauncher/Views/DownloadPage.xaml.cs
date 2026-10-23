using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MinecraftLauncher.Models;
using MinecraftLauncher.Services;

namespace MinecraftLauncher.Views
{
    public partial class DownloadPage : UserControl
    {
        private readonly ObservableCollection<DownloadGroup> _groups = new ObservableCollection<DownloadGroup>();
        private bool _installing;
        private readonly ObservableCollection<ModrinthProjectView> _packResults = new ObservableCollection<ModrinthProjectView>();
        private readonly ObservableCollection<ModrinthProjectView> _modResults = new ObservableCollection<ModrinthProjectView>();
        private readonly ObservableCollection<ModrinthProjectView> _shaderResults = new ObservableCollection<ModrinthProjectView>();

        public DownloadPage()
        {
            InitializeComponent();
            GroupsControl.ItemsSource = _groups;
            PackResults.ItemsSource = _packResults;
            ModResults.ItemsSource = _modResults;
            ShaderResults.ItemsSource = _shaderResults;
            LoadManifest();
            LoadRecommendedPacks();
            LoadRecommendedMods();
            LoadRecommendedShaders();
        }

        private void DownloadTabs_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private static string GetComboText(ComboBox cb)
        {
            var item = cb.SelectedItem as ComboBoxItem;
            return item == null ? null : item.Content as string;
        }

        // ===== 原版 =====
        private void LoadManifest()
        {
            StatusText.Text = "正在加载版本清单...";
            Task.Run(() =>
            {
                try
                {
                    var m = VersionService.FetchManifest();
                    Dispatcher.Invoke(() => Build(m));
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => StatusText.Text = "版本清单加载失败：" + ex.Message);
                }
            });
        }

        private void Build(VersionManifest m)
        {
            if (m == null || m.versions == null) { StatusText.Text = "版本清单为空"; return; }
            var installed = new HashSet<string>(VersionService.GetInstalledVersions());
            if (m.latest != null)
            {
                LatestReleaseText.Text = m.latest.release;
                LatestSnapshotText.Text = m.latest.snapshot;
            }
            _groups.Clear();
            AddGroup("正式版", m.versions.Where(v => v.type == "release"), installed);
            AddGroup("预览版", m.versions.Where(v => v.type == "snapshot"), installed);
            AddGroup("远古版", m.versions.Where(v => v.type == "old_beta" || v.type == "old_alpha"), installed);
            StatusText.Text = "共 " + m.versions.Length + " 个版本，点击任意版本开始安装";
        }

        private void AddGroup(string title, IEnumerable<VersionEntry> versions, HashSet<string> installed)
        {
            var rows = new ObservableCollection<DownloadVersionRow>();
            foreach (var v in versions)
                rows.Add(new DownloadVersionRow { Entry = v, Status = installed.Contains(v.id) ? "已安装" : "未安装" });
            if (rows.Count == 0) return;
            _groups.Add(new DownloadGroup { Title = title + "（" + rows.Count + "）", Versions = rows });
        }

        private void LatestRelease_Click(object sender, RoutedEventArgs e) => StartInstallById(LatestReleaseText.Text);
        private void LatestSnapshot_Click(object sender, RoutedEventArgs e) => StartInstallById(LatestSnapshotText.Text);

        private void StartInstallById(string id)
        {
            if (string.IsNullOrEmpty(id) || id == "加载中...") return;
            foreach (var g in _groups)
                foreach (var r in g.Versions)
                    if (r.Id == id) { StartInstall(r); return; }
            StatusText.Text = "未找到版本 " + id;
        }

        private void InstallVersion_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var row = btn == null ? null : btn.Tag as DownloadVersionRow;
            if (row != null) StartInstall(row);
        }

        private void StartInstall(DownloadVersionRow row)
        {
            if (_installing) { StatusText.Text = "已有安装任务进行中，请稍候"; return; }
            if (row.Status == "已安装") { StatusText.Text = row.Id + " 已安装"; return; }
            _installing = true;
            row.Status = "安装中...";
            ProgressBar.Value = 0;
            StatusText.Text = "开始安装 " + row.Id;
            DownloadCenter.Start("安装 " + row.Id, 0);
            Task.Run(() =>
            {
                try
                {
                    VersionInstallService.Install(row.Entry,
                        msg => Dispatcher.Invoke(() => StatusText.Text = msg),
                        (done, total) => Dispatcher.Invoke(() =>
                        {
                            if (done % 20 == 0 || done >= total)
                            {
                                ProgressBar.Maximum = Math.Max(1, total);
                                ProgressBar.Value = done;
                                DownloadCenter.Update(done, total, "下载中 " + done + "/" + total);
                            }
                        }));
                    Dispatcher.Invoke(() => { row.Status = "已安装"; StatusText.Text = row.Id + " 安装完成，可到「启动」页启动"; DownloadCenter.Finish("完成：" + row.Id); });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => { row.Status = "失败"; StatusText.Text = "安装失败：" + ex.Message; DownloadCenter.Finish("失败：" + ex.Message); });
                }
                finally { Dispatcher.Invoke(() => _installing = false); }
            });
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            _groups.Clear();
            LoadManifest();
            LoadRecommendedPacks();
            LoadRecommendedMods();
            LoadRecommendedShaders();
        }

        // ===== 整合包 =====
        private void LoadRecommendedPacks()
        {
            PackStatusText.Text = "正在加载推荐整合包...";
            Task.Run(() =>
            {
                try
                {
                    var hits = ModrinthService.Search("", "modpack", null, null, "downloads");
                    Dispatcher.Invoke(() =>
                    {
                        _packResults.Clear();
                        foreach (var h in hits) _packResults.Add(new ModrinthProjectView(h));
                        PackStatusText.Text = "推荐整合包（按下载量排序）";
                    });
                }
                catch (Exception ex) { Dispatcher.Invoke(() => PackStatusText.Text = "推荐加载失败：" + ex.Message); }
            });
        }

        private void SearchPack_Click(object sender, RoutedEventArgs e)
        {
            string q = PackSearchBox.Text.Trim();
            PackStatusText.Text = "搜索中...";
            Task.Run(() =>
            {
                try
                {
                    var hits = ModrinthService.Search(q, "modpack", GetComboText(PackVersionCombo), GetComboText(PackTypeCombo));
                    Dispatcher.Invoke(() =>
                    {
                        _packResults.Clear();
                        foreach (var h in hits) _packResults.Add(new ModrinthProjectView(h));
                        PackStatusText.Text = "找到 " + _packResults.Count + " 个整合包";
                    });
                }
                catch (Exception ex) { Dispatcher.Invoke(() => PackStatusText.Text = "搜索失败：" + ex.Message); }
            });
        }

        private void InstallPack_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var view = btn == null ? null : btn.Tag as ModrinthProjectView;
            if (view == null) return;
            PackStatusText.Text = "正在获取整合包版本...";
            Task.Run(() =>
            {
                try
                {
                    var versions = ModrinthService.GetVersions(view.Project.project_id);
                    var ver = ModrinthService.PickVersion(versions, null);
                    var file = ModrinthService.PickFile(ver);
                    if (file == null) { Dispatcher.Invoke(() => PackStatusText.Text = "该整合包没有可下载文件"); return; }
                    string gameVer = null;
                    if (ver.game_versions != null && ver.game_versions.Length > 0) gameVer = ver.game_versions[0];

                    Dispatcher.Invoke(() => PackStatusText.Text = "开始安装整合包 " + view.Project.title + " ...");
                    DownloadCenter.Start("整合包 " + view.Project.title, 0);
                    string name = ModpackService.Install(view.Project.title, ver.id, file, gameVer,
                        msg => Dispatcher.Invoke(() => PackStatusText.Text = msg),
                        (d, t) => Dispatcher.Invoke(() =>
                        {
                            PackStatusText.Text = "下载文件 " + d + "/" + t;
                            DownloadCenter.Update(d, t, "下载文件 " + d + "/" + t);
                        }),
                        view.Project.icon_url);
                    Dispatcher.Invoke(() => { PackStatusText.Text = "整合包安装完成：" + name + "，可到「启动」页启动"; DownloadCenter.Finish("整合包完成：" + name); });
                }
                catch (Exception ex) { Dispatcher.Invoke(() => { PackStatusText.Text = "安装失败：" + ex.Message; DownloadCenter.Finish("失败：" + ex.Message); }); }
            });
        }

        // ===== Mod =====
        private void LoadRecommendedMods()
        {
            ModStatusText.Text = "正在加载推荐 Mod...";
            Task.Run(() =>
            {
                try
                {
                    var hits = ModrinthService.Search("", "mod", null, null, "downloads");
                    Dispatcher.Invoke(() =>
                    {
                        _modResults.Clear();
                        foreach (var h in hits) _modResults.Add(new ModrinthProjectView(h));
                        ModStatusText.Text = "推荐 Mod（按下载量排序）";
                    });
                }
                catch (Exception ex) { Dispatcher.Invoke(() => ModStatusText.Text = "推荐加载失败：" + ex.Message); }
            });
        }

        private void LoadRecommendedShaders()
        {
            ShaderStatusText.Text = "正在加载推荐光影...";
            Task.Run(() =>
            {
                try
                {
                    var hits = ModrinthService.Search("", "shader", null, null, "downloads");
                    Dispatcher.Invoke(() =>
                    {
                        _shaderResults.Clear();
                        foreach (var h in hits) _shaderResults.Add(new ModrinthProjectView(h));
                        ShaderStatusText.Text = "推荐光影（按下载量排序）";
                    });
                }
                catch (Exception ex) { Dispatcher.Invoke(() => ShaderStatusText.Text = "推荐加载失败：" + ex.Message); }
            });
        }

        private void SearchMod_Click(object sender, RoutedEventArgs e)
        {
            string q = ModSearchBox.Text.Trim();
            ModStatusText.Text = "搜索中...";
            Task.Run(() =>
            {
                try
                {
                    var hits = ModrinthService.Search(q, "mod", GetComboText(ModVersionCombo), GetComboText(ModTypeCombo));
                    Dispatcher.Invoke(() =>
                    {
                        _modResults.Clear();
                        foreach (var h in hits) _modResults.Add(new ModrinthProjectView(h));
                        ModStatusText.Text = "找到 " + _modResults.Count + " 个 Mod";
                    });
                }
                catch (Exception ex) { Dispatcher.Invoke(() => ModStatusText.Text = "搜索失败：" + ex.Message); }
            });
        }

        private void DownloadMod_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var view = btn == null ? null : btn.Tag as ModrinthProjectView;
            if (view != null) DownloadModFile(view, "mods", ModStatusText);
        }

        // ===== 光影 =====
        private void SearchShader_Click(object sender, RoutedEventArgs e)
        {
            string q = ShaderSearchBox.Text.Trim();
            ShaderStatusText.Text = "搜索中...";
            Task.Run(() =>
            {
                try
                {
                    var hits = ModrinthService.Search(q, "shader", GetComboText(ShaderVersionCombo), GetComboText(ShaderTypeCombo));
                    Dispatcher.Invoke(() =>
                    {
                        _shaderResults.Clear();
                        foreach (var h in hits) _shaderResults.Add(new ModrinthProjectView(h));
                        ShaderStatusText.Text = "找到 " + _shaderResults.Count + " 个光影";
                    });
                }
                catch (Exception ex) { Dispatcher.Invoke(() => ShaderStatusText.Text = "搜索失败：" + ex.Message); }
            });
        }

        private void DownloadShader_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            var view = btn == null ? null : btn.Tag as ModrinthProjectView;
            if (view != null) DownloadModFile(view, "shaderpacks", ShaderStatusText);
        }

        private void DownloadModFile(ModrinthProjectView view, string subDir, TextBlock status)
        {
            status.Text = "正在获取下载地址...";
            Task.Run(() =>
            {
                try
                {
                    var versions = ModrinthService.GetVersions(view.Project.project_id);
                    var ver = ModrinthService.PickVersion(versions, null);
                    var file = ModrinthService.PickFile(ver);
                    if (file == null) { Dispatcher.Invoke(() => status.Text = "没有可下载文件"); return; }

                    string gamedir = ConfigService.Current.GameDir;
                    string dest = Path.Combine(gamedir, subDir, file.filename);
                    Dispatcher.Invoke(() => status.Text = "下载 " + file.filename + " ...");
                    bool ok = DownloadService.DownloadFile(file.url, dest);
                    Dispatcher.Invoke(() => status.Text = ok ? ("下载完成：" + file.filename + "（已放到 " + subDir + " 目录）") : "下载失败");
                }
                catch (Exception ex) { Dispatcher.Invoke(() => status.Text = "下载失败：" + ex.Message); }
            });
        }
    }

    public class DownloadGroup
    {
        public string Title { get; set; }
        public ObservableCollection<DownloadVersionRow> Versions { get; set; }
    }

    public class DownloadVersionRow : INotifyPropertyChanged
    {
        public VersionEntry Entry { get; set; }
        public string Id { get { return Entry.id; } }
        public string ReleaseTime { get { return Entry.releaseTime; } }
        private string _status = "未安装";
        public string Status
        {
            get { return _status; }
            set { _status = value; OnPropertyChanged("Status"); }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string n) { if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(n)); }
    }

        public class ModrinthProjectView
    {
        public ModrinthProject Project { get; set; }
        public ModrinthProjectView(ModrinthProject p) { Project = p; }
        public string title { get { return Project.title; } }
        public string description { get { return Project.description; } }
        public string IconUrl { get { return Project.icon_url; } }
        public System.Windows.Media.ImageSource IconImage
        {
            get
            {
                if (string.IsNullOrEmpty(Project.icon_url)) return null;
                try
                {
                    string url = Project.icon_url;
                    // WPF 不支持 webp，用图片代理转成 png
                    if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                        url = "https://wsrv.nl/?url=" + Uri.EscapeDataString(url) + "&output=png";
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(url, UriKind.Absolute);
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    return bmp;
                }
                catch { return null; }
            }
        }
        public string Tags
        {
            get
            {
                if (Project.categories == null || Project.categories.Length == 0) return "";
                return string.Join("  ", Project.categories);
            }
        }
        public string Loaders
        {
            get
            {
                string s = "";
                if (Project.loaders != null && Project.loaders.Length > 0)
                    s = string.Join(" ", Project.loaders);
                if (Project.game_versions != null && Project.game_versions.Length > 0)
                    s = (s == "" ? "" : s + " ") + Project.game_versions[0] + "+";
                return s;
            }
        }
        public string DownloadsText { get { return "下载 " + FormatNum(Project.downloads); } }
        public string TimeText { get { return "更新 " + (Project.date_modified ?? "").Replace("T", " ").Replace("Z", ""); } }
        private static string FormatNum(long n)
        {
            if (n >= 10000) return (n / 10000) + "万";
            return n.ToString();
        }
    }
}