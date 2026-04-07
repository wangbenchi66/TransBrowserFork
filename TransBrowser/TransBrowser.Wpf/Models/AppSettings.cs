using System.Drawing;

namespace TransBrowser.Wpf.Models
{
    /// <summary>
    /// Application settings – persisted as XML via SettingsService.
    /// Mirrors all settings from the WinForms Properties.Settings.Default.
    /// </summary>
    [Serializable]
    public class AppSettings
    {
        // ── Appearance ─────────────────────────────────────────────────────
        public double FormOpacity { get; set; } = 100;
        public string FormPosition { get; set; } = "100,100";
        public string FormSize { get; set; } = "590,410";
        public int ThemeBackColorArgb { get; set; } = Color.FromArgb(255, 249, 249, 249).ToArgb();

        // ── Window behaviour ────────────────────────────────────────────────
        public bool ShowInTaskbar { get; set; } = true;
        public bool TopMostWindow { get; set; } = false;
        public bool AutoHide { get; set; } = false;
        public bool CloseToTray { get; set; } = false;
        public bool ShowMinimizeNotification { get; set; } = false;
        public bool DisableWindowShadow { get; set; } = false;
        public bool HoverHeaderMode { get; set; } = false;
        public bool ShowTabBar { get; set; } = true;

        // ── Transparency ────────────────────────────────────────────────────
        public bool WindowTransparent { get; set; } = false;
        public bool TransparentBackground { get; set; } = false;

        // ── Browser ─────────────────────────────────────────────────────────
        public string DefaultUrl { get; set; } = "";
        public string DefaultUA { get; set; } = "";
        public bool MobileMold { get; set; } = false;
        public bool NoImageMode { get; set; } = false;
        public bool GrayscaleMode { get; set; } = false;
        public bool AntiScreenshotMode { get; set; } = false;
        public bool ClickThroughMode { get; set; } = false;

        // ── Custom sites & history ──────────────────────────────────────────
        public string CustomSites { get; set; } = "";
        public string BrowsingHistory { get; set; } = "";

        // ── Hotkeys ─────────────────────────────────────────────────────────
        public string HotkeyBossKey { get; set; } = "Alt+Q";
        public string HotkeyOpacityUp { get; set; } = "Alt+Right";
        public string HotkeyOpacityDown { get; set; } = "Alt+Left";
        public string HotkeyClickThrough { get; set; } = "Alt+P";

        // ── Icon ─────────────────────────────────────────────────────────────
        public string CustomIconPath { get; set; } = "";
        public string SelectedIconType { get; set; } = "Default";
    }
}
