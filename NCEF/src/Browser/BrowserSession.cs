using System;
using System.Threading.Tasks;
using NCEF.Controller;
using NCEF.Handler;
using NCEF.RPC;

namespace NCEF.Browser
{
    public class BrowserSession : IDisposable
    {
        public BrowserInstance Browser { get; private set; }
        public string SpoutId { get; private set; }
        public AudioHandler AudioHandler { get; private set; }
        private RpcServer<IBrowserController> _rpc;
        private BrowserControllerImpl _controller;

        public BrowserSession(string url, string spoutId, int maxFps)
        {
            SpoutId = spoutId;
            Browser = new BrowserInstance(url, maxFps, spoutId, Dispose);
            AudioHandler = new AudioHandler();
            _controller = new BrowserControllerImpl(this);
            _rpc = new RpcServer<IBrowserController>(spoutId, _controller);
            _controller.SetJsBridge(new JsBridgeHandler(_rpc));
        }

        public async Task StartAsync(int w, int h)
        {
            await Browser.InitializeAsync(_controller);
            Browser.WebBrowser.Resize(w, h);
        }

        public void Dispose()
        {
            _rpc?.Dispose();
            Browser?.WebBrowser?.Dispose();
        }
    }
}