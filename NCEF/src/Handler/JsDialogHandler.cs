using CefSharp;


namespace NCEF.Handler
{
    public class JsDialogHandler : IJsDialogHandler
    {
        public JsDialogHandler()
        {
 
        }

        public bool OnJSDialog(IWebBrowser browserControl, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage)
        {
            return true;
        }

        public bool OnBeforeUnloadDialog(IWebBrowser browserControl, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        {
            callback.Continue(true);
            return true;
        }

        public void OnResetDialogState(IWebBrowser browserControl, IBrowser browser)
        {
        }

        public void OnDialogClosed(IWebBrowser browserControl, IBrowser browser)
        {
        }

        private void ShowDialog(CefJsDialogType type, string message, string defaultPrompt, IJsDialogCallback callback)
        {
            return;
        }
    }
}
