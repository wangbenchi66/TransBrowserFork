using AntdUI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
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

        private static WebView2 GetTabWebView(TabPage page)
        {
            foreach (Control c in page.Controls)
                if (c is WebView2 wv) return wv;
            return null;
        }

        // ─── Constructor ──────────────────────────────────────────────────────
        public MainForm()
        {
            InitializeComponent();
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
            ShowWindowsBar(Properties.Settings.Default.NoTitle);
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

        private TabPage AddNewTab(string url = null)
        {
            int insertIndex = tabControl1.TabPages.Count - 1; // before the "+" tab
            var page = new TabPage("新标签页");
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
                        var menu = new ContextMenuStrip();
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
            _settingForm.FormClosed += (s, e2) => _settingForm = null;
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
            _headerHideTimer = new System.Windows.Forms.Timer();
            _headerHideTimer.Interval = 1500;
            _headerHideTimer.Tick += (s, e2) => {
                _headerHideTimer.Stop();
                Point cur = this.PointToClient(System.Windows.Forms.Cursor.Position);
                if (cur.Y >= HEADER_HOVER_HEIGHT)
                {
                    pageHeader1.Visible = false;
                    _headerVisible = false;
                    UpdateTopMostButtonPosition();
                }
            };
            this.MouseMove += MainForm_MouseMoveForHeader;
            tabControl1.MouseMove += MainForm_MouseMoveForHeader;
            pageHeader1.MouseLeave += (s, e2) => _headerHideTimer.Start();
            pageHeader1.MouseEnter += (s, e2) => { _headerHideTimer.Stop(); };
        }

        private void MainForm_MouseMoveForHeader(object sender, MouseEventArgs e)
        {
            Point formPt = (sender == (object)tabControl1)
                ? new Point(e.X + tabControl1.Left, e.Y + tabControl1.Top)
                : e.Location;

            if (formPt.Y < HEADER_HOVER_HEIGHT && !Properties.Settings.Default.NoTitle)
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
            btnTopMost.Type = this.TopMost ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
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
            return @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width'>
<style>
*{box-sizing:border-box;margin:0;padding:0}
body{font-family:system-ui,-apple-system,sans-serif;background:#f0f2f5;color:#333;height:100vh;display:flex;align-items:center;justify-content:center}
.container{width:90%;max-width:620px}
h2{text-align:center;color:#555;font-size:18px;font-weight:500;margin-bottom:24px}
.url-row{display:flex;gap:8px;margin-bottom:28px}
.url-row input{flex:1;padding:10px 14px;border:1px solid #d9d9d9;border-radius:8px;font-size:14px;outline:none;transition:border-color .2s}
.url-row input:focus{border-color:#1677ff}
.url-row button{padding:10px 18px;background:#1677ff;color:#fff;border:none;border-radius:8px;cursor:pointer;font-size:14px;transition:background .2s}
.url-row button:hover{background:#4096ff}
.section-title{font-size:12px;color:#999;margin-bottom:10px;text-transform:uppercase;letter-spacing:.5px}
.grid{display:grid;grid-template-columns:repeat(4,1fr);gap:12px}
@media(max-width:480px){.grid{grid-template-columns:repeat(3,1fr)}}
.card{background:#fff;border:1px solid #f0f0f0;border-radius:10px;padding:14px 8px;text-align:center;cursor:pointer;transition:all .2s;text-decoration:none;color:#333;display:block}
.card:hover{border-color:#1677ff;box-shadow:0 4px 12px rgba(22,119,255,.15);transform:translateY(-2px)}
.card .emoji{font-size:26px;margin-bottom:6px}
.card .name{font-size:12px;color:#666}
</style></head>
<body>
<div class='container'>
  <h2>🌐 新标签页</h2>
  <div class='url-row'>
    <input id='u' type='text' placeholder='输入网址，回车打开...' autofocus onkeydown=""if(event.key==='Enter')go()"">
    <button onclick='go()'>打开</button>
  </div>
  <div class='section-title'>快捷网站</div>
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
</div>
<script>
function go(){var u=document.getElementById('u').value.trim();if(!u)return;if(!/^https?:\/\//i.test(u))u='https://'+u;location.href=u;}
function nav(u){location.href=u;}
</script>
</body></html>";
        }
    }
}

