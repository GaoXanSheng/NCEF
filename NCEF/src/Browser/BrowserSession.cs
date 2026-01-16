using System;
using System.Threading.Tasks;
using NCEF.Controller;
using NCEF.Handler;
using NCEF.RPC;
using Size = System.Drawing.Size;

namespace NCEF.Browser
{
    public class BrowserSession : IDisposable
    {
        public BrowserInstance Browser { get; private set; }
        public RenderHandler RenderHandler { get; private set; }
        public string SpoutId { get; private set; }
        public AudioHandler AudioHandler { get; private set; }
        private RpcServer<IBrowserController> _rpc;
        private BrowserControllerImpl _controller;

        public BrowserSession(string url, string spoutId, int maxFps)
        {
            SpoutId = spoutId;
            Browser = new BrowserInstance(url, maxFps, spoutId, Dispose);
            RenderHandler = new RenderHandler(spoutId);
            AudioHandler = new AudioHandler();
            _controller = new BrowserControllerImpl(this);
            _rpc = new RpcServer<IBrowserController>(spoutId, _controller);
            _controller.SetJsBridge(new JsBridgeHandler(_rpc));
        }

        public async Task StartAsync()
        {
            await Browser.InitializeAsync(_controller);
            Browser.chromiumWebBrowser.Size= new Size(800, 600);
            Browser.chromiumWebBrowser.AudioHandler = AudioHandler;
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