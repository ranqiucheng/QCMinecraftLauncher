using System;
using System.IO;
using System.Windows;

namespace MinecraftLauncher
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                try
                {
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), e.Exception.ToString());
                }
                catch { }
                e.Handled = true;
            };
        }
    }
}