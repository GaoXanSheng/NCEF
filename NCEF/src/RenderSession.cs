using System;
using System.Drawing;
using System.Threading.Tasks;
using CefSharp;

namespace NCEF
{
    public class RenderSession : IDisposable
    {
        public BrowserManager Browser { get; private set; }
        public GraphicsManager Graphics { get; private set; }
        public string SpoutId { get; private set; }
        public int Width { get; }
        public int Height { get; }

        public RenderSession(AppCore appCore, string url, int width, int height, string spoutId, int maxFps)
        {
            SpoutId = spoutId;
            Width = width;
            Height = height;
            
            Graphics = new GraphicsManager(width, height, spoutId);
            Browser = new BrowserManager(appCore, url, maxFps);
        }

        public async Task StartAsync(int debugPort)
        {
            await Browser.InitializeAsync(debugPort);
            
            Browser.Browser.Size = new Size(Width, Height);
            
            Browser.Browser.Paint += (s, e) => Graphics.HandleBrowserPaint(e);
        }

        public void Dispose()
        {
            if (Browser?.Browser != null)
            {
                Browser.Browser.Paint -= (s, e) => Graphics.HandleBrowserPaint(e);
            }
            Browser?.Browser?.Dispose();
            Graphics?.Dispose();
        }
    }
}