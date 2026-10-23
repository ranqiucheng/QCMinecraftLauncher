using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MinecraftLauncher.Services;
using MinecraftLauncher.Views;

namespace MinecraftLauncher
{
    public partial class MainWindow : Window
    {
        private readonly HomePage _homePage = new HomePage();
        private readonly DownloadPage _downloadPage = new DownloadPage();
        private readonly AccountPage _accountPage = new AccountPage();
        private readonly ModPage _modPage = new ModPage();
        private readonly SettingsPage _settingsPage = new SettingsPage();
        private readonly SkinEditorPage _skinPage = new SkinEditorPage();
        private bool _first = true;

        public MainWindow()
        {
            InitializeComponent();
            DownloadCenter.Changed += (s, e) => UpdateDownloadBadge();
            UpdateDownloadBadge();
            ThemeService.Apply(ConfigService.Current.DarkTheme, ConfigService.Current.ThemeColor);
            NavList.SelectedIndex = 0;
        }

        private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UIElement page;
            switch (NavList.SelectedIndex)
            {
                case 0: _homePage.RefreshVersions(); page = _homePage; break;
                case 1: page = _downloadPage; break;
                case 2: page = _accountPage; break;
                case 3: _modPage.RefreshVersions(); page = _modPage; break;
                case 4: page = _skinPage; break;
                case 5: page = _settingsPage; break;
                default: page = _homePage; break;
            }
            SwitchPage(page);
        }

        private void SwitchPage(UIElement page)
        {
            ContentHost.Content = page;

            if (_first)
            {
                _first = false;
                return;
            }

            // 页面切换动画：淡入 + 从右侧轻微滑入
            page.Opacity = 0;
            var translate = new TranslateTransform(24, 0);
            page.RenderTransform = translate;

            page.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            translate.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(24, 0, TimeSpan.FromMilliseconds(260)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }
        // ===== 浮动下载进度 =====
        private bool _badgeDragging;
        private Point _badgeStart;
        private Thickness _badgeMargin;
        private bool _badgeMoved;

        private void UpdateDownloadBadge()
        {
            if (DownloadBadge == null) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(UpdateDownloadBadge));
                return;
            }
            if (DownloadCenter.IsActive)
            {
                DownloadBadge.Visibility = Visibility.Visible;
                DownloadBadgeTitle.Text = DownloadCenter.Name;
                DownloadBadgeProgress.Value = DownloadCenter.Percent;
                DownloadBadgeStatus.Text = DownloadCenter.Status;
            }
            else
            {
                DownloadBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void DownloadCancel_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            e.Handled = true;
            DownloadCenter.Cancel();
            UpdateDownloadBadge();
        }

        private void DownloadBadge_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _badgeDragging = true;
            _badgeMoved = false;
            _badgeStart = e.GetPosition(this);
            _badgeMargin = DownloadBadge.Margin;
            DownloadBadge.CaptureMouse();
        }

        private void DownloadBadge_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!_badgeDragging) return;
            var p = e.GetPosition(this);
            double dx = p.X - _badgeStart.X;
            double dy = p.Y - _badgeStart.Y;
            if (Math.Abs(dx) > 3 || Math.Abs(dy) > 3) _badgeMoved = true;
            DownloadBadge.Margin = new Thickness(_badgeMargin.Left, _badgeMargin.Top, _badgeMargin.Right - dx, _badgeMargin.Bottom - dy);
        }

        private void DownloadBadge_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _badgeDragging = false;
            DownloadBadge.ReleaseMouseCapture();
            // 点击（没拖动）→ 跳转到下载页查看详情
            if (!_badgeMoved)
            {
                var nav = NavList;
                if (nav != null) nav.SelectedIndex = 1;
            }
        }
    }
}