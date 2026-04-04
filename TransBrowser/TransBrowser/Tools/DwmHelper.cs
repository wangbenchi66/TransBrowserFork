using System;
using System.Runtime.InteropServices;

namespace TransBrowser.Tools
{
    /// <summary>
    /// Provides DWM (Desktop Window Manager) material effects for Win10 Acrylic and Win11 Mica.
    /// </summary>
    internal static class DwmHelper
    {
        // ── P/Invoke declarations ──────────────────────────────────────────────

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd,
            ref WindowCompositionAttributeData data);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr,
            ref int pvAttribute, int cbAttribute);

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd,
            ref MARGINS pMarInset);

        // ── Structures ────────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;  // packed ABGR (little-endian: R, G, B, A)
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        // ── Constants ─────────────────────────────────────────────────────────

        private const int WCA_ACCENT_POLICY = 19;

        private const int ACCENT_DISABLED = 0;
        private const int ACCENT_ENABLE_BLURBEHIND = 3;
        private const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

        // Win11 22H2+ (build 22621+)
        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_AUTO = 1;
        private const int DWMSBT_MAINWINDOW = 2;        // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3;   // Acrylic

        // Win11 original (build 22000–22620) – undocumented
        private const int DWMWA_MICA_EFFECT = 1029;

        // ── OS version helpers ────────────────────────────────────────────────

        /// <summary>Windows 11 or higher (build ≥ 22000).</summary>
        public static bool IsWindows11OrHigher =>
            Environment.OSVersion.Version.Build >= 22000;

        /// <summary>Windows 11 22H2 or higher (build ≥ 22621).</summary>
        public static bool IsWin11Build22H2OrHigher =>
            Environment.OSVersion.Version.Build >= 22621;

        /// <summary>Windows 10 1809 or higher (build ≥ 17763) – Acrylic supported.</summary>
        public static bool IsAcrylicSupported =>
            Environment.OSVersion.Version.Build >= 17763;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Apply Mica (Win11) or Acrylic (Win10) glass material to the given window handle.
        /// </summary>
        /// <param name="hwnd">Win32 window handle.</param>
        /// <param name="intensity">
        /// Tint intensity: 0 = nearly transparent (almost pure blur), 100 = opaque tint.
        /// Only applies to the Acrylic path; Mica ignores this value.
        /// </param>
        public static void ApplyMaterial(IntPtr hwnd, int intensity)
        {
            try
            {
                if (IsWindows11OrHigher)
                    ApplyMica(hwnd);
                else if (IsAcrylicSupported)
                    ApplyAcrylic(hwnd, intensity);
                else
                    ApplyLegacyBlur(hwnd, intensity);
            }
            catch { /* DWM not available – silently ignore */ }
        }

        /// <summary>Remove any previously applied DWM material from the window.</summary>
        public static void RemoveMaterial(IntPtr hwnd)
        {
            try
            {
                if (IsWindows11OrHigher)
                {
                    int none = DWMSBT_AUTO;
                    DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref none, sizeof(int));

                    int disable = 0;
                    DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref disable, sizeof(int));
                }

                // Always reset the accent policy (covers Win10 and as fallback on Win11)
                SetAccentPolicy(hwnd, ACCENT_DISABLED, 0, 0);

                // Reset frame extension
                var noMargins = new MARGINS();
                DwmExtendFrameIntoClientArea(hwnd, ref noMargins);
            }
            catch { }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private static void ApplyMica(IntPtr hwnd)
        {
            // Extend the DWM glass frame to cover the entire client area.
            // With the frame extended, GDI pixels painted with Color.Black are treated
            // as DWM-transparent (alpha = 0), allowing Mica to show through.
            var fullMargins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref fullMargins);

            if (IsWin11Build22H2OrHigher)
            {
                // Windows 11 22H2+: official DWMWA_SYSTEMBACKDROP_TYPE API
                int backdropType = DWMSBT_MAINWINDOW;
                DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdropType, sizeof(int));
            }
            else
            {
                // Windows 11 original (22000–22620): undocumented Mica attribute
                int micaEnabled = 1;
                DwmSetWindowAttribute(hwnd, DWMWA_MICA_EFFECT, ref micaEnabled, sizeof(int));
            }
        }

        private static void ApplyAcrylic(IntPtr hwnd, int intensity)
        {
            // Extend frame so DWM-black = transparent trick works for child controls.
            var fullMargins = new MARGINS { cxLeftWidth = -1, cxRightWidth = -1, cyTopHeight = -1, cyBottomHeight = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref fullMargins);

            // Map intensity 0–100 to tint alpha 15–200 so the effect is never invisible.
            int alpha = 15 + (int)(intensity * 1.85);
            alpha = Math.Max(15, Math.Min(200, alpha));

            // GradientColor is packed ABGR (in little-endian memory: R, G, B, A).
            // White tint: R=FF, G=FF, B=FF, A=alpha → stored as (A<<24)|(B<<16)|(G<<8)|R
            // But in the struct field it is passed as a 32-bit int with bytes: [A][B][G][R]
            int gradientColor = (alpha << 24) | (0xFF << 16) | (0xFF << 8) | 0xFF;

            SetAccentPolicy(hwnd, ACCENT_ENABLE_ACRYLICBLURBEHIND, 0x20, gradientColor);
        }

        private static void ApplyLegacyBlur(IntPtr hwnd, int intensity)
        {
            // Fallback for older Windows – plain blur without tint.
            int alpha = 15 + (int)(intensity * 1.85);
            alpha = Math.Max(15, Math.Min(200, alpha));
            int gradientColor = (alpha << 24) | (0xFF << 16) | (0xFF << 8) | 0xFF;
            SetAccentPolicy(hwnd, ACCENT_ENABLE_BLURBEHIND, 0, gradientColor);
        }

        private static void SetAccentPolicy(IntPtr hwnd, int accentState, int flags, int gradientColor)
        {
            var accent = new AccentPolicy
            {
                AccentState = accentState,
                AccentFlags = flags,
                GradientColor = gradientColor,
                AnimationId = 0
            };

            int accentSize = Marshal.SizeOf(accent);
            IntPtr accentPtr = Marshal.AllocHGlobal(accentSize);
            try
            {
                Marshal.StructureToPtr(accent, accentPtr, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WCA_ACCENT_POLICY,
                    SizeOfData = accentSize,
                    Data = accentPtr
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(accentPtr);
            }
        }
    }
}
