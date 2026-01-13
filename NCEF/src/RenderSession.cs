using System;
using System.Threading.Tasks;
using NCEF.Controller;
using NCEF.Handler;
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
        public BrowserAudioManager AudioManager { get; private set; }
        private RpcServer<IBrowserController> _rpc; 
        private BrowserControllerImpl _controller;
        public RenderSession(string url, int width, int height, string spoutId, int maxFps)
        {
            SpoutId = spoutId;
            Browser = new BrowserManager(url, maxFps, spoutId, Dispose);
            RenderHandler = new BrowserRender(spoutId,width, height);
            AudioManager = new BrowserAudioManager();
            _controller = new BrowserControllerImpl(this);
            _rpc = new RpcServer<IBrowserController>(spoutId, _controller);
            _controller.SetJsBridge(new JsBridge(_rpc));
        }

        public async Task StartAsync(int width, int height)
        {
            await Browser.InitializeAsync(_controller);
            Browser.chromiumWebBrowser.Size = new Size(width, height);
            Browser.chromiumWebBrowser.RenderHandler = RenderHandler;
            Browser.chromiumWebBrowser.AudioHandler = AudioManager;

        }
        public void Dispose()
        {
            _rpc?.Dispose();
            Browser?.chromiumWebBrowser?.Dispose();
            RenderHandler?.Dispose();
        }
    }
}