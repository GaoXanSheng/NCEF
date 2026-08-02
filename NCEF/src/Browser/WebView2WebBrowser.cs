using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using NCEF.Utils;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Device1 = SharpDX.Direct3D11.Device1;

namespace NCEF.Browser
{
    public class WebView2WebBrowser : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        public CoreWebView2Controller Controller { get; private set; }
        public CoreWebView2 CoreWebView2 => Controller?.CoreWebView2;

        private Device1 _dxDevice;
        private SpoutDX _spoutSender;
        private Texture2D _compositeTexture;
        private string _spoutId;
        private Size _currentSize;
        private IntPtr _hostHwnd = IntPtr.Zero;

        public WebView2WebBrowser(string spoutId)
        {
            _spoutId = spoutId;
            _currentSize = new Size(Config.Bounds.Width, Config.Bounds.Height);
            InitializeDirectX(spoutId);
        }

        private void InitializeDirectX(string spoutId)
        {
            var device0 = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            _dxDevice = device0.QueryInterface<Device1>();
            device0.Dispose();

            _spoutSender = new SpoutDX();
            _spoutSender.SetSenderName(spoutId);
            _spoutSender.OpenDirectX(_dxDevice.NativePointer);
        }

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors;
        }

        private System.Threading.Timer _frameTimer;
        private byte[] _pixelBuffer;

        public async Task InitializeAsync(string initialUrl, string cachePath)
        {
            // Position offscreen (-32000, -32000) with WS_EX_TOOLWINDOW so no taskbar button appears
            _hostHwnd = CreateWindowEx(0x00000080 /* WS_EX_TOOLWINDOW */, "STATIC", "NCEF_WV2_HOST", 0x80000000 | 0x10000000 /* WS_POPUP | WS_VISIBLE */, -32000, -32000, _currentSize.Width, _currentSize.Height, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            var options = new CoreWebView2EnvironmentOptions();
            var environment = await CoreWebView2Environment.CreateAsync(null, cachePath, options);

            Controller = await environment.CreateCoreWebView2ControllerAsync(_hostHwnd);
            Controller.Bounds = new Rectangle(0, 0, _currentSize.Width, _currentSize.Height);
            Controller.IsVisible = true;

            EnsureCompositeTexture(_currentSize.Width, _currentSize.Height);

            // Intercept new window requests (target="_blank", window.open) and force current window navigation
            CoreWebView2.NewWindowRequested += (sender, args) =>
            {
                args.Handled = true;
                if (!string.IsNullOrEmpty(args.Uri))
                {
                    CoreWebView2.Navigate(args.Uri);
                }
            };

            string targetUrl = initialUrl;
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                targetUrl = "about:blank";
            }
            else if (!targetUrl.Contains("://") && !targetUrl.StartsWith("about:") && !targetUrl.StartsWith("data:"))
            {
                targetUrl = "https://" + targetUrl;
            }

            try
            {
                CoreWebView2.Navigate(targetUrl);
            }
            catch
            {
                CoreWebView2.Navigate("about:blank");
            }

            _frameTimer = new System.Threading.Timer(_ => RenderLoop(), null, 100, 16);
        }

        private readonly object _textureLock = new object();

        private void RenderLoop()
        {
            if (Controller == null || _hostHwnd == IntPtr.Zero || _isDisposed) return;

            try
            {
                IntPtr hdcScreen = GetWindowDC(_hostHwnd);
                IntPtr hdcMem = CreateCompatibleDC(hdcScreen);
                IntPtr hBitmap = CreateCompatibleBitmap(hdcScreen, _currentSize.Width, _currentSize.Height);
                IntPtr hOld = SelectObject(hdcMem, hBitmap);

                PrintWindow(_hostHwnd, hdcMem, 2 /* PW_RENDERFULLCONTENT */);

                int bufferSize = _currentSize.Width * _currentSize.Height * 4;
                if (_pixelBuffer == null || _pixelBuffer.Length != bufferSize)
                {
                    _pixelBuffer = new byte[bufferSize];
                }

                BITMAPINFO bmi = new BITMAPINFO();
                bmi.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
                bmi.bmiHeader.biWidth = _currentSize.Width;
                bmi.bmiHeader.biHeight = -_currentSize.Height; // Top-down
                bmi.bmiHeader.biPlanes = 1;
                bmi.bmiHeader.biBitCount = 32;
                bmi.bmiHeader.biCompression = 0; // BI_RGB

                GetDIBits(hdcMem, hBitmap, 0, (uint)_currentSize.Height, _pixelBuffer, ref bmi, 0);

                SelectObject(hdcMem, hOld);
                DeleteObject(hBitmap);
                DeleteDC(hdcMem);
                ReleaseDC(_hostHwnd, hdcScreen);

                // Lock DX11 Texture and SpoutSender to prevent AccessViolationException when resizing or disposing
                lock (_textureLock)
                {
                    if (!_isDisposed && _compositeTexture != null && !_compositeTexture.IsDisposed && _spoutSender != null)
                    {
                        var ctx = _dxDevice.ImmediateContext;
                        var gcHandle = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);
                        try
                        {
                            var dataBox = new DataBox(gcHandle.AddrOfPinnedObject(), _currentSize.Width * 4, 0);
                            ctx.UpdateSubresource(dataBox, _compositeTexture, 0);
                            _spoutSender.SendTexture(_compositeTexture.NativePointer);
                        }
                        finally
                        {
                            gcHandle.Free();
                        }
                    }
                }
            }
            catch
            {
                // Silently ignore transient capture errors
            }
        }

        public void SendTextureFrame()
        {
            lock (_textureLock)
            {
                if (!_isDisposed && _compositeTexture != null && !_compositeTexture.IsDisposed)
                {
                    _spoutSender?.SendTexture(_compositeTexture.NativePointer);
                }
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0 || _isDisposed) return;
            _currentSize = new Size(width, height);
            _pixelBuffer = new byte[width * height * 4];

            if (_hostHwnd != IntPtr.Zero)
            {
                SetWindowPos(_hostHwnd, IntPtr.Zero, 0, 0, width, height, 0x0002 | 0x0004 | 0x0010);
            }

            if (Controller != null)
            {
                Controller.Bounds = new Rectangle(0, 0, width, height);
                EnsureCompositeTexture(width, height);
            }
        }

        private void EnsureCompositeTexture(int width, int height)
        {
            lock (_textureLock)
            {
                if (_compositeTexture != null && !_compositeTexture.IsDisposed &&
                    _compositeTexture.Description.Width == width &&
                    _compositeTexture.Description.Height == height)
                {
                    return;
                }

                _compositeTexture?.Dispose();
                _compositeTexture = new Texture2D(_dxDevice, new Texture2DDescription
                {
                    Width = width,
                    Height = height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Default,
                    BindFlags = BindFlags.ShaderResource,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.Shared
                });
            }
        }

        private bool _isDisposed = false;

        public void Dispose()
        {
            lock (_textureLock)
            {
                if (_isDisposed) return;
                _isDisposed = true;

                try
                {
                    _frameTimer?.Dispose();
                    _frameTimer = null;
                    Controller?.Close();
                }
                catch { }

                if (_hostHwnd != IntPtr.Zero)
                {
                    DestroyWindow(_hostHwnd);
                    _hostHwnd = IntPtr.Zero;
                }

                try
                {
                    _compositeTexture?.Dispose();
                    _spoutSender?.Dispose();
                    _dxDevice?.Dispose();
                }
                catch { }

                Controller = null;
                _compositeTexture = null;
                _spoutSender = null;
                _dxDevice = null;
            }
        }
    }
}
