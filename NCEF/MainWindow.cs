using CefSharp;
using CefSharp.OffScreen;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp.Structs;
using Size = System.Drawing.Size;

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
            string gameDir = Environment.CurrentDirectory;
            settings.CachePath = Path.Combine(gameDir, "User Data"); // browserCache
            settings.LogFile = Path.Combine(gameDir, "cef.log");
            settings.WindowlessRenderingEnabled = true;
            settings.EnableAudio();
            settings.CefCommandLineArgs.Add("remote-debugging-port", this.BROWSER_PORT.ToString());
            if (!Cef.IsInitialized.GetValueOrDefault())
            {
                bool initialized = Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
                if (!initialized)
                {
                    Console.Error.WriteLine("CefSharp initialization failed! See cef.log for details.");
                    Environment.Exit(-1); // If initialization fails, the process will be exited
                }
            }

            var browserSettings = new BrowserSettings()
            {
                WindowlessFrameRate = MAXFPS,
                JavascriptCloseWindows = CefState.Disabled,
            };
            this._browser = new ChromiumWebBrowser(CUSTOMIZE_LOADING_SCREEN_URL, browserSettings);
            _browser.FrameLoadEnd += async (sender, e) =>
            {
                if (!e.Frame.IsMain) return;
                string script = @"document.addEventListener(""mousedown"",function(e){const s=e.target.closest(""select"");if(!s)return;e.preventDefault();const o=Array.from(s.options).map(o=>({value:o.value,text:o.text}));let old=document.getElementById(""custom-select-list"");if(old)old.remove();const r=s.getBoundingClientRect(),ul=document.createElement(""ul"");ul.id=""custom-select-list"";ul.style.position=""absolute"";ul.style.left=r.left+window.scrollX+""px"";ul.style.top=r.bottom+window.scrollY+""px"";ul.style.background=""#fff"";ul.style.border=""1px solid #ccc"";ul.style.padding=""0"";ul.style.margin=""0"";ul.style.listStyle=""none"";ul.style.zIndex=9999;o.forEach(opt=>{const li=document.createElement(""li"");li.textContent=opt.text;li.style.padding=""5px 10px"";li.style.cursor=""pointer"";li.addEventListener(""mouseenter"",()=>li.style.background=""#eee"");li.addEventListener(""mouseleave"",()=>li.style.background=""#fff"");li.addEventListener(""click"",()=>{s.value=opt.value;ul.remove();s.dispatchEvent(new Event(""change"",{bubbles:true}))});ul.appendChild(li)});document.body.appendChild(ul)});document.addEventListener(""click"",e=>{const ul=document.getElementById(""custom-select-list"");if(ul&&!ul.contains(e.target)&&e.target.tagName.toLowerCase()!==""select"")ul.remove()});";
                e.Frame.ExecuteJavaScriptAsync(script);
            };
            this._browser.LifeSpanHandler = new LifeSpanHandler();
            await this._browser.WaitForInitialLoadAsync();
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

        private SharpDX.Direct3D11.Texture2D _popupTexture = null;
        private Rect _popupRect;

        private void Browser_Paint(object sender, OnPaintEventArgs e)
        {
            if (_dxTexture == null || _spoutSender == null || e.BufferHandle == IntPtr.Zero)
                return;

            var context = _dxDevice.ImmediateContext;

            int width = e.Width;
            int height = e.Height;
            int rowPitch = width * 4;

            // === Step 1: createAPopupTexture
            if (e.IsPopup)
            {
                _popupRect = e.DirtyRect;
                int popupWidth = _popupRect.Width;
                int popupHeight = _popupRect.Height;

                // If the popup texture doesn't exist or the dimensions don't match, recreate
                if (_popupTexture == null || _popupTexture.Description.Width != popupWidth ||
                    _popupTexture.Description.Height != popupHeight)
                {
                    _popupTexture?.Dispose();
                    var desc = new SharpDX.Direct3D11.Texture2DDescription
                    {
                        Width = popupWidth,
                        Height = popupHeight,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm,
                        SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                        Usage = SharpDX.Direct3D11.ResourceUsage.Dynamic,
                        BindFlags = BindFlags.ShaderResource,
                        CpuAccessFlags = CpuAccessFlags.Write,
                        OptionFlags = ResourceOptionFlags.None
                    };
                    _popupTexture = new SharpDX.Direct3D11.Texture2D(_dxDevice, desc);
                }

                // Only the part of the popup is copied from the large buffer
                var dataBox = context.MapSubresource(_popupTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
                try
                {
                    IntPtr srcBase = e.BufferHandle + (_popupRect.Y * rowPitch) + (_popupRect.X * 4);
                    IntPtr destBase = dataBox.DataPointer;
                    int copyRowPitch = popupWidth * 4;

                    for (int y = 0; y < popupHeight; y++)
                    {
                        Utilities.CopyMemory(destBase + y * dataBox.RowPitch, srcBase + y * rowPitch, copyRowPitch);
                    }
                }
                finally
                {
                    context.UnmapSubresource(_popupTexture, 0);
                }
    
                return; // After updating the popup texture, you can go back and wait for the main page to draw events to merge
            }

            // === Step 2 ===
            var mainDataBox = context.MapSubresource((SharpDX.Direct3D11.Resource)_dxTexture, 0,
                MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);

            try
            {
                // copy buffer
                for (int y = 0; y < height; y++)
                {
                    IntPtr src = e.BufferHandle + y * rowPitch;
                    IntPtr dest = mainDataBox.DataPointer + y * mainDataBox.RowPitch;
                    Utilities.CopyMemory(dest, src, rowPitch);
                }
            }
            finally
            {
                context.UnmapSubresource((SharpDX.Direct3D11.Resource)_dxTexture, 0);
            }

            // === Step 3: merger popup ===
            if (_popupTexture != null)
            {
                var srcRegion = new ResourceRegion
                {
                    Left = 0,
                    Top = 0,
                    Front = 0,
                    Right = _popupTexture.Description.Width,
                    Bottom = _popupTexture.Description.Height,
                    Back = 1
                };

                context.CopySubresourceRegion(
                    _popupTexture, 0, srcRegion,
                    (SharpDX.Direct3D11.Resource)_dxTexture, 0,
                    _popupRect.X, _popupRect.Y, 0
                );
            }


            // === Step 4:  spout ===
            _spoutSender.SendTexture(_dxTexture.NativePointer);
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