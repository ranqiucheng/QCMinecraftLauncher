using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MinecraftLauncher.Services;

namespace MinecraftLauncher.Views
{
    public partial class AccountPage : UserControl
    {
        public AccountPage()
        {
            InitializeComponent();
            Refresh();
        }

        private void Refresh()
        {
            var acc = ConfigService.Current.Account;
            if (acc == null)
            {
                CurrentAccountText.Text = "未登录";
                CurrentAccountType.Text = "";
            }
            else
            {
                CurrentAccountText.Text = acc.Name;
                CurrentAccountType.Text = acc.IsOffline ? "离线模式" : "Microsoft 正版";
                if (acc.IsOffline) OfflineNameBox.Text = acc.Name;
            }
        }

        private void OfflineButton_Click(object sender, RoutedEventArgs e)
        {
            string name = OfflineNameBox.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入用户名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            ConfigService.Current.Account = AuthService.CreateOfflineAccount(name);
            ConfigService.Save();
            Refresh();
            MessageBox.Show("已切换为离线账号：" + name, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MsLoginButton_Click(object sender, RoutedEventArgs e)
        {
            MsLoginButton.IsEnabled = false;
            MsStatusText.Text = "正在启动本地验证服务器...";

            // 先在后台启动本地服务器等待回调
            var serverTask = Task.Run(() => MicrosoftAuthService.WaitForCode(180000));

            // 稍等让监听就绪，再打开浏览器
            Task.Delay(500).ContinueWith(_ =>
            {
                try { Process.Start(MicrosoftAuthService.BuildAuthUrl()); } catch { }
            });
            MsStatusText.Text = "已打开浏览器，请登录 Microsoft 账号。登录完成后浏览器会提示成功，请回到启动器。";

            Task.Run(() =>
            {
                try
                {
                    string code = serverTask.Result;
                    if (string.IsNullOrEmpty(code))
                    {
                        Dispatcher.Invoke(() => MsStatusText.Text = "等待授权超时或失败，请重试。");
                        return;
                    }
                    Dispatcher.Invoke(() => MsStatusText.Text = "授权成功，正在验证...");
                    string token = MicrosoftAuthService.ExchangeCodeForToken(code);
                    Dispatcher.Invoke(() => MsStatusText.Text = "正在获取账号信息...");
                    var acc = MicrosoftAuthService.CompleteLogin(token,
                        msg => Dispatcher.Invoke(() => MsStatusText.Text = msg));
                    Dispatcher.Invoke(() =>
                    {
                        ConfigService.Current.Account = acc;
                        ConfigService.Save();
                        Refresh();
                        MsStatusText.Text = "正版登录成功：" + acc.Name;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MsStatusText.Text = "登录失败：" + ex.Message);
                }
                finally
                {
                    Dispatcher.Invoke(() => MsLoginButton.IsEnabled = true);
                }
            });
        }
    }
}