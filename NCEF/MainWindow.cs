using CefSharp;
using CefSharp.OffScreen;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NCEF
{
    public class MainWindow
    {
        private ChromiumWebBrowser _browser;
        private Texture2D _dxTexture;
        private SpoutDX _spoutSender;
        private SharpDX.Direct3D11.Device _dxDevice;
        private int BROWSER_PORT;
        private string SPOUT_ID;
        private int MAXFPS;
        private string CUSTOMIZE_LOADING_SCREEN_URL = "";
        public MainWindow()
        {
            this.SPOUT_ID = GetEnvString("SPOUT_ID", "NCEF");
            this.BROWSER_PORT = GetEnvInt("BROWSER_PORT", 9222);
            this.MAXFPS = GetEnvInt("MAXFPS", 120);
            this.CUSTOMIZE_LOADING_SCREEN_URL = GetEnvString("CUSTOMIZE_LOADING_SCREEN_URL", "https://example.com/");
        }

        private static string GetEnvString(string name, string defaultValue = "")
        {
            string value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
        }

        private static int GetEnvInt(string name, int defaultValue = 0)
        {
            string value = Environment.GetEnvironmentVariable(name);
            return int.TryParse(value, out int result) ? result : defaultValue;
        }

        public async Task InitWebViewAsync()
        {
            CefSettings settings = new CefSettings();
            // 设定 CEF 缓存和资源目录
            string gameDir = Environment.CurrentDirectory;
            settings.CachePath = Path.Combine(gameDir, "User Data"); // 浏览器缓存
            settings.LogFile = Path.Combine(gameDir, "cef.log");

            settings.WindowlessRenderingEnabled = true;
            settings.EnableAudio();
            settings.CefCommandLineArgs.Add("remote-debugging-port", this.BROWSER_PORT.ToString());
            if (Cef.IsInitialized == false)
            {
                Cef.Initialize((CefSettingsBase)settings, true);
            }
            this._browser = new ChromiumWebBrowser(CUSTOMIZE_LOADING_SCREEN_URL, (IBrowserSettings)new BrowserSettings()
            {
                WindowlessFrameRate = MAXFPS
            });
            LoadUrlAsyncResponse urlAsyncResponse = await this._browser.WaitForInitialLoadAsync();
            Rectangle bounds = Screen.PrimaryScreen.Bounds;
            int screenWidth = bounds.Width;
            bounds = Screen.PrimaryScreen.Bounds;
            int screenHeight = bounds.Height;
            this.InitDirectXAndSpout(screenWidth, screenHeight);
            this._browser.Paint += new EventHandler<OnPaintEventArgs>(this.Browser_Paint);
            this._browser.Size = new Size(screenWidth, screenHeight);
            settings = (CefSettings)null;
        }

        private void InitDirectXAndSpout(int width, int height)
        {
            this._dxDevice = new SharpDX.Direct3D11.Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            this._dxTexture = new Texture2D(this._dxDevice, new Texture2DDescription()
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None
            });
            this._spoutSender = new SpoutDX();
            this._spoutSender.OpenDirectX(this._dxDevice.NativePointer);
            this._spoutSender.SetSenderName("WebViewSpoutCapture_" + this.SPOUT_ID);
        }

        private void Browser_Paint(object sender, OnPaintEventArgs e)
        {
            if (this._dxTexture == null || this._spoutSender == null || e.BufferHandle == IntPtr.Zero)
                return;
            int width = e.Width;
            int height = e.Height;
            DeviceContext immediateContext = this._dxDevice.ImmediateContext;
            DataBox dataBox = immediateContext.MapSubresource((SharpDX.Direct3D11.Resource)this._dxTexture, 0,
                MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
            try
            {
                int length = width * 4;
                byte[] numArray = new byte[length];
                for (int index = 0; index < height; ++index)
                {
                    Marshal.Copy(e.BufferHandle + index * length, numArray, 0, length);
                    Marshal.Copy(numArray, 0, dataBox.DataPointer + index * dataBox.RowPitch, length);
                }
            }
            finally
            {
                immediateContext.UnmapSubresource((SharpDX.Direct3D11.Resource)this._dxTexture, 0);
            }

            this._spoutSender.SendTexture(this._dxTexture.NativePointer);
        }

        public void OnClosed(EventArgs e)
        {
            this._browser?.Dispose();
            this._dxTexture?.Dispose();
            this._spoutSender?.Dispose();
            this._dxDevice?.Dispose();
            Cef.Shutdown();
        }
    }
}