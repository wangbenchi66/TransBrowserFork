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
            // When glass material mode is ON the slider shows material intensity, otherwise opacity.
            bool materialOn = Properties.Settings.Default.WindowTransparent;
            int sliderVal = materialOn
                ? Properties.Settings.Default.MaterialIntensity
                : 100 - (int)Properties.Settings.Default.FormOpacity;
            this.slider1.Value = Math.Max(this.slider1.MinValue, Math.Min(100, sliderVal));
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
            this.swWindowTransparent.Checked = materialOn;

            // Slider label reflects current mode
            UpdateSliderLabel(materialOn);

            // Hotkey fields
            this.txtBossKey.Text = Properties.Settings.Default.HotkeyBossKey;
            this.txtOpacityUp.Text = Properties.Settings.Default.HotkeyOpacityUp;
            this.txtOpacityDown.Text = Properties.Settings.Default.HotkeyOpacityDown;
            this.txtClickThrough.Text = Properties.Settings.Default.HotkeyClickThrough;

            // Wire events
            this.slider1.ValueChanged += slider1_ValueChanged;
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

        /// <summary>Update slider label text to reflect current mode.</summary>
        private void UpdateSliderLabel(bool materialMode)
        {
            label1.Text = materialMode ? "材质强度" : "透明度";
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
            bool materialOn = Properties.Settings.Default.WindowTransparent;
            if (materialOn)
            {
                // Slider controls material intensity (1–100; higher = more opaque tint)
                int intensity = Math.Max(1, Math.Min(100, slider1.Value));
                mainForm?.SetMaterialIntensity(intensity);
                Properties.Settings.Default.MaterialIntensity = intensity;
                Properties.Settings.Default.Save();
            }
            else
            {
                // Slider controls window opacity: 0%透明=100%不透明, 100%透明=1%不透明
                int opacity = 100 - slider1.Value;
                if (opacity < 1) opacity = 1;
                mainForm?.SetTans(opacity);
                Properties.Settings.Default.FormOpacity = opacity;
                Properties.Settings.Default.Save();
            }
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

        public void SyncOpacity(int value)
        {
            bool materialOn = Properties.Settings.Default.WindowTransparent;
            this.slider1.ValueChanged -= slider1_ValueChanged;
            if (materialOn)
            {
                // value is material intensity (1–100) – show directly
                this.slider1.Value = Math.Max(this.slider1.MinValue, Math.Min(100, value));
            }
            else
            {
                // value is opacity (1–100) – convert to transparency display
                int transparencyValue = 100 - value;
                this.slider1.Value = Math.Max(this.slider1.MinValue, Math.Min(100, transparencyValue));
            }
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
            bool enable = e.Value;
            // Always use the persisted material intensity when toggling
            int intensity = Properties.Settings.Default.MaterialIntensity;

            try { mainForm?.ApplyMaterialEffect(enable, intensity); } catch { }

            // Update slider label to reflect the new mode
            UpdateSliderLabel(enable);

            // Sync slider: material mode → show intensity, opacity mode → show transparency
            this.slider1.ValueChanged -= slider1_ValueChanged;
            this.slider1.Value = enable
                ? Properties.Settings.Default.MaterialIntensity
                : 100 - (int)Properties.Settings.Default.FormOpacity;
            this.slider1.ValueChanged += slider1_ValueChanged;
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