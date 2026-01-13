using System;
using System.Threading.Tasks;
using NCEF.Controller;
using NCEF.IController;
using NCEF.Manager;
using NCEF.RPC;
using Size = System.Drawing.Size;

namespace NCEF
{
    public class RenderSession : IDisposable
    {
        public BrowserManager Browser { get; private set; }
        public BrowserRender RenderHandler { get; private set; }
        public string SpoutId { get; private set; }

        private RpcServer<IBrowserController> _rpc; 

        public RenderSession(string url, int width, int height, string spoutId, int maxFps)
        {
            SpoutId = spoutId;
            RenderHandler = new BrowserRender(spoutId,width, height);
            Browser = new BrowserManager(url, maxFps, spoutId, Dispose);
            var impl = new BrowserControllerImpl(this);
            _rpc = new RpcServer<IBrowserController>(spoutId, impl);
        }

        public async Task StartAsync(int width, int height)
        {
            await Browser.InitializeAsync();
            Browser.chromiumWebBrowser.Size = new Size(width, height);
            Browser.chromiumWebBrowser.RenderHandler = RenderHandler;
        }
        public void Dispose()
        {
            _rpc?.Dispose();
            Browser?.chromiumWebBrowser?.Dispose();
            RenderHandler?.Dispose();
        }
    }
}