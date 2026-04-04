using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace TransBrowser.Tools
{
    [Serializable]
    public class LocalConfigData
    {
        public double FormOpacity { get; set; }
        public string FormPosition { get; set; }
        public string FormSize { get; set; }
        public bool ShowInTaskbar { get; set; }
        public string DefaultUrl { get; set; }
        public int ThemeBackColorArgb { get; set; }
        public bool MobileMold { get; set; }
        public string DefaultUA { get; set; }
        public bool AutoHide { get; set; }
        public bool ClickThroughMode { get; set; }
        public string HotkeyBossKey { get; set; }
        public string HotkeyOpacityUp { get; set; }
        public string HotkeyOpacityDown { get; set; }
        public string HotkeyClickThrough { get; set; }
        public bool ShowTabBar { get; set; }
        public bool NoImageMode { get; set; }
        public bool TopMostWindow { get; set; }
        public string CustomSites { get; set; }
        public bool HoverHeaderMode { get; set; }
        public bool TransparentBackground { get; set; }
        public string BrowsingHistory { get; set; }
        public bool GrayscaleMode { get; set; }
        public bool AntiScreenshotMode { get; set; }
    }

    internal static class LocalConfig
    {
        private static string GetConfigPath()
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var cfgDir = Path.Combine(exeDir ?? ".", "config");
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
            return Path.Combine(cfgDir, "appsettings.xml");
        }

        public static void Load()
        {
            try
            {
                var path = GetConfigPath();
                if (!File.Exists(path)) return;
                var ser = new XmlSerializer(typeof(LocalConfigData));
                using (var fs = File.OpenRead(path))
                {
                    var data = (LocalConfigData)ser.Deserialize(fs);
                    if (data == null) return;

                    var s = Properties.Settings.Default;
                    s.FormOpacity = data.FormOpacity;
                    try { var pp = data.FormPosition?.Split(','); if (pp?.Length == 2) s.FormPosition = new System.Drawing.Point(int.Parse(pp[0]), int.Parse(pp[1])); } catch { }
                    try { var ps = data.FormSize?.Split(','); if (ps?.Length == 2) s.FormSize = new System.Drawing.Size(int.Parse(ps[0]), int.Parse(ps[1])); } catch { }
                    s.ShowInTaskbar = data.ShowInTaskbar;
                    s.DefaultUrl = data.DefaultUrl;
                    try { s.ThemeBackColor = System.Drawing.Color.FromArgb(data.ThemeBackColorArgb); } catch { }
                    s.MobileMold = data.MobileMold;
                    s.DefaultUA = data.DefaultUA;
                    s.AutoHide = data.AutoHide;
                    s.ClickThroughMode = data.ClickThroughMode;
                    s.HotkeyBossKey = data.HotkeyBossKey;
                    s.HotkeyOpacityUp = data.HotkeyOpacityUp;
                    s.HotkeyOpacityDown = data.HotkeyOpacityDown;
                    s.HotkeyClickThrough = data.HotkeyClickThrough;
                    s.ShowTabBar = data.ShowTabBar;
                    s.NoImageMode = data.NoImageMode;
                    s.TopMostWindow = data.TopMostWindow;
                    s.CustomSites = data.CustomSites;
                    s.HoverHeaderMode = data.HoverHeaderMode;
                    s.TransparentBackground = data.TransparentBackground;
                    s.BrowsingHistory = data.BrowsingHistory;
                    s.GrayscaleMode = data.GrayscaleMode;
                    s.AntiScreenshotMode = data.AntiScreenshotMode;
                }
            }
            catch (Exception ex)
            {
                try { Tools.LogHelper.Error(ex); } catch { }
            }
        }

        public static void Save()
        {
            try
            {
                var s = Properties.Settings.Default;
                var data = new LocalConfigData
                {
                    FormOpacity = s.FormOpacity,
                    FormPosition = s.FormPosition.X + "," + s.FormPosition.Y,
                    FormSize = s.FormSize.Width + "," + s.FormSize.Height,
                    ShowInTaskbar = s.ShowInTaskbar,
                    DefaultUrl = s.DefaultUrl,
                    ThemeBackColorArgb = s.ThemeBackColor.ToArgb(),
                    MobileMold = s.MobileMold,
                    DefaultUA = s.DefaultUA,
                    AutoHide = s.AutoHide,
                    ClickThroughMode = s.ClickThroughMode,
                    HotkeyBossKey = s.HotkeyBossKey,
                    HotkeyOpacityUp = s.HotkeyOpacityUp,
                    HotkeyOpacityDown = s.HotkeyOpacityDown,
                    HotkeyClickThrough = s.HotkeyClickThrough,
                    ShowTabBar = s.ShowTabBar,
                    NoImageMode = s.NoImageMode,
                    TopMostWindow = s.TopMostWindow,
                    CustomSites = s.CustomSites,
                    HoverHeaderMode = s.HoverHeaderMode,
                    TransparentBackground = s.TransparentBackground,
                    BrowsingHistory = s.BrowsingHistory,
                    GrayscaleMode = s.GrayscaleMode,
                    AntiScreenshotMode = s.AntiScreenshotMode
                };

                var path = GetConfigPath();
                var ser = new XmlSerializer(typeof(LocalConfigData));
                using (var fs = File.Create(path))
                {
                    ser.Serialize(fs, data);
                }
            }
            catch (Exception ex)
            {
                try { Tools.LogHelper.Error(ex); } catch { }
            }
        }
    }
}
