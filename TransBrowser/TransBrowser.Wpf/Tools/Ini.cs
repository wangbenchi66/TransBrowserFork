using System.IO;
using System.Text;

namespace TransBrowser.Wpf.Tools
{
    public class Ini
    {
        readonly Dictionary<string, Dictionary<string, string>> _ini =
            new(StringComparer.InvariantCultureIgnoreCase);
        readonly string _file;

        public Ini(string file)
        {
            _file = file;
            if (!File.Exists(file)) File.Create(file).Dispose();
            Load();
        }

        public void Load()
        {
            var txt = File.ReadAllText(_file);
            var currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            _ini[""] = currentSection;

            int idx = 0;
            foreach (var line in txt.Split('\n').Select(l => l.Trim()))
            {
                if (line.StartsWith(";") || string.IsNullOrWhiteSpace(line))
                { currentSection[";" + idx++] = line; continue; }
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
                    _ini[line[1..^1]] = currentSection; continue;
                }
                int eq = line.IndexOf('=');
                currentSection[eq < 0 ? line : line[..eq]] = eq < 0 ? "" : line[(eq + 1)..];
                idx++;
            }
        }

        public string GetValue(string key) => GetValue(key, "", "");
        public string GetValue(string key, string section) => GetValue(key, section, "");
        public string GetValue(string key, string section, string @default) =>
            _ini.TryGetValue(section, out var s) && s.TryGetValue(key, out var v) ? v : @default;

        public void WriteValue(string key, string value) => WriteValue(key, "", value);
        public void WriteValue(string key, string section, string value)
        {
            if (!_ini.TryGetValue(section, out var sec))
                _ini[section] = sec = new Dictionary<string, string>();
            sec[key] = value;
        }

        public void Save()
        {
            var sb = new StringBuilder();
            foreach (var section in _ini)
            {
                if (section.Key != "") sb.AppendLine($"[{section.Key}]");
                foreach (var kv in section.Value)
                {
                    if (kv.Key.StartsWith(";")) sb.AppendLine(kv.Value);
                    else sb.AppendLine($"{kv.Key}={kv.Value}");
                }
                sb.AppendLine();
            }
            File.WriteAllText(_file, sb.ToString());
        }
    }
}
