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
        public Setting()
        {
            InitializeComponent();
        }

        private void Setting_Load(object sender, EventArgs e)
        { }

        public void Init()
        {
            // 不透明度改为透明度：100%不透明 = 0%透明
            this.slider1.Value = 100 - (int)Properties.Settings.Default.FormOpacity;
            this.inputUrl.Text = Properties.Settings.Default.DefaultUrl;
            this.colorPicker1.Value = Properties.Settings.Default.ThemeBackColor;
            this.autohide_sw.Checked = Properties.Settings.Default.AutoHide;
            this.swShowTabBar.Checked = Properties.Settings.Default.ShowTabBar;
            this.swNoImage.Checked = Properties.Settings.Default.NoImageMode;
            this.swHoverHeader.Checked = Properties.Settings.Default.HoverHeaderMode;
            this.swTransparentBg.Checked = Properties.Settings.Default.TransparentBackground;

            // 新增功能设置
            this.swGrayscale.Checked = Properties.Settings.Default.GrayscaleMode;
            this.swAntiScreenshot.Checked = Properties.Settings.Default.AntiScreenshotMode;
            this.swWindowTransparent.Checked = Properties.Settings.Default.WindowTransparent;

            // Hotkey fields
            this.txtBossKey.Text = Properties.Settings.Default.HotkeyBossKey;
            this.txtOpacityUp.Text = Properties.Settings.Default.HotkeyOpacityUp;
            this.txtOpacityDown.Text = Properties.Settings.Default.HotkeyOpacityDown;
            this.txtClickThrough.Text = Properties.Settings.Default.HotkeyClickThrough;

            // Wire events
            this.slider1.ValueChanged += new AntdUI.IntEventHandler(this.slider1_ValueChanged);
            this.btnOpenUrl.Click += new System.EventHandler(this.button1_Click);
            this.colorPicker1.ValueChanged += new AntdUI.ColorEventHandler(this.colorPicker1_ValueChanged);
            this.autohide_sw.CheckedChanged += new AntdUI.BoolEventHandler(this.switch4_CheckedChanged);
            this.btnApplyHotkeys.Click += new System.EventHandler(this.btnApplyHotkeys_Click);
            this.swShowTabBar.CheckedChanged += new AntdUI.BoolEventHandler(this.swShowTabBar_CheckedChanged);
            this.swNoImage.CheckedChanged += new AntdUI.BoolEventHandler(this.swNoImage_CheckedChanged);
            this.swHoverHeader.CheckedChanged += new AntdUI.BoolEventHandler(this.swHoverHeader_CheckedChanged);
            this.swTransparentBg.CheckedChanged += new AntdUI.BoolEventHandler(this.swTransparentBg_CheckedChanged);
            this.swGrayscale.CheckedChanged += new AntdUI.BoolEventHandler(this.swGrayscale_CheckedChanged);
            this.swAntiScreenshot.CheckedChanged += new AntdUI.BoolEventHandler(this.swAntiScreenshot_CheckedChanged);
            this.swWindowTransparent.CheckedChanged += new AntdUI.BoolEventHandler(this.swWindowTransparent_CheckedChanged);

            // Hotkey textboxes capture key presses
            foreach (TextBox tb in new[] { txtBossKey, txtOpacityUp, txtOpacityDown, txtClickThrough })
            {
                tb.ReadOnly = true;
                tb.KeyDown += HotkeyTextBox_KeyDown;
                tb.GotFocus += (s, e2) => ((TextBox)s).BackColor = Color.LightYellow;
                tb.LostFocus += (s, e2) => ((TextBox)s).BackColor = SystemColors.Window;
            }
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
            // Clamp to 1 so we never save 0 (which Init() would reset to 100 on next launch)
            int opacity = Math.Max(1, 100 - e.Value);
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