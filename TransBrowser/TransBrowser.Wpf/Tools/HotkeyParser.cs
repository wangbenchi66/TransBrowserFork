using System.Windows.Forms;
using static TransBrowser.Wpf.Tools.GlobalHotkey;

namespace TransBrowser.Wpf.Tools
{
    /// <summary>Parses and formats hotkey strings like "Ctrl+Alt+Up".</summary>
    public static class HotkeyParser
    {
        public static bool TryParse(string hotkeyStr, out KeyModifiers modifiers, out Keys vk)
        {
            modifiers = KeyModifiers.None;
            vk = Keys.None;
            if (string.IsNullOrWhiteSpace(hotkeyStr)) return false;

            var parts = hotkeyStr.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            Keys parsedKey = Keys.None;

            foreach (var part in parts)
            {
                switch (part.Trim().ToLowerInvariant())
                {
                    case "ctrl": case "control": modifiers |= KeyModifiers.Ctrl; break;
                    case "alt": modifiers |= KeyModifiers.Alt; break;
                    case "shift": modifiers |= KeyModifiers.Shift; break;
                    case "win": case "windows": modifiers |= KeyModifiers.WindowsKey; break;
                    default:
                        if (Enum.TryParse<Keys>(part.Trim(), true, out Keys k))
                            parsedKey = k;
                        else
                            return false;
                        break;
                }
            }
            vk = parsedKey;
            return vk != Keys.None;
        }

        public static string Format(KeyModifiers modifiers, Keys vk)
        {
            var parts = new List<string>();
            if ((modifiers & KeyModifiers.Ctrl) != 0) parts.Add("Ctrl");
            if ((modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((modifiers & KeyModifiers.WindowsKey) != 0) parts.Add("Win");
            if (vk != Keys.None) parts.Add(vk.ToString());
            return string.Join("+", parts);
        }

        /// <summary>Builds a hotkey string from a WPF KeyEventArgs.</summary>
        public static string FromWpfKeyEventArgs(System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
            // ignore pure modifiers
            if (key == System.Windows.Input.Key.LeftCtrl || key == System.Windows.Input.Key.RightCtrl ||
                key == System.Windows.Input.Key.LeftAlt || key == System.Windows.Input.Key.RightAlt ||
                key == System.Windows.Input.Key.LeftShift || key == System.Windows.Input.Key.RightShift ||
                key == System.Windows.Input.Key.LWin || key == System.Windows.Input.Key.RWin)
                return string.Empty;

            var mods = KeyModifiers.None;
            if (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) mods |= KeyModifiers.Ctrl;
            if (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) mods |= KeyModifiers.Alt;
            if (e.KeyboardDevice.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) mods |= KeyModifiers.Shift;

            // Convert WPF Key to WinForms Keys
            if (Enum.TryParse<Keys>(key.ToString(), true, out Keys wfKey))
                return Format(mods, wfKey);
            return string.Empty;
        }
    }
}
