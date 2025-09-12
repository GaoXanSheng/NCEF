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
  private int BROWSER_PORT = 9222;
  private string SPOUT_ID = "";

  public MainWindow()
  {
    string environmentVariable1 = Environment.GetEnvironmentVariable(nameof (BROWSER_PORT));
    string environmentVariable2 = Environment.GetEnvironmentVariable(nameof (BROWSER_PORT));
    if (!string.IsNullOrEmpty(environmentVariable2))
      this.SPOUT_ID = environmentVariable2;
    int result;
    if (string.IsNullOrEmpty(environmentVariable1) || !int.TryParse(environmentVariable1, out result))
      return;
    this.BROWSER_PORT = result;
  }

  public async Task InitWebViewAsync()
  {
    CefSettings settings = new CefSettings();
    // 设定 CEF 缓存和资源目录
    string gameDir = Path.Combine(Environment.CurrentDirectory, "cef_data");
    settings.CachePath = Path.Combine(gameDir, "cache"); // 浏览器缓存
    settings.ResourcesDirPath = Path.Combine(gameDir, "resources"); // CEF 自带的资源文件
    settings.LocalesDirPath = Path.Combine(gameDir, "locales"); // 本地化
    settings.LogFile = Path.Combine(gameDir, "cef.log");

    settings.WindowlessRenderingEnabled = true;
    settings.EnableAudio();
    settings.CefCommandLineArgs.Add("remote-debugging-port", this.BROWSER_PORT.ToString());
    Cef.Initialize((CefSettingsBase) settings, true);
    this._browser = new ChromiumWebBrowser("https://example.com/", (IBrowserSettings) new BrowserSettings()
    {
      WindowlessFrameRate = 120
    });
    LoadUrlAsyncResponse urlAsyncResponse = await this._browser.WaitForInitialLoadAsync();
    Rectangle bounds = Screen.PrimaryScreen.Bounds;
    int screenWidth = bounds.Width;
    bounds = Screen.PrimaryScreen.Bounds;
    int screenHeight = bounds.Height;
    this.InitDirectXAndSpout(screenWidth, screenHeight);
    this._browser.Paint += new EventHandler<OnPaintEventArgs>(this.Browser_Paint);
    this._browser.Size = new Size(screenWidth, screenHeight);
    settings = (CefSettings) null;
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
    DataBox dataBox = immediateContext.MapSubresource((SharpDX.Direct3D11.Resource) this._dxTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
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
      immediateContext.UnmapSubresource((SharpDX.Direct3D11.Resource) this._dxTexture, 0);
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