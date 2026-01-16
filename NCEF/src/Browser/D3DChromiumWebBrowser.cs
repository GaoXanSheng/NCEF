using System;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.OffScreen;
using NCEF.Controller;

namespace NCEF.Browser
{
    public class D3DChromiumWebBrowser : ChromiumWebBrowser
    {
        private readonly BrowserControllerImpl _controller;
        private readonly Action _onClose;

        public D3DChromiumWebBrowser(string url,BrowserSettings browserSettings,string cachePath) : base(url, null,
            new RequestContext(new RequestContextSettings() { CachePath =cachePath }),false)
        {
            WindowInfo windowInfo = new WindowInfo();
            windowInfo.SetAsWindowless(IntPtr.Zero);
            windowInfo.WindowlessRenderingEnabled = true;
            windowInfo.ExternalBeginFrameEnabled = false;
            windowInfo.SharedTextureEnabled = true;
            CreateBrowser(windowInfo, browserSettings);
        }

        public async Task WaitReadyAsync()
        {
            await WaitForInitialLoadAsync();
        }
    }
}