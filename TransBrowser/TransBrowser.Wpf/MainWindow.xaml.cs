using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using TransBrowser.Wpf.Services;
using TransBrowser.Wpf.Tools;
using static TransBrowser.Wpf.Tools.GlobalHotkey;
using System.Drawing;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;

namespace TransBrowser.Wpf
{
    public partial class MainWindow : Window
    {
        // ── P/Invoke ──────────────────────────────────────────────────────────
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int val);
        [DllImport("user32.dll", SetLastError = true)] static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        [DllImport("user32.dll")] static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint affinity);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x20;
        const int WS_EX_LAYERED = 0x80000;
        const uint LWA_ALPHA = 0x2;
        const uint WDA_NONE = 0;
        const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        // NOTE: Do not use SetWindowCompositionAttribute/AccentPolicy here.
        // Accent policy overlays a gradient/tint over the window and can turn the content black.
        const int DWMWA_NCRENDERING_POLICY = 2;
        const int DWMNCRP_DISABLED = 1;
        const int WM_HOTKEY = 0x0312;

        // ── State ─────────────────────────────────────────────────────────────
        private SettingsService _s = SettingsService.Instance;
        private bool _inited = false;
        private bool _allowExit = false;
        private bool _clickThrough = false;
        private bool _mobileMode = false;
        private bool _hadSavedBounds = false;
        private double _savedWidth, _savedHeight, _savedLeft, _savedTop;
        private bool _savedShowTabBar = true;
        private bool _headerVisible = true;
        private double _lastOpacityBeforeReset = -1;

        // Header hover behavior
        private const int HEADER_HOVER_HEIGHT = 28;
        private DispatcherTimer? _headerHideTimer;
        private DispatcherTimer? _headerPollTimer;

        // Tray
        private WinForms.NotifyIcon? _trayIcon;
        private WinForms.ContextMenuStrip? _trayMenu;
        private WinForms.ToolStripMenuItem? _trayShowHideItem;
        private WinForms.ToolStripMenuItem? _trayClickThroughItem;

        // Settings / ControlPanel windows
        private SettingWindow? _settingWindow;
        private ControlPanelWindow? _controlPanel;

        // Transparent-background CSS
        const string TransparentBgJs =
            "(function(){var s=document.getElementById('__trans_bg');" +
            "if(!s){s=document.createElement('style');s.id='__trans_bg';" +
            "(document.head||document.documentElement).appendChild(s);}" +
            "s.textContent='html,body{background:transparent!important;" +
            "background-color:transparent!important}';})()";

        // Hotkey IDs
        enum HotkeyId
        {
            LegacyToggleShow = 100, LegacyToggleTop = 101,
            LegacyGoBack = 102, LegacyRunJs = 104,
            BossKey = 200, OpacityUp = 201, OpacityDown = 202,
            ClickThrough = 203, OpacityReset = 204
        }

        // Tab data
        private sealed class BrowserTab
        {
            public string Title { get; set; } = "新标签页";
            public bool IsAddTab { get; set; } = false;
            public WebView2? WebView { get; set; }
        }

        // History
        private sealed class HistoryItem
        {
            public string Title { get; set; } = "";
            public string Url { get; set; } = "";
            public DateTime VisitTime { get; set; }
        }

        // custom sites
        private sealed class CustomSite { public string Name = ""; public string Url = ""; }

        // ── Constructor ───────────────────────────────────────────────────────
        public MainWindow()
        {
            InitializeComponent();
            SetupTrayIcon();
        }

        // ── Loaded ────────────────────────────────────────────────────────────
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var src = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            src?.AddHook(WndProc);

            Init();
            RegisterAllHotkeys();
            InitFloatingHeader();
            AddFirstTab();
            _inited = true;
        }

        // ── Tray setup ────────────────────────────────────────────────────────
        private void SetupTrayIcon()
        {
            _trayMenu = new WinForms.ContextMenuStrip();
            _trayShowHideItem = new WinForms.ToolStripMenuItem("显示 / 隐藏");
            _trayShowHideItem.Click += (_, _) => { if (IsVisible) Hide(); else ShowMainWindow(); };
            _trayClickThroughItem = new WinForms.ToolStripMenuItem("鼠标穿透模式");
            _trayClickThroughItem.Click += (_, _) => ToggleClickThrough();
            var settingsItem = new WinForms.ToolStripMenuItem("设置");
            settingsItem.Click += (_, _) => OpenSettings();
            var sep = new WinForms.ToolStripSeparator();
            var exitItem = new WinForms.ToolStripMenuItem("退出");
            exitItem.Click += (_, _) => ExitApp();
            _trayMenu.Items.AddRange(new WinForms.ToolStripItem[] {
                _trayShowHideItem, _trayClickThroughItem, settingsItem, sep, exitItem });

            _trayIcon = new WinForms.NotifyIcon
            {
                Visible = true,
                Text = "TransBrowser",
                ContextMenuStrip = _trayMenu
            };
            _trayIcon.MouseDoubleClick += (_, _) => ShowMainWindow();

            // Set icon
            try
            {
                var iconPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".",
                    "Resources", "icon.ico");
                if (File.Exists(iconPath))
                    _trayIcon.Icon = new System.Drawing.Icon(iconPath);
                else
                    _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(WinForms.Application.ExecutablePath);
            }
            catch { }
        }

        // ── Init (restore settings) ───────────────────────────────────────────
        public void Init()
        {
            var s = _s.Current;

            if (s.FormOpacity <= 0 || s.FormOpacity > 100) s.FormOpacity = 100;

            var pos = _s.GetFormPosition();
            Left = pos.X; Top = pos.Y;

            var sz = _s.GetFormSize();
            Width = sz.Width; Height = sz.Height;

            // Do not set Window.Opacity (turns window into layered HWND which breaks WebView2 rendering).
            // Use RootGrid.Opacity to make WPF content translucent while keeping WebView2 handled separately.
            try { RootGrid.Opacity = s.FormOpacity / 100.0; } catch { }

            Topmost = s.TopMostWindow;
            UpdateTopMostButton();

            ApplyThemeColor(_s.GetThemeBackColor());
            SetTabBarVisible(s.ShowTabBar);
            SetShowInTaskbar(s.ShowInTaskbar);
            SetCustomIcon(s.CustomIconPath);

            if (s.ClickThroughMode) SetClickThrough(true);
            if (_trayClickThroughItem != null)
                _trayClickThroughItem.Checked = s.ClickThroughMode;

            if (s.DisableWindowShadow) SetWindowShadowDisabled(true);

            ApplyHoverHeaderMode(s.HoverHeaderMode);
            SetMobileMold(s.MobileMold);

            _s.Save();
        }

        // ── First tab ─────────────────────────────────────────────────────────
        private void AddFirstTab()
        {
            var tab = new TabItem { Header = "新标签页" };
            var wv = CreateWebView2();
            tab.Content = wv;
            TabCtrl.Items.Insert(0, tab);

            // Add "+" sentinel tab
            var addTab = new TabItem { Header = "+" };
            addTab.Loaded += (_, _) => { /* handled in SelectionChanged */ };
            TabCtrl.Items.Add(addTab);

            TabCtrl.SelectedIndex = 0;
            _ = InitWebViewAsync(wv, _s.Current.DefaultUrl);
        }

        private WebView2 CreateWebView2()
        {
            var wv = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.White };
            // If overall window opacity is not fully opaque or transparent-background setting is on,
            // initialize webview with transparent background so WPF content shows through.
            if (_s.Current.TransparentBackground || _s.Current.FormOpacity < 100)
                wv.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            return wv;
        }

        // ── Tab management ────────────────────────────────────────────────────
        public TabItem AddNewTab(string? url = null)
        {
            int insertAt = TabCtrl.Items.Count - 1; // before "+"
            var tab = new TabItem { Header = "新标签页" };
            var wv = CreateWebView2();
            tab.Content = wv;
            TabCtrl.Items.Insert(insertAt, tab);
            TabCtrl.SelectedItem = tab;
            _ = InitWebViewAsync(wv, url);
            return tab;
        }

        private async Task InitWebViewAsync(WebView2 wv, string? url)
        {
            if (_s.Current.TransparentBackground)
                wv.DefaultBackgroundColor = System.Drawing.Color.Transparent;
            await wv.EnsureCoreWebView2Async();
            if (wv.CoreWebView2 == null) return;

            // User-agent
            try
            {
                if (_mobileMode)
                    wv.CoreWebView2.Settings.UserAgent = MobileUA;
                else if (!string.IsNullOrEmpty(_s.Current.DefaultUA))
                    wv.CoreWebView2.Settings.UserAgent = _s.Current.DefaultUA;
            }
            catch { }

            // Events
            wv.CoreWebView2.DocumentTitleChanged += (_, _) => UpdateTabTitle(wv);
            wv.CoreWebView2.NewWindowRequested += (_, e) => { e.Handled = true; AddNewTab(e.Uri); };
            wv.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
            wv.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            wv.NavigationCompleted += (_, e) => OnNavigationCompleted(wv);

            if (_s.Current.TransparentBackground)
                await RegisterTransparentBgScript(wv);

            // Apply current opacity to web content so it visually matches the window opacity.
            try { await ApplyOpacityToWebViewAsync(wv, _s.Current.FormOpacity); } catch { }

            if (!string.IsNullOrEmpty(url))
                wv.CoreWebView2.Navigate(url);
            else
                wv.CoreWebView2.NavigateToString(GetNewTabHtml());
        }

        const string MobileUA = "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1";

        private void UpdateTabTitle(WebView2 wv)
        {
            Dispatcher.InvokeAsync(() =>
            {
                for (int i = 0; i < TabCtrl.Items.Count - 1; i++)
                {
                    if (TabCtrl.Items[i] is TabItem ti && ti.Content == wv)
                    {
                        string t = wv.CoreWebView2?.DocumentTitle ?? "";
                        if (string.IsNullOrEmpty(t)) t = "新标签页";
                        if (t.Length > 20) t = t[..18] + "…";
                        ti.Header = t;
                        break;
                    }
                }
            });
        }

        private void TabCtrl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // If "+" tab selected -> open new tab
            if (TabCtrl.SelectedIndex == TabCtrl.Items.Count - 1)
                Dispatcher.InvokeAsync(() => AddNewTab());
        }

        private void TabCtrl_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Middle-click to close tab (handled via individual tab item mouse events)
        }

        private void TabCloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Walk up visual tree to find the TabItem
            var btn = sender as WpfButton;
            var tabItem = FindParent<TabItem>(btn);
            if (tabItem != null) CloseTab(tabItem);
        }

        private void CloseTab(TabItem tab)
        {
            int idx = TabCtrl.Items.IndexOf(tab);
            if (idx < 0 || idx >= TabCtrl.Items.Count - 1) return;

            if (tab.Content is WebView2 wv)
            {
                try { wv.Dispose(); } catch { }
            }

            TabCtrl.Items.Remove(tab);

            if (TabCtrl.Items.Count <= 1) // only "+" left
            {
                AddNewTab();
                return;
            }

            int sel = Math.Max(0, Math.Min(idx, TabCtrl.Items.Count - 2));
            TabCtrl.SelectedIndex = sel;
        }

        private WebView2? ActiveWebView
        {
            get
            {
                int idx = TabCtrl.SelectedIndex;
                if (idx < 0 || idx >= TabCtrl.Items.Count - 1) idx = 0;
                if (TabCtrl.Items.Count == 0) return null;
                if (idx < 0 || idx >= TabCtrl.Items.Count) return null;
                return (TabCtrl.Items[idx] as TabItem)?.Content as WebView2;
            }
        }

        // ── Navigation events ─────────────────────────────────────────────────
        private void OnNavigationCompleted(WebView2 wv)
        {
            if (wv == ActiveWebView && wv.Source != null)
            {
                var uri = wv.Source.AbsoluteUri;
                _s.Current.DefaultUrl = uri;
                _s.Save();
                AddToHistory(wv.CoreWebView2?.DocumentTitle ?? "", uri);
            }

            if (_s.Current.NoImageMode) _ = ApplyNoImageCss(wv, true);
            if (_s.Current.TransparentBackground) _ = ApplyTransparentBackground(wv, true);
            if (_s.Current.GrayscaleMode) _ = ApplyGrayscaleCss(wv, true);
        }

        private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            var core = sender as CoreWebView2;
            if (core == null) return;
            var items = e.MenuItems;
            items.Add(core.Environment.CreateContextMenuItem("", null, CoreWebView2ContextMenuItemKind.Separator));
            if (!string.IsNullOrEmpty(e.ContextMenuTarget.LinkUri))
            {
                var copy = core.Environment.CreateContextMenuItem("复制链接", null, CoreWebView2ContextMenuItemKind.Command);
                string lu = e.ContextMenuTarget.LinkUri;
                copy.CustomItemSelected += (_, _) => WpfClipboard.SetText(lu);
                items.Insert(0, copy);
            }
            var dt = core.Environment.CreateContextMenuItem("开发者工具", null, CoreWebView2ContextMenuItemKind.Command);
            dt.CustomItemSelected += (_, _) => core.OpenDevToolsWindow();
            items.Add(dt);
        }

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string msg = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(msg)) return;
            var parts = msg.Split('\t');
            if (parts.Length < 2) return;
            var sites = LoadCustomSites();
            if (parts[0] == "add" && parts.Length >= 3)
            {
                string name = parts[1].Trim(), url = parts[2].Trim();
                if (string.IsNullOrEmpty(url)) return;
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;
                if (string.IsNullOrEmpty(name)) name = url;
                sites.Add(new CustomSite { Name = name, Url = url });
                SaveCustomSites(sites);
                Dispatcher.InvokeAsync(RefreshNewTabPages);
            }
            else if (parts[0] == "remove" && parts.Length >= 2)
            {
                sites.RemoveAll(s => s.Url == parts[1].Trim());
                SaveCustomSites(sites);
                Dispatcher.InvokeAsync(RefreshNewTabPages);
            }
        }

        private void RefreshNewTabPages()
        {
            for (int i = 0; i < TabCtrl.Items.Count - 1; i++)
            {
                if ((TabCtrl.Items[i] as TabItem)?.Content is WebView2 wv && wv.CoreWebView2 != null)
                {
                    var src = wv.Source?.AbsoluteUri;
                    if (src == null || src == "about:blank")
                        wv.CoreWebView2.NavigateToString(GetNewTabHtml());
                }
            }
        }

        // ── CSS injection helpers ─────────────────────────────────────────────
        private async Task RegisterTransparentBgScript(WebView2 wv)
        {
            try { await wv.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(TransparentBgJs); } catch { }
        }

        private async Task ApplyTransparentBackground(WebView2 wv, bool enable)
        {
            wv.DefaultBackgroundColor = enable ? System.Drawing.Color.Transparent : System.Drawing.Color.White;
            if (wv.CoreWebView2 == null) return;
            if (enable)
                await wv.CoreWebView2.ExecuteScriptAsync(TransparentBgJs);
            else
                await wv.CoreWebView2.ExecuteScriptAsync("(function(){var s=document.getElementById('__trans_bg');if(s)s.remove();})()");
        }

        private async Task ApplyOpacityToWebViewAsync(WebView2 wv, double pct)
        {
            if (wv.CoreWebView2 == null) return;
            // pct: 1..100
            int clamped = (int)Math.Max(1, Math.Min(100, pct));
            double op = clamped / 100.0;
            // Set opacity and ensure background is transparent so underlying WPF shows through
            string js =
                $"(function(){{var d=document.documentElement,s=document.body; if(d){{d.style.opacity='{op}'; d.style.background='transparent';}} if(s){{s.style.opacity='{op}'; s.style.background='transparent';}} }})()";
            try { await wv.CoreWebView2.ExecuteScriptAsync(js); } catch { }
        }

        private async Task ApplyNoImageCss(WebView2 wv, bool enable)
        {
            if (wv.CoreWebView2 == null) return;
            if (enable)
                await wv.CoreWebView2.ExecuteScriptAsync(
                    "(function(){var s=document.getElementById('__trans_noimg');if(!s){s=document.createElement('style');s.id='__trans_noimg';document.head.appendChild(s);}s.textContent='img,picture,video{display:none!important}*{background-image:none!important}';})()");
            else
                await wv.CoreWebView2.ExecuteScriptAsync(
                    "(function(){var s=document.getElementById('__trans_noimg');if(s)s.remove();})()");
        }

        private async Task ApplyGrayscaleCss(WebView2 wv, bool enable)
        {
            if (wv.CoreWebView2 == null) return;
            if (enable)
                await wv.CoreWebView2.ExecuteScriptAsync(
                    "(function(){var s=document.getElementById('__trans_grayscale');if(!s){s=document.createElement('style');s.id='__trans_grayscale';(document.head||document.documentElement).appendChild(s);}s.textContent='html{filter:grayscale(100%)!important;-webkit-filter:grayscale(100%)!important}';})()");
            else
                await wv.CoreWebView2.ExecuteScriptAsync(
                    "(function(){var s=document.getElementById('__trans_grayscale');if(s)s.remove();})()");
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void LoadUrl(string url)
        {
            try
            {
                var wv = ActiveWebView;
                if (wv == null) return;
                if (wv.Source?.AbsoluteUri == url) { wv.Reload(); return; }
                wv.Source = new Uri(url);
            }
            catch (Exception ex) { LogHelper.Error(ex); }
        }

        public void GoBack() => ActiveWebView?.GoBack();
        public void GoForward() => ActiveWebView?.GoForward();

        public void RunJs(string script)
        {
            ActiveWebView?.CoreWebView2?.ExecuteScriptAsync(script);
        }

        public WebView2? GetWebView2() => ActiveWebView;

        public void SetOpacity(double pct)
        {
            // Apply opacity to WPF content container instead of the Window to avoid WS_EX_LAYERED
            try { RootGrid.Opacity = Math.Max(0.01, Math.Min(1.0, pct / 100.0)); } catch { }
            // Don't change layered window alpha (can cause black rendering of WebView2).
            // Instead: set web content opacity for each WebView2 so the whole window appears translucent.
            foreach (var wv in AllWebViews())
            {
                try
                {
                    if (pct < 100)
                        wv.DefaultBackgroundColor = System.Drawing.Color.Transparent;
                    else
                        wv.DefaultBackgroundColor = System.Drawing.Color.White;
                }
                catch { }
                _ = ApplyOpacityToWebViewAsync(wv, pct);
            }

            _s.Current.FormOpacity = pct;
            _s.Save();
        }

        public void SyncOpacity(int pct)
        {
            _settingWindow?.SyncOpacity(pct);
        }

        public void SetTabBarVisible(bool show)
        {
            _s.Current.ShowTabBar = show;
            // Show/hide the tab strip row
            if (TabCtrl.Template?.FindName("HeaderPanel", TabCtrl) is System.Windows.Controls.Primitives.TabPanel panel)
                panel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetShowInTaskbar(bool show)
        {
            _s.Current.ShowInTaskbar = show;
            Dispatcher.InvokeAsync(() => { try { ShowInTaskbar = show; } catch { } });
        }

        public void ApplyThemeColor(System.Drawing.Color c)
        {
            var wpfColor = Color.FromArgb(c.A, c.R, c.G, c.B);
            TitleBar.Background = new SolidColorBrush(wpfColor);
            // Sync button backgrounds
            BtnTopMost.Background = new SolidColorBrush(wpfColor);
            BtnMinimize.Background = new SolidColorBrush(wpfColor);
            BtnClose.Background = new SolidColorBrush(wpfColor);
            double lum = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
            var fg = lum > 0.55 ? Colors.Black : Colors.White;
            TitleText.Foreground = new SolidColorBrush(fg);
            BtnMinimize.Foreground = new SolidColorBrush(fg);
            BtnClose.Foreground = new SolidColorBrush(fg);
            _s.SetThemeBackColor(c);
            UpdateTopMostButton();
        }

        public void SetNoImageMode(bool enable)
        {
            _s.Current.NoImageMode = enable;
            foreach (var wv in AllWebViews()) _ = ApplyNoImageCss(wv, enable);
        }

        public void SetTransparentBackground(bool enable)
        {
            _s.Current.TransparentBackground = enable;
            SetWindowBackgroundTransparent(enable);
            foreach (var wv in AllWebViews()) _ = ApplyTransparentBackground(wv, enable);
        }

        public void SetWindowBackgroundTransparent(bool enable)
        {
            _s.Current.WindowTransparent = enable;
            if (enable)
            {
                // AllowsTransparency cannot be changed after the window is shown.
                // Use WS_EX_LAYERED/WS_EX_TRANSPARENT via P/Invoke for transparency.
                Background = System.Windows.Media.Brushes.Transparent;
            }
            else
            {
                Background = new SolidColorBrush(Colors.White);
            }
        }

        public void SetGrayscaleMode(bool enable)
        {
            _s.Current.GrayscaleMode = enable;
            foreach (var wv in AllWebViews()) _ = ApplyGrayscaleCss(wv, enable);
        }

        public void SetAntiScreenshotMode(bool enable)
        {
            _s.Current.AntiScreenshotMode = enable;
            var hwnd = new WindowInteropHelper(this).Handle;
            bool ok = SetWindowDisplayAffinity(hwnd, enable ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
            if (ok)
            {
                _trayIcon?.ShowBalloonTip(2000, "TransBrowser",
                    enable ? "防截屏模式已开启" : "防截屏模式已关闭",
                    WinForms.ToolTipIcon.Info);
            }
            else if (enable)
            {
                WpfMsgBox.Show("防截屏模式启动失败，需要 Windows 10 2004+", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _s.Current.AntiScreenshotMode = false;
            }
        }

        public void SetWindowShadowDisabled(bool disable)
        {
            _s.Current.DisableWindowShadow = disable;
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int policy = disable ? DWMNCRP_DISABLED : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_NCRENDERING_POLICY, ref policy, sizeof(int));
            }
        }

        public void SetClickThrough(bool enable)
        {
            _clickThrough = enable;
            _s.Current.ClickThroughMode = enable;
            var hwnd = new WindowInteropHelper(this).Handle;
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE,
                enable ? (ex | WS_EX_TRANSPARENT | WS_EX_LAYERED)
                       : (ex & ~WS_EX_TRANSPARENT));
            if (_trayClickThroughItem != null) _trayClickThroughItem.Checked = enable;
            _trayIcon?.ShowBalloonTip(1500, "TransBrowser",
                enable ? "鼠标穿透已开启" : "鼠标穿透已关闭", WinForms.ToolTipIcon.Info);
        }

        public void ToggleClickThrough() => SetClickThrough(!_clickThrough);

        public void SetCustomIcon(string? path)
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    System.Windows.Media.ImageSource? src = null;
                    if (!string.IsNullOrEmpty(path) && File.Exists(path))
                        src = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(path, UriKind.Absolute));

                    if (src == null)
                    {
                        string defPath = Path.Combine(
                            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".",
                            "Resources", "icon.ico");
                        if (File.Exists(defPath))
                            src = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(defPath, UriKind.Absolute));
                    }

                    if (src != null) Icon = src;

                    // Update tray icon
                    if (_trayIcon != null)
                    {
                        try
                        {
                            string p = !string.IsNullOrEmpty(path) && File.Exists(path) ? path :
                                       Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", "Resources", "icon.ico");
                            if (File.Exists(p))
                            {
                                _trayIcon.Icon = new System.Drawing.Icon(p);
                                _trayIcon.Visible = false;
                                _trayIcon.Visible = true;
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            });
        }

        public void SetUA(string ua)
        {
            var wv = ActiveWebView;
            if (wv?.CoreWebView2 != null)
                wv.CoreWebView2.Settings.UserAgent = ua;
        }

        public void SetMobileMold(bool enable)
        {
            _mobileMode = enable;
            _s.Current.MobileMold = enable;

            if (enable)
            {
                if (!_hadSavedBounds)
                {
                    _savedLeft = Left; _savedTop = Top;
                    _savedWidth = Width; _savedHeight = Height;
                    _savedShowTabBar = _s.Current.ShowTabBar;
                    _hadSavedBounds = true;
                }
                Width = 390; Height = 844;
                var area = System.Windows.SystemParameters.WorkArea;
                Left = area.Left + (area.Width - 390) / 2;
                Top = area.Top + (area.Height - 844) / 2;
                SetTabBarVisible(false);

                foreach (var wv in AllWebViews())
                {
                    if (wv.CoreWebView2 != null)
                    {
                        wv.CoreWebView2.Settings.UserAgent = MobileUA;
                        wv.Reload();
                    }
                }
            }
            else
            {
                if (_hadSavedBounds)
                {
                    Left = _savedLeft; Top = _savedTop;
                    Width = _savedWidth; Height = _savedHeight;
                    SetTabBarVisible(_savedShowTabBar);
                    _hadSavedBounds = false;
                }

                string defUa = _s.Current.DefaultUA;
                foreach (var wv in AllWebViews())
                {
                    if (wv.CoreWebView2 != null)
                    {
                        wv.CoreWebView2.Settings.UserAgent = defUa ?? "";
                        wv.Reload();
                    }
                }
            }
        }

        public void ApplyHoverHeaderMode(bool enable)
        {
            _headerHideTimer?.Stop();
            _headerPollTimer?.Stop();

            if (enable)
            {
                TitleBar.Visibility = Visibility.Collapsed;
                _headerVisible = false;
                _headerPollTimer?.Start();
            }
            else
            {
                TitleBar.Visibility = Visibility.Visible;
                _headerVisible = true;
            }
            _s.Current.HoverHeaderMode = enable;
        }

        // ── Floating header timers ────────────────────────────────────────────
        private void InitFloatingHeader()
        {
            _headerHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _headerHideTimer.Tick += (_, _) =>
            {
                _headerHideTimer.Stop();
                var pt = PointFromScreen(GetCursorPos());
                if (pt.Y >= HEADER_HOVER_HEIGHT)
                {
                    TitleBar.Visibility = Visibility.Collapsed;
                    _headerVisible = false;
                }
            };

            _headerPollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _headerPollTimer.Tick += (_, _) =>
            {
                var pt = PointFromScreen(GetCursorPos());
                bool inside = pt.X >= 0 && pt.X < ActualWidth && pt.Y >= 0 && pt.Y < ActualHeight;

                if (inside && pt.Y < HEADER_HOVER_HEIGHT)
                {
                    _headerHideTimer?.Stop();
                    if (!_headerVisible)
                    {
                        TitleBar.Visibility = Visibility.Visible;
                        _headerVisible = true;
                    }
                }
                else if (_headerVisible)
                {
                    if (!(_headerHideTimer?.IsEnabled ?? false))
                        _headerHideTimer?.Start();
                }
            };

            ApplyHoverHeaderMode(_s.Current.HoverHeaderMode);
        }

        [DllImport("user32.dll")]
        static extern bool GetCursorPos(out POINT lpPoint);
        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        private Point GetCursorPos()
        {
            GetCursorPos(out POINT p);
            return new Point(p.X, p.Y);
        }

        // ── Window chrome buttons ─────────────────────────────────────────────
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void BtnTopMost_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            _s.Current.TopMostWindow = Topmost;
            _s.Save();
            UpdateTopMostButton();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            ExitApp();
        }

        private void BtnClose_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            BtnClose.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35));
            BtnClose.Foreground = System.Windows.Media.Brushes.White;
        }

        private void BtnClose_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            BtnClose.Background = TitleBar.Background;
            var bg = _s.GetThemeBackColor();
            double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
            BtnClose.Foreground = new SolidColorBrush(lum > 0.55 ? Colors.Black : Colors.White);
        }

        private void UpdateTopMostButton()
        {
            var bg = _s.GetThemeBackColor();
            double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;

            if (Topmost)
            {
                BtnTopMost.Foreground = new SolidColorBrush(
                    lum > 0.55 ? Color.FromRgb(24, 144, 255) : Color.FromRgb(255, 185, 50));
            }
            else
            {
                var fg = lum > 0.55 ? Colors.Black : Colors.White;
                BtnTopMost.Foreground = new SolidColorBrush(
                    Color.FromArgb(140, fg.R, fg.G, fg.B));
            }
        }

        // ── Window event handlers ─────────────────────────────────────────────
        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e) { /* polling handles hover */ }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (_inited)
            {
                _s.SetFormPosition(Left, Top);
                _s.Save();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_inited)
            {
                _s.SetFormSize(ActualWidth, ActualHeight);
                _s.Save();
            }
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                if (_s.Current.ShowMinimizeNotification)
                    _trayIcon?.ShowBalloonTip(1000, "TransBrowser", "已最小化", WinForms.ToolTipIcon.Info);
            }
            else if (WindowState == WindowState.Normal)
            {
                SetShowInTaskbar(_s.Current.ShowInTaskbar);
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (Topmost) return;
            if (_s.Current.AutoHide)
                Hide();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowExit)
            {
                if (_s.Current.CloseToTray)
                {
                    e.Cancel = true;
                    PauseAllWebViews();
                    Hide();
                }
                else
                {
                    DoCleanup();
                }
            }
            else
            {
                DoCleanup();
            }
        }

        private void DoCleanup()
        {
            UnregisterAllHotkeys();
            if (_trayIcon != null) _trayIcon.Visible = false;
        }

        // ── Tray / show-hide ──────────────────────────────────────────────────
        private void ShowMainWindow()
        {
            bool show = _s.Current.ShowInTaskbar;
            if (!IsVisible) Show();
            WindowState = WindowState.Normal;
            Dispatcher.InvokeAsync(() => { try { ShowInTaskbar = show; } catch { } });
            Activate();
            ResumeAllWebViews();
        }

        public void OpenSettings()
        {
            if (_settingWindow != null && _settingWindow.IsVisible)
            {
                _settingWindow.Activate();
                return;
            }
            _settingWindow = new SettingWindow(this);
            _settingWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _settingWindow.Closed += (_, _) => _settingWindow = null;
            _settingWindow.Show();
        }

        public void OpenControlPanel()
        {
            _controlPanel ??= new ControlPanelWindow(this);
            _controlPanel.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _controlPanel.Show();
            _controlPanel.Activate();
        }

        public void ExitApp()
        {
            if (WindowState == WindowState.Normal)
            {
                _s.SetFormPosition(Left, Top);
                _s.SetFormSize(ActualWidth, ActualHeight);
            }
            // Store the logical form opacity from RootGrid (not Window.Opacity)
            try { _s.Current.FormOpacity = RootGrid.Opacity * 100.0; } catch { _s.Current.FormOpacity = 100; }
            _s.Save();
            _allowExit = true;
            Close();
            System.Windows.Application.Current.Shutdown();
        }

        // ── Hotkeys ───────────────────────────────────────────────────────────
        private IntPtr GetHwnd() => new WindowInteropHelper(this).Handle;

        private void RegisterAllHotkeys()
        {
            var hwnd = GetHwnd();
            RegisterHotKey(hwnd, (int)HotkeyId.LegacyToggleShow, KeyModifiers.Alt, WinForms.Keys.D);
            RegisterHotKey(hwnd, (int)HotkeyId.LegacyToggleTop, KeyModifiers.Alt, WinForms.Keys.F);
            RegisterHotKey(hwnd, (int)HotkeyId.LegacyGoBack, KeyModifiers.Alt | KeyModifiers.Shift, WinForms.Keys.Z);
            RegisterHotKey(hwnd, (int)HotkeyId.LegacyRunJs, KeyModifiers.Alt | KeyModifiers.Shift, WinForms.Keys.C);
            RegisterHotKey(hwnd, (int)HotkeyId.OpacityReset, KeyModifiers.Alt, WinForms.Keys.Up);
            RegisterHotKey(hwnd, (int)HotkeyId.OpacityDown, KeyModifiers.Alt, WinForms.Keys.Left);
            RegisterHotKey(hwnd, (int)HotkeyId.OpacityUp, KeyModifiers.Alt, WinForms.Keys.Right);
            RegisterHotkeyFromSetting((int)HotkeyId.BossKey, _s.Current.HotkeyBossKey);
            RegisterHotkeyFromSetting((int)HotkeyId.ClickThrough, _s.Current.HotkeyClickThrough);
        }

        private void UnregisterAllHotkeys()
        {
            var hwnd = GetHwnd();
            foreach (HotkeyId id in Enum.GetValues(typeof(HotkeyId)))
                UnregisterHotKey(hwnd, (int)id);
        }

        private void RegisterHotkeyFromSetting(int id, string hotkeyStr)
        {
            if (HotkeyParser.TryParse(hotkeyStr, out KeyModifiers mods, out WinForms.Keys vk))
                RegisterHotKey(GetHwnd(), id, mods, vk);
        }

        public void ReRegisterConfigurableHotkeys()
        {
            var hwnd = GetHwnd();
            UnregisterHotKey(hwnd, (int)HotkeyId.BossKey);
            UnregisterHotKey(hwnd, (int)HotkeyId.ClickThrough);
            RegisterHotkeyFromSetting((int)HotkeyId.BossKey, _s.Current.HotkeyBossKey);
            RegisterHotkeyFromSetting((int)HotkeyId.ClickThrough, _s.Current.HotkeyClickThrough);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                HandleHotkey((int)wParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void HandleHotkey(int id)
        {
            switch ((HotkeyId)id)
            {
                case HotkeyId.LegacyToggleShow:
                case HotkeyId.BossKey:
                    if (IsVisible && WindowState != WindowState.Minimized)
                    {
                        PauseAllWebViews();
                        Hide();
                        Dispatcher.InvokeAsync(() => { try { ShowInTaskbar = false; } catch { } });
                    }
                    else { ShowMainWindow(); }
                    break;
                case HotkeyId.LegacyToggleTop:
                    Topmost = !Topmost; UpdateTopMostButton(); break;
                case HotkeyId.LegacyGoBack:
                    GoBack(); break;
                case HotkeyId.LegacyRunJs:
                    _controlPanel?.RunCurrentJs(); break;
                case HotkeyId.OpacityUp:
                    AdjustOpacity(+5); break;
                case HotkeyId.OpacityDown:
                    AdjustOpacity(-5); break;
                case HotkeyId.OpacityReset:
                    double cur = _s.Current.FormOpacity;
                    if (cur != 100) { _lastOpacityBeforeReset = cur; SetOpacity(100); SyncOpacity(100); }
                    else { double r = _lastOpacityBeforeReset > 0 ? _lastOpacityBeforeReset : 100; SetOpacity(r); SyncOpacity((int)r); _lastOpacityBeforeReset = -1; }
                    break;
                case HotkeyId.ClickThrough:
                    ToggleClickThrough(); break;
            }
        }

        private void AdjustOpacity(int delta)
        {
            double cur = (RootGrid?.Opacity ?? 1.0) * 100.0;
            double nv = Math.Max(1, Math.Min(100, cur + delta));
            SetOpacity(nv);
            SyncOpacity((int)nv);
        }

        // ── Media pause/resume ────────────────────────────────────────────────
        private void PauseAllWebViews()
        {
            const string js = "(function(){document.querySelectorAll('video,audio').forEach(function(m){if(!m.paused){m.pause();m.setAttribute('data-was-playing','1');}});})();";
            foreach (var wv in AllWebViews())
                try { wv.CoreWebView2?.ExecuteScriptAsync(js); } catch { }
        }

        private void ResumeAllWebViews()
        {
            const string js = "(function(){document.querySelectorAll('video,audio').forEach(function(m){if(m.getAttribute('data-was-playing')==='1'){m.play();m.removeAttribute('data-was-playing');}});})();";
            foreach (var wv in AllWebViews())
                try { wv.CoreWebView2?.ExecuteScriptAsync(js); } catch { }
        }

        private IEnumerable<WebView2> AllWebViews()
        {
            for (int i = 0; i < TabCtrl.Items.Count - 1; i++)
                if ((TabCtrl.Items[i] as TabItem)?.Content is WebView2 wv) yield return wv;
        }

        // ── History & custom sites ────────────────────────────────────────────
        private List<CustomSite> LoadCustomSites()
        {
            var list = new List<CustomSite>();
            string raw = _s.Current.CustomSites ?? "";
            if (string.IsNullOrWhiteSpace(raw) || raw == "[]") return list;
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                int t = line.IndexOf('\t');
                if (t >= 0) list.Add(new CustomSite { Name = line[..t], Url = line[(t + 1)..].Trim() });
            }
            return list;
        }

        private void SaveCustomSites(List<CustomSite> sites)
        {
            var sb = new StringBuilder();
            foreach (var s in sites) sb.Append(s.Name.Replace('\t', ' ')).Append('\t').Append(s.Url).Append('\n');
            _s.Current.CustomSites = sb.ToString();
            _s.Save();
        }

        private List<HistoryItem> LoadHistory()
        {
            var list = new List<HistoryItem>();
            string raw = _s.Current.BrowsingHistory ?? "";
            foreach (var line in raw.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Split('\t');
                if (p.Length >= 3 && DateTime.TryParse(p[2], out DateTime dt))
                    list.Add(new HistoryItem { Title = p[0], Url = p[1], VisitTime = dt });
            }
            return list.OrderByDescending(h => h.VisitTime).Take(MaxHistoryItems).ToList();
        }

        private void SaveHistory(List<HistoryItem> history)
        {
            var sb = new StringBuilder();
            foreach (var h in history.Take(MaxHistoryItems))
                sb.Append(h.Title.Replace('\t', ' ')).Append('\t').Append(h.Url).Append('\t').Append(h.VisitTime.ToString("o")).Append('\n');
            _s.Current.BrowsingHistory = sb.ToString();
            _s.Save();
        }

        private void AddToHistory(string title, string url)
        {
            if (string.IsNullOrEmpty(url) || url == "about:blank") return;
            if (url.StartsWith("data:") || url.StartsWith("javascript:")) return;
            var h = LoadHistory();
            h.RemoveAll(x => x.Url == url);
            h.Insert(0, new HistoryItem { Title = string.IsNullOrEmpty(title) ? url : title, Url = url, VisitTime = DateTime.Now });
            SaveHistory(h);
        }

        // ── New tab HTML ──────────────────────────────────────────────────────
        private string GetNewTabHtml()
        {
            var sites = LoadCustomSites();
            var history = LoadHistory();

            string customJson = BuildSitesJson(sites);
            string historyJson = BuildHistoryJson(history);

            string templatePath = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".",
                "Resources", "newtab.html");

            string html = File.Exists(templatePath)
                ? File.ReadAllText(templatePath, Encoding.UTF8)
                : "<!DOCTYPE html><html><body><h1>新标签页</h1></body></html>";

            return html.Replace("##CUSTOM_DATA##", customJson).Replace("##HISTORY_DATA##", historyJson);
        }

        private const int MaxHistoryItems = 50;

        private static string BuildSitesJson(List<CustomSite> sites)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < sites.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"n\":\"{JsEscape(sites[i].Name)}\",\"u\":\"{JsEscape(sites[i].Url)}\"}}");
            }
            return sb.Append("]").ToString();
        }

        private static string BuildHistoryJson(List<HistoryItem> history)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < history.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append($"{{\"t\":\"{JsEscape(history[i].Title)}\",\"u\":\"{JsEscape(history[i].Url)}\"}}");
            }
            return sb.Append("]").ToString();
        }

        private static string JsEscape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("'", "\\'")
                    .Replace("`", "\\`")
                    .Replace("/", "\\/")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t")
                    .Replace("\u2028", "\\u2028")
                    .Replace("\u2029", "\\u2029");
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T t) return t;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }
    }
}
