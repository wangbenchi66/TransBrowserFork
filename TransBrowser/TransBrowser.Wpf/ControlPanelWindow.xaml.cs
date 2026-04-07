using System.IO;
using System.Windows;
using TransBrowser.Wpf.Tools;

namespace TransBrowser.Wpf
{
    public partial class ControlPanelWindow : Window
    {
        private readonly MainWindow _main;
        private readonly Ini _ini;

        public ControlPanelWindow(MainWindow main)
        {
            _main = main;
            InitializeComponent();

            _ini = new Ini(Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "controlpanel.ini"));

            TxtJsCode.Text = _ini.GetValue("Runjs");

            // Auto-run hook: subscribe to navigation completed of the active webview
            var wv = _main.GetWebView2();
            if (wv != null) wv.NavigationCompleted += WebView_NavigationCompleted;
        }

        private void WebView_NavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (ChkAutoRun.IsChecked == true && !string.IsNullOrEmpty(TxtJsCode.Text))
                _main.RunJs(TxtJsCode.Text);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => _main.GoBack();
        private void BtnForward_Click(object sender, RoutedEventArgs e) => _main.GoForward();

        private void BtnRunJs_Click(object sender, RoutedEventArgs e)
        {
            string code = TxtJsCode.Text;
            if (string.IsNullOrEmpty(code)) { WpfMsgBox.Show("请输入JS代码"); return; }
            _main.RunJs(code);
        }

        private void BtnLoadFile_Click(object sender, RoutedEventArgs e)
        {
            using var ofd = new System.Windows.Forms.OpenFileDialog
            {
                Title = "选择要注入的JS文件",
                Filter = "JS内容 (*.js;*.txt)|*.js;*.txt"
            };
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    string content = File.ReadAllText(ofd.FileName);
                    TxtJsCode.Text = content;
                    _ini.WriteValue("Runjs", content);
                    _ini.Save();
                }
                catch (Exception ex) { WpfMsgBox.Show(ex.Message); }
            }
        }

        /// <summary>Called by hotkey handler to run JS from the panel.</summary>
        public void RunCurrentJs()
        {
            if (!string.IsNullOrEmpty(TxtJsCode.Text))
                _main.RunJs(TxtJsCode.Text);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Hide instead of close, like WinForms version
            e.Cancel = true;
            Hide();
        }
    }
}
