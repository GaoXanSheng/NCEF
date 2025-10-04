using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using NCEF;


#region MainWindow

public class MainWindow
{
    private WebViewManager _webView;
    private GraphicsManager _graphics;

    public MainWindow()
    {
        _webView = new WebViewManager(
            EnvManager.GetString("CUSTOMIZE_LOADING_SCREEN_URL", "https://example.com/"),
            EnvManager.GetInt("MAXFPS", 120)
        );
    }

    public async Task InitAsync()
    {
        await _webView.InitializeAsync(EnvManager.GetInt("BROWSER_PORT", 9222));

        var bounds = Screen.PrimaryScreen.Bounds;
        _graphics = new GraphicsManager(bounds.Width, bounds.Height, EnvManager.GetString("SPOUT_ID", "NCEF"));

        _webView.Browser.Paint += (s, e) => _graphics.HandleBrowserPaint(e);
        _webView.Browser.Size = new Size(bounds.Width, bounds.Height);
    }

    public void OnClosed(EventArgs e)
    {
        _webView?.Browser?.Dispose();
        _graphics?.Dispose();
        Cef.Shutdown();
    }
}

#endregion