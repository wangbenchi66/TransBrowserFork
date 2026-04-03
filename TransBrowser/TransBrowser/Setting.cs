using AntdUI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using static TransBrowser.Tools.GlobalHotkey;

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

        private void Setting_Load(object sender, EventArgs e) { }

        public void Init()
        {
            this.slider1.Value = (int)Properties.Settings.Default.FormOpacity;
            this.input1.Text = Properties.Settings.Default.DefaultUrl;
            this.switch1.Checked = Properties.Settings.Default.NoTitle;
            this.switch2.Checked = Properties.Settings.Default.ShowInTaskbar;
            this.colorPicker1.Value = Properties.Settings.Default.ThemeBackColor;
            this.autohide_sw.Checked = Properties.Settings.Default.AutoHide;

            // Hotkey fields
            this.txtBossKey.Text = Properties.Settings.Default.HotkeyBossKey;
            this.txtOpacityUp.Text = Properties.Settings.Default.HotkeyOpacityUp;
            this.txtOpacityDown.Text = Properties.Settings.Default.HotkeyOpacityDown;
            this.txtClickThrough.Text = Properties.Settings.Default.HotkeyClickThrough;

            // Wire events
            this.slider1.ValueChanged += new AntdUI.IntEventHandler(this.slider1_ValueChanged);
            this.button1.Click += new System.EventHandler(this.button1_Click);
            this.switch1.CheckedChanged += new AntdUI.BoolEventHandler(this.switch1_CheckedChanged);
            this.switch2.CheckedChanged += new AntdUI.BoolEventHandler(this.switch2_CheckedChanged);
            this.colorPicker1.ValueChanged += new AntdUI.ColorEventHandler(this.colorPicker1_ValueChanged);
            this.autohide_sw.CheckedChanged += new AntdUI.BoolEventHandler(this.switch4_CheckedChanged);
            this.btnApplyHotkeys.Click += new System.EventHandler(this.btnApplyHotkeys_Click);

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
                ((TextBox)sender).Text = formatted;
        }

        private void btnApplyHotkeys_Click(object sender, EventArgs e)
        {
            // Conflict check: no two configurable hotkeys may be identical
            var vals = new[] { txtBossKey.Text, txtOpacityUp.Text, txtOpacityDown.Text, txtClickThrough.Text };
            var labels = new[] { "老板键", "不透明度+", "不透明度-", "鼠标穿透" };
            var seen = new Dictionary<string, string>();
            foreach (int i in new[] { 0, 1, 2, 3 })
            {
                if (!Tools.HotkeyParser.TryParse(vals[i], out _, out _))
                {
                    MessageBox.Show($"快捷键 "{labels[i]}" 格式无效：{vals[i]}", "快捷键错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (seen.ContainsKey(vals[i]))
                {
                    MessageBox.Show($"快捷键冲突：{labels[i]} 与 {seen[vals[i]]} 相同（{vals[i]}）",
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

        private void select1_SelectedIndexChanged(object sender, IntEventArgs e) { }

        private void slider1_ValueChanged(object sender, IntEventArgs e)
        {
            mainForm.SetTans(slider1.Value);
            Properties.Settings.Default.FormOpacity = slider1.Value;
            Properties.Settings.Default.Save();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            mainForm.LoadUrl(this.input1.Text);
            Properties.Settings.Default.DefaultUrl = this.input1.Text;
            Properties.Settings.Default.Save();
        }

        private void switch1_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool notitle = e.Value;
            mainForm.ShowWindowsBar(notitle);
            Properties.Settings.Default.NoTitle = notitle;
            Properties.Settings.Default.Save();
        }

        private void switch2_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool showintask = e.Value;
            mainForm.SetShowInTaskBar(showintask);
            Properties.Settings.Default.ShowInTaskbar = showintask;
            Properties.Settings.Default.Save();
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
        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            var ua = this.ua_input.Text;
            Properties.Settings.Default.DefaultUA = ua;
            Properties.Settings.Default.Save();
            mainForm.SetUA(ua);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/yclown/TransBrowser");
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://gitee.com/yclown/TransBrowser");
        }

        private void switch4_CheckedChanged(object sender, BoolEventArgs e)
        {
            bool AutoHide = e.Value;
            Properties.Settings.Default.AutoHide = AutoHide;
            Properties.Settings.Default.Save();
        }
    }
}

