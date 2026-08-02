using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NCEF_Test.TestCases;

namespace NCEF_Test;

public partial class MainWindow : Window
{
    private MasterRpcClient _masterClient;
    private BrowserRpcClient _browserClient = null!;
    private string _currentSpoutId = null!;
    private SpoutReceiver _spoutReceiver;
    private DispatcherTimer _renderTimer;
    private DispatcherTimer _monitorTimer;
    private int _browserWidth = 1280;
    private int _browserHeight = 720;
    private bool _isMouseMovePending;
    private int _pendingMouseX;
    private int _pendingMouseY;
    private readonly object _mouseMoveLock = new object();
    private BrowserMemoryMapWindow? _browserMemoryMapWindow;

    public MainWindow()
    {
        InitializeComponent();

        _masterClient = new MasterRpcClient();
        _spoutReceiver = new SpoutReceiver();
        _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _renderTimer.Tick += RenderTimer_Tick;
        _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _monitorTimer.Tick += MonitorTimer_Tick;
        _mouseMoveTimer.Tick += MouseMoveTimer_Tick;

        SpoutPreviewBorder.MouseLeftButtonDown += SpoutPreviewBorder_MouseLeftButtonDown;
        SpoutPreviewBorder.MouseMove += SpoutPreviewBorder_MouseMove;
        SpoutPreviewBorder.MouseLeftButtonUp += SpoutPreviewBorder_MouseLeftButtonUp;
        SpoutPreviewBorder.MouseWheel += SpoutPreviewBorder_MouseWheel;
        SpoutPreviewBorder.KeyDown += SpoutPreviewBorder_KeyDown;
        SpoutPreviewBorder.KeyUp += SpoutPreviewBorder_KeyUp;
        SpoutPreviewBorder.TextInput += SpoutPreviewBorder_TextInput;
    }

    private void BtnConnectMaster_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string rpcId = TxtMasterRpcId.Text.Trim();
            if (string.IsNullOrEmpty(rpcId))
            {
                Log("RPC ID required");
                return;
            }

            _masterClient.Connect(rpcId);
            Log($"Connected to Master RPC: {rpcId}");
            _monitorTimer.Start();
            BtnConnectMaster.IsEnabled = false;
            TxtMasterRpcId.IsEnabled = false;
            BtnCreateBrowser.IsEnabled = true;
            BtnRunTests.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Log($"Connect failed: {ex.Message}");
        }
    }

    private async void BtnSelectSpout_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _spoutReceiver._receiver.SelectSender();
            var spoutId = _spoutReceiver._receiver.GetSenderName();
            await ConnectSpout(spoutId);
            await ConnectToBrowser(spoutId);
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }

    private void TxtBrowserUrl_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        string currentUrl = TxtBrowserUrl.Text.Trim();
        if (_browserClient != null && !string.IsNullOrEmpty(currentUrl))
        {
            _browserClient.LoadUrl(currentUrl);
        }
    }

    private async void BtnCreateBrowser_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string url = TxtBrowserUrl.Text.Trim();
            if (!int.TryParse(TxtBrowserWidth.Text, out _browserWidth)) _browserWidth = 1280;
            if (!int.TryParse(TxtBrowserHeight.Text, out _browserHeight)) _browserHeight = 720;
            if (!int.TryParse(TxtBrowserFps.Text, out int fps)) fps = 60;

            Log($"Creating browser: {url}, {_browserWidth}x{_browserHeight}@{fps}fps");

            string spoutId = await Task.Run(() => _masterClient.CreateBrowser(url, _browserWidth, _browserHeight, fps));

            if (string.IsNullOrEmpty(spoutId))
            {
                Log("CreateBrowser returned empty SpoutId");
                return;
            }

            _currentSpoutId = spoutId;
            Log($"Browser created, SpoutId: {spoutId}");
            TxtCurrentSpoutId.Text = spoutId;

            await ConnectToBrowser(spoutId);
            await ConnectSpout(spoutId);
        }
        catch (Exception ex)
        {
            Log($"Create browser failed: {ex.Message}");
        }
    }

    private Task ConnectToBrowser(string spoutId)
    {
        return Task.Run(() =>
        {
            try
            {
                _browserClient = new BrowserRpcClient();
                _browserClient.Connect(spoutId);
                Log($"Connected to Browser RPC: {spoutId}");

                Dispatcher.Invoke(() =>
                {
                    _browserMemoryMapWindow = new BrowserMemoryMapWindow();
                    _browserMemoryMapWindow.SetBrowserClient(_browserClient);
                    _browserMemoryMapWindow.Show();
                });
            }
            catch (Exception ex)
            {
                Log($"Browser RPC connect failed: {ex.Message}");
            }
        });
    }

    private Task ConnectSpout(string spoutId)
    {
        return Task.Run(() =>
        {
            try
            {
                _spoutReceiver.Connect(spoutId);
                _renderTimer.Start();
                Log($"Spout receiver started: {spoutId}");
                Dispatcher.Invoke(() =>
                {
                    TxtSpoutStatus.Text = "Connected";
                    TxtSpoutInfo.Text = $"SpoutId: {spoutId}";
                });
            }
            catch (Exception ex)
            {
                Log($"Spout connect failed: {ex.Message}");
                Dispatcher.Invoke(() => TxtSpoutStatus.Text = "Connect Failed");
            }
        });
    }

    private void RenderTimer_Tick(object? sender, EventArgs e)
    {
        if (_spoutReceiver == null || !_spoutReceiver.IsConnected) return;

        _spoutReceiver.Update();

        if (_spoutReceiver.Width > 0 && _spoutReceiver.Height > 0)
        {
            TxtFrameSize.Text = $"{_spoutReceiver.Width} x {_spoutReceiver.Height}";
            TxtFps.Text = _spoutReceiver.Fps.ToString("F1");
            TxtSpoutInfo.Text =
                $"{_currentSpoutId}\n{_spoutReceiver.Width}x{_spoutReceiver.Height}@{_spoutReceiver.Fps:F1}fps";

            byte[] frameData = _spoutReceiver.CaptureFrame();
            if (frameData != null)
            {
                try
                {
                    var bitmap = new WriteableBitmap(_spoutReceiver.Width, _spoutReceiver.Height, 96, 96,
                        PixelFormats.Bgra32, null);
                    bitmap.Lock();
                    bitmap.WritePixels(new Int32Rect(0, 0, _spoutReceiver.Width, _spoutReceiver.Height), frameData,
                        _spoutReceiver.Width * 4, 0);
                    bitmap.Unlock();
                    SpoutPreviewImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Render error: {ex.Message}");
                }
            }
        }
    }

    private void MonitorTimer_Tick(object? sender, EventArgs e)
    {
        if (_masterClient.IsConnected)
        {
            TxtReqMemory.Text = _masterClient.ReadReqMemory();
            TxtResMemory.Text = _masterClient.ReadResMemory();
            TxtEvtMemory.Text = _masterClient.ReadEvtMemory();
        }
    }

    private Point? GetBrowserCoordinates(MouseEventArgs e)
    {
        if (_spoutReceiver.Width == 0 || _spoutReceiver.Height == 0)
            return null;

        var pos = e.GetPosition(SpoutPreviewImage);
        double uiWidth = SpoutPreviewImage.ActualWidth;
        double uiHeight = SpoutPreviewImage.ActualHeight;

        if (uiWidth == 0 || uiHeight == 0)
            return null;

        double imgWidth = _spoutReceiver.Width;
        double imgHeight = _spoutReceiver.Height;

        double scaleX = uiWidth / imgWidth;
        double scaleY = uiHeight / imgHeight;
        double scale = Math.Min(scaleX, scaleY);

        double renderedWidth = imgWidth * scale;
        double renderedHeight = imgHeight * scale;

        double offsetX = (uiWidth - renderedWidth) / 2.0;
        double offsetY = (uiHeight - renderedHeight) / 2.0;

        double mappedX = (pos.X - offsetX) / scale;
        double mappedY = (pos.Y - offsetY) / scale;

        if (mappedX < 0) mappedX = 0;
        if (mappedX > imgWidth - 1) mappedX = imgWidth - 1;
        if (mappedY < 0) mappedY = 0;
        if (mappedY > imgHeight - 1) mappedY = imgHeight - 1;

        return new Point(mappedX, mappedY);
    }

    private void SpoutPreviewBorder_MouseMove(object sender, MouseEventArgs e)
    {
        var pt = GetBrowserCoordinates(e);
        if (pt.HasValue)
        {
            lock (_mouseMoveLock)
            {
                _pendingMouseX = (int)pt.Value.X;
                _pendingMouseY = (int)pt.Value.Y;
                _isMouseMovePending = true;
            }

            if (!_mouseMoveTimer.IsEnabled)
            {
                _mouseMoveTimer.Start();
            }
        }
    }

    private readonly DispatcherTimer _mouseMoveTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(16)
    };

    private void MouseMoveTimer_Tick(object? sender, EventArgs e)
    {
        if (_browserClient == null) return;
        

        int x, y;
        lock (_mouseMoveLock)
        {
            if (!_isMouseMovePending)
            {
                _mouseMoveTimer.Stop();
                return;
            }

            x = _pendingMouseX;
            y = _pendingMouseY;
            _isMouseMovePending = false;
        }

        _browserClient.SendMouseMove(x, y);
    }

    private void SpoutPreviewBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        SendMouseClick(e, false);
        SpoutPreviewBorder.Focus();
    }

    private void SpoutPreviewBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        SendMouseClick(e, true);
    }

    private void SendMouseClick(MouseButtonEventArgs e, bool mouseUp)
    {
        if (_browserClient == null) return;
        var pt = GetBrowserCoordinates(e);
        if (pt.HasValue)
        {
            _browserClient.SendMouseClick((int)pt.Value.X, (int)pt.Value.Y, 0, mouseUp);
        }
    }

    private void SpoutPreviewBorder_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_browserClient == null) return;
        var pt = GetBrowserCoordinates(e);
        if (pt.HasValue)
        {
            int x = (int)pt.Value.X;
            int y = (int)pt.Value.Y;
            int delta = e.Delta;
            _browserClient.SendMouseWheel(x, y, 0, delta);
        }
    }

    private void SpoutPreviewBorder_KeyDown(object sender, KeyEventArgs e)
    {
        if (_browserClient == null) return;
        int keyCode = InputHelper.GetVirtualKeyCode(e.Key);
        if (keyCode != 0)
        {
            int modifiers = InputHelper.GetCurrentModifiers();
            _browserClient.SendKeyEvent(keyCode, false, modifiers);
        }
    }

    private void SpoutPreviewBorder_KeyUp(object sender, KeyEventArgs e)
    {
        if (_browserClient == null) return;
        int keyCode = InputHelper.GetVirtualKeyCode(e.Key);
        if (keyCode != 0)
        {
            int modifiers = InputHelper.GetCurrentModifiers();
            _browserClient.SendKeyEvent(keyCode, true, modifiers);
        }
    }

    private void SpoutPreviewBorder_TextInput(object sender, TextCompositionEventArgs e)
    {
        // Chromium internal input pipeline handles character translation automatically from WM_KEYDOWN.
        // Calling SendText here causes double character input (e.g. 'ww').
        e.Handled = true;
    }

    private void BtnShowDevTools_Click(object sender, RoutedEventArgs e)
    {
        if (_browserClient == null) return;
        _browserClient.ShowDevTools();
        Log("ShowDevTools");
    }

    private async void BtnGetUrl_Click(object sender, RoutedEventArgs e)
    {
        if (_browserClient == null) return;
        string url = _browserClient.GetUrl();
        Log($"URL: {url}");
    }

    private void BtnExecuteJs_Click(object sender, RoutedEventArgs e)
    {
        if (_browserClient == null) return;
        _browserClient.ExecuteJs("alert('Hello from NCEF Test!');");
        Log("Executed JS: alert('Hello from NCEF Test!')");
    }

    private async void BtnGetCursorType_Click(object sender, RoutedEventArgs e)
    {
        if (_browserClient == null) return;
        int cursorType = _browserClient.GetCursorType();
        Log($"CursorType: {cursorType}");
    }

    private async void BtnRunTests_Click(object sender, RoutedEventArgs e)
    {
        if (_browserClient == null) return;
        BtnRunTests.IsEnabled = false;
        Log("\n========== Running Tests ==========");

        await Task.Run(() =>
        {
            try
            {
                var executor = new TestCaseExecutor(Log);
                executor.RunAllTests(_masterClient);
                Thread.Sleep(2000);

                if (_browserClient.IsConnected)
                {
                    Log("--- Browser Tests ---");
                    Log($"GetSpoutId: {_browserClient.GetSpoutId()}");
                    Thread.Sleep(500);
                    Log($"GetUrl: {_browserClient.GetUrl()}");
                    Log("LoadUrl: https://www.example.com");
                    _browserClient.LoadUrl("https://www.example.com");
                    Thread.Sleep(1000);
                    Log("ExecuteJs: document.title='Test'");
                    _browserClient.ExecuteJs("document.title='Test';");
                    Thread.Sleep(500);
                    Log("SendMouseMove(200,150)");
                    _browserClient.SendMouseMove(200, 150);
                    Thread.Sleep(300);
                    Log("ClickLeft(200,150)");
                    _browserClient.ClickLeft(200, 150);
                    Thread.Sleep(300);
                    Log("Scroll(200,150,120)");
                    _browserClient.Scroll(200, 150, 120);
                    Thread.Sleep(300);
                    Log("SendText: Hello NCEF!");
                    _browserClient.SendText("Hello NCEF!");
                    Log("SetVolume(0.5)");
                    _browserClient.SetVolume(0.5f);
                    Log("Resize(800,600,1,false)");
                    _browserClient.Resize(800, 600, 1, false);
                    Log("Tests completed");
                }

                Log("\n========== Tests Done ==========");
            }
            catch (Exception ex)
            {
                Log($"Test failed: {ex.Message}");
            }
        });

        BtnRunTests.IsEnabled = true;
    }

    private void Log(string message)
    {
        Dispatcher.Invoke(() =>
        {
            TxtLogs.AppendText($"[{DateTime.Now:HH:mm:ss.fff}] {message}\r\n");
            TxtLogs.ScrollToEnd();
        });
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _renderTimer.Stop();
        _monitorTimer.Stop();
        _spoutReceiver.Dispose();
        _masterClient.Disconnect();
        _browserClient?.Disconnect();
    }
}