using System;
using System.Collections.Generic;
using System.Windows.Forms;
using static TransBrowser.Tools.GlobalHotkey;

namespace TransBrowser.Tools
{
    /// <summary>
    /// Parses and formats hotkey strings like "Ctrl+Alt+Up".
    /// </summary>
    public static class HotkeyParser
    {
        /// <summary>
        /// Tries to parse a hotkey string (e.g. "Ctrl+Alt+Up") into modifier flags and a virtual key.
        /// </summary>
        public static bool TryParse(string hotkeyStr, out KeyModifiers modifiers, out Keys vk)
        {
            modifiers = KeyModifiers.None;
            vk = Keys.None;

            if (string.IsNullOrWhiteSpace(hotkeyStr))
                return false;

            var parts = hotkeyStr.Split(new char[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            Keys parsedKey = Keys.None;

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                switch (trimmed.ToLowerInvariant())
                {
                    case "ctrl":
                    case "control":
                        modifiers |= KeyModifiers.Ctrl;
                        break;
                    case "alt":
                        modifiers |= KeyModifiers.Alt;
                        break;
                    case "shift":
                        modifiers |= KeyModifiers.Shift;
                        break;
                    case "win":
                    case "windows":
                        modifiers |= KeyModifiers.WindowsKey;
                        break;
                    default:
                        if (Enum.TryParse<Keys>(trimmed, true, out Keys k))
                            parsedKey = k;
                        else
                            return false;
                        break;
                }
            }

            vk = parsedKey;
            return vk != Keys.None;
        }

        /// <summary>
        /// Formats modifier flags and a virtual key into a human-readable string.
        /// </summary>
        public static string Format(KeyModifiers modifiers, Keys vk)
        {
            var parts = new List<string>();
            if ((modifiers & KeyModifiers.Ctrl) != 0) parts.Add("Ctrl");
            if ((modifiers & KeyModifiers.Alt) != 0) parts.Add("Alt");
            if ((modifiers & KeyModifiers.Shift) != 0) parts.Add("Shift");
            if ((modifiers & KeyModifiers.WindowsKey) != 0) parts.Add("Win");
            if (vk != Keys.None)
                parts.Add(vk.ToString());
            return string.Join("+", parts);
        }

        /// <summary>
        /// Builds a hotkey string from a KeyEventArgs, stripping modifier-only presses.
        /// Returns empty string if no non-modifier key is present.
        /// </summary>
        public static string FromKeyEventArgs(KeyEventArgs e)
        {
            // Ignore pure modifier key presses
            if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Menu ||
                e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
                return string.Empty;

            var mods = KeyModifiers.None;
            if (e.Control) mods |= KeyModifiers.Ctrl;
            if (e.Alt) mods |= KeyModifiers.Alt;
            if (e.Shift) mods |= KeyModifiers.Shift;
            return Format(mods, e.KeyCode);
        }
    }
}
