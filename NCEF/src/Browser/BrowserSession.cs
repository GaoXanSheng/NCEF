using System;
using System.Threading.Tasks;
using log4net;
using NCEF.Controller;
using NCEF.Handler;
using NCEF.RPC;

namespace NCEF.Browser
{
    public class BrowserSession : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(BrowserSession));

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
            Log.Info($"BrowserSession created for SpoutID: {spoutId}");
        }

        public async Task StartAsync(int w, int h)
        {
            Log.Info($"Starting BrowserSession for SpoutID: {SpoutId} (Size: {w}x{h})");
            await Browser.InitializeAsync(_controller);
            Browser.WebBrowser.Resize(w, h);
        }

        public void Dispose()
        {
            Log.Info($"Disposing BrowserSession for SpoutID: {SpoutId}");
            try
            {
                _rpc?.Dispose();
                Browser?.WebBrowser?.Dispose();
                AudioHandler?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Error($"Error disposing BrowserSession {SpoutId}: " + ex.Message, ex);
            }
            _rpc = null;
            AudioHandler = null;
            _controller = null;
        }
    }
}