using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MinecraftLauncher.Services;

namespace MinecraftLauncher.Views
{
    public partial class SettingsPage : UserControl
    {
        public SettingsPage()
        {
            InitializeComponent();
            LoadConfig();
        }

        private void LoadConfig()
        {
            var c = ConfigService.Current;
            GameDirBox.Text = c.GameDir ?? "";
            JavaBox.Text = c.JavaPath ?? "";
            SourceCombo.SelectedIndex = (c.DownloadSource >= 0 && c.DownloadSource <= 1) ? c.DownloadSource : 0;
            MemoryCombo.SelectedIndex = MemoryToIndex(c.MaxMemoryMb);
            IsolationCheck.IsChecked = c.VersionIsolation;
            CloseAfterLaunchCheck.IsChecked = c.CloseAfterLaunch;
            ColorCombo.SelectedIndex = c.ThemeColor;
            ThemeCombo.SelectedIndex = c.DarkTheme ? 0 : 1;
        }

        private static int MemoryToIndex(int mb)
        {
            if (mb <= 1024) return 0;
            if (mb <= 2048) return 1;
            if (mb <= 4096) return 2;
            if (mb <= 6144) return 3;
            if (mb <= 8192) return 4;
            if (mb <= 12288) return 5;
            return 6;
        }

        private static int IndexToMemory(int idx)
        {
            switch (idx)
            {
                case 0: return 1024;
                case 1: return 2048;
                case 2: return 4096;
                case 3: return 6144;
                case 4: return 8192;
                case 5: return 12288;
                default: return 16384;
            }
        }

        private void BrowseGameDir_Click(object sender, RoutedEventArgs e)
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
            {
                dlg.Description = "选择 Minecraft 游戏目录（.minecraft 文件夹）";
                dlg.ShowNewFolderButton = false;
                if (!string.IsNullOrEmpty(GameDirBox.Text))
                    dlg.SelectedPath = GameDirBox.Text;
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    GameDirBox.Text = dlg.SelectedPath;
            }
        }

        private void DetectJava_Click(object sender, RoutedEventArgs e)
        {
            JavaDetectStatus.Text = "正在检测 Java...";
            Task.Run(() =>
            {
                try
                {
                    var list = JavaService.DetectAll();
                    Dispatcher.Invoke(() =>
                    {
                        DetectedJavaCombo.Items.Clear();
                        foreach (var j in list) DetectedJavaCombo.Items.Add(j);
                        JavaDetectStatus.Text = list.Count == 0
                            ? "未检测到 Java，请手动指定或安装 Java"
                            : "检测到 " + list.Count + " 个 Java 运行时，选择后自动填入路径";
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => JavaDetectStatus.Text = "检测失败：" + ex.Message);
                }
            });
        }

        private void DetectedJava_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var info = DetectedJavaCombo.SelectedItem as JavaInfo;
            if (info != null) JavaBox.Text = info.Path;
        }
        private void BrowseJava_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择 Java 可执行文件",
                Filter = "Java 可执行文件 (java.exe)|java.exe|所有文件 (*.*)|*.*",
                FileName = "java.exe"
            };
            if (dlg.ShowDialog() == true)
                JavaBox.Text = dlg.FileName;
        }

        private void ThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigService.Current == null || ThemeCombo == null || ColorCombo == null) return;
            bool dark = ThemeCombo.SelectedIndex == 0;
            int color = ColorCombo.SelectedIndex < 0 ? 0 : ColorCombo.SelectedIndex;
            ConfigService.Current.DarkTheme = dark;
            ConfigService.Current.ThemeColor = color;
            ConfigService.Save();
            ThemeService.Apply(dark, color);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var c = ConfigService.Current;
            c.GameDir = GameDirBox.Text.Trim();
            c.JavaPath = JavaBox.Text.Trim();
            c.DownloadSource = SourceCombo.SelectedIndex;
            c.MaxMemoryMb = IndexToMemory(MemoryCombo.SelectedIndex);
            c.VersionIsolation = IsolationCheck.IsChecked == true;
            c.CloseAfterLaunch = CloseAfterLaunchCheck.IsChecked == true;
            c.DarkTheme = ThemeCombo.SelectedIndex == 0;
            c.ThemeColor = ColorCombo.SelectedIndex < 0 ? 0 : ColorCombo.SelectedIndex;
            ConfigService.Save();
            MessageBox.Show("设置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}