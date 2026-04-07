using System.Drawing;
using System.IO;
using System.Reflection;
using System.Xml.Serialization;
using TransBrowser.Wpf.Models;

namespace TransBrowser.Wpf.Services
{
    /// <summary>
    /// Singleton settings service – reads/writes config/appsettings.xml
    /// in the executable directory (same location as the WinForms version).
    /// </summary>
    public sealed class SettingsService
    {
        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        public AppSettings Current { get; private set; } = new AppSettings();

        private SettingsService() { }

        private static string GetConfigPath()
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            var cfgDir = Path.Combine(exeDir, "config");
            if (!Directory.Exists(cfgDir)) Directory.CreateDirectory(cfgDir);
            return Path.Combine(cfgDir, "appsettings.xml");
        }

        public void Load()
        {
            try
            {
                var path = GetConfigPath();
                if (!File.Exists(path)) return;
                var ser = new XmlSerializer(typeof(AppSettings));
                using var fs = File.OpenRead(path);
                var data = (AppSettings?)ser.Deserialize(fs);
                if (data != null) Current = data;
            }
            catch (Exception ex)
            {
                Tools.LogHelper.Error(ex);
            }
        }

        public void Save()
        {
            try
            {
                var path = GetConfigPath();
                var ser = new XmlSerializer(typeof(AppSettings));
                using var fs = File.Create(path);
                ser.Serialize(fs, Current);
            }
            catch (Exception ex)
            {
                Tools.LogHelper.Error(ex);
            }
        }

        // ── Convenience helpers ──────────────────────────────────────────────

        public System.Windows.Point GetFormPosition()
        {
            try
            {
                var parts = Current.FormPosition?.Split(',');
                if (parts?.Length == 2)
                    return new System.Windows.Point(double.Parse(parts[0]), double.Parse(parts[1]));
            }
            catch { }
            return new System.Windows.Point(100, 100);
        }

        public System.Windows.Size GetFormSize()
        {
            try
            {
                var parts = Current.FormSize?.Split(',');
                if (parts?.Length == 2)
                    return new System.Windows.Size(double.Parse(parts[0]), double.Parse(parts[1]));
            }
            catch { }
            return new System.Windows.Size(590, 410);
        }

        public void SetFormPosition(double x, double y)
        {
            Current.FormPosition = $"{(int)x},{(int)y}";
        }

        public void SetFormSize(double w, double h)
        {
            Current.FormSize = $"{(int)w},{(int)h}";
        }

        public Color GetThemeBackColor()
        {
            try { return Color.FromArgb(Current.ThemeBackColorArgb); }
            catch { return Color.FromArgb(255, 249, 249, 249); }
        }

        public void SetThemeBackColor(Color c)
        {
            Current.ThemeBackColorArgb = c.ToArgb();
        }
    }
}
