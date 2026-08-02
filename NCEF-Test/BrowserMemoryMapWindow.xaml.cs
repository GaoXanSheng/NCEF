using System.Windows;
using NCEF_Test.TestCases;

namespace NCEF_Test;

public partial class BrowserMemoryMapWindow : Window
{
    private BrowserRpcClient? _browserClient;
    private System.Windows.Threading.DispatcherTimer _timer;

    public BrowserMemoryMapWindow()
    {
        InitializeComponent();
        _timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _timer.Tick += Timer_Tick;
    }

    public void SetBrowserClient(BrowserRpcClient client)
    {
        _browserClient = client;
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_browserClient != null && _browserClient.IsConnected)
        {
            TxtReqMemory.Text = _browserClient.ReadReqMemory();
            TxtResMemory.Text = _browserClient.ReadResMemory();
            TxtEvtMemory.Text = _browserClient.ReadEvtMemory();
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}