using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;
using Microsoft.Web.WebView2.Core;
using NCEF.Utils;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Device1 = SharpDX.Direct3D11.Device1;

namespace NCEF.Browser
{
    public class WebView2WebBrowser : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(WebView2WebBrowser));
        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

        public CoreWebView2Controller Controller { get; private set; }
        public CoreWebView2 CoreWebView2 => Controller?.CoreWebView2;

        private Device1 _dxDevice;
        private SpoutDX _spoutSender;
        private Texture2D _compositeTexture;
        private Surface1 _dxgiSurface1;
        private string _spoutId;
        private Size _currentSize;
        private Form _hostForm;

        private System.Threading.Timer _frameTimer;
        private readonly object _textureLock = new object();
        private bool _isDisposed = false;

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
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_LAYERED = 0x00080000;

        public async Task InitializeAsync(string initialUrl, string cachePath)
        {
            // Position hostForm far offscreen at (-20000, -20000) with click-through (WS_EX_TRANSPARENT) and no-activate styles
            _hostForm = new Form
            {
                Text = "NCEF_WV2_HOST",
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000),
                Size = new Size(_currentSize.Width, _currentSize.Height),
                ShowInTaskbar = false,
                Opacity = 0.01
            };
            _hostForm.Show();

            try
            {
                int exStyle = GetWindowLong(_hostForm.Handle, GWL_EXSTYLE);
                SetWindowLong(_hostForm.Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED);
            }
            catch { }

            EnsureCompositeTexture(_currentSize.Width, _currentSize.Height);

            var options = new CoreWebView2EnvironmentOptions();
            var environment = await CoreWebView2Environment.CreateAsync(null, cachePath, options);

            Controller = await environment.CreateCoreWebView2ControllerAsync(_hostForm.Handle);
            Controller.Bounds = new Rectangle(0, 0, _currentSize.Width, _currentSize.Height);
            Controller.IsVisible = true;

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
                Log.Info($"WebView2 navigating to: {targetUrl}");
                CoreWebView2.Navigate(targetUrl);
            }
            catch (Exception ex)
            {
                Log.Error($"WebView2 navigation to {targetUrl} failed, falling back to about:blank", ex);
                CoreWebView2.Navigate("about:blank");
            }

            _frameTimer = new System.Threading.Timer(_ => RenderLoop(), null, 100, 16);
        }

        private void RenderLoop()
        {
            if (Controller == null || _hostForm == null || _hostForm.IsDisposed || _isDisposed) return;

            lock (_textureLock)
            {
                if (!_isDisposed && _compositeTexture != null && !_compositeTexture.IsDisposed && _dxgiSurface1 != null && _spoutSender != null)
                {
                    try
                    {
                        // Direct GDI-to-D3D11 GPU Texture VRAM write (ZERO CPU byte copying, ZERO CPU memory allocation!)
                        IntPtr hdc = _dxgiSurface1.GetDC(true);
                        if (hdc != IntPtr.Zero)
                        {
                            try
                            {
                                PrintWindow(_hostForm.Handle, hdc, 2 /* PW_RENDERFULLCONTENT */);
                            }
                            finally
                            {
                                _dxgiSurface1.ReleaseDC();
                            }

                            _spoutSender.SendTexture(_compositeTexture.NativePointer);
                        }
                    }
                    catch { }
                }
            }
        }
        

        public void Resize(int width, int height)
        {
            if (width <= 0 || height <= 0 || _isDisposed) return;
            _currentSize = new Size(width, height);

            if (_hostForm != null && !_hostForm.IsDisposed)
            {
                _hostForm.Invoke(new Action(() =>
                {
                    _hostForm.Size = new Size(width, height);
                }));
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

                _dxgiSurface1?.Dispose();
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
                    BindFlags = BindFlags.ShaderResource | BindFlags.RenderTarget,
                    CpuAccessFlags = CpuAccessFlags.None,
                    OptionFlags = ResourceOptionFlags.Shared | ResourceOptionFlags.GdiCompatible
                });

                _dxgiSurface1 = _compositeTexture.QueryInterface<Surface1>();
            }
        }

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

                if (_hostForm != null && !_hostForm.IsDisposed)
                {
                    try
                    {
                        _hostForm.Invoke(new Action(() =>
                        {
                            _hostForm.Close();
                            _hostForm.Dispose();
                        }));
                    }
                    catch { }
                    _hostForm = null;
                }

                try
                {
                    _dxgiSurface1?.Dispose();
                    _compositeTexture?.Dispose();
                    _spoutSender?.Dispose();
                    _dxDevice?.Dispose();
                }
                catch { }

                Controller = null;
                _dxgiSurface1 = null;
                _compositeTexture = null;
                _spoutSender = null;
                _dxDevice = null;
            }
        }
    }
}
