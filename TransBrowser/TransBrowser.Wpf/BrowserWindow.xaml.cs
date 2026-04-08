using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Windows;

namespace TransBrowser.Wpf
{
    public partial class BrowserWindow : Window
    {
        public BrowserWindow()
        {
            InitializeComponent();
        }

        public async System.Threading.Tasks.Task InitializeAsync(string initialUrl)
        {
            await BrowserWebView.EnsureCoreWebView2Async();
            // Ensure the webview background is transparent and inject a document-created script
            try
            {
                BrowserWebView.DefaultBackgroundColor = System.Drawing.Color.Transparent;
                if (BrowserWebView.CoreWebView2 != null)
                {
                    // forward core events
                    BrowserWebView.CoreWebView2.DocumentTitleChanged += (_, _) => TitleChanged?.Invoke(this, BrowserWebView.CoreWebView2.DocumentTitle ?? "");
                    BrowserWebView.CoreWebView2.NavigationCompleted += (_, e) => NavigationCompleted?.Invoke(this, BrowserWebView.Source?.AbsoluteUri);

                    // Inject a strong transparent CSS on document created (best-effort)
                    string transparentCss =
                        "(function(){var s=document.getElementById('__trans_bg');" +
                        "if(!s){s=document.createElement('style');s.id='__trans_bg';(document.head||document.documentElement).appendChild(s);}" +
                        "s.textContent='html,body,div,section,header,footer,main,article{background:transparent!important;background-color:transparent!important;background-image:none!important}*{background:transparent!important;background-color:transparent!important;background-image:none!important}';})();";
                    try { await BrowserWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(transparentCss); } catch { }

                    // (WebResourceRequested handler removed) Rely on document-created script and NavigateTransparentAsync for HTML injection.
                }
            }
            catch { }

            if (!string.IsNullOrEmpty(initialUrl))
                BrowserWebView.CoreWebView2.Navigate(initialUrl);
        }

        // Try to fetch the page HTML, inject transparent CSS + <base>, and load via NavigateToString.
        // This is best-effort: some sites (SPA/CSP) may not work properly.
        public async System.Threading.Tasks.Task NavigateTransparentAsync(string url, bool isWeChat = false)
        {
            try
            {
                await BrowserWebView.EnsureCoreWebView2Async();
                if (string.IsNullOrEmpty(url)) return;
                using var http = new System.Net.Http.HttpClient();
                var resp = await http.GetAsync(url);
                if (!resp.IsSuccessStatusCode) { BrowserWebView.CoreWebView2.Navigate(url); return; }
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                var body = System.Text.Encoding.UTF8.GetString(bytes);
                // inject <base> and transparent style
                string baseTag = $"<base href=\"{url}\">";
                string style = "<style id=\"__trans_inject\">html,body,div,section,header,footer,main,article{background:transparent!important;background-color:transparent!important;background-image:none!important}*{background:transparent!important;background-color:transparent!important;background-image:none!important}</style>";
                // extra rules for WeChat reader / similar reader pages
                if (isWeChat)
                {
                    var wechatCss =
                        "<style id=\"__we_chat_fix\">#js_content,.rich_media,.rich_media_area_primary,article,section{background:transparent!important;background-color:transparent!important}p,div{background:transparent!important}</style>";
                    style = wechatCss + style;
                }
                int headIndex = body.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
                int insertPos = -1;
                if (headIndex >= 0)
                {
                    int headClose = body.IndexOf('>', headIndex);
                    insertPos = headClose >= 0 ? headClose + 1 : -1;
                }
                if (insertPos < 0) insertPos = 0;
                body = body.Insert(insertPos, baseTag + style);
                BrowserWebView.CoreWebView2.NavigateToString(body);
            }
            catch
            {
                try { BrowserWebView.CoreWebView2.Navigate(url); } catch { }
            }
        }

        public void SetBounds(double left, double top, double width, double height)
        {
            this.Left = left; this.Top = top; this.Width = width; this.Height = height;
        }

        public CoreWebView2? GetCore() => BrowserWebView.CoreWebView2;

        // Expose the internal WebView2 control so MainWindow can interact with it
        public WebView2 BrowserControl => BrowserWebView;

        public event EventHandler<string>? TitleChanged;
        public event EventHandler<string?>? NavigationCompleted;
    }
}