using CefSharp;
using NCEF.IController;

namespace NCEF.Controller
{
    public class BrowserControllerImpl : IBrowserController
    {
        private readonly RenderSession _session;

        public BrowserControllerImpl(RenderSession session) => _session = session;

        public string GetSpoutId() => _session.SpoutId;
        public void LoadUrl(string url) => _session.Browser.chromiumWebBrowser.Load(url);
        public void ExecuteJs(string script) => _session.Browser.chromiumWebBrowser.EvaluateScriptAsync(script);
        public string GetUrl() => _session.Browser.chromiumWebBrowser.Address;

        public bool Resize(int width, int height, int deviceScaleFactor, bool mobile)
        {
            var browser = _session.Browser.chromiumWebBrowser;
            if (!browser.IsBrowserInitialized) return false;
            using (var devTools = _session.Browser.chromiumWebBrowser.GetDevToolsClient())
            {
                _ = devTools.Emulation.SetDeviceMetricsOverrideAsync(width, height, deviceScaleFactor, mobile);
            }
            return true;
        }

        // --- 鼠标移动 ---
        public void SendMouseMove(int x, int y, bool mouseLeave = false)
        {
            var host = GetHost();
            if (host == null) return;

            var mouseEvent = new MouseEvent(x, y, CefEventFlags.None);
            host.SendMouseMoveEvent(mouseEvent, mouseLeave);
        }

        // --- 鼠标点击 ---
        public void SendMouseClick(int x, int y, int button, bool mouseUp)
        {
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

            // 发送点击事件，clickCount 设为 1 代表单击
            host.SendMouseClickEvent(mouseEvent, btnType, mouseUp, clickCount: 1);
        }

        // --- 鼠标滚轮 ---
        public void SendMouseWheel(int x, int y, int deltaX, int deltaY)
        {
             var host = GetHost();
             if (host == null) return;
             
             var mouseEvent = new MouseEvent(x, y, CefEventFlags.None);
             host.SendMouseWheelEvent(mouseEvent, deltaX, deltaY);
        }

        // --- 键盘按键 (功能键) ---
        public void SendKeyEvent(int windowsKeyCode, bool isUp)
        {
            var host = GetHost();
            if (host == null) return;

            var keyEvent = new KeyEvent();
            keyEvent.WindowsKeyCode = windowsKeyCode;
            keyEvent.Type = isUp ? KeyEventType.KeyUp : KeyEventType.RawKeyDown; // RawKeyDown 用于非字符键
            keyEvent.Modifiers = CefEventFlags.None;
            keyEvent.IsSystemKey = false;

            host.SendKeyEvent(keyEvent);
        }

        // --- 文本输入 ---
        public void SendText(string text)
        {
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

        // --- 获取光标样式 ---
        public int GetCursorType()
        {
            return (int)_session.RenderHandler.CurrentCursor;
        }

        // 辅助方法：获取 IBrowserHost
        private IBrowserHost GetHost()
        {
            var browser = _session.Browser.chromiumWebBrowser.GetBrowser();
            return browser?.GetHost();
        }
    }
}