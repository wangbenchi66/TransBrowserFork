using AntdUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TransBrowser
{
    public partial class Setting : Window
    {
        private MainForm mainForm;
        public Setting(MainForm mainForm)
        {
            this.mainForm = mainForm;
            InitializeComponent();
            Init();
        }

        private void swMinimizeNotify_CheckedChanged(object sender, AntdUI.BoolEventArgs e)
        {
            try
            {
                Properties.Settings.Default.ShowMinimizeNotification = e.Value;
                Properties.Settings.Default.Save();
            }
            catch { }
        }
        public Setting()
        {
            InitializeComponent();
        }

        // Remove default OS drop shadow for the settings window as well.
        protected override CreateParams CreateParams
        {
            get
            {
                // Do not modify class style here. Shadow policy is controlled by
                // the main window according to user settings so Settings should
                // retain the default OS drop shadow.
                return base.CreateParams;
            }
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        private const int DWMWA_NCRENDERING_POLICY = 2;
        private const int DWMNCRP_DISABLED = 1;

        protected override void OnHandleCreated(EventArgs e)
        {
            // Do not change DWM non-client rendering policy for the settings window.
            // The main form is responsible for applying the user's DisableWindowShadow setting.
            base.OnHandleCreated(e);
        }

        private void Setting_Load(object sender, EventArgs e)
        { }

        private void Setting_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                PersistSettingsFromUI();

                // Apply settings immediately to main form if available
                try
                {
                    if (mainForm != null)
                    {
                        try { mainForm.SetShowInTaskBar(Properties.Settings.Default.ShowInTaskbar); } catch { }
                        try { mainForm.SetTans(Properties.Settings.Default.FormOpacity); } catch { }
                        try { mainForm.SetTabBarVisible(Properties.Settings.Default.ShowTabBar); } catch { }
                        try { mainForm.SetNoImageMode(Properties.Settings.Default.NoImageMode); } catch { }
                        try { mainForm.SetWindowBackgroundTransparent(Properties.Settings.Default.WindowTransparent); } catch { }
                        try { mainForm.SetAntiScreenshotMode(Properties.Settings.Default.AntiScreenshotMode); } catch { }
                        try { mainForm.SetWindowShadowDisabled(Properties.Settings.Default.DisableWindowShadow); } catch { }
                        try { mainForm.SetMobileMold(Properties.Settings.Default.MobileMold); } catch { }
                        try { mainForm.SetClickThrough(Properties.Settings.Default.ClickThroughMode); } catch { }

                        try
                        {
                            string custom = Properties.Settings.Default.CustomIconPath;
                            if (!string.IsNullOrEmpty(custom) && System.IO.File.Exists(custom))
                            {
                                mainForm.SetCustomIcon(custom);
                            }
                            else
                            {
                                string sel = Properties.Settings.Default.SelectedIconType ?? "Default";
                                string path = null;
                                switch (sel)
                                {
                                    case "Excel":
                                        path = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Microsoft Office\\root\\Office16\\EXCEL.EXE";
                                        break;
                                    case "Word":
                                        path = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Microsoft Office\\root\\Office16\\WINWORD.EXE";
                                        break;
                                    case "Notepad":
                                        path = System.IO.Path.Combine(Environment.SystemDirectory, "notepad.exe");
                                        break;
                                    case "Chrome":
                                        path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google\\Chrome\\Application\\chrome.exe");
                                        break;
                                    default:
                                        path = null; break;
                                }
                                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                                    mainForm.SetCustomIcon(path);
                                else
                                    mainForm.SetCustomIcon(null);
                            }
                        }
                        catch { }

                        try { mainForm.ReRegisterConfigurableHotkeys(); } catch { }
                    }
                }
                catch { }
            }
            catch { }
        }

        public void Init()
        {
            // Ensure we have a reference to the running MainForm instance. Sometimes the
            // designer or callers use the parameterless constructor so mainForm may be null.
            if (this.mainForm == null)
            {
                foreach (Form f in Application.OpenForms)
                {
                    if (f is MainForm mf)
                    {
                        this.mainForm = mf;
                        break;
                    }
                }
            }

            // 不透明度改为透明度：100%不透明 = 0%透明
            this.slider1.Value = 100 - (int)Properties.Settings.Default.FormOpacity;
            this.inputUrl.Text = Properties.Settings.Default.DefaultUrl;
            this.colorPicker1.Value = Properties.Settings.Default.ThemeBackColor;
            this.autohide_sw.Checked = Properties.Settings.Default.AutoHide;
            this.swShowTabBar.Checked = Properties.Settings.Default.ShowTabBar;
            // Taskbar display toggle
            try { this.switch2.Checked = Properties.Settings.Default.ShowInTaskbar; } catch { }
            this.swNoImage.Checked = Properties.Settings.Default.NoImageMode;
            this.swHoverHeader.Checked = Properties.Settings.Default.HoverHeaderMode;
            this.swTransparentBg.Checked = Properties.Settings.Default.TransparentBackground;

            // 新增功能设置
            this.swGrayscale.Checked = Properties.Settings.Default.GrayscaleMode;
            this.swAntiScreenshot.Checked = Properties.Settings.Default.AntiScreenshotMode;
            this.swWindowTransparent.Checked = Properties.Settings.Default.WindowTransparent;
            // window shadow switch
            try
            {
                this.swWindowShadow.Checked = Properties.Settings.Default.DisableWindowShadow;
            }
            catch { }
            // Hotkey fields
            this.txtBossKey.Text = Properties.Settings.Default.HotkeyBossKey;
            this.txtOpacityUp.Text = Properties.Settings.Default.HotkeyOpacityUp;
            this.txtOpacityDown.Text = Properties.Settings.Default.HotkeyOpacityDown;
            this.txtClickThrough.Text = Properties.Settings.Default.HotkeyClickThrough;

            // Wire events
            this.slider1.ValueChanged += new AntdUI.IntEventHandler(this.slider1_ValueChanged);
            this.btnOpenUrl.Click += new System.EventHandler(this.button1_Click);
            this.colorPicker1.ValueChanged += new AntdUI.ColorEventHandler(this.colorPicker1_ValueChanged);
            this.switch2.CheckedChanged += new AntdUI.BoolEventHandler(this.switch2_CheckedChanged);
            this.autohide_sw.CheckedChanged += new AntdUI.BoolEventHandler(this.switch4_CheckedChanged);
            this.btnApplyHotkeys.Click += new System.EventHandler(this.btnApplyHotkeys_Click);
            this.swShowTabBar.CheckedChanged += new AntdUI.BoolEventHandler(this.swShowTabBar_CheckedChanged);
            this.swNoImage.CheckedChanged += new AntdUI.BoolEventHandler(this.swNoImage_CheckedChanged);
            this.swHoverHeader.CheckedChanged += new AntdUI.BoolEventHandler(this.swHoverHeader_CheckedChanged);
            this.swTransparentBg.CheckedChanged += new AntdUI.BoolEventHandler(this.swTransparentBg_CheckedChanged);
            this.swGrayscale.CheckedChanged += new AntdUI.BoolEventHandler(this.swGrayscale_CheckedChanged);
            this.swCloseToTray.CheckedChanged += new AntdUI.BoolEventHandler(this.swCloseToTray_CheckedChanged);
            // window shadow switch (new)
            try { this.swWindowShadow.CheckedChanged += new AntdUI.BoolEventHandler(this.swWindowShadow_CheckedChanged); } catch { }
            this.swAntiScreenshot.CheckedChanged += new AntdUI.BoolEventHandler(this.swAntiScreenshot_CheckedChanged);
            this.swWindowTransparent.CheckedChanged += new AntdUI.BoolEventHandler(this.swWindowTransparent_CheckedChanged);
            this.swMinimizeNotify.CheckedChanged += new AntdUI.BoolEventHandler(this.swMinimizeNotify_CheckedChanged);

            // icon controls
            try { this.txtIconPath.Text = Properties.Settings.Default.CustomIconPath ?? ""; } catch { }
            try { this.btnBrowseIcon.Click += new EventHandler(this.btnBrowseIcon_Click); } catch { }
            try { this.swCloseToTray.Checked = Properties.Settings.Default.CloseToTray; } catch { }
            try { /* ComboBox already populated in designer; set selected value */ this.cmbPresetIcons.SelectedItem = Properties.Settings.Default.SelectedIconType ?? "Default"; } catch { }
            try { this.swMinimizeNotify.Checked = Properties.Settings.Default.ShowMinimizeNotification; } catch { }

            // Hotkey textboxes capture key presses
            foreach (TextBox tb in new[] { txtBossKey, txtOpacityUp, txtOpacityDown, txtClickThrough })
            {
                tb.ReadOnly = true;
                tb.KeyDown += HotkeyTextBox_KeyDown;
                tb.GotFocus += (s, e2) => ((TextBox)s).BackColor = Color.LightYellow;
                tb.LostFocus += (s, e2) => ((TextBox)s).BackColor = SystemColors.Window;
            }

            // Ensure settings are saved when the settings window is closed
            this.FormClosing += Setting_FormClosing;
        }

        private void PersistSettingsFromUI()
        {
            try
            {
                // Opacity: slider stores transparency value (slider.Value), convert to opacity
                int opacity = 100 - this.slider1.Value;
                if (opacity < 1) opacity = 1;
                Properties.Settings.Default.FormOpacity = opacity;

                Properties.Settings.Default.DefaultUrl = this.inputUrl.Text ?? "";
                Properties.Settings.Default.ThemeBackColor = this.colorPicker1.Value;
                Properties.Settings.Default.AutoHide = this.autohide_sw.Checked;
                try { Properties.Settings.Default.ShowInTaskbar = this.switch2.Checked; } catch { }
                Properties.Settings.Default.ShowTabBar = this.swShowTabBar.Checked;
                Properties.Settings.Default.NoImageMode = this.swNoImage.Checked;
                Properties.Settings.Default.HoverHeaderMode = this.swHoverHeader.Checked;
                Properties.Settings.Default.TransparentBackground = this.swTransparentBg.Checked;
                Properties.Settings.Default.GrayscaleMode = this.swGrayscale.Checked;
                Properties.Settings.Default.AntiScreenshotMode = this.swAntiScreenshot.Checked;
                Properties.Settings.Default.WindowTransparent = this.swWindowTransparent.Checked;
                try { Properties.Settings.Default.DisableWindowShadow = this.swWindowShadow.Checked; } catch { }

                // Hotkeys
                try { Properties.Settings.Default.HotkeyBossKey = this.txtBossKey.Text ?? Properties.Settings.Default.HotkeyBossKey; } catch { }
                try { Properties.Settings.Default.HotkeyOpacityUp = this.txtOpacityUp.Text ?? Properties.Settings.Default.HotkeyOpacityUp; } catch { }
                try { Properties.Settings.Default.HotkeyOpacityDown = this.txtOpacityDown.Text ?? Properties.Settings.Default.HotkeyOpacityDown; } catch { }
                try { Properties.Settings.Default.HotkeyClickThrough = this.txtClickThrough.Text ?? Properties.Settings.Default.HotkeyClickThrough; } catch { }

                // Icon settings
                try { Properties.Settings.Default.CustomIconPath = this.txtIconPath.Text ?? ""; } catch { }
                try { Properties.Settings.Default.SelectedIconType = this.cmbPresetIcons.SelectedItem?.ToString() ?? Properties.Settings.Default.SelectedIconType; } catch { }

                // Close-to-tray preference
                try { Properties.Settings.Default.CloseToTray = this.swCloseToTray.Checked; } catch { }

                // If main form present, persist its window state values
                try
                {
                    if (this.mainForm != null)
                    {
                        if (this.mainForm.WindowState == FormWindowState.Normal)
                        {
                            Properties.Settings.Default.FormPosition = this.mainForm.Location;
                            Properties.Settings.Default.FormSize = this.mainForm.Size;
                        }
                        Properties.Settings.Default.TopMostWindow = this.mainForm.TopMost;
                    }
                }
                catch { }

                Properties.Settings.Default.Save();
            }
            catch { }
        }

        private void switch2_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool show = e.Value;
            try
            {
                Properties.Settings.Default.ShowInTaskbar = show;
                Properties.Settings.Default.Save();
                mainForm?.SetShowInTaskBar(show);
            }
            catch { }
        }

        private void btnBrowseIcon_Click(object sender, EventArgs e)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Icon files (*.ico)|*.ico|All files (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        Properties.Settings.Default.CustomIconPath = ofd.FileName;
                        Properties.Settings.Default.Save();
                        this.txtIconPath.Text = ofd.FileName;
                        mainForm?.SetCustomIcon(ofd.FileName);
                    }
                    catch { }
                }
            }
        }
        private void swWindowShadow_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool disabled = e.Value; // switch on -> disable shadow
            try
            {
                Properties.Settings.Default.DisableWindowShadow = disabled;
                Properties.Settings.Default.Save();
                mainForm?.SetWindowShadowDisabled(disabled);
            }
            catch { }
        }

        private void swCloseToTray_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool closeToTray = e.Value;
            try
            {
                Properties.Settings.Default.CloseToTray = closeToTray;
                Properties.Settings.Default.Save();
            }
            catch { }
        }

        private void btnApplyIconSelection_Click(object sender, EventArgs e)
        {
            string sel = "Default";
            try { sel = this.cmbPresetIcons.SelectedItem?.ToString() ?? "Default"; } catch { }
            try
            {
                Properties.Settings.Default.SelectedIconType = sel;
                Properties.Settings.Default.Save();

                // Map presets to known executable icons
                string path = null;
                switch (sel)
                {
                    case "Excel":
                        path = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Microsoft Office\\root\\Office16\\EXCEL.EXE";
                        break;
                    case "Word":
                        path = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Microsoft Office\\root\\Office16\\WINWORD.EXE";
                        break;
                    case "Notepad":
                        path = System.IO.Path.Combine(Environment.SystemDirectory, "notepad.exe");
                        break;
                    case "Chrome":
                        path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google\\Chrome\\Application\\chrome.exe");
                        break;
                    default:
                        path = null; break;
                }

                if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
                {
                    mainForm?.SetCustomIcon(path);
                    AntdUI.Message.success(this, "已应用伪装图标");
                }
                else if (!string.IsNullOrEmpty(Properties.Settings.Default.CustomIconPath))
                {
                    // If custom icon set, prefer that
                    mainForm?.SetCustomIcon(Properties.Settings.Default.CustomIconPath);
                    AntdUI.Message.success(this, "已应用自定义图标");
                }
                else
                {
                    // fallback to exe icon
                    mainForm?.SetCustomIcon(null);
                    AntdUI.Message.success(this, "已恢复默认图标");
                }
            }
            catch { }
        }

        private void btnRestoreDefaultIcon_Click(object sender, EventArgs e)
        {
            try
            {
                Properties.Settings.Default.SelectedIconType = "Default";
                Properties.Settings.Default.CustomIconPath = "";
                Properties.Settings.Default.Save();
                this.txtIconPath.Text = "";
                this.cmbPresetIcons.SelectedItem = "Default";
                mainForm?.SetCustomIcon(null);
                AntdUI.Message.success(this, "已恢复默认图标");
            }
            catch { }
        }

        // ─── Hotkey capture ───────────────────────────────────────────────────

        private void HotkeyTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            e.SuppressKeyPress = true;
            string formatted = Tools.HotkeyParser.FromKeyEventArgs(e);
            if (!string.IsNullOrEmpty(formatted))
            {
                var tb = (TextBox)sender;
                tb.Text = formatted;

                // Persist immediately for configurable hotkeys so they take effect without pressing 保存
                try
                {
                    if (tb == txtBossKey)
                    {
                        Properties.Settings.Default.HotkeyBossKey = formatted;
                        Properties.Settings.Default.Save();
                        mainForm?.ReRegisterConfigurableHotkeys();
                    }
                    else if (tb == txtClickThrough)
                    {
                        Properties.Settings.Default.HotkeyClickThrough = formatted;
                        Properties.Settings.Default.Save();
                        mainForm?.ReRegisterConfigurableHotkeys();
                    }
                    else
                    {
                        // For other fields keep value in textbox; user may still press 保存
                    }
                }
                catch { }
            }
        }

        private void btnApplyHotkeys_Click(object sender, EventArgs e)
        {
            // Conflict check: no two configurable hotkeys may be identical
            var vals = new[] { txtBossKey.Text, txtOpacityUp.Text, txtOpacityDown.Text, txtClickThrough.Text };
            var labels = new[] { "老板键", "降低透明度", "提高透明度", "鼠标穿透" };
            var seen = new Dictionary<string, string>();
            foreach (int i in new[] { 0, 1, 2, 3 })
            {
                if (!Tools.HotkeyParser.TryParse(vals[i], out _, out _))
                {
                    MessageBox.Show($"快捷键 '{labels[i]}' 格式无效：{vals[i]}", "快捷键错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (seen.ContainsKey(vals[i]))
                {
                    MessageBox.Show($"快捷键冲突：'{labels[i]}' 与 '{seen[vals[i]]}' 相同（{vals[i]}）",
                        "快捷键冲突", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                seen[vals[i]] = labels[i];
            }

            Properties.Settings.Default.HotkeyBossKey = txtBossKey.Text;
            Properties.Settings.Default.HotkeyOpacityUp = txtOpacityUp.Text;
            Properties.Settings.Default.HotkeyOpacityDown = txtOpacityDown.Text;
            Properties.Settings.Default.HotkeyClickThrough = txtClickThrough.Text;
            Properties.Settings.Default.Save();

            mainForm?.ReRegisterConfigurableHotkeys();

            AntdUI.Message.success(this, "快捷键已保存并生效");
        }

        // ─── Existing handlers ────────────────────────────────────────────────

        private void select1_SelectedIndexChanged(object sender, IntEventArgs e)
        { }

        private void slider1_ValueChanged(object sender, IntEventArgs e)
        {
            // 透明度slider：0%透明=100%不透明，100%透明=1%不透明
            int opacity = 100 - slider1.Value;
            // 确保至少有1%不透明度，避免完全透明
            if (opacity < 1) opacity = 1;
            mainForm.SetTans(opacity);
            Properties.Settings.Default.FormOpacity = opacity;
            Properties.Settings.Default.Save();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            // 修改3：点击加载按钮时在新标签页中打开网页
            string url = this.inputUrl.Text.Trim();
            if (!string.IsNullOrEmpty(url))
            {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }
                mainForm.AddNewTab(url);
                Properties.Settings.Default.DefaultUrl = url;
                Properties.Settings.Default.Save();
            }
        }

        private void colorPicker1_ValueChanged(object sender, ColorEventArgs e)
        {
            var color = e.Value;
            mainForm.SetDefaultColor(color);
            Properties.Settings.Default.ThemeBackColor = color;
            Properties.Settings.Default.Save();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            this.ShowInTaskbar = !this.ShowInTaskbar;
        }

        private void switch3_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool MobileMold = e.Value;
            Properties.Settings.Default.MobileMold = MobileMold;
            Properties.Settings.Default.Save();
            mainForm?.SetMobileMold(MobileMold);
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            var ua = this.ua_input.Text;
            Properties.Settings.Default.DefaultUA = ua;
            Properties.Settings.Default.Save();
            mainForm.SetUA(ua);
        }

        private void switch4_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool AutoHide = e.Value;
            Properties.Settings.Default.AutoHide = AutoHide;
            Properties.Settings.Default.Save();
        }

        public void SyncOpacity(int opacityValue)
        {
            // opacityValue是不透明度(1-100)，需要转换为透明度显示
            int transparencyValue = 100 - opacityValue;
            this.slider1.ValueChanged -= slider1_ValueChanged;
            this.slider1.Value = transparencyValue;
            this.slider1.ValueChanged += slider1_ValueChanged;
        }

        private void swShowTabBar_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool showTabBar = e.Value;
            mainForm.SetTabBarVisible(showTabBar);
            Properties.Settings.Default.ShowTabBar = showTabBar;
            Properties.Settings.Default.Save();
        }

        private void swNoImage_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool noImage = e.Value;
            mainForm.SetNoImageMode(noImage);
            Properties.Settings.Default.NoImageMode = noImage;
            Properties.Settings.Default.Save();
        }

        private void swHoverHeader_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool hoverMode = e.Value;
            mainForm?.ApplyHoverHeaderMode(hoverMode);
            Properties.Settings.Default.HoverHeaderMode = hoverMode;
            Properties.Settings.Default.Save();
        }

        private void swTransparentBg_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool transparentBg = e.Value;
            mainForm.SetTransparentBackground(transparentBg);
            Properties.Settings.Default.TransparentBackground = transparentBg;
            Properties.Settings.Default.Save();
        }

        private void swGrayscale_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool grayscaleMode = e.Value;
            mainForm.SetGrayscaleMode(grayscaleMode);
            Properties.Settings.Default.GrayscaleMode = grayscaleMode;
            Properties.Settings.Default.Save();
        }

        private void swWindowTransparent_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool windowTransparent = e.Value;
            Properties.Settings.Default.WindowTransparent = windowTransparent;
            Properties.Settings.Default.Save();
            try { mainForm?.SetWindowBackgroundTransparent(windowTransparent); } catch { }
        }

        private void swAntiScreenshot_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool antiScreenshot = e.Value;
            mainForm.SetAntiScreenshotMode(antiScreenshot);
            Properties.Settings.Default.AntiScreenshotMode = antiScreenshot;
            Properties.Settings.Default.Save();
        }
    }
}