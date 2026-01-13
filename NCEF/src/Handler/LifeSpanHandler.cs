using System;
using CefSharp;

namespace NCEF.Handler
{
    public class LifeSpanHandler : ILifeSpanHandler
    {
        private readonly Action _onBeforeClose;
        private int _closeCalled = 0;

        public LifeSpanHandler(Action onBeforeClose)
        {
            _onBeforeClose = onBeforeClose;
        }

        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl,
            string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures,
            IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null;
            chromiumWebBrowser.Load(targetUrl);
            return true;
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
           
        }

        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            return false;
        }

        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
          
        }
    }
}