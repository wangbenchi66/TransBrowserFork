using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TransBrowser.Tools
{
    /// <summary>
    /// INI 配置文件管理器
    /// 负责读取、写入和验证 config.ini 配置文件
    /// </summary>
    public class IniConfigManager
    {
        // ─── Windows API for INI file operations ───────────────────────────
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPrivateProfileString(
            string section, string key, string defaultValue,
            StringBuilder returnValue, int size, string filePath);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool WritePrivateProfileString(
            string section, string key, string value, string filePath);

        // ─── Configuration file path ───────────────────────────────────────
        private readonly string _configPath;
        private readonly Dictionary<string, string> _errors = new Dictionary<string, string>();

        public IniConfigManager(string configFileName = "config.ini")
        {
            string appDir = Path.GetDirectoryName(Application.ExecutablePath);
            _configPath = Path.Combine(appDir, configFileName);

            // 如果配置文件不存在，创建默认配置
            if (!File.Exists(_configPath))
            {
                CreateDefaultConfig();
            }
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        public string ConfigPath => _configPath;

        /// <summary>
        /// 获取所有配置错误信息
        /// </summary>
        public IReadOnlyDictionary<string, string> Errors => _errors;

        /// <summary>
        /// 是否有配置错误
        /// </summary>
        public bool HasErrors => _errors.Count > 0;

        // ─── Read methods with validation ──────────────────────────────────

        /// <summary>
        /// 读取整数值，带验证
        /// </summary>
        public int GetInt(string section, string key, int defaultValue, int? min = null, int? max = null)
        {
            string value = ReadValue(section, key, defaultValue.ToString());
            if (int.TryParse(value, out int result))
            {
                // 范围验证
                if (min.HasValue && result < min.Value)
                {
                    RecordError(section, key, $"值 {result} 小于最小值 {min.Value}，使用默认值 {defaultValue}");
                    return defaultValue;
                }
                if (max.HasValue && result > max.Value)
                {
                    RecordError(section, key, $"值 {result} 大于最大值 {max.Value}，使用默认值 {defaultValue}");
                    return defaultValue;
                }
                return result;
            }
            RecordError(section, key, $"无效的整数值 '{value}'，使用默认值 {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// 读取布尔值，带验证
        /// </summary>
        public bool GetBool(string section, string key, bool defaultValue)
        {
            string value = ReadValue(section, key, defaultValue.ToString()).ToLower();
            if (value == "true" || value == "1" || value == "yes" || value == "on")
                return true;
            if (value == "false" || value == "0" || value == "no" || value == "off")
                return false;

            RecordError(section, key, $"无效的布尔值 '{value}'，使用默认值 {defaultValue}");
            return defaultValue;
        }

        /// <summary>
        /// 读取字符串值
        /// </summary>
        public string GetString(string section, string key, string defaultValue)
        {
            return ReadValue(section, key, defaultValue);
        }

        /// <summary>
        /// 读取颜色值，带验证
        /// </summary>
        public Color GetColor(string section, string key, Color defaultValue)
        {
            string value = ReadValue(section, key, ColorToString(defaultValue));
            
            // 尝试解析颜色名称
            try
            {
                if (value.StartsWith("#"))
                {
                    // 十六进制颜色 #RRGGBB
                    if (value.Length == 7)
                    {
                        int r = Convert.ToInt32(value.Substring(1, 2), 16);
                        int g = Convert.ToInt32(value.Substring(3, 2), 16);
                        int b = Convert.ToInt32(value.Substring(5, 2), 16);
                        return Color.FromArgb(r, g, b);
                    }
                }
                else
                {
                    // 颜色名称
                    return Color.FromName(value);
                }
            }
            catch
            {
                RecordError(section, key, $"无效的颜色值 '{value}'，使用默认值 {ColorToString(defaultValue)}");
            }
            return defaultValue;
        }

        /// <summary>
        /// 读取点（位置）值，带验证
        /// </summary>
        public Point GetPoint(string section, string key, Point defaultValue)
        {
            string value = ReadValue(section, key, $"{defaultValue.X},{defaultValue.Y}");
            var parts = value.Split(',');
            if (parts.Length == 2 && 
                int.TryParse(parts[0].Trim(), out int x) && 
                int.TryParse(parts[1].Trim(), out int y))
            {
                return new Point(x, y);
            }
            RecordError(section, key, $"无效的坐标值 '{value}'，使用默认值 {defaultValue.X},{defaultValue.Y}");
            return defaultValue;
        }

        /// <summary>
        /// 读取尺寸值，带验证
        /// </summary>
        public Size GetSize(string section, string key, Size defaultValue)
        {
            string value = ReadValue(section, key, $"{defaultValue.Width},{defaultValue.Height}");
            var parts = value.Split(',');
            if (parts.Length == 2 && 
                int.TryParse(parts[0].Trim(), out int width) && 
                int.TryParse(parts[1].Trim(), out int height))
            {
                // 验证尺寸合理性
                if (width < 200 || height < 150)
                {
                    RecordError(section, key, $"窗口尺寸过小 {width}x{height}，使用默认值 {defaultValue.Width}x{defaultValue.Height}");
                    return defaultValue;
                }
                return new Size(width, height);
            }
            RecordError(section, key, $"无效的尺寸值 '{value}'，使用默认值 {defaultValue.Width}x{defaultValue.Height}");
            return defaultValue;
        }

        /// <summary>
        /// 读取快捷键，带验证
        /// </summary>
        public string GetHotkey(string section, string key, string defaultValue)
        {
            string value = ReadValue(section, key, defaultValue);
            if (HotkeyParser.TryParse(value, out _, out _))
                return value;

            RecordError(section, key, $"无效的快捷键格式 '{value}'，使用默认值 {defaultValue}");
            return defaultValue;
        }

        // ─── Write methods ─────────────────────────────────────────────────

        public void SetInt(string section, string key, int value)
        {
            WriteValue(section, key, value.ToString());
        }

        public void SetBool(string section, string key, bool value)
        {
            WriteValue(section, key, value.ToString().ToLower());
        }

        public void SetString(string section, string key, string value)
        {
            WriteValue(section, key, value ?? "");
        }

        public void SetColor(string section, string key, Color value)
        {
            WriteValue(section, key, ColorToString(value));
        }

        public void SetPoint(string section, string key, Point value)
        {
            WriteValue(section, key, $"{value.X},{value.Y}");
        }

        public void SetSize(string section, string key, Size value)
        {
            WriteValue(section, key, $"{value.Width},{value.Height}");
        }

        // ─── Helper methods ────────────────────────────────────────────────

        private string ReadValue(string section, string key, string defaultValue)
        {
            var sb = new StringBuilder(2048);
            GetPrivateProfileString(section, key, defaultValue, sb, sb.Capacity, _configPath);
            return sb.ToString();
        }

        private void WriteValue(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, _configPath);
        }

        private void RecordError(string section, string key, string message)
        {
            _errors[$"{section}.{key}"] = message;
        }

        private string ColorToString(Color color)
        {
            if (color.IsNamedColor)
                return color.Name;
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private void CreateDefaultConfig()
        {
            // config.ini 文件已经在项目中创建了，只需要复制到输出目录
            // 这个方法在文件不存在时可以创建一个最小配置
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# TransBrowser 配置文件");
            sb.AppendLine("# 已使用默认配置初始化");
            sb.AppendLine();
            sb.AppendLine("[Window]");
            sb.AppendLine("Opacity=100");
            sb.AppendLine("Position=100,100");
            sb.AppendLine("Size=800,600");
            sb.AppendLine("ThemeColor=White");
            sb.AppendLine();
            sb.AppendLine("[Browser]");
            sb.AppendLine("AutoHide=false");
            sb.AppendLine();
            sb.AppendLine("[Hotkeys]");
            sb.AppendLine("BossKey=Alt+Q");
            
            File.WriteAllText(_configPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// 显示所有配置错误
        /// </summary>
        public void ShowErrors()
        {
            if (!HasErrors) return;

            var sb = new StringBuilder();
            sb.AppendLine("配置文件中发现以下问题，已使用默认值：");
            sb.AppendLine();
            foreach (var error in _errors)
            {
                sb.AppendLine($"• {error.Key}: {error.Value}");
            }
            sb.AppendLine();
            sb.AppendLine($"配置文件位置：{_configPath}");

            MessageBox.Show(sb.ToString(), "配置警告", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
