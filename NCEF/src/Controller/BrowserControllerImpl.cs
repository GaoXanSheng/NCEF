using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using NCEF.Browser;
using NCEF.Handler;

namespace NCEF.Controller
{
    public class BrowserControllerImpl : IBrowserController
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint WM_CHAR = 0x0102;
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        private const uint WM_MBUTTONDOWN = 0x0207;
        private const uint WM_MBUTTONUP = 0x0208;
        private const uint WM_MOUSEMOVE = 0x0200;
        private const uint WM_MOUSEWHEEL = 0x020A;

        private readonly BrowserSession _session;
        private JsBridgeHandler _jsBridgeHandler;

        public BrowserControllerImpl(BrowserSession session)
        {
            _session = session;
        }

        private CoreWebView2Controller Controller => _session.Browser?.WebBrowser?.Controller;
        private CoreWebView2 CoreWebView2 => _session.Browser?.WebBrowser?.CoreWebView2;

        private T RunOnUIThread<T>(Func<T> func)
        {
            if (Program.MainSyncContext != null)
            {
                T result = default;
                Exception exception = null;
                using (var evt = new System.Threading.ManualResetEvent(false))
                {
                    Program.MainSyncContext.Post(_ =>
                    {
                        try { result = func(); }
                        catch (Exception ex) { exception = ex; }
                        finally { evt.Set(); }
                    }, null);
                    evt.WaitOne();
                }
                if (exception != null) Program.Logger.Error("UI Func Failed", exception);
                return result;
            }
            else
            {
                try { return func(); } catch { return default; }
            }
        }
        private void RunOnUIThread(Action action)
        {
            if (Program.MainSyncContext != null)
            {
                Program.MainSyncContext.Post(_ =>
                {
                    try { action(); } catch { }
                }, null);
            }
            else
            {
                try { action(); } catch { }
            }
        }

        public string GetSpoutId()
        {
            return _session.SpoutId;
        }

        public void LoadUrl(string url)
        {
            RunOnUIThread(() => CoreWebView2?.Navigate(url));
        }

        public void ExecuteJs(string script)
        {
            RunOnUIThread(() => CoreWebView2?.ExecuteScriptAsync(script));
        }

        public string GetUrl()
        {
            return RunOnUIThread(() => CoreWebView2?.Source) ?? string.Empty;
        }

        public void SetAudioMuted(bool b)
        {
            RunOnUIThread(() =>
            {
                if (CoreWebView2 != null)
                {
                    CoreWebView2.IsMuted = b;
                }
            });
        }

        public void SetJsBridge(JsBridgeHandler bridgeHandler)
        {
            _jsBridgeHandler = bridgeHandler;
        }

        public void BindJsBridge()
        {
            RunOnUIThread(() =>
            {
                if (CoreWebView2 != null && _jsBridgeHandler != null)
                {
                    CoreWebView2.AddHostObjectToScript("craftBridge", _jsBridgeHandler);
                }
            });
        }

        public bool Resize(int width, int height, int deviceScaleFactor, bool mobile)
        {
            RunOnUIThread(() => _session.Browser?.WebBrowser?.Resize(width, height));
            return true;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, System.Text.StringBuilder lpClassName, int nMaxCount);

        private IntPtr GetInputTargetHwnd(IntPtr parentHwnd)
        {
            if (parentHwnd == IntPtr.Zero) return IntPtr.Zero;

            IntPtr foundHwnd = IntPtr.Zero;
            EnumChildWindows(parentHwnd, (hWnd, lParam) =>
            {
                var sb = new System.Text.StringBuilder(256);
                GetClassName(hWnd, sb, sb.Capacity);
                string cls = sb.ToString();
                if (cls.Contains("Chrome_RenderWidgetHostHWND") || cls.Contains("Intermediate D3D Window"))
                {
                    foundHwnd = hWnd;
                    return false; // Stop enumeration
                }
                return true;
            }, IntPtr.Zero);

            return foundHwnd != IntPtr.Zero ? foundHwnd : parentHwnd;
        }

        private const uint WM_MOUSELEAVE = 0x02A3;

        public void SendMouseMove(int x, int y, bool mouseLeave = false, bool leftButtonPressed = false)
        {
            RunOnUIThread(() =>
            {
                var controller = Controller;
                if (controller == null) return;
                IntPtr targetHwnd = GetInputTargetHwnd(controller.ParentWindow);
                if (mouseLeave)
                {
                    PostMessage(targetHwnd, WM_MOUSELEAVE, IntPtr.Zero, IntPtr.Zero);
                }
                else
                {
                    IntPtr wParam = leftButtonPressed ? (IntPtr)0x0001 /* MK_LBUTTON */ : IntPtr.Zero;
                    IntPtr lParam = (IntPtr)((ushort)x | ((ushort)y << 16));
                    PostMessage(targetHwnd, WM_MOUSEMOVE, wParam, lParam);
                }
            });
        }

        public void SetVolume(float vol)
        {
            _session.AudioHandler.SetVolume(vol);
        }

        public void SendMouseClick(int x, int y, int button, bool mouseUp)
        {
            RunOnUIThread(() =>
            {
                var controller = Controller;
                if (controller == null) return;
                IntPtr targetHwnd = GetInputTargetHwnd(controller.ParentWindow);
                IntPtr lParam = (IntPtr)((ushort)x | ((ushort)y << 16));
                uint msg = 0;
                IntPtr wParam = IntPtr.Zero;
                switch (button)
                {
                    case 0:
                        msg = mouseUp ? WM_LBUTTONUP : WM_LBUTTONDOWN;
                        wParam = mouseUp ? IntPtr.Zero : (IntPtr)0x0001 /* MK_LBUTTON */;
                        break;
                    case 1:
                        msg = mouseUp ? WM_RBUTTONUP : WM_RBUTTONDOWN;
                        wParam = mouseUp ? IntPtr.Zero : (IntPtr)0x0002 /* MK_RBUTTON */;
                        break;
                    case 2:
                        msg = mouseUp ? WM_MBUTTONUP : WM_MBUTTONDOWN;
                        wParam = mouseUp ? IntPtr.Zero : (IntPtr)0x0010 /* MK_MBUTTON */;
                        break;
                }
                if (msg != 0)
                {
                    if (!mouseUp)
                    {
                        controller.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
                        PostMessage(targetHwnd, WM_MOUSEMOVE, IntPtr.Zero, lParam);
                    }
                    PostMessage(targetHwnd, msg, wParam, lParam);
                }
            });
        }

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public void SendMouseWheel(int x, int y, int deltaX, int deltaY)
        {
            RunOnUIThread(() =>
            {
                var controller = Controller;
                if (controller == null) return;
                IntPtr targetHwnd = GetInputTargetHwnd(controller.ParentWindow);
                POINT pt = new POINT { X = x, Y = y };
                ClientToScreen(targetHwnd, ref pt);
                IntPtr wParam = (IntPtr)((deltaY & 0xFFFF) << 16);
                IntPtr lParam = (IntPtr)((ushort)pt.X | ((ushort)pt.Y << 16));
                PostMessage(targetHwnd, WM_MOUSEWHEEL, wParam, lParam);
            });
        }

        public void SendKeyEvent(int windowsKeyCode, bool isUp, int modifiers)
        {
            RunOnUIThread(() =>
            {
                var controller = Controller;
                if (controller == null) return;
                IntPtr targetHwnd = GetInputTargetHwnd(controller.ParentWindow);
                
                // Construct lParam with repeat count 1 (bits 0-15) and previous key state / transition state
                uint flags = 1; // Repeat count
                if (isUp)
                {
                    flags |= (1U << 30) | (1U << 31); // Transition state & Previous key state
                    PostMessage(targetHwnd, WM_KEYUP, (IntPtr)windowsKeyCode, (IntPtr)flags);
                }
                else
                {
                    PostMessage(targetHwnd, WM_KEYDOWN, (IntPtr)windowsKeyCode, (IntPtr)flags);
                }
            });
        }

        public void SendText(string text)
        {
            RunOnUIThread(() =>
            {
                var controller = Controller;
                if (controller == null || string.IsNullOrEmpty(text)) return;
                IntPtr targetHwnd = GetInputTargetHwnd(controller.ParentWindow);
                foreach (char c in text)
                {
                    PostMessage(targetHwnd, WM_CHAR, (IntPtr)c, IntPtr.Zero);
                }
            });
        }

        public void ShowDevTools()
        {
            RunOnUIThread(() => CoreWebView2?.OpenDevToolsWindow());
        }

        public int GetCursorType()
        {
            return 0;
        }

        public void ResolveJsPromise(string reqId, object result)
        {
            if (_jsBridgeHandler != null)
            {
                _jsBridgeHandler.CompleteRequest(reqId, result);
            }
        }
    }
}