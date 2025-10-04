using CefSharp;

namespace NCEF
{
    public class LifeSpanHandler: ILifeSpanHandler
    {
        public bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl,
            string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures,
            IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
        {
            newBrowser = null;
            chromiumWebBrowser.Load(targetUrl);
            return true;
        }

        public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
        public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) => false;
        public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser) { }
    }
}