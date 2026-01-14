using CefSharp;
using NCEF.Handler;
using NCEF.IController;
using System;

namespace NCEF.Controller
{
    public class BrowserControllerImpl : IBrowserController
    {
        private readonly RenderSession _session;
        private JsBridge _jsBridge; 

        public BrowserControllerImpl(RenderSession session) => _session = session;

        public string GetSpoutId()
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return null;
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

        public void SetJsBridge(JsBridge bridge)
        {
            _jsBridge = bridge;
        }

        public void BindJsBridge()
        {
            var browser = _session.Browser?.chromiumWebBrowser;
            if (browser != null && _jsBridge != null)
            {
                if (!browser.JavascriptObjectRepository.IsBound("craftBridge"))
                {
                    browser.JavascriptObjectRepository.Register(
                        "craftBridge", 
                        _jsBridge, 
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

        public void SendMouseMove(int x, int y, bool mouseLeave = false)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            var host = GetHost();
            if (host == null) return;

            var mouseEvent = new MouseEvent(x, y, CefEventFlags.None);
            host.SendMouseMoveEvent(mouseEvent, mouseLeave);
        }

        public void SetVolume(float vol)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            _session.AudioManager.SetVolume(vol);
        }

        public void SendMouseClick(int x, int y, int button, bool mouseUp)
        {
            if (!_session.Browser.chromiumWebBrowser.IsBrowserInitialized) return;
            var host = GetHost();
            if (host == null) return;

            var mouseEvent = new MouseEvent(x, y, CefEventFlags.None);
            MouseButtonType btnType = MouseButtonType.Left;

            switch (button)
            {
                case 0: btnType = MouseButtonType.Left; break;
                case 1: btnType = MouseButtonType.Right; break;
                case 2: btnType = MouseButtonType.Middle; break;
            }
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
            if (_jsBridge != null) 
            {
                _jsBridge.CompleteRequest(reqId, result);
            }
        }
    }
}