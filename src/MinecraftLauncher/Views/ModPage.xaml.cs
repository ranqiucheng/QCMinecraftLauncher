using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using MinecraftLauncher.Services;

namespace MinecraftLauncher.Views
{
    public partial class ModPage : UserControl
    {
        public ModPage()
        {
            InitializeComponent();
            RefreshVersions();
        }

        public void RefreshVersions()
        {
            string selected = VersionCombo.SelectedItem as string;
            var versions = VersionService.GetInstalledVersions();
            VersionCombo.Items.Clear();
            foreach (var v in versions)
                VersionCombo.Items.Add(v);

            if (selected != null && versions.Contains(selected))
                VersionCombo.SelectedItem = selected;
            else if (versions.Count > 0)
                VersionCombo.SelectedIndex = 0;
            UpdateTip();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshVersions();
        }

        private void VersionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateTip();
        }

        private void UpdateTip()
        {
            string v = VersionCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(v))
            {
                TipText.Text = "请先在「启动」页安装一个版本，再回到这里选择。";
                ButtonsPanel.IsEnabled = false;
            }
            else
            {
                TipText.Text = "当前版本：" + v + "    目录：" + ConfigService.GetVersionGameDir(v);
                ButtonsPanel.IsEnabled = true;
            }
        }

        private void OpenDir_Click(object sender, RoutedEventArgs e)
        {
            string v = VersionCombo.SelectedItem as string;
            if (string.IsNullOrEmpty(v))
            {
                MessageBox.Show("请先选择一个版本", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var btn = sender as Button;
            string tag = btn == null ? null : btn.Tag as string;
            string path = ResolvePath(tag, v);
            try
            {
                Directory.CreateDirectory(path);
                Process.Start("explorer.exe", path);
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开失败：" + ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string ResolvePath(string tag, string versionId)
        {
            string gamedir = ConfigService.Current.GameDir;
            string vdir = ConfigService.GetVersionGameDir(versionId);
            switch (tag)
            {
                case "mods": return Path.Combine(vdir, "mods");
                case "resourcepacks": return Path.Combine(vdir, "resourcepacks");
                case "shaderpacks": return Path.Combine(vdir, "shaderpacks");
                case "saves": return Path.Combine(vdir, "saves");
                case "screenshots": return Path.Combine(vdir, "screenshots");
                case "versions": return Path.Combine(gamedir, "versions", versionId);
                case "root": return gamedir;
                default: return vdir;
            }
        }
    }
}