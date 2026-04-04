using AntdUI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using static TransBrowser.Tools.GlobalHotkey;

namespace TransBrowser
{
    public partial class MainForm : Window
    {
        public bool inited = false;

        // ─── Floating header state ────────────────────────────────────────────
        private const int HEADER_HOVER_HEIGHT = 28;
        private bool _headerVisible = true;

        // ─── P/Invoke for click-through ───────────────────────────────────────
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_LAYERED = 0x00080000;

        // ─── WM_NCHITTEST constants ────────────────────────────────────────────
        private const int WM_NCHITTEST = 0x0084;
        private const int WM_HOTKEY = 0x0312;
        private const int HTCLIENT = 1;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        // ─── Resize border size in pixels ─────────────────────────────────────
        private const int ResizeBorder = 8;

        private const int HTCAPTION = 2;
        private const int TOPMOST_BTN_OFFSET = 35; // pixels from right edge for TopMost button

        // ─── Click-through state ───────────────────────────────────────────────
        private bool _clickThrough = false;

        // ─── Prevent duplicate settings windows ───────────────────────────────
        private Setting _settingForm = null;

        // ─── Floating header timer ────────────────────────────────────────────
        private System.Windows.Forms.Timer _headerHideTimer;
        // Polls cursor position every 50 ms – works even when WebView2 captures the mouse
        private System.Windows.Forms.Timer _headerHoverPollTimer;

        // ─── Hotkey IDs ────────────────────────────────────────────────────────
        private enum HotkeyId
        {
            LegacyToggleShow = 100,
            LegacyToggleTop = 101,
            LegacyGoBack = 102,
            LegacyGoForward = 103,
            LegacyRunJs = 104,
            BossKey = 200,
            OpacityUp = 201,
            OpacityDown = 202,
            ClickThrough = 203
        }

        // ─── Multi-tab state ───────────────────────────────────────────────────
        /// <summary>Returns the WebView2 in the currently selected tab (never the "+" sentinel tab).</summary>
        private WebView2 ActiveWebView
        {
            get
            {
                int idx = tabControl1.SelectedIndex;
                // If the "+" sentinel tab is selected, fall back to first tab
                if (idx < 0 || idx >= tabControl1.TabPages.Count - 1)
                    idx = 0;
                return GetTabWebView(tabControl1.TabPages[idx]);
            }
        }

        private static WebView2 GetTabWebView(System.Windows.Forms.TabPage page)
        {
            foreach (Control c in page.Controls)
                if (c is WebView2 wv) return wv;
            return null;
        }

        // ─── Custom site management (#3/#4) ──────────────────────────────────

        private class CustomSite
        {
            public string Name { get; set; }
            public string Url { get; set; }
        }

        private List<CustomSite> LoadCustomSites()
        {
            var list = new List<CustomSite>();
            string raw = Properties.Settings.Default.CustomSites ?? "";
            if (raw == "[]" || string.IsNullOrWhiteSpace(raw)) return list;
            foreach (string line in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                int tab = line.IndexOf('\t');
                if (tab >= 0)
                    list.Add(new CustomSite { Name = line.Substring(0, tab), Url = line.Substring(tab + 1).Trim() });
            }
            return list;
        }

        private void SaveCustomSites(List<CustomSite> sites)
        {
            var sb = new StringBuilder();
            foreach (var s in sites)
            {
                string name = (s.Name ?? "").Replace("\t", " ").Replace("\n", " ");
                string url = (s.Url ?? "").Replace("\t", "").Replace("\n", "");
                sb.Append(name).Append('\t').Append(url).Append('\n');
            }
            Properties.Settings.Default.CustomSites = sb.ToString();
            Properties.Settings.Default.Save();
        }

        private void RefreshNewTabPages()
        {
            for (int i = 0; i < tabControl1.TabPages.Count - 1; i++)
            {
                var wv = GetTabWebView(tabControl1.TabPages[i]);
                if (wv?.CoreWebView2 != null)
                {
                    var src = wv.Source?.AbsoluteUri;
                    if (src == null || src == "about:blank")
                        wv.CoreWebView2.NavigateToString(GetNewTabHtml());
                }
            }
        }

        // ─── Constructor ──────────────────────────────────────────────────────
        public MainForm()
        {
            InitializeComponent();
            // #6: Disable default window shadow
            this.Shadow = 0;
            // Start async WebView2 init for the first tab
            InitializeWebView();
        }

        // ─── Form Load ────────────────────────────────────────────────────────
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Restore settings
            Init();

            // Hook form events
            this.ResizeEnd += MainForm_ResizeEnd;
            this.LocationChanged += MainForm_LocationChanged;
            this.Deactivate += Form1_Deactivate;

            // Register global hotkeys
            RegisterAllHotkeys();

            // Set up floating header behavior
            InitFloatingHeader();
        }

        // ─── First-tab WebView2 async init ────────────────────────────────────
        private async void InitializeWebView()
        {
            await webView21.EnsureCoreWebView2Async(null);
            SetupWebViewEvents(webView21);
            tabPageFirst.Text = "新标签页";
            string startUrl = Properties.Settings.Default.DefaultUrl;
            if (string.IsNullOrEmpty(startUrl))
                webView21.CoreWebView2.NavigateToString(GetNewTabHtml());
        }

        // ─── Settings restoration ─────────────────────────────────────────────
        public void Init()
        {
            // Always start with floating header (no explicit "无窗口" toggle)
            ShowWindowsBar(false);
            SetShowInTaskBar(Properties.Settings.Default.ShowInTaskbar);
            SetPosition(Properties.Settings.Default.FormPosition);
            SetTans(Properties.Settings.Default.FormOpacity);
            SetDefaultColor(Properties.Settings.Default.ThemeBackColor);
            SetSize(Properties.Settings.Default.FormSize);

            // Restore click-through state
            if (Properties.Settings.Default.ClickThroughMode)
                SetClickThrough(true);

            // Update tray click-through menu item
            trayClickThroughMenuItem.Checked = Properties.Settings.Default.ClickThroughMode;

            string startUrl = Properties.Settings.Default.DefaultUrl;
            if (!string.IsNullOrEmpty(startUrl))
                LoadUrl(startUrl);
            // else: InitializeWebView will show custom new tab page

            // Restore new settings
            SetTabBarVisible(Properties.Settings.Default.ShowTabBar);
            this.TopMost = Properties.Settings.Default.TopMostWindow;
            UpdateTopMostButton();

            inited = true;
        }

        // ─── Tab management ───────────────────────────────────────────────────

        private System.Windows.Forms.TabPage AddNewTab(string url = null)
        {
            int insertIndex = tabControl1.TabPages.Count - 1; // before the "+" tab
            var page = new System.Windows.Forms.TabPage("新标签页");
            page.Padding = System.Windows.Forms.Padding.Empty;   // no inner whitespace
            var wv = new WebView2();
            wv.Dock = DockStyle.Fill;
            wv.DefaultBackgroundColor = Color.White;
            wv.CreationProperties = null;
            page.Controls.Add(wv);

            tabControl1.TabPages.Insert(insertIndex, page);
            tabControl1.SelectedTab = page;

            SetupNewWebView(wv, url); // null = show custom new-tab page
            return page;
        }

        private async void SetupNewWebView(WebView2 wv, string url)
        {
            await wv.EnsureCoreWebView2Async(null);
            SetupWebViewEvents(wv);
            if (!string.IsNullOrEmpty(url))
                wv.CoreWebView2.Navigate(url);
            else
                wv.CoreWebView2.NavigateToString(GetNewTabHtml());
        }

        private void SetupWebViewEvents(WebView2 wv)
        {
            wv.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;
            wv.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;

            // Use lambda so the compiler picks the right delegate type automatically
            wv.CoreWebView2.DocumentTitleChanged += (s, _) => UpdateTabTitle(s as CoreWebView2);

            wv.NavigationCompleted -= Wv_NavigationCompleted;
            wv.NavigationCompleted += Wv_NavigationCompleted;

            // Custom context menu (right-click)
            wv.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
            wv.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;

            // Handle messages from new-tab page (add/remove custom sites)
            wv.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
            wv.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string msg = e.TryGetWebMessageAsString();
            if (string.IsNullOrEmpty(msg)) return;

            // Protocol: "add\tname\turl" or "remove\turl"
            var parts = msg.Split('\t');
            if (parts.Length < 2) return;

            var sites = LoadCustomSites();

            if (parts[0] == "add" && parts.Length >= 3)
            {
                string name = parts[1].Trim();
                string url = parts[2].Trim();
                if (string.IsNullOrEmpty(url)) return;
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;
                if (string.IsNullOrEmpty(name)) name = url;
                sites.Add(new CustomSite { Name = name, Url = url });
                SaveCustomSites(sites);
                this.BeginInvoke((MethodInvoker)RefreshNewTabPages);
            }
            else if (parts[0] == "remove" && parts.Length >= 2)
            {
                string url = parts[1].Trim();
                sites.RemoveAll(s => s.Url == url);
                SaveCustomSites(sites);
                this.BeginInvoke((MethodInvoker)RefreshNewTabPages);
            }
        }

        private void UpdateTabTitle(CoreWebView2 core)
        {
            if (core == null) return;
            for (int i = 0; i < tabControl1.TabPages.Count - 1; i++)
            {
                var wv = GetTabWebView(tabControl1.TabPages[i]);
                if (wv != null && wv.CoreWebView2 == core)
                {
                    string title = string.IsNullOrEmpty(core.DocumentTitle) ? "新标签页" : core.DocumentTitle;
                    if (title.Length > 20) title = title.Substring(0, 18) + "…";
                    int capturedI = i;
                    this.BeginInvoke((MethodInvoker)(() => tabControl1.TabPages[capturedI].Text = title));
                    break;
                }
            }
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Last tab is the "+" sentinel – create a new tab when clicked
            if (tabControl1.SelectedIndex == tabControl1.TabPages.Count - 1)
                AddNewTab();
        }

        private void tabControl1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Middle && e.Button != MouseButtons.Right)
                return;

            for (int i = 0; i < tabControl1.TabPages.Count - 1; i++) // skip "+" tab
            {
                if (tabControl1.GetTabRect(i).Contains(e.Location))
                {
                    if (e.Button == MouseButtons.Middle)
                    {
                        CloseTab(i);
                    }
                    else // right-click
                    {
                        var menu = new System.Windows.Forms.ContextMenuStrip();
                        int capturedI = i;
                        menu.Items.Add("关闭标签", null, (s, _) => CloseTab(capturedI));
                        menu.Items.Add("在新标签页中打开", null, (s, _) => AddNewTab());
                        menu.Show(tabControl1, e.Location);
                    }
                    break;
                }
            }
        }

        private void CloseTab(int index)
        {
            if (tabControl1.TabPages.Count <= 2) // only one real tab + "+" tab
                return; // don't close the last real tab

            var page = tabControl1.TabPages[index];
            var wv = GetTabWebView(page);
            if (wv != null)
            {
                wv.Dispose();
            }
            tabControl1.TabPages.RemoveAt(index);

            // Ensure we're not sitting on the "+" tab after removal
            if (tabControl1.SelectedIndex >= tabControl1.TabPages.Count - 1)
                tabControl1.SelectedIndex = tabControl1.TabPages.Count - 2;
        }

        // ─── WebView2 events ──────────────────────────────────────────────────

        private void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            // Open requested URL in a new tab
            AddNewTab(e.Uri);
        }

        private void Wv_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            var wv = sender as WebView2;
            if (wv == null) return;
            // Persist URL only for the active tab
            if (wv == ActiveWebView && wv.Source != null)
            {
                Properties.Settings.Default.DefaultUrl = wv.Source.AbsoluteUri;
                Properties.Settings.Default.Save();
            }
            if (Properties.Settings.Default.NoImageMode)
                ApplyNoImageCss(wv, true);
        }

        // ─── Custom right-click context menu ──────────────────────────────────

        private void CoreWebView2_ContextMenuRequested(object sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            var wv = sender as CoreWebView2;
            if (wv == null) return;

            var items = e.MenuItems;

            // Add separator
            var separator = wv.Environment.CreateContextMenuItem(
                string.Empty, null, CoreWebView2ContextMenuItemKind.Separator);
            items.Add(separator);

            // "Copy link" – only enabled when a link is in context
            if (!string.IsNullOrEmpty(e.ContextMenuTarget.LinkUri))
            {
                var copyLinkItem = wv.Environment.CreateContextMenuItem(
                    "复制链接", null, CoreWebView2ContextMenuItemKind.Command);
                string linkUri = e.ContextMenuTarget.LinkUri;
                copyLinkItem.CustomItemSelected += (s, _) => Clipboard.SetText(linkUri);
                items.Insert(0, copyLinkItem);
            }

            // "DevTools"
            var devToolsItem = wv.Environment.CreateContextMenuItem(
                "开发者工具", null, CoreWebView2ContextMenuItemKind.Command);
            devToolsItem.CustomItemSelected += (s, _) => wv.OpenDevToolsWindow();
            items.Add(devToolsItem);
        }

        // ─── Public API (used by Setting / ControlPanel) ──────────────────────

        public void LoadUrl(string url)
        {
            try
            {
                var wv = ActiveWebView;
                if (wv == null) return;
                if (wv.Source != null && url == wv.Source.AbsoluteUri)
                {
                    wv.Reload();
                    return;
                }
                wv.Source = new Uri(url);
            }
            catch (Exception ex)
            {
                Tools.LogHelper.Error(ex);
            }
        }

        public void SetTans(double trans)
        {
            trans = Math.Round(trans / 100.0, 2);
            this.Opacity = trans;
        }

        public void SetDefaultColor(Color color)
        {
            this.pageHeader1.BackColor = color;
            // Recompute pin-button contrast colour for the new background
            UpdateTopMostButton();
        }

        public void SetShowInTaskBar(bool show)
        {
            this.ShowInTaskbar = show;
        }

        public void SetSize(Size size)
        {
            this.Size = size;
        }

        public void ShowWindowsBar(bool noTitle)
        {
            if (noTitle)
            {
                this.pageHeader1.Visible = false;
                _headerVisible = false;
                if (_headerHideTimer != null) _headerHideTimer.Stop();
            }
            else
            {
                this.pageHeader1.Visible = true;
                _headerVisible = true;
            }
        }

        public void SetPosition(Point point)
        {
            this.Location = point;
        }

        public void SetMobileMold(bool mobileMold) { }

        public void SetUA(string UA)
        {
            if (string.IsNullOrEmpty(UA)) return;
            var wv = ActiveWebView;
            if (wv?.CoreWebView2 != null)
                wv.CoreWebView2.Settings.UserAgent = UA;
        }

        public void SetTabBarVisible(bool show)
        {
            if (show)
            {
                tabControl1.Appearance = System.Windows.Forms.TabAppearance.Normal;
                tabControl1.SizeMode = System.Windows.Forms.TabSizeMode.Normal;
                tabControl1.ItemSize = new System.Drawing.Size(0, 0);
            }
            else
            {
                tabControl1.Appearance = System.Windows.Forms.TabAppearance.FlatButtons;
                tabControl1.SizeMode = System.Windows.Forms.TabSizeMode.Fixed;
                tabControl1.ItemSize = new System.Drawing.Size(0, 1);
            }
        }

        public void SetNoImageMode(bool enable)
        {
            for (int i = 0; i < tabControl1.TabPages.Count - 1; i++)
            {
                var wv = GetTabWebView(tabControl1.TabPages[i]);
                if (wv?.CoreWebView2 != null)
                    ApplyNoImageCss(wv, enable);
            }
        }

        private async void ApplyNoImageCss(Microsoft.Web.WebView2.WinForms.WebView2 wv, bool enable)
        {
            if (enable)
            {
                string css = "img,picture,video{display:none!important}*{background-image:none!important}";
                string js = $"(function(){{var s=document.getElementById('__trans_noimg');if(!s){{s=document.createElement('style');s.id='__trans_noimg';document.head.appendChild(s);}}s.textContent='{css}';}})()";
                await wv.CoreWebView2.ExecuteScriptAsync(js);
            }
            else
            {
                string js = "(function(){var s=document.getElementById('__trans_noimg');if(s)s.remove();})()";
                await wv.CoreWebView2.ExecuteScriptAsync(js);
            }
        }

        public void GoBack()
        {
            ActiveWebView?.GoBack();
        }

        public void GoForward()
        {
            ActiveWebView?.GoForward();
        }

        public void RunJs(string script)
        {
            ActiveWebView?.ExecuteScriptAsync(script);
        }

        /// <summary>Returns the WebView2 of the active tab (legacy callers).</summary>
        public WebView2 GetWebView2()
        {
            return ActiveWebView ?? webView21;
        }

        // ─── Click-through mode ────────────────────────────────────────────────

        public void SetClickThrough(bool enabled)
        {
            _clickThrough = enabled;
            Properties.Settings.Default.ClickThroughMode = enabled;
            Properties.Settings.Default.Save();

            int exStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
            if (enabled)
                SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED);
            else
                SetWindowLong(this.Handle, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);

            trayClickThroughMenuItem.Checked = enabled;

            notifyIcon1.ShowBalloonTip(
                1500, "TransBrowser",
                enabled ? "鼠标穿透已开启 – 窗口不接收鼠标事件" : "鼠标穿透已关闭 – 恢复正常交互",
                ToolTipIcon.Info);
        }

        public void ToggleClickThrough()
        {
            SetClickThrough(!_clickThrough);
        }

        // ─── Tray ──────────────────────────────────────────────────────────────

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ShowMainWindow();
        }

        private void ShowMainWindow()
        {
            if (!this.Visible)
                this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void trayShowHideMenuItem_Click(object sender, EventArgs e)
        {
            if (this.Visible)
                this.Hide();
            else
                ShowMainWindow();
        }

        private void trayClickThroughMenuItem_Click(object sender, EventArgs e)
        {
            ToggleClickThrough();
        }

        private void traySettingsMenuItem_Click(object sender, EventArgs e)
        {
            OpenSettings();
        }

        private void trayExitMenuItem_Click(object sender, EventArgs e)
        {
            ExitApp();
        }

        // ─── Minimize to tray ─────────────────────────────────────────────────

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.Hide();
                notifyIcon1.ShowBalloonTip(1000, "TransBrowser", "已最小化到托盘，双击图标恢复", ToolTipIcon.Info);
            }
            UpdateTopMostButtonPosition();
        }

        // ─── Menu handlers (legacy) ───────────────────────────────────────────

        private void 设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenSettings();
        }

        private void OpenSettings()
        {
            if (_settingForm != null && !_settingForm.IsDisposed)
            {
                _settingForm.Activate();
                _settingForm.BringToFront();
                return;
            }
            _settingForm = new Setting(this);
            _settingForm.StartPosition = FormStartPosition.CenterScreen;
            _settingForm.FormClosed += (s, args) => _settingForm = null;
            _settingForm.Show();
        }

        private void 退出ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExitApp();
        }

        private void ExitApp()
        {
            UnregisterAllHotkeys();
            notifyIcon1.Visible = false;
            System.Environment.Exit(0);
        }

        private void 控制器ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            control.setMainForm(this);
            control.StartPosition = FormStartPosition.CenterScreen;
            control.Show();
        }

        ControlPanel control = new ControlPanel();

        // ─── Location / resize persistence ────────────────────────────────────

        private void MainForm_LocationChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal && inited)
            {
                Properties.Settings.Default.FormPosition = this.Location;
                Properties.Settings.Default.Save();
            }
        }

        private void MainForm_ResizeEnd(object sender, EventArgs e)
        {
            var size = ((System.Windows.Forms.Control)sender).Size;
            Properties.Settings.Default.FormSize = size;
            Properties.Settings.Default.Save();
        }

        // ─── Auto-hide on focus loss ───────────────────────────────────────────

        private void Form1_Deactivate(object sender, EventArgs e)
        {
            if (this.TopMost) return;
            if (Properties.Settings.Default.AutoHide)
                this.Hide();
        }

        // ─── Global hotkeys ───────────────────────────────────────────────────

        private void RegisterAllHotkeys()
        {
            // Legacy hotkeys (kept for backward compat)
            RegisterHotKey(this.Handle, (int)HotkeyId.LegacyToggleShow, KeyModifiers.Alt, Keys.D);
            RegisterHotKey(this.Handle, (int)HotkeyId.LegacyToggleTop, KeyModifiers.Alt, Keys.F);
            RegisterHotKey(this.Handle, (int)HotkeyId.LegacyGoBack, KeyModifiers.Alt | KeyModifiers.Shift, Keys.Z);
            RegisterHotKey(this.Handle, (int)HotkeyId.LegacyRunJs, KeyModifiers.Alt | KeyModifiers.Shift, Keys.C);

            // New configurable hotkeys
            RegisterHotkeyFromSetting((int)HotkeyId.BossKey, Properties.Settings.Default.HotkeyBossKey);
            RegisterHotkeyFromSetting((int)HotkeyId.OpacityUp, Properties.Settings.Default.HotkeyOpacityUp);
            RegisterHotkeyFromSetting((int)HotkeyId.OpacityDown, Properties.Settings.Default.HotkeyOpacityDown);
            RegisterHotkeyFromSetting((int)HotkeyId.ClickThrough, Properties.Settings.Default.HotkeyClickThrough);
        }

        private void UnregisterAllHotkeys()
        {
            foreach (HotkeyId id in Enum.GetValues(typeof(HotkeyId)))
                UnregisterHotKey(this.Handle, (int)id);
        }

        private void RegisterHotkeyFromSetting(int id, string hotkeyStr)
        {
            if (Tools.HotkeyParser.TryParse(hotkeyStr, out KeyModifiers mods, out Keys vk))
                RegisterHotKey(this.Handle, id, mods, vk);
        }

        /// <summary>
        /// Re-registers configurable hotkeys after settings change.
        /// </summary>
        public void ReRegisterConfigurableHotkeys()
        {
            // Unregister configurable ones first
            UnregisterHotKey(this.Handle, (int)HotkeyId.BossKey);
            UnregisterHotKey(this.Handle, (int)HotkeyId.OpacityUp);
            UnregisterHotKey(this.Handle, (int)HotkeyId.OpacityDown);
            UnregisterHotKey(this.Handle, (int)HotkeyId.ClickThrough);

            RegisterHotkeyFromSetting((int)HotkeyId.BossKey, Properties.Settings.Default.HotkeyBossKey);
            RegisterHotkeyFromSetting((int)HotkeyId.OpacityUp, Properties.Settings.Default.HotkeyOpacityUp);
            RegisterHotkeyFromSetting((int)HotkeyId.OpacityDown, Properties.Settings.Default.HotkeyOpacityDown);
            RegisterHotkeyFromSetting((int)HotkeyId.ClickThrough, Properties.Settings.Default.HotkeyClickThrough);
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            switch (m.Msg)
            {
                case WM_HOTKEY:
                    HandleHotkey(m.WParam.ToInt32());
                    base.WndProc(ref m);
                    return;

                case WM_NCHITTEST:
                    // Let the base class do its thing first (handles title bar drag etc.)
                    base.WndProc(ref m);

                    // Fix bottom-left and bottom-right corners (and bottom/left/right edges)
                    Point pos = PointToClient(new Point(m.LParam.ToInt32()));
                    bool onLeft = pos.X < ResizeBorder;
                    bool onRight = pos.X >= ClientSize.Width - ResizeBorder;
                    bool onBottom = pos.Y >= ClientSize.Height - ResizeBorder;

                    // Allow dragging from top strip when header is hidden
                    if (!_headerVisible && pos.Y >= 0 && pos.Y < HEADER_HOVER_HEIGHT && !onLeft && !onRight && !onBottom)
                    {
                        m.Result = (IntPtr)HTCAPTION;
                        return;
                    }

                    if (onBottom && onLeft) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (onBottom && onRight) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (onBottom) m.Result = (IntPtr)HTBOTTOM;
                    else if (onLeft && m.Result == (IntPtr)HTCLIENT) m.Result = (IntPtr)HTLEFT;
                    else if (onRight && m.Result == (IntPtr)HTCLIENT) m.Result = (IntPtr)HTRIGHT;
                    return;
            }
            base.WndProc(ref m);
        }

        private void HandleHotkey(int id)
        {
            switch ((HotkeyId)id)
            {
                case HotkeyId.LegacyToggleShow:
                case HotkeyId.BossKey:
                    if (this.Visible && this.WindowState != FormWindowState.Minimized)
                        this.Hide();
                    else
                        ShowMainWindow();
                    break;

                case HotkeyId.LegacyToggleTop:
                    this.TopMost = !this.TopMost;
                    break;

                case HotkeyId.LegacyGoBack:
                    GoBack();
                    break;

                case HotkeyId.LegacyRunJs:
                    RunJs(control.GetJs());
                    break;

                case HotkeyId.OpacityUp:
                    AdjustOpacity(+5);
                    break;

                case HotkeyId.OpacityDown:
                    AdjustOpacity(-5);
                    break;

                case HotkeyId.ClickThrough:
                    ToggleClickThrough();
                    break;
            }
        }

        private void AdjustOpacity(int delta)
        {
            double current = Properties.Settings.Default.FormOpacity;
            double newVal = Math.Max(1, Math.Min(100, current + delta));
            SetTans(newVal);
            Properties.Settings.Default.FormOpacity = newVal;
            Properties.Settings.Default.Save();
            if (_settingForm != null && !_settingForm.IsDisposed)
                _settingForm.SyncOpacity((int)newVal);
        }

        // ─── Floating header behavior (#2) ────────────────────────────────────

        private void InitFloatingHeader()
        {
            // Delayed-hide timer: fires after cursor leaves the header zone.
            // Reduced from 1 500 ms to 400 ms for snappier UX.
            _headerHideTimer = new System.Windows.Forms.Timer();
            _headerHideTimer.Interval = 400;
            _headerHideTimer.Tick += (s, args) =>
            {
                _headerHideTimer.Stop();
                // Re-verify cursor position before hiding to avoid false dismissals.
                Point cur = this.PointToClient(Cursor.Position);
                if (cur.Y >= HEADER_HOVER_HEIGHT)
                {
                    pageHeader1.Visible = false;
                    _headerVisible = false;
                    UpdateTopMostButtonPosition();
                }
            };

            // Polling timer: samples cursor position every 50 ms.
            // Unlike MouseMove events, this fires reliably even when WebView2
            // (a native HwndHost) has captured the mouse – fixing the root cause
            // of the intermittent "header doesn't appear" bug.
            _headerHoverPollTimer = new System.Windows.Forms.Timer();
            _headerHoverPollTimer.Interval = 50;
            _headerHoverPollTimer.Tick += HeaderHoverPollTick;

            // Apply the persisted mode.
            ApplyHoverHeaderMode(Properties.Settings.Default.HoverHeaderMode);
        }

        private void HeaderHoverPollTick(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.NoTitle) return;

            Point cur = this.PointToClient(Cursor.Position);
            bool inForm = cur.X >= 0 && cur.X < this.ClientSize.Width
                       && cur.Y >= 0 && cur.Y < this.ClientSize.Height;

            if (inForm && cur.Y < HEADER_HOVER_HEIGHT)
            {
                // Cursor is inside the trigger zone – cancel any pending hide
                // and show the header if it is currently hidden.
                _headerHideTimer.Stop();
                if (!_headerVisible)
                {
                    pageHeader1.Visible = true;
                    _headerVisible = true;
                    UpdateTopMostButtonPosition();
                }
            }
            else if (_headerVisible)
            {
                // Cursor moved away from the header zone – start the hide countdown
                // (only if it is not already running).
                if (!_headerHideTimer.Enabled)
                    _headerHideTimer.Start();
            }
        }

        /// <summary>
        /// Switches header visibility mode at runtime.
        /// <para>false (default) – fixed: header is always visible.</para>
        /// <para>true – hover: header auto-hides and reappears when the cursor
        /// approaches the top <see cref="HEADER_HOVER_HEIGHT"/> pixels.</para>
        /// </summary>
        public void ApplyHoverHeaderMode(bool enableHover)
        {
            if (_headerHideTimer != null) _headerHideTimer.Stop();
            if (_headerHoverPollTimer != null) _headerHoverPollTimer.Stop();

            if (enableHover)
            {
                // Hover mode: hide the header initially; the polling timer will
                // reveal it as soon as the cursor enters the trigger zone.
                if (!Properties.Settings.Default.NoTitle)
                {
                    pageHeader1.Visible = false;
                    _headerVisible = false;
                    UpdateTopMostButtonPosition();
                }
                _headerHoverPollTimer.Start();
            }
            else
            {
                // Fixed mode: header is always visible (unless NoTitle is active).
                if (!Properties.Settings.Default.NoTitle)
                {
                    pageHeader1.Visible = true;
                    _headerVisible = true;
                    UpdateTopMostButtonPosition();
                }
            }
        }

        // Kept for backwards compatibility (no longer registered as an event handler).
        private void MainForm_MouseMoveForHeader(object sender, MouseEventArgs e)
        {
            Point formPt = (sender == (object)tabControl1)
                ? new Point(e.X + tabControl1.Left, e.Y + tabControl1.Top)
                : e.Location;

            if (formPt.Y < HEADER_HOVER_HEIGHT)
            {
                _headerHideTimer.Stop();
                if (!_headerVisible)
                {
                    pageHeader1.Visible = true;
                    _headerVisible = true;
                    UpdateTopMostButtonPosition();
                }
            }
            else if (_headerVisible && formPt.Y >= HEADER_HOVER_HEIGHT + 5)
            {
                _headerHideTimer.Start();
            }
        }

        // ─── TopMost toggle button (#11) ─────────────────────────────────────

        private void btnTopMost_Click(object sender, EventArgs e)
        {
            this.TopMost = !this.TopMost;
            Properties.Settings.Default.TopMostWindow = this.TopMost;
            Properties.Settings.Default.Save();
            UpdateTopMostButton();
        }

        private void UpdateTopMostButton()
        {
            if (btnTopMost == null) return;

            // Derive a foreground colour that contrasts with the current header background
            Color bg = pageHeader1.BackColor;
            Color contrast = ContrastColor(bg);

            if (this.TopMost)
            {
                // Active: show a clearly visible accent that works on both dark and light backgrounds.
                // Pick the hue that contrasts better: blue on light bg, orange on dark bg.
                double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
                btnTopMost.ForeColor = lum > 0.55
                    ? Color.FromArgb(24, 144, 255)   // blue on light header
                    : Color.FromArgb(255, 185, 50);  // gold/orange on dark header
            }
            else
            {
                // Inactive: semi-transparent contrast colour – blends with any background
                btnTopMost.ForeColor = Color.FromArgb(140, contrast.R, contrast.G, contrast.B);
            }

            UpdateTopMostButtonPosition();
        }

        private void UpdateTopMostButtonPosition()
        {
            if (btnTopMost == null) return;
            btnTopMost.Location = new System.Drawing.Point(this.ClientSize.Width - TOPMOST_BTN_OFFSET, 0);
            btnTopMost.BringToFront();
            btnTopMost.Visible = _headerVisible;
        }

        // ─── Custom new-tab HTML (#3/#4/#9) ──────────────────────────────────

        private string GetNewTabHtml()
        {
            var customSites = LoadCustomSites();

            // Build custom sites JSON for JS injection
            var jsonSb = new StringBuilder("[");
            for (int i = 0; i < customSites.Count; i++)
            {
                if (i > 0) jsonSb.Append(",");
                jsonSb.Append("{\"n\":\"")
                      .Append(JsEscape(customSites[i].Name))
                      .Append("\",\"u\":\"")
                      .Append(JsEscape(customSites[i].Url))
                      .Append("\"}");
            }
            jsonSb.Append("]");
            string customJson = jsonSb.ToString();

            return @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:system-ui,-apple-system,sans-serif;background:#f0f2f5;color:#333;min-height:100vh;display:flex;align-items:center;justify-content:center;padding:20px}
.container{width:90%;max-width:640px}
h2{text-align:center;color:#555;font-size:18px;font-weight:500;margin-bottom:24px}
.url-row{display:flex;gap:8px;margin-bottom:28px}
.url-row input{flex:1;padding:10px 14px;border:1px solid #d9d9d9;border-radius:8px;font-size:14px;outline:none;transition:border-color .2s}
.url-row input:focus{border-color:#1677ff}
.url-row button{padding:10px 18px;background:#1677ff;color:#fff;border:none;border-radius:8px;cursor:pointer;font-size:14px;transition:background .2s;white-space:nowrap}
.url-row button:hover{background:#4096ff}
.section-title{font-size:12px;color:#999;margin-bottom:10px;text-transform:uppercase;letter-spacing:.5px;display:flex;align-items:center;justify-content:space-between}
.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin-bottom:24px}
@media(max-width:480px){.grid{grid-template-columns:repeat(3,1fr)}}
.card{background:#fff;border:1px solid #f0f0f0;border-radius:10px;padding:12px 8px;text-align:center;cursor:pointer;transition:all .2s;text-decoration:none;color:#333;display:block;position:relative}
.card:hover{border-color:#1677ff;box-shadow:0 4px 12px rgba(22,119,255,.15);transform:translateY(-2px)}
.card .emoji{font-size:24px;margin-bottom:5px}
.card .name{font-size:11px;color:#666;word-break:break-all}
.card .del{position:absolute;top:3px;right:4px;font-size:11px;color:#ccc;cursor:pointer;line-height:1;display:none}
.card:hover .del{display:block}
.card .del:hover{color:#ff4d4f}
.add-card{background:#f8f9fb;border:1px dashed #d9d9d9;border-radius:10px;padding:12px 8px;text-align:center;cursor:pointer;color:#bbb;font-size:22px;transition:all .2s}
.add-card:hover{border-color:#1677ff;color:#1677ff}
.add-form{display:none;background:#fff;border:1px solid #e0e0e0;border-radius:10px;padding:16px;margin-top:10px}
.add-form input{width:100%;padding:8px 10px;border:1px solid #d9d9d9;border-radius:6px;font-size:13px;outline:none;margin-bottom:8px}
.add-form input:focus{border-color:#1677ff}
.add-form .btns{display:flex;gap:8px}
.add-form .btns button{flex:1;padding:8px;border:none;border-radius:6px;cursor:pointer;font-size:13px}
.add-form .btns .save{background:#1677ff;color:#fff}
.add-form .btns .cancel{background:#f0f0f0;color:#666}
</style></head>
<body>
<div class='container'>
  <h2>🌐 新标签页</h2>
  <div class='url-row'>
    <input id='u' type='text' placeholder='输入网址，回车打开...' autofocus onkeydown=""if(event.key==='Enter')go()"">
    <button onclick='go()'>打开</button>
  </div>
  <div class='section-title'><span>内置快捷</span></div>
  <div class='grid'>
    <a class='card' onclick=""nav('https://weread.qq.com/')"">
      <div class='emoji'>📚</div><div class='name'>微信读书</div>
    </a>
    <a class='card' onclick=""nav('https://www.xiaohongshu.com')"">
      <div class='emoji'>📕</div><div class='name'>小红书</div>
    </a>
    <a class='card' onclick=""nav('https://www.bilibili.com/')"">
      <div class='emoji'>📺</div><div class='name'>哔哩哔哩</div>
    </a>
  </div>
  <div class='section-title'><span>我的网站</span></div>
  <div class='grid' id='customGrid'></div>
  <div class='add-form' id='addForm'>
    <input id='siteName' placeholder='网站名称（可选）'>
    <input id='siteUrl' placeholder='网站地址，如 https://example.com'>
    <div class='btns'>
      <button class='save' onclick='saveCustom()'>保存</button>
      <button class='cancel' onclick='hideForm()'>取消</button>
    </div>
  </div>
</div>
<script>
var CUSTOM=" + customJson + @";
function renderCustom(){
  var g=document.getElementById('customGrid');
  g.innerHTML='';
  for(var i=0;i<CUSTOM.length;i++){
    var s=CUSTOM[i];
    var a=document.createElement('a');
    a.className='card';
    a.setAttribute('data-url',s.u);
    a.onclick=(function(u){return function(e){if(e.target.classList.contains('del'))return;nav(u);};})(s.u);
    a.innerHTML='<div class=""emoji"">🔗</div><div class=""name"">'+esc(s.n)+'</div><span class=""del"" title=""删除"" onclick=""removeCustom(\''+esc(s.u)+'\');"">✕</span>';
    g.appendChild(a);
  }
  var plus=document.createElement('div');
  plus.className='add-card';
  plus.title='添加自定义网站';
  plus.textContent='+';
  plus.onclick=function(){document.getElementById('addForm').style.display='block';document.getElementById('siteUrl').focus();};
  g.appendChild(plus);
}
function esc(s){return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/'/g,'&#39;');}
function nav(u){if(/^javascript:/i.test(u)||/^data:/i.test(u))return;location.href=u;}
function go(){var u=document.getElementById('u').value.trim();if(!u)return;if(/^javascript:/i.test(u)||/^data:/i.test(u))return;if(!/^https?:\/\//i.test(u))u='https://'+u;location.href=u;}
function saveCustom(){
  var n=document.getElementById('siteName').value.trim();
  var u=document.getElementById('siteUrl').value.trim();
  if(!u)return;
  if(/^javascript:/i.test(u)||/^data:/i.test(u))return;
  window.chrome.webview.postMessage('add\t'+(n||u)+'\t'+u);
  hideForm();
}
function removeCustom(u){
  if(confirm('确认删除该网站？'))window.chrome.webview.postMessage('remove\t'+u);
}
function hideForm(){
  document.getElementById('addForm').style.display='none';
  document.getElementById('siteName').value='';
  document.getElementById('siteUrl').value='';
}
renderCustom();
</script>
</body></html>";
        }

        private static string JsEscape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("/", "\\/")
                    .Replace("\r", "\\r")
                    .Replace("\n", "\\n")
                    .Replace("\t", "\\t");
        }

        // ─── Contrast colour helper ───────────────────────────────────────────

        /// <summary>Returns Color.White for dark backgrounds, Color.Black for light ones.</summary>
        private static Color ContrastColor(Color bg)
        {
            double lum = (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255.0;
            return lum > 0.55 ? Color.Black : Color.White;
        }

        // ─── BorderlessTabControl ─────────────────────────────────────────────

        /// <summary>
        /// TabControl whose content area covers the native 2 px drawn border.
        /// Intercepts TCM_ADJUSTRECT (0x1328) and expands the returned display
        /// rectangle outward by 2 px on the left, right, and bottom sides.
        /// </summary>
        internal sealed class BorderlessTabControl : System.Windows.Forms.TabControl
        {
            [System.Runtime.InteropServices.StructLayout(
                System.Runtime.InteropServices.LayoutKind.Sequential)]
            private struct RECT { public int Left, Top, Right, Bottom; }

            private const int TCM_ADJUSTRECT = 0x1328;

            protected override void WndProc(ref System.Windows.Forms.Message m)
            {
                // wParam != 0 -> "given display rect, give me window rect"
                // wParam == 0 -> "given window rect, give me display rect"  <- this is the one
                //                WinForms uses to size/position tab pages.
                if (m.Msg == TCM_ADJUSTRECT && m.WParam == System.IntPtr.Zero && !DesignMode)
                {
                    base.WndProc(ref m);   // let Windows compute the inset rect
                    var rc = (RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(
                        m.LParam, typeof(RECT));
                    rc.Left   -= 2;        // expand: cover left border
                    rc.Right  += 2;        // expand: cover right border
                    rc.Bottom += 2;        // expand: cover bottom border
                    System.Runtime.InteropServices.Marshal.StructureToPtr(rc, m.LParam, false);
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}
