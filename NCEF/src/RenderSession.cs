using System;
using System.Drawing;
using System.Threading.Tasks;
using CefSharp.OffScreen;
using NCEF.Manager;

namespace NCEF
{
    public class RenderSession : IDisposable
    {
        public BrowserManager Browser { get; private set; }
        public GraphicsManager Graphics { get; private set; }
        public string SpoutId { get; private set; }
        public int Width { get; }
        public int Height { get; }
        private readonly AppController _appController;
        private readonly EventHandler<OnPaintEventArgs> _paintEventHandler;
        private readonly Action<RenderSession> _onSessionDisposed;

        public RenderSession(string url, int width, int height, string spoutId, int maxFps, AppController appController)
        {
            SpoutId = spoutId;
            Width = width;
            Height = height;
            _appController = appController;
            Graphics = new GraphicsManager(width, height, spoutId);
            Browser = new BrowserManager(url, maxFps, spoutId, _appController, Dispose);
            _paintEventHandler = (s, e) => Graphics.HandleBrowserPaint(e);
        }

        public async Task StartAsync(int debugPort)
        {
            await Browser.InitializeAsync(debugPort);
            Browser.chromiumWebBrowser.Size = new Size(Width, Height);
            Browser.chromiumWebBrowser.Paint += _paintEventHandler;
        }
        

        public void Dispose()
        {
            if (Browser?.chromiumWebBrowser != null)
            {
                Browser.chromiumWebBrowser.Paint -= _paintEventHandler;
            }
            Browser?.chromiumWebBrowser?.Dispose();
            Graphics?.Dispose();
            Console.WriteLine($"Disposed Session: {SpoutId}");
            _onSessionDisposed?.Invoke(this);
        }
    }
}