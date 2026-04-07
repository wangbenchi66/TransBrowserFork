using System.Windows;
using TransBrowser.Wpf.Services;

namespace TransBrowser.Wpf
{
    public partial class App : System.Windows.Application
    {
        private void App_Startup(object sender, StartupEventArgs e)
        {
            // Load persisted settings before creating any window
            SettingsService.Instance.Load();

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SettingsService.Instance.Save();
            base.OnExit(e);
        }
    }
}
