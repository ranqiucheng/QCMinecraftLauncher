using System.Windows;
using System.Windows.Media;

namespace MinecraftLauncher.Services
{
    /// <summary>主题服务：明暗 + 主题色（配合 DynamicResource 生效）。</summary>
    public static class ThemeService
    {
        // 每种颜色：[Primary, Dark, Light]
        private static readonly string[][] DarkColors = {
            new[] { "#4C8DFF", "#3A7AE8", "#6BA5FF" }, // 蓝
            new[] { "#9C6BFF", "#7B4FE8", "#B28BFF" }, // 紫
            new[] { "#4CAF50", "#3E8E41", "#6BCF6F" }, // 绿
            new[] { "#F44336", "#D32F2F", "#F77066" }, // 红
            new[] { "#FF9800", "#E68A00", "#FFB84D" }, // 橙
            new[] { "#00BCD4", "#0097A7", "#4DD0E1" }, // 青
            new[] { "#E91E63", "#C2185B", "#F06292" }, // 粉
        };
        private static readonly string[][] LightColors = {
            new[] { "#1370F3", "#0B5BCB", "#4890F5" }, // 蓝
            new[] { "#7B4FE8", "#6338C4", "#9C6BFF" }, // 紫
            new[] { "#2E7D32", "#1B5E20", "#4CAF50" }, // 绿
            new[] { "#D32F2F", "#B71C1C", "#F44336" }, // 红
            new[] { "#E65100", "#BF360C", "#FF9800" }, // 橙
            new[] { "#00838F", "#006064", "#00BCD4" }, // 青
            new[] { "#C2185B", "#880E4F", "#E91E63" }, // 粉
        };

        public static void Apply(bool dark, int colorIndex)
        {
            var res = Application.Current.Resources;
            var colors = dark ? DarkColors : LightColors;
            if (colorIndex < 0 || colorIndex >= colors.Length) colorIndex = 0;
            var c = colors[colorIndex];
            res["PrimaryColor"] = (Color)ColorConverter.ConvertFromString(c[0]);
            res["PrimaryBrush"] = B(c[0]);
            res["PrimaryDarkBrush"] = B(c[1]);
            res["PrimaryLightBrush"] = B(c[2]);

            if (dark) ApplyDark(res); else ApplyLight(res);
        }

        private static void ApplyDark(ResourceDictionary res)
        {
            res["WindowBackgroundBrush"] = B("#1E1F22");
            res["CardBackgroundBrush"] = B("#2B2D31");
            res["TextPrimaryBrush"] = B("#E6E6E6");
            res["TextSecondaryBrush"] = B("#9A9A9A");
            res["DividerBrush"] = B("#3A3C41");
            res["HoverBackgroundBrush"] = B("#35373C");
            res["SidebarBackgroundBrush"] = B("#232428");
        }

        private static void ApplyLight(ResourceDictionary res)
        {
            res["WindowBackgroundBrush"] = B("#F0F3F8");
            res["CardBackgroundBrush"] = B("#FFFFFF");
            res["TextPrimaryBrush"] = B("#343D4A");
            res["TextSecondaryBrush"] = B("#737373");
            res["DividerBrush"] = B("#E0E4EA");
            res["HoverBackgroundBrush"] = B("#EAF2FE");
            res["SidebarBackgroundBrush"] = B("#FFFFFF");
        }

        private static SolidColorBrush B(string hex)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
    }
}