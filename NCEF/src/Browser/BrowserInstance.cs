using System;
using System.IO;
using System.Threading.Tasks;
using NCEF.Controller;

namespace NCEF.Browser
{
    public class BrowserInstance
    {
        public WebView2WebBrowser WebBrowser { get; private set; }
        private string InitialUrl { get; }
        private readonly string _spoutId;
        private readonly Action _onClose;

        public BrowserInstance(string url, int maxFPS, string spoutId, Action onClose)
        {
            InitialUrl = url;
            _spoutId = spoutId;
            _onClose = onClose;
        }

        public async Task InitializeAsync(BrowserControllerImpl _controller)
        {
            string userDataPath = Path.Combine(Environment.CurrentDirectory, "User Data");
            WebBrowser = new WebView2WebBrowser(_spoutId);
            await WebBrowser.InitializeAsync(InitialUrl, userDataPath);
        }
    }
}