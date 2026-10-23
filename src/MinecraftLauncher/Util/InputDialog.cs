using System.Windows;
using System.Windows.Controls;

namespace MinecraftLauncher.Util
{
    /// <summary>简单的文本输入对话框。</summary>
    public static class InputDialog
    {
        public static string Show(string title, string prompt, string defaultValue)
        {
            var win = new Window
            {
                Title = title,
                Width = 380,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow
            };
            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 10) });
            var tb = new TextBox { Text = defaultValue, Height = 28, VerticalContentAlignment = VerticalAlignment.Center };
            sp.Children.Add(tb);
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            var ok = new Button { Content = "确定", Width = 84, Height = 30, Margin = new Thickness(0, 0, 10, 0) };
            var cancel = new Button { Content = "取消", Width = 84, Height = 30 };
            string result = null;
            ok.Click += (s, e) => { result = tb.Text; win.DialogResult = true; };
            cancel.Click += (s, e) => { win.DialogResult = false; };
            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            sp.Children.Add(btns);
            win.Content = sp;
            bool? res = win.ShowDialog();
            return res == true ? result : null;
        }
    }
}