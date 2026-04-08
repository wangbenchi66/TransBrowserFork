using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TransBrowser.Wpf.Services;
using TransBrowser.Wpf.Tools;

namespace TransBrowser.Wpf
{
    public partial class SettingWindow : Window
    {
        private readonly MainWindow _main;
        private readonly SettingsService _s = SettingsService.Instance;
        private bool _loading = true;

        public SettingWindow(MainWindow main)
        {
            _main = main;
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            _loading = true;
            var s = _s.Current;

            // Opacity slider: 0=opaque (100%), 99=almost transparent (1%)
            SliderOpacity.Value = 100 - (int)s.FormOpacity;
            UpdateOpacityLabel();

            TxtDefaultUrl.Text = s.DefaultUrl ?? "";
            TxtUA.Text = s.DefaultUA ?? "";
            TxtIconPath.Text = s.CustomIconPath ?? "";
            TxtBossKey.Text = s.HotkeyBossKey;
            TxtOpacityUp.Text = s.HotkeyOpacityUp;
            TxtOpacityDown.Text = s.HotkeyOpacityDown;
            TxtClickThrough.Text = s.HotkeyClickThrough;

            SwAutoHide.IsChecked = s.AutoHide;
            SwMobileMold.IsChecked = s.MobileMold;
            SwShowInTaskbar.IsChecked = s.ShowInTaskbar;
            SwShowTabBar.IsChecked = s.ShowTabBar;
            SwHoverHeader.IsChecked = s.HoverHeaderMode;
            SwNoImage.IsChecked = s.NoImageMode;
            SwTransparentBg.IsChecked = s.TransparentBackground;
            SwWindowTransparent.IsChecked = s.WindowTransparent;
            SwGrayscale.IsChecked = s.GrayscaleMode;
            SwAntiScreenshot.IsChecked = s.AntiScreenshotMode;
            SwWindowShadow.IsChecked = s.DisableWindowShadow;
            SwMinimizeNotify.IsChecked = s.ShowMinimizeNotification;
            SwCloseToTray.IsChecked = s.CloseToTray;

            // Color preview
            var c = _s.GetThemeBackColor();
            ColorPreview.Background = new SolidColorBrush(
                System.Windows.Media.Color.FromArgb(c.A, c.R, c.G, c.B));

            // Preset icon combo
            string sel = s.SelectedIconType ?? "Default";
            foreach (ComboBoxItem item in CmbPresetIcons.Items)
                if (item.Content?.ToString() == sel) { CmbPresetIcons.SelectedItem = item; break; }
            if (CmbPresetIcons.SelectedItem == null) CmbPresetIcons.SelectedIndex = 0;

            _loading = false;
        }

        // ── Sync opacity from main window ─────────────────────────────────────
        public void SyncOpacity(int opacityPct)
        {
            SliderOpacity.ValueChanged -= SliderOpacity_ValueChanged;
            SliderOpacity.Value = 100 - opacityPct;
            UpdateOpacityLabel();
            SliderOpacity.ValueChanged += SliderOpacity_ValueChanged;
        }

        private void UpdateOpacityLabel()
        {
            LblOpacityValue.Text = $"{100 - (int)SliderOpacity.Value}%";
        }

        // ── Closing ───────────────────────────────────────────────────────────
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            PersistSettingsFromUI();
        }

        private void PersistSettingsFromUI()
        {
            var s = _s.Current;
            int opacity = 100 - (int)SliderOpacity.Value;
            if (opacity < 1) opacity = 1;
            s.FormOpacity = opacity;
            s.DefaultUrl = TxtDefaultUrl.Text ?? "";
            s.DefaultUA = TxtUA.Text ?? "";
            s.CustomIconPath = TxtIconPath.Text ?? "";
            s.HotkeyBossKey = TxtBossKey.Text ?? s.HotkeyBossKey;
            s.HotkeyOpacityUp = TxtOpacityUp.Text ?? s.HotkeyOpacityUp;
            s.HotkeyOpacityDown = TxtOpacityDown.Text ?? s.HotkeyOpacityDown;
            s.HotkeyClickThrough = TxtClickThrough.Text ?? s.HotkeyClickThrough;
            s.SelectedIconType = (CmbPresetIcons.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Default";
            _s.Save();

            // Apply to main form
            try { _main.SetOpacity(s.FormOpacity); } catch { }
            try { _main.SetTabBarVisible(s.ShowTabBar); } catch { }
            try { _main.SetNoImageMode(s.NoImageMode); } catch { }
            try { _main.SetWindowBackgroundTransparent(s.WindowTransparent); } catch { }
            try { _main.SetAntiScreenshotMode(s.AntiScreenshotMode); } catch { }
            try { _main.SetWindowShadowDisabled(s.DisableWindowShadow); } catch { }
            try { _main.SetMobileMold(s.MobileMold); } catch { }
            try { _main.SetClickThrough(s.ClickThroughMode); } catch { }
            try { _main.SetShowInTaskbar(s.ShowInTaskbar); } catch { }
            try { _main.ReRegisterConfigurableHotkeys(); } catch { }
        }

        // ── Slider ────────────────────────────────────────────────────────────
        private void SliderOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loading) return;
            UpdateOpacityLabel();
            int opacity = 100 - (int)SliderOpacity.Value;
            if (opacity < 1) opacity = 1;
            _main.SetOpacity(opacity);
            _s.Current.FormOpacity = opacity;
            _s.Save();
        }

        // ── URL ───────────────────────────────────────────────────────────────
        private void BtnLoadUrl_Click(object sender, RoutedEventArgs e)
        {
            string url = TxtDefaultUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                url = "https://" + url;
            var tab = _main.AddNewTab(url);
            tab.Tag = url;
            _s.Current.DefaultUrl = url;
            _s.Save();
            try { var bw = _main.GetBrowserWindow(); if (bw != null) _ = bw.InitializeAsync(url); } catch { }
        }

        // ── Color ─────────────────────────────────────────────────────────────
        private void ColorPreview_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Use WinForms ColorDialog (no native WPF equivalent)
            using var dlg = new System.Windows.Forms.ColorDialog();
            var c = _s.GetThemeBackColor();
            dlg.Color = c;
            dlg.FullOpen = true;
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var chosen = dlg.Color;
                ColorPreview.Background = new SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(chosen.A, chosen.R, chosen.G, chosen.B));
                _main.ApplyThemeColor(chosen);
                _s.SetThemeBackColor(chosen);
                _s.Save();
            }
        }

        // ── UA ────────────────────────────────────────────────────────────────
        private void BtnSetUA_Click(object sender, RoutedEventArgs e)
        {
            string ua = TxtUA.Text ?? "";
            _s.Current.DefaultUA = ua;
            _s.Save();
            _main.SetUA(ua);
        }

        // ── Icons ─────────────────────────────────────────────────────────────
        private void BtnBrowseIcon_Click(object sender, RoutedEventArgs e)
        {
            using var ofd = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "Icon files (*.ico)|*.ico|All files (*.*)|*.*"
            };
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _s.Current.CustomIconPath = ofd.FileName;
                _s.Save();
                TxtIconPath.Text = ofd.FileName;
                _main.SetCustomIcon(ofd.FileName);
            }
        }

        private void BtnApplyPresetIcon_Click(object sender, RoutedEventArgs e)
        {
            string sel = (CmbPresetIcons.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Default";
            _s.Current.SelectedIconType = sel;
            _s.Save();
            string? path = ResolvePresetIconPath(sel);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                _main.SetCustomIcon(path);
            else
                _main.SetCustomIcon(_s.Current.CustomIconPath);
        }

        private void BtnRestoreIcon_Click(object sender, RoutedEventArgs e)
        {
            _s.Current.SelectedIconType = "Default";
            _s.Current.CustomIconPath = "";
            _s.Save();
            TxtIconPath.Text = "";
            foreach (ComboBoxItem item in CmbPresetIcons.Items)
                if (item.Content?.ToString() == "Default") { CmbPresetIcons.SelectedItem = item; break; }
            _main.SetCustomIcon(null);
        }

        private static string? ResolvePresetIconPath(string sel) => sel switch
        {
            "Excel" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Office\\root\\Office16\\EXCEL.EXE"),
            "Word" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Office\\root\\Office16\\WINWORD.EXE"),
            "Notepad" => Path.Combine(Environment.SystemDirectory, "notepad.exe"),
            "Chrome" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google\\Chrome\\Application\\chrome.exe"),
            _ => null
        };

        // ── Switch handlers ───────────────────────────────────────────────────
        private void SwAutoHide_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _s.Current.AutoHide = SwAutoHide.IsChecked == true;
            _s.Save();
        }

        private void SwMobileMold_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.SetMobileMold(SwMobileMold.IsChecked == true);
        }

        private void SwShowInTaskbar_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.SetShowInTaskbar(SwShowInTaskbar.IsChecked == true);
        }

        private void SwShowTabBar_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.SetTabBarVisible(SwShowTabBar.IsChecked == true);
            _s.Save();
        }

        private void SwHoverHeader_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.ApplyHoverHeaderMode(SwHoverHeader.IsChecked == true);
            _s.Save();
        }

        private void SwNoImage_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.SetNoImageMode(SwNoImage.IsChecked == true);
            _s.Save();
        }

        private void SwTransparentBg_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.SetTransparentBackground(SwTransparentBg.IsChecked == true);
            _s.Save();
        }

        private void SwWindowTransparent_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _s.Current.WindowTransparent = SwWindowTransparent.IsChecked == true;
            _main.SetWindowBackgroundTransparent(_s.Current.WindowTransparent);
            _s.Save();
        }

        private void SwGrayscale_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.SetGrayscaleMode(SwGrayscale.IsChecked == true);
            _s.Save();
        }

        private void SwAntiScreenshot_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.SetAntiScreenshotMode(SwAntiScreenshot.IsChecked == true);
        }

        private void SwWindowShadow_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _main.SetWindowShadowDisabled(SwWindowShadow.IsChecked == true);
        }

        private void SwMinimizeNotify_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _s.Current.ShowMinimizeNotification = SwMinimizeNotify.IsChecked == true;
            _s.Save();
        }

        private void SwCloseToTray_Changed(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _s.Current.CloseToTray = SwCloseToTray.IsChecked == true;
            _s.Save();
        }

        // ── Hotkey capture ────────────────────────────────────────────────────
        private void HotkeyBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            string formatted = HotkeyParser.FromWpfKeyEventArgs(e);
            if (string.IsNullOrEmpty(formatted)) return;

            var tb = (System.Windows.Controls.TextBox)sender;
            tb.Text = formatted;

            // Persist immediately for boss key and click-through
            if (tb == TxtBossKey)
            {
                _s.Current.HotkeyBossKey = formatted; _s.Save();
                _main.ReRegisterConfigurableHotkeys();
            }
            else if (tb == TxtClickThrough)
            {
                _s.Current.HotkeyClickThrough = formatted; _s.Save();
                _main.ReRegisterConfigurableHotkeys();
            }
        }

        private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ((System.Windows.Controls.TextBox)sender).Background =
                new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 204));
        }

        private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ((System.Windows.Controls.TextBox)sender).Background = System.Windows.Media.Brushes.White;
        }

        private void BtnSaveHotkeys_Click(object sender, RoutedEventArgs e)
        {
            // Conflict check
            var vals = new[] { TxtBossKey.Text, TxtOpacityUp.Text, TxtOpacityDown.Text, TxtClickThrough.Text };
            var labels = new[] { "老板键", "提高透明度", "降低透明度", "鼠标穿透" };
            var seen = new Dictionary<string, string>();
            for (int i = 0; i < vals.Length; i++)
            {
                if (!HotkeyParser.TryParse(vals[i], out _, out _))
                {
                    WpfMsgBox.Show($"热键 '{labels[i]}' 格式无效：{vals[i]}", "热键错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (seen.ContainsKey(vals[i]))
                {
                    WpfMsgBox.Show($"热键冲突：'{labels[i]}' 与 '{seen[vals[i]]}' 相同（{vals[i]}）",
                        "热键冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                seen[vals[i]] = labels[i];
            }

            _s.Current.HotkeyBossKey = TxtBossKey.Text;
            _s.Current.HotkeyOpacityUp = TxtOpacityUp.Text;
            _s.Current.HotkeyOpacityDown = TxtOpacityDown.Text;
            _s.Current.HotkeyClickThrough = TxtClickThrough.Text;
            _s.Save();
            _main.ReRegisterConfigurableHotkeys();
            WpfMsgBox.Show("热键已保存并生效", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
