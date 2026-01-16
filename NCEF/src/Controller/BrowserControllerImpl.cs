using CefSharp;
using NCEF.Browser;
using NCEF.Handler;

namespace NCEF.Controller
{
    public class BrowserControllerImpl : IBrowserController
    {
        private readonly BrowserSession _session;
        private JsBridgeHandler _jsBridgeHandler;

        public BrowserControllerImpl(BrowserSession session)
        {
            _session = session;
        }

        public string GetSpoutId()
        {
            return _session.SpoutId;
        }

        public void LoadUrl(string url)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            _session.Browser.chromiumWebBrowser.Load(url);
        }

        public void ExecuteJs(string script)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            _session.Browser.chromiumWebBrowser.EvaluateScriptAsync(script);
        }

        public string GetUrl()
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return string.Empty;
            return _session.Browser.chromiumWebBrowser.Address;
        }

        public void SetAudioMuted(bool b)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            _session.Browser.chromiumWebBrowser.GetBrowserHost().SetAudioMuted(b);
        }

        public void SetJsBridge(JsBridgeHandler bridgeHandler)
        {
            _jsBridgeHandler = bridgeHandler;
        }

        public void BindJsBridge()
        {
            var browser = _session.Browser?.chromiumWebBrowser;
            if (browser != null && _jsBridgeHandler != null)
            {
                if (!browser.JavascriptObjectRepository.IsBound("craftBridge"))
                {
                    browser.JavascriptObjectRepository.Register(
                        "craftBridge",
                        _jsBridgeHandler,
                        isAsync: true,
                        options: BindingOptions.DefaultBinder
                    );
                }
            }
        }

        public bool Resize(int width, int height, int deviceScaleFactor, bool mobile)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return false;
            using (var devTools = _session.Browser.chromiumWebBrowser.GetDevToolsClient())
            {
                _ = devTools.Emulation.SetDeviceMetricsOverrideAsync(width, height, deviceScaleFactor, mobile);
            }

            return true;
        }

        public void SendMouseMove(int x, int y, bool mouseLeave = false, bool leftButtonPressed = false)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            var host = GetHost();
            if (host == null) return;
            CefEventFlags flags = CefEventFlags.None;
            if (leftButtonPressed)
            {
                flags |= CefEventFlags.LeftMouseButton;
            }

            var mouseEvent = new MouseEvent(x, y, flags);
            host.SendMouseMoveEvent(mouseEvent, mouseLeave);
        }

        public void SetVolume(float vol)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            _session.AudioHandler.SetVolume(vol);
        }

        public void SendMouseClick(int x, int y, int button, bool mouseUp)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            var host = GetHost();
            if (host == null) return;

            CefEventFlags flags = CefEventFlags.None;
            MouseButtonType btnType = MouseButtonType.Left;

            switch (button)
            {
                case 0:
                    btnType = MouseButtonType.Left;
                    flags |= CefEventFlags.LeftMouseButton;
                    break;
                case 1:
                    btnType = MouseButtonType.Right;
                    flags |= CefEventFlags.RightMouseButton;
                    break;
                case 2:
                    btnType = MouseButtonType.Middle;
                    flags |= CefEventFlags.MiddleMouseButton;
                    break;
            }

            var mouseEvent = new MouseEvent(x, y, flags);
            host.SendMouseClickEvent(mouseEvent, btnType, mouseUp, clickCount: 1);
        }

        public void SendMouseWheel(int x, int y, int deltaX, int deltaY)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            var host = GetHost();
            if (host == null) return;

            var mouseEvent = new MouseEvent(x, y, CefEventFlags.None);
            host.SendMouseWheelEvent(mouseEvent, deltaX, deltaY);
        }

        public void SendKeyEvent(int windowsKeyCode, bool isUp)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            var host = GetHost();
            if (host == null) return;

            var keyEvent = new KeyEvent();
            keyEvent.WindowsKeyCode = windowsKeyCode;
            keyEvent.Type = isUp ? KeyEventType.KeyUp : KeyEventType.RawKeyDown;
            keyEvent.Modifiers = CefEventFlags.None;
            keyEvent.IsSystemKey = false;

            host.SendKeyEvent(keyEvent);
        }

        public void SendText(string text)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            var host = GetHost();
            if (host == null || string.IsNullOrEmpty(text)) return;

            foreach (char c in text)
            {
                var keyEvent = new KeyEvent();
                keyEvent.WindowsKeyCode = c;
                keyEvent.Type = KeyEventType.Char;
                keyEvent.Modifiers = CefEventFlags.None;

                host.SendKeyEvent(keyEvent);
            }
        }

        public int GetCursorType()
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return 0;
            return (int)_session.RenderHandler.CurrentCursor;
        }

        private IBrowserHost GetHost()
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return null;
            var browser = _session.Browser.chromiumWebBrowser.GetBrowser();
            return browser?.GetHost();
        }

        public void ResolveJsPromise(string reqId, object result)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            if (_jsBridgeHandler != null)
            {
                _jsBridgeHandler.CompleteRequest(reqId, result);
            }
        }
    }
}