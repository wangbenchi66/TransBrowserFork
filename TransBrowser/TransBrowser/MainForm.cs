using AntdUI;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using static TransBrowser.Tools.GlobalHotkey;

namespace TransBrowser
{
    public partial class MainForm : Window
    {
        public bool inited = false;

        // Mobile mode state
        private bool _mobileMode = false;
        private Size _prevWindowSize;
        private Point _prevWindowLocation;
        private bool _hadSavedWindowBounds = false;
        private bool _prevShowTabBar = true;

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
            ClickThrough = 203,
            OpacityReset = 204
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

        // 用于存储 WebView2 的事件处理器映射，避免 Lambda 无法移除的问题
        private Dictionary<CoreWebView2, EventHandler<object>> _titleChangedHandlers
            = new Dictionary<CoreWebView2, EventHandler<object>>();

        // ─── Custom site management (#3/#4) ──────────────────────────────────

        private class CustomSite
        {
            public string Name { get; set; }
            public string Url { get; set; }
        }

        private class HistoryItem
        {
            public string Title { get; set; }
            public string Url { get; set; }
            public DateTime VisitTime { get; set; }
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

        // ─── Browsing history management ─────────────────────────────────────

        private List<HistoryItem> LoadHistory()
        {
            var list = new List<HistoryItem>();
            string raw = Properties.Settings.Default.BrowsingHistory ?? "";
            if (string.IsNullOrWhiteSpace(raw)) return list;

            foreach (string line in raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = line.Split('\t');
                if (parts.Length >= 3)
                {
                    if (DateTime.TryParse(parts[2], out DateTime visitTime))
                    {
                        list.Add(new HistoryItem
                        {
                            Title = parts[0],
                            Url = parts[1],
                            VisitTime = visitTime
                        });
                    }
                }
            }

            // 按访问时间倒序排列，最新的在前面
            return list.OrderByDescending(h => h.VisitTime).Take(50).ToList();
        }

        private void SaveHistory(List<HistoryItem> history)
        {
            var sb = new StringBuilder();
            foreach (var h in history.Take(50)) // 只保存最近50条
            {
                string title = (h.Title ?? "").Replace("\t", " ").Replace("\n", " ");
                string url = (h.Url ?? "").Replace("\t", "").Replace("\n", "");
                sb.Append(title).Append('\t').Append(url).Append('\t')
                  .Append(h.VisitTime.ToString("o")).Append('\n');
            }
            Properties.Settings.Default.BrowsingHistory = sb.ToString();
            Properties.Settings.Default.Save();
        }

        private void AddToHistory(string title, string url)
        {
            if (string.IsNullOrEmpty(url) || url == "about:blank") return;
            if (url.StartsWith("data:") || url.StartsWith("javascript:")) return;

            var history = LoadHistory();

            // 移除相同URL的旧记录
            history.RemoveAll(h => h.Url == url);

            // 添加新记录
            history.Insert(0, new HistoryItem
            {
                Title = string.IsNullOrEmpty(title) ? url : title,
                Url = url,
                VisitTime = DateTime.Now
            });

            SaveHistory(history);
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
            // 完全移除所有内边距，消除窗体和控件之间的间距
            this.Padding = System.Windows.Forms.Padding.Empty;
            tabControl1.Padding = System.Drawing.Point.Empty;
            tabControl1.Margin = System.Windows.Forms.Padding.Empty;
            pageHeader1.Margin = System.Windows.Forms.Padding.Empty;
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

            // Enable custom tab drawing for close buttons
            InitTabControlDrawing();
        }

        // ─── Custom tab drawing (close button) ────────────────────────────────
        private void InitTabControlDrawing()
        {
            tabControl1.DrawMode = System.Windows.Forms.TabDrawMode.OwnerDrawFixed;
            tabControl1.DrawItem += TabControl1_DrawItem;
            tabControl1.Multiline = false; // 强制单行显示
        }

        private void TabControl1_DrawItem(object sender, System.Windows.Forms.DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var tab = tabControl1.TabPages[e.Index];
            var tabRect = tabControl1.GetTabRect(e.Index);
            bool isLastTab = e.Index == tabControl1.TabPages.Count - 1; // "+" tab
            bool isSelected = e.Index == tabControl1.SelectedIndex;

            // ─── 现代化配色方案 ────────────────────────────────────────────
            Color bgColor, textColor, borderColor, hoverColor;

            if (isSelected)
            {
                // 选中标签：纯白背景，深色文字
                bgColor = Color.FromArgb(255, 255, 255);
                textColor = Color.FromArgb(32, 32, 32);
            }
            else
            {
                // 未选中标签：浅灰背景，中灰文字
                bgColor = Color.FromArgb(242, 242, 242);
                textColor = Color.FromArgb(90, 90, 90);
            }

            borderColor = Color.FromArgb(218, 218, 218);
            hoverColor = Color.FromArgb(232, 17, 35); // 关闭按钮悬停色

            // ─── 绘制背景 ──────────────────────────────────────────────────
            using (var bgBrush = new SolidBrush(bgColor))
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // 绘制圆角矩形背景（顶部圆角）
                if (isSelected)
                {
                    // 选中标签：完整背景
                    e.Graphics.FillRectangle(bgBrush, tabRect);
                }
                else
                {
                    // 未选中标签：稍微缩小的背景
                    var innerRect = new Rectangle(
                        tabRect.X + 1,
                        tabRect.Y + 2,
                        tabRect.Width - 2,
                        tabRect.Height - 2);
                    e.Graphics.FillRectangle(bgBrush, innerRect);
                }
            }

            // ─── 绘制分隔线 ────────────────────────────────────────────────
            // 只在标签之间绘制细微的分隔线
            if (!isLastTab && !isSelected && e.Index + 1 < tabControl1.TabPages.Count - 1)
            {
                bool nextIsSelected = (e.Index + 1) == tabControl1.SelectedIndex;
                if (!nextIsSelected)
                {
                    using (var pen = new Pen(Color.FromArgb(230, 230, 230), 1))
                    {
                        e.Graphics.DrawLine(pen,
                            tabRect.Right - 1, tabRect.Top + 8,
                            tabRect.Right - 1, tabRect.Bottom - 8);
                    }
                }
            }

            // 移除蓝色下划线，使用更简洁的设计

            // ─── 绘制文本 ──────────────────────────────────────────────────
            int leftPadding = isLastTab ? 8 : 12;
            int rightPadding = isLastTab ? 8 : 28; // 为关闭按钮留空间

            var textRect = new Rectangle(
                tabRect.X + leftPadding,
                tabRect.Y + 2,
                tabRect.Width - leftPadding - rightPadding,
                tabRect.Height - 2);

            using (var textBrush = new SolidBrush(textColor))
            {
                var sf = new StringFormat
                {
                    Alignment = isLastTab ? StringAlignment.Center : StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };

                // 使用稍大字体使其更清晰
                using (var font = new Font(e.Font.FontFamily, e.Font.Size, FontStyle.Regular))
                {
                    e.Graphics.DrawString(tab.Text, font, textBrush, textRect, sf);
                }
            }

            // ─── 绘制关闭按钮（圆形背景 + X 符号）─────────────────────────
            if (!isLastTab)
            {
                var closeRect = new Rectangle(
                    tabRect.Right - 20,
                    tabRect.Y + (tabRect.Height - 14) / 2,
                    14, 14);

                // 检测鼠标悬停
                Point mousePos = tabControl1.PointToClient(Cursor.Position);
                bool isHovering = closeRect.Contains(mousePos);

                // 绘制圆形背景（悬停时）
                if (isHovering)
                {
                    using (var hoverBrush = new SolidBrush(Color.FromArgb(240, 240, 240)))
                    {
                        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        e.Graphics.FillEllipse(hoverBrush, closeRect);
                    }
                }

                // 绘制 X 符号
                Color closeColor = isHovering ? hoverColor : Color.FromArgb(120, 120, 120);
                using (var closePen = new Pen(closeColor, 1.5f))
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                    // X 的两条对角线
                    int offset = 4;
                    e.Graphics.DrawLine(closePen,
                        closeRect.Left + offset, closeRect.Top + offset,
                        closeRect.Right - offset, closeRect.Bottom - offset);
                    e.Graphics.DrawLine(closePen,
                        closeRect.Right - offset, closeRect.Top + offset,
                        closeRect.Left + offset, closeRect.Bottom - offset);
                }
            }
        }

        // ─── First-tab WebView2 async init ────────────────────────────────────
        private async void InitializeWebView()
        {
            if (Properties.Settings.Default.TransparentBackground)
                webView21.DefaultBackgroundColor = Color.Transparent;
            await webView21.EnsureCoreWebView2Async(null);
            // Apply UA for mobile mode or saved default UA
            try
            {
                if (_mobileMode)
                {
                    webView21.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1";
                }
                else if (!string.IsNullOrEmpty(Properties.Settings.Default.DefaultUA))
                {
                    webView21.CoreWebView2.Settings.UserAgent = Properties.Settings.Default.DefaultUA;
                }
            }
            catch { }
            SetupWebViewEvents(webView21);
            tabPageFirst.Text = "新标签页";
            // 启动时总是显示新标签页
            webView21.CoreWebView2.NavigateToString(GetNewTabHtml());
        }

        // ─── Settings restoration ─────────────────────────────────────────────
        public void Init()
        {
            // 首次启动时初始化默认值
            if (Properties.Settings.Default.FormOpacity <= 0 || Properties.Settings.Default.FormOpacity > 100)
            {
                Properties.Settings.Default.FormOpacity = 100;
                Properties.Settings.Default.Save();
            }
            // Note: "无窗口模式" (NoTitle) removed. Floating header and related
            // behavior are driven by the HoverHeaderMode setting instead.
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

            // Restore anti-screenshot mode
            if (Properties.Settings.Default.AntiScreenshotMode)
                SetAntiScreenshotMode(true);

            // Restore mobile mode if enabled
            SetMobileMold(Properties.Settings.Default.MobileMold);

            inited = true;
        }

        // ─── Tab management ───────────────────────────────────────────────────

        public System.Windows.Forms.TabPage AddNewTab(string url = null)
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
            if (Properties.Settings.Default.TransparentBackground)
                wv.DefaultBackgroundColor = Color.Transparent;
            await wv.EnsureCoreWebView2Async(null);
            try
            {
                if (_mobileMode && wv.CoreWebView2 != null)
                {
                    wv.CoreWebView2.Settings.UserAgent = "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1";
                }
                else if (wv.CoreWebView2 != null && !string.IsNullOrEmpty(Properties.Settings.Default.DefaultUA))
                {
                    wv.CoreWebView2.Settings.UserAgent = Properties.Settings.Default.DefaultUA;
                }
            }
            catch { }
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

            // 创建并存储标题变化事件处理器，以便正确移除
            EventHandler<object> titleHandler = (s, _) => UpdateTabTitle(s as CoreWebView2);
            _titleChangedHandlers[wv.CoreWebView2] = titleHandler;
            wv.CoreWebView2.DocumentTitleChanged += titleHandler;

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
                    this.BeginInvoke((MethodInvoker)(() =>
                    {
                        // 边界检查：确保标签还存在
                        if (capturedI >= 0 && capturedI < tabControl1.TabPages.Count - 1)
                        {
                            try
                            {
                                tabControl1.TabPages[capturedI].Text = title;
                            }
                            catch (ArgumentOutOfRangeException)
                            {
                                // 标签已被删除，忽略
                            }
                        }
                    }));
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
            for (int i = 0; i < tabControl1.TabPages.Count - 1; i++) // skip "+" tab
            {
                var tabRect = tabControl1.GetTabRect(i);
                if (tabRect.Contains(e.Location))
                {
                    // 同绘制时保持一致的关闭按钮矩形（右边距20px，14x14居中）
                    var closeRect = new Rectangle(
                        tabRect.Right - 20,
                        tabRect.Y + (tabRect.Height - 14) / 2,
                        14, 14);

                    if (e.Button == MouseButtons.Left && closeRect.Contains(e.Location))
                    {
                        CloseTab(i);
                        return;
                    }

                    // Middle-click to close
                    if (e.Button == MouseButtons.Middle)
                    {
                        CloseTab(i);
                        return;
                    }

                    // Right-click menu
                    if (e.Button == MouseButtons.Right)
                    {
                        var menu = new System.Windows.Forms.ContextMenuStrip();
                        int capturedI = i;
                        menu.Items.Add("关闭标签", null, (s, _) => CloseTab(capturedI));
                        menu.Items.Add("在新标签页中打开", null, (s, _) => AddNewTab());
                        menu.Show(tabControl1, e.Location);
                        return;
                    }
                    break;
                }
            }
        }

        private void CloseTab(int index)
        {
            if (tabControl1.TabPages.Count <= 2) // only one real tab + "+" tab
                return; // don't close the last real tab

            // 边界检查
            if (index < 0 || index >= tabControl1.TabPages.Count - 1)
                return;

            var page = tabControl1.TabPages[index];
            var wv = GetTabWebView(page);
            if (wv != null)
            {
                // 先移除事件处理，避免在 Dispose 时触发
                try
                {
                    if (wv.CoreWebView2 != null)
                    {
                        wv.CoreWebView2.NewWindowRequested -= CoreWebView2_NewWindowRequested;

                        // 移除存储的标题变化事件处理器
                        if (_titleChangedHandlers.ContainsKey(wv.CoreWebView2))
                        {
                            wv.CoreWebView2.DocumentTitleChanged -= _titleChangedHandlers[wv.CoreWebView2];
                            _titleChangedHandlers.Remove(wv.CoreWebView2);
                        }

                        wv.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                        wv.CoreWebView2.WebMessageReceived -= CoreWebView2_WebMessageReceived;
                    }
                    wv.NavigationCompleted -= Wv_NavigationCompleted;
                }
                catch { }

                wv.Dispose();
            }
            tabControl1.TabPages.RemoveAt(index);

            // Ensure we're not sitting on the "+" tab after removal
            if (tabControl1.SelectedIndex >= tabControl1.TabPages.Count - 1)
            {
                int newIndex = Math.Max(0, tabControl1.TabPages.Count - 2);
                if (newIndex >= 0 && newIndex < tabControl1.TabPages.Count)
                    tabControl1.SelectedIndex = newIndex;
            }
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

                // 添加到历史记录
                string title = wv.CoreWebView2?.DocumentTitle ?? "";
                AddToHistory(title, wv.Source.AbsoluteUri);
            }
            if (Properties.Settings.Default.NoImageMode)
                ApplyNoImageCss(wv, true);

            // 应用背景透明
            if (Properties.Settings.Default.TransparentBackground)
                ApplyTransparentBackground(wv, true);

            // 应用灰度模式
            if (Properties.Settings.Default.GrayscaleMode)
                ApplyGrayscaleCss(wv, true);
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

            // 同时设置操作按钮的背景色，实现整体统一
            // 如果颜色是透明的（Alpha < 255），则需要特殊处理
            bool isTransparent = color.A < 255;

            if (btnMinimize != null)
            {
                btnMinimize.BackColor = color;
                // 透明时需要确保控件支持透明度
                if (isTransparent && btnMinimize.Parent != null)
                    btnMinimize.Parent.Refresh();
            }

            if (btnClose != null)
            {
                // 关闭按钮在未悬停时也使用统一背景色
                btnClose.BackColor = color;
                // 更新悬停事件以保持正确的背景色
                btnClose.MouseEnter -= BtnClose_MouseEnter;
                btnClose.MouseLeave -= BtnClose_MouseLeave;
                btnClose.MouseEnter += BtnClose_MouseEnter;
                btnClose.MouseLeave += BtnClose_MouseLeave;

                if (isTransparent && btnClose.Parent != null)
                    btnClose.Parent.Refresh();
            }

            if (btnTopMost != null)
            {
                btnTopMost.BackColor = color;
                if (isTransparent && btnTopMost.Parent != null)
                    btnTopMost.Parent.Refresh();
            }

            // Recompute pin-button contrast colour for the new background
            UpdateTopMostButton();
        }

        private void BtnClose_MouseEnter(object sender, EventArgs e)
        {
            btnClose.BackColor = Color.FromArgb(232, 17, 35);
            btnClose.ForeColor = Color.White;
        }

        private void BtnClose_MouseLeave(object sender, EventArgs e)
        {
            btnClose.BackColor = pageHeader1.BackColor; // 恢复为标题栏背景色
            btnClose.ForeColor = Color.Gray;
        }

        public void SetShowInTaskBar(bool show)
        {
            // 避免在窗口句柄未创建或正在销毁时修改 ShowInTaskbar
            if (!this.IsHandleCreated || this.IsDisposed || this.Disposing)
                return;

            try
            {
                // 使用 BeginInvoke 延迟执行，避免在消息处理期间直接修改属性
                // 这样可以防止创建窗口句柄时出错
                this.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 再次检查窗口状态
                        if (this.IsDisposed || this.Disposing || !this.IsHandleCreated)
                            return;

                        if (show)
                        {
                            // 显示在任务栏
                            if (!this.ShowInTaskbar)
                                this.ShowInTaskbar = true;
                        }
                        else
                        {
                            // 隐藏任务栏图标
                            // 只有在窗口不是最小化状态时才立即隐藏
                            // 最小化状态下会在 OnResize 中处理
                            if (this.WindowState != FormWindowState.Minimized && this.ShowInTaskbar)
                            {
                                this.ShowInTaskbar = false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 捕获内部异常，防止程序崩溃
                        Tools.LogHelper.Error(ex);
                    }
                }));
            }
            catch (Exception ex)
            {
                // 如果 BeginInvoke 失败（比如窗口正在销毁），记录错误但不抛出
                Tools.LogHelper.Error(ex);
            }
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

        public void SetMobileMold(bool mobileMold)
        {
            try
            {
                _mobileMode = mobileMold;
                Properties.Settings.Default.MobileMold = mobileMold;
                Properties.Settings.Default.Save();

                // Save/restore some UI state (size/location/tab bar)
                if (mobileMold)
                {
                    if (!_hadSavedWindowBounds)
                    {
                        _prevWindowSize = this.Size;
                        _prevWindowLocation = this.Location;
                        _prevShowTabBar = Properties.Settings.Default.ShowTabBar;
                        _hadSavedWindowBounds = true;
                    }

                    // Typical mobile portrait size (approx. phone viewport)
                    var mobileSize = new Size(390, 844);
                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            this.Size = mobileSize;
                            var screen = Screen.PrimaryScreen.WorkingArea;
                            this.Location = new Point(
                                Math.Max(0, screen.Left + (screen.Width - mobileSize.Width) / 2),
                                Math.Max(0, screen.Top + (screen.Height - mobileSize.Height) / 2));
                            // hide tab bar to emulate single-tab mobile view
                            SetTabBarVisible(false);
                        }));
                    }
                    else
                    {
                        this.Size = mobileSize;
                        var screen = Screen.PrimaryScreen.WorkingArea;
                        this.Location = new Point(
                            Math.Max(0, screen.Left + (screen.Width - mobileSize.Width) / 2),
                            Math.Max(0, screen.Top + (screen.Height - mobileSize.Height) / 2));
                        SetTabBarVisible(false);
                    }

                    // Apply mobile user agent to all ready WebView2 controls
                    const string mobileUa = "Mozilla/5.0 (iPhone; CPU iPhone OS 15_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/15.0 Mobile/15E148 Safari/604.1";
                    for (int i = 0; i < tabControl1.TabPages.Count - 1; i++)
                    {
                        var wv = GetTabWebView(tabControl1.TabPages[i]);
                        if (wv?.CoreWebView2 != null)
                        {
                            try
                            {
                                wv.CoreWebView2.Settings.UserAgent = mobileUa;
                                wv.Reload();
                            }
                            catch { }
                        }
                    }
                }
                else
                {
                    // Restore previous window bounds if available
                    if (_hadSavedWindowBounds)
                    {
                        if (this.InvokeRequired)
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                this.Size = _prevWindowSize;
                                this.Location = _prevWindowLocation;
                                SetTabBarVisible(_prevShowTabBar);
                            }));
                        }
                        else
                        {
                            this.Size = _prevWindowSize;
                            this.Location = _prevWindowLocation;
                            SetTabBarVisible(_prevShowTabBar);
                        }
                        _hadSavedWindowBounds = false;
                    }

                    // Restore user agent from settings (if provided)
                    string defaultUa = Properties.Settings.Default.DefaultUA;
                    for (int i = 0; i < tabControl1.TabPages.Count - 1; i++)
                    {
                        var wv = GetTabWebView(tabControl1.TabPages[i]);
                        if (wv?.CoreWebView2 != null)
                        {
                            try
                            {
                                if (!string.IsNullOrEmpty(defaultUa))
                                    wv.CoreWebView2.Settings.UserAgent = defaultUa;
                                else
                                    wv.CoreWebView2.Settings.UserAgent = string.Empty;
                                wv.Reload();
                            }
                            catch { }
                        }
                    }
                }

                // Ensure future webviews created pick up the mobile UA by leaving _mobileMode flag
                UpdateTopMostButton();
            }
            catch (Exception ex)
            {
                Tools.LogHelper.Error(ex);
            }
        }

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
                tabControl1.SizeMode = System.Windows.Forms.TabSizeMode.Normal; // 自适应宽度
                tabControl1.ItemSize = new System.Drawing.Size(0, 20); // 高度20，宽度0表示自适应
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

        public void SetTransparentBackground(bool enable)
        {
            Properties.Settings.Default.TransparentBackground = enable;
            Properties.Settings.Default.Save();

            for (int i = 0; i < tabControl1.TabPages.Count - 1; i++)
            {
                var wv = GetTabWebView(tabControl1.TabPages[i]);
                if (wv != null)
                {
                    // Set the WebView2-level background first; CSS handles the page content
                    wv.DefaultBackgroundColor = enable ? Color.Transparent : Color.White;
                    if (wv.CoreWebView2 != null)
                        ApplyTransparentBackground(wv, enable);
                }
            }
        }

        private async void ApplyTransparentBackground(Microsoft.Web.WebView2.WinForms.WebView2 wv, bool enable)
        {
            // Keep DefaultBackgroundColor in sync when called from NavigationCompleted
            wv.DefaultBackgroundColor = enable ? Color.Transparent : Color.White;

            if (enable)
            {
                // Remove page-level backgrounds so the transparent WebView2 shows through
                string css = "html,body{background:transparent!important;background-color:transparent!important}";
                string js = $"(function(){{var s=document.getElementById('__trans_bg');if(!s){{s=document.createElement('style');s.id='__trans_bg';(document.head||document.documentElement).appendChild(s);}}s.textContent='{css}';}})()";
                await wv.CoreWebView2.ExecuteScriptAsync(js);
            }
            else
            {
                string js = "(function(){var s=document.getElementById('__trans_bg');if(s)s.remove();})()";
                await wv.CoreWebView2.ExecuteScriptAsync(js);
            }
        }

        // ─── Grayscale mode (灰度模式) ────────────────────────────────────────

        public void SetGrayscaleMode(bool enable)
        {
            Properties.Settings.Default.GrayscaleMode = enable;
            Properties.Settings.Default.Save();

            for (int i = 0; i < tabControl1.TabPages.Count - 1; i++)
            {
                var wv = GetTabWebView(tabControl1.TabPages[i]);
                if (wv?.CoreWebView2 != null)
                    ApplyGrayscaleCss(wv, enable);
            }
        }

        private async void ApplyGrayscaleCss(Microsoft.Web.WebView2.WinForms.WebView2 wv, bool enable)
        {
            if (enable)
            {
                // 灰度滤镜CSS
                string css = "html{filter:grayscale(100%)!important;-webkit-filter:grayscale(100%)!important}";
                string js = $@"(function(){{
                    try{{
                        var s=document.getElementById('__trans_grayscale');
                        if(!s){{
                            s=document.createElement('style');
                            s.id='__trans_grayscale';
                            (document.head||document.documentElement).appendChild(s);
                        }}
                        s.textContent='{css}';
                    }}catch(e){{}}
                }})()";
                await wv.CoreWebView2.ExecuteScriptAsync(js);
            }
            else
            {
                string js = @"(function(){
                    try{
                        var s=document.getElementById('__trans_grayscale');
                        if(s)s.remove();
                    }catch(e){}
                })()";
                await wv.CoreWebView2.ExecuteScriptAsync(js);
            }
        }

        // ─── Anti-screenshot mode (防截屏模式) ────────────────────────────────

        [DllImport("user32.dll")]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        private const uint WDA_NONE = 0x00000000;
        private const uint WDA_MONITOR = 0x00000001;
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        /// <summary>Applies or removes the anti-screenshot affinity for a single window handle.</summary>
        private static void ApplyAffinityToHandle(IntPtr handle, bool enable)
        {
            if (handle != IntPtr.Zero)
                SetWindowDisplayAffinity(handle, enable ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
        }

        /// <summary>Propagates the current anti-screenshot state to all open child windows.</summary>
        private void ApplyAntiScreenshotToAllWindows(bool enable)
        {
            if (_settingForm != null && !_settingForm.IsDisposed && _settingForm.IsHandleCreated)
                ApplyAffinityToHandle(_settingForm.Handle, enable);
            if (control != null && !control.IsDisposed && control.IsHandleCreated)
                ApplyAffinityToHandle(control.Handle, enable);
        }

        public void SetAntiScreenshotMode(bool enable)
        {
            Properties.Settings.Default.AntiScreenshotMode = enable;
            Properties.Settings.Default.Save();

            if (this.IsHandleCreated)
            {
                bool success = SetWindowDisplayAffinity(this.Handle,
                    enable ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);

                if (success)
                {
                    // Also apply to all currently open child windows
                    ApplyAntiScreenshotToAllWindows(enable);

                    string msg = enable ? "防截屏模式已开启\n大部分截屏软件将无法捕获此窗口"
                                        : "防截屏模式已关闭";
                    notifyIcon1.ShowBalloonTip(2000, "TransBrowser", msg, ToolTipIcon.Info);
                }
                else
                {
                    if (enable)
                    {
                        MessageBox.Show("防截屏模式启动失败\n可能需要 Windows 10 2004 或更高版本",
                            "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        Properties.Settings.Default.AntiScreenshotMode = false;
                        Properties.Settings.Default.Save();
                    }
                }
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
            // 恢复窗口时，先恢复任务栏设置，再显示窗口
            // 这样可以避免任务栏预览冲突
            try
            {
                bool shouldShowInTaskbar = Properties.Settings.Default.ShowInTaskbar;

                // 先显示窗口和恢复状态
                if (!this.Visible)
                    this.Show();
                this.WindowState = FormWindowState.Normal;

                // 然后安全地设置任务栏显示
                if (this.IsHandleCreated && !this.IsDisposed)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!this.IsDisposed && this.IsHandleCreated)
                                this.ShowInTaskbar = shouldShowInTaskbar;
                        }
                        catch { }
                    }));
                }

                this.Activate();
            }
            catch (Exception ex)
            {
                Tools.LogHelper.Error(ex);
            }
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

            try
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    // 最小化时确保在任务栏图标存在的情况下再隐藏
                    // 这样可以避免任务栏预览冲突
                    this.Hide();

                    // 如果设置不显示在任务栏，延迟隐藏任务栏图标
                    if (!Properties.Settings.Default.ShowInTaskbar && this.IsHandleCreated)
                    {
                        this.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (!this.IsDisposed && this.IsHandleCreated && !this.Visible)
                                    this.ShowInTaskbar = false;
                            }
                            catch { }
                        }));
                    }

                    notifyIcon1.ShowBalloonTip(1000, "TransBrowser", "已最小化到托盘，双击图标恢复", ToolTipIcon.Info);
                }
                else if (this.WindowState == FormWindowState.Normal)
                {
                    // 恢复正常状态时，重新应用任务栏设置
                    if (this.IsHandleCreated)
                    {
                        bool shouldShow = Properties.Settings.Default.ShowInTaskbar;
                        this.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (!this.IsDisposed && this.IsHandleCreated)
                                    this.ShowInTaskbar = shouldShow;
                            }
                            catch { }
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                Tools.LogHelper.Error(ex);
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
            // Apply anti-screenshot to the newly opened settings window if the mode is active
            if (Properties.Settings.Default.AntiScreenshotMode && _settingForm.IsHandleCreated)
                ApplyAffinityToHandle(_settingForm.Handle, true);
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
            // Use BeginInvoke so the affinity is set after the window message loop
            // has fully processed the Show() – guarantees the handle exists
            if (Properties.Settings.Default.AntiScreenshotMode)
                control.BeginInvoke(new Action(() =>
                {
                    if (!control.IsDisposed && control.IsHandleCreated)
                        ApplyAffinityToHandle(control.Handle, true);
                }));
        }

        private ControlPanel control = new ControlPanel();

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
            // 置顶窗口不自动隐藏
            if (this.TopMost) return;

            // 开启失焦隐藏时，窗口失去焦点自动隐藏
            // 注意：隐藏后老板键仍可以通过全局快捷键调用ShowMainWindow()恢复窗口
            if (Properties.Settings.Default.AutoHide)
            {
                // 先隐藏窗口，再处理任务栏图标，避免闪烁
                this.Hide();

                // 延迟隐藏任务栏图标，避免窗口句柄错误
                if (!Properties.Settings.Default.ShowInTaskbar && this.IsHandleCreated)
                {
                    this.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (!this.IsDisposed && this.IsHandleCreated && !this.Visible)
                                this.ShowInTaskbar = false;
                        }
                        catch { }
                    }));
                }
            }
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

            // Default transparency hotkeys (修改8：alt+方向键)
            RegisterHotKey(this.Handle, (int)HotkeyId.OpacityReset, KeyModifiers.Alt, Keys.Up);     // Alt+↑ 重置100%
            RegisterHotKey(this.Handle, (int)HotkeyId.OpacityDown, KeyModifiers.Alt, Keys.Left);    // Alt+← 减少
            RegisterHotKey(this.Handle, (int)HotkeyId.OpacityUp, KeyModifiers.Alt, Keys.Right);     // Alt+→ 增加
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
                    {
                        // 修改5：隐藏时暂停所有网页并静音
                        PauseAllWebViews();
                        // 先隐藏窗口，再处理任务栏图标，避免闪烁
                        this.Hide();

                        // 延迟隐藏任务栏图标，避免窗口句柄错误
                        if (!Properties.Settings.Default.ShowInTaskbar && this.IsHandleCreated)
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (!this.IsDisposed && this.IsHandleCreated && !this.Visible)
                                        this.ShowInTaskbar = false;
                                }
                                catch { }
                            }));
                        }
                    }
                    else
                    {
                        ShowMainWindow();
                        // 恢复时继续播放
                        ResumeAllWebViews();
                    }
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

                case HotkeyId.OpacityReset:
                    // 修改7：重置透明度到100%
                    SetTans(100);
                    Properties.Settings.Default.FormOpacity = 100;
                    Properties.Settings.Default.Save();
                    if (_settingForm != null && !_settingForm.IsDisposed)
                        _settingForm.SyncOpacity(100);
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

        // ─── Pause/Resume all WebViews (for boss key) ────────────────────────

        private void PauseAllWebViews()
        {
            string js = @"
(function(){
    document.querySelectorAll('video,audio').forEach(function(m){
        if(!m.paused){
            m.pause();
            m.setAttribute('data-was-playing','1');
        }
    });
})();";
            for (int i = 0; i < tabControl1.TabPages.Count - 1; i++)
            {
                var wv = GetTabWebView(tabControl1.TabPages[i]);
                if (wv?.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.ExecuteScriptAsync(js); } catch { }
                }
            }
        }

        private void ResumeAllWebViews()
        {
            string js = @"
(function(){
    document.querySelectorAll('video,audio').forEach(function(m){
        if(m.getAttribute('data-was-playing')==='1'){
            m.play();
            m.removeAttribute('data-was-playing');
        }
    });
})();";
            for (int i = 0; i < tabControl1.TabPages.Count - 1; i++)
            {
                var wv = GetTabWebView(tabControl1.TabPages[i]);
                if (wv?.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.ExecuteScriptAsync(js); } catch { }
                }
            }
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
                // When the tab bar is visible, extend the "keep visible" zone to cover
                // the tab strip so the user can reach the tabs without the header hiding.
                bool inTabBarZone = false;
                if (Properties.Settings.Default.ShowTabBar && inForm)
                {
                    int tabBarBottom = pageHeader1.Bottom + tabControl1.ItemSize.Height + 2;
                    inTabBarZone = cur.Y < tabBarBottom;
                }

                if (inTabBarZone)
                    _headerHideTimer.Stop(); // cursor is on tab bar – do not hide header
                else if (!_headerHideTimer.Enabled)
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
                pageHeader1.Visible = false;
                _headerVisible = false;
                UpdateTopMostButtonPosition();
                _headerHoverPollTimer.Start();
            }
            else
            {
                // Fixed mode: header is always visible.
                pageHeader1.Visible = true;
                _headerVisible = true;
                UpdateTopMostButtonPosition();
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

        private void btnMinimize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void UpdateTopMostButton()
        {
            if (btnTopMost == null) return;

            // Derive a foreground colour that contrasts with the current header background
            Color bg = pageHeader1.BackColor;
            Color contrast = ContrastColor(bg);

            // 设置背景色为标题栏颜色
            btnTopMost.BackColor = bg;
            if (btnMinimize != null) btnMinimize.BackColor = bg;
            if (btnClose != null) btnClose.BackColor = bg;

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

            // 更新最小化和关闭按钮的前景色以适应背景
            if (btnMinimize != null) btnMinimize.ForeColor = ContrastColor(bg);
            if (btnClose != null) btnClose.ForeColor = ContrastColor(bg);

            UpdateButtonPositions();
        }

        private void UpdateTopMostButtonPosition()
        {
            UpdateButtonPositions();
        }

        private void UpdateButtonPositions()
        {
            if (btnTopMost == null || btnMinimize == null || btnClose == null) return;

            int rightEdge = this.ClientSize.Width;
            const int buttonWidth = 26;
            const int minWidthToShow = 300; // 最小宽度阈值

            // 修改4：窗口小于一定宽度时隐藏最小化和关闭按钮
            bool showExtraButtons = rightEdge >= minWidthToShow;

            if (showExtraButtons)
            {
                // 从右到左排列：关闭、最小化、置顶
                btnClose.Location = new System.Drawing.Point(rightEdge - buttonWidth, 0);
                btnMinimize.Location = new System.Drawing.Point(rightEdge - buttonWidth * 2, 0);
                btnTopMost.Location = new System.Drawing.Point(rightEdge - buttonWidth * 3, 0);

                btnClose.Visible = _headerVisible;
                btnMinimize.Visible = _headerVisible;
            }
            else
            {
                // 只显示置顶按钮
                btnTopMost.Location = new System.Drawing.Point(rightEdge - TOPMOST_BTN_OFFSET, 0);
                btnClose.Visible = false;
                btnMinimize.Visible = false;
            }

            btnTopMost.Visible = _headerVisible;
            btnTopMost.BringToFront();
            btnMinimize.BringToFront();
            btnClose.BringToFront();
        }

        // ─── Custom new-tab HTML (#3/#4/#9) ──────────────────────────────────

        private string GetNewTabHtml()
        {
            var customSites = LoadCustomSites();
            var history = LoadHistory();

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

            // Build history JSON (all history for client-side filtering)
            var historyJson = new StringBuilder("[");
            for (int i = 0; i < history.Count; i++)
            {
                if (i > 0) historyJson.Append(",");
                historyJson.Append("{\"t\":\"")
                           .Append(JsEscape(history[i].Title))
                           .Append("\",\"u\":\"")
                           .Append(JsEscape(history[i].Url))
                           .Append("\"}");
            }
            historyJson.Append("]");

            // Load HTML template from file
            string templatePath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "Resources", "newtab.html");

            string html;
            if (System.IO.File.Exists(templatePath))
            {
                html = System.IO.File.ReadAllText(templatePath, Encoding.UTF8);
            }
            else
            {
                // Fallback to embedded minimal HTML if file not found
                html = GetFallbackNewTabHtml();
            }

            // Replace placeholders with actual data
            html = html.Replace("##CUSTOM_DATA##", customJson);
            html = html.Replace("##HISTORY_DATA##", historyJson.ToString());

            return html;
        }

        private string GetFallbackNewTabHtml()
        {
            return @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><title>新标签页</title></head>
<body><h1 style='text-align:center;color:#999;margin-top:50px'>新标签页加载失败</h1></body></html>";
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
            private struct RECT
            { public int Left, Top, Right, Bottom; }

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
                    rc.Left -= 2;        // expand: cover left border
                    rc.Right += 2;        // expand: cover right border
                    rc.Bottom += 2;        // expand: cover bottom border
                    System.Runtime.InteropServices.Marshal.StructureToPtr(rc, m.LParam, false);
                    return;
                }
                base.WndProc(ref m);
            }
        }
    }
}