using log4net;
using System.Text;

[assembly: log4net.Config.XmlConfigurator()]

namespace TransBrowser.Wpf.Tools
{
    public static class LogHelper
    {
        static readonly ILog _logger = LogManager.GetLogger("LogTrace");

        public static void Info(string message) => _logger.Info(message);
        public static void Debug(string message) => _logger.Debug(message);
        public static void Warn(string message) => _logger.Warn(message);
        public static void Error(string message) => _logger.Error(message);

        public static void Error(Exception ex, string memo = "")
        {
            _logger.Error(FormatExceptionDetails(ex, memo));
        }

        private static string FormatExceptionDetails(Exception ex, string memo = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine(memo + "Exception Details:");
            sb.AppendLine($"Type: {ex.GetType().FullName}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine("Stack: " + ex.StackTrace);
            var inner = ex.InnerException;
            while (inner != null)
            {
                sb.AppendLine("Inner: " + inner.Message);
                sb.AppendLine(inner.StackTrace);
                inner = inner.InnerException;
            }
            sb.AppendLine($"Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
            return sb.ToString();
        }
    }
}
