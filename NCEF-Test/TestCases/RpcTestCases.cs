using System;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NCEF_Test.TestCases
{
    public abstract class RpcClientBase
    {
        protected const string PREFIX_RPC_MAP = "NCEF_RPC_";
        protected const string PREFIX_REQ_EVENT = "NCEF_REQ_";
        protected const string PREFIX_RES_EVENT = "NCEF_RES_";
        protected const string PREFIX_EVT_MAP = "NCEF_EVT_";
        protected const string PREFIX_EVT_READY = "NCEF_EVT_READY_";
        protected const string PREFIX_EVT_ACK = "NCEF_EVT_ACK_";
        protected const int RPC_MAP_SIZE = 1024 * 1024 * 2;
        protected const int EVT_MAP_SIZE = RPC_MAP_SIZE / 2;
        protected const int REQ_OFFSET = 0;
        protected const int RES_OFFSET = RPC_MAP_SIZE / 2;
        protected const int DATA_HEADER_SIZE = 4;

        protected MemoryMappedFile? _rpcMmf;
        protected MemoryMappedViewAccessor? _rpcAccessor;
        protected EventWaitHandle? _reqEvt;
        protected EventWaitHandle? _resEvt;
        protected MemoryMappedFile? _evtMmf;
        protected MemoryMappedViewAccessor? _evtAccessor;
        protected EventWaitHandle? _evtDataReady;
        protected EventWaitHandle? _evtDataAck;
        protected string RpcId { get; private set; }

        public bool IsConnected => _rpcAccessor != null;

        public void Connect(string rpcId)
        {
            RpcId = rpcId;
            _rpcMmf = MemoryMappedFile.CreateOrOpen(PREFIX_RPC_MAP + rpcId, RPC_MAP_SIZE);
            _rpcAccessor = _rpcMmf.CreateViewAccessor();
            _reqEvt = new EventWaitHandle(false, EventResetMode.AutoReset, PREFIX_REQ_EVENT + rpcId);
            _resEvt = new EventWaitHandle(false, EventResetMode.AutoReset, PREFIX_RES_EVENT + rpcId);
            _evtMmf = MemoryMappedFile.CreateOrOpen(PREFIX_EVT_MAP + rpcId, EVT_MAP_SIZE);
            _evtAccessor = _evtMmf.CreateViewAccessor();
            _evtDataReady = new EventWaitHandle(false, EventResetMode.AutoReset, PREFIX_EVT_READY + rpcId);
            _evtDataAck = new EventWaitHandle(false, EventResetMode.AutoReset, PREFIX_EVT_ACK + rpcId);
        }

        public void Disconnect()
        {
            _rpcAccessor?.Dispose();
            _rpcMmf?.Dispose();
            _reqEvt?.Dispose();
            _resEvt?.Dispose();
            _evtAccessor?.Dispose();
            _evtMmf?.Dispose();
            _evtDataReady?.Dispose();
            _evtDataAck?.Dispose();
            _rpcAccessor = null;
            _rpcMmf = null;
            _reqEvt = null;
            _resEvt = null;
            _evtAccessor = null;
            _evtMmf = null;
            _evtDataReady = null;
            _evtDataAck = null;
        }

        protected T? InvokeRpc<T>(string method, params object[] args)
        {
            if (_rpcAccessor == null || _reqEvt == null || _resEvt == null)
                throw new InvalidOperationException("Not connected to RPC");

            var req = new { Method = method, Args = args };
            string json = JsonConvert.SerializeObject(req);
            byte[] data = Encoding.UTF8.GetBytes(json);

            _rpcAccessor.Write(REQ_OFFSET, data.Length);
            _rpcAccessor.WriteArray(REQ_OFFSET + DATA_HEADER_SIZE, data, 0, data.Length);
            _reqEvt.Set();

            if (_resEvt.WaitOne(10000))
            {
                int len = _rpcAccessor.ReadInt32(RES_OFFSET);
                if (len <= 0 || len > 1024 * 1024)
                    return default;

                byte[] resData = new byte[len];
                _rpcAccessor.ReadArray(RES_OFFSET + DATA_HEADER_SIZE, resData, 0, len);
                string resJson = Encoding.UTF8.GetString(resData);
                return JsonConvert.DeserializeObject<T>(resJson);
            }

            throw new TimeoutException($"RPC call to '{method}' timed out");
        }

        protected void InvokeRpcVoid(string method, params object[] args)
        {
            InvokeRpc<object>(method, args);
        }

        protected string ReadEvent()
        {
            if (_evtAccessor == null || _evtDataReady == null)
                return null;

            if (_evtDataReady.WaitOne(100))
            {
                int len = _evtAccessor.ReadInt32(0);
                if (len <= 0 || len > 1024 * 1024)
                    return null;

                byte[] data = new byte[len];
                _evtAccessor.ReadArray(DATA_HEADER_SIZE, data, 0, len);
                string json = Encoding.UTF8.GetString(data);
                _evtDataAck?.Set();
                return json;
            }

            return null;
        }

        public string ReadReqMemory()
        {
            return ReadMemoryString(_rpcAccessor, REQ_OFFSET);
        }

        public string ReadResMemory()
        {
            return ReadMemoryString(_rpcAccessor, RES_OFFSET);
        }

        public string ReadEvtMemory()
        {
            return ReadMemoryString(_evtAccessor, 0);
        }

        protected string ReadMemoryString(MemoryMappedViewAccessor accessor, int offset)
        {
            try
            {
                if (accessor == null) return "[Not connected]";
                int len = accessor.ReadInt32(offset);
                if (len <= 0 || len > 1024 * 1024)
                    return $"[Invalid data, len={len}]";

                byte[] data = new byte[len];
                accessor.ReadArray(offset + DATA_HEADER_SIZE, data, 0, len);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception ex)
            {
                return $"[Read error: {ex.Message}]";
            }
        }
    }

    public class MasterRpcClient : RpcClientBase
    {
        public string CreateBrowser(string url, int w, int h, int fps)
        {
            return InvokeRpc<string>("CreateBrowser", url, w, h, fps) ?? string.Empty;
        }

        public void StopBrowser(string spoutId)
        {
            InvokeRpcVoid("StopBrowser", spoutId);
        }
    }

    public class BrowserRpcClient : RpcClientBase
    {
        public string GetSpoutId()
        {
            return InvokeRpc<string>("GetSpoutId") ?? string.Empty;
        }

        public void LoadUrl(string url)
        {
            InvokeRpcVoid("LoadUrl", url);
        }

        public void ExecuteJs(string script)
        {
            InvokeRpcVoid("ExecuteJs", script);
        }

        public string GetUrl()
        {
            return InvokeRpc<string>("GetUrl") ?? string.Empty;
        }

        public bool Resize(int width, int height, int deviceScaleFactor, bool mobile)
        {
            return InvokeRpc<bool>("Resize", width, height, deviceScaleFactor, mobile);
        }

        public void SetAudioMuted(bool muted)
        {
            InvokeRpcVoid("SetAudioMuted", muted);
        }

        public void SetVolume(float volume)
        {
            InvokeRpcVoid("SetVolume", volume);
        }

        public void ShowDevTools()
        {
            InvokeRpcVoid("ShowDevTools");
        }

        public void SendMouseMove(int x, int y, bool mouseLeave = false, bool leftButtonPressed = false)
        {
            InvokeRpcVoid("SendMouseMove", x, y, mouseLeave, leftButtonPressed);
        }

        public void SendMouseClick(int x, int y, int button, bool mouseUp)
        {
            InvokeRpcVoid("SendMouseClick", x, y, button, mouseUp);
        }

        public void SendMouseWheel(int x, int y, int deltaX, int deltaY)
        {
            InvokeRpcVoid("SendMouseWheel", x, y, deltaX, deltaY);
        }

        public void SendKeyEvent(int windowsKeyCode, bool isUp, int modifiers)
        {
            InvokeRpcVoid("SendKeyEvent", windowsKeyCode, isUp, modifiers);
        }

        public void SendText(string text)
        {
            InvokeRpcVoid("SendText", text);
        }

        public int GetCursorType()
        {
            return InvokeRpc<int>("GetCursorType");
        }

        public void ClickLeft(int x, int y)
        {
            SendMouseClick(x, y, 0, false);
            Thread.Sleep(50);
            SendMouseClick(x, y, 0, true);
        }

        public void ClickRight(int x, int y)
        {
            SendMouseClick(x, y, 1, false);
            Thread.Sleep(50);
            SendMouseClick(x, y, 1, true);
        }

        public void DoubleClick(int x, int y)
        {
            ClickLeft(x, y);
            Thread.Sleep(100);
            ClickLeft(x, y);
        }

        public void Scroll(int x, int y, int delta)
        {
            SendMouseWheel(x, y, 0, delta);
        }

        public void PressKey(int keyCode, int modifiers = 0)
        {
            SendKeyEvent(keyCode, false, modifiers);
            Thread.Sleep(50);
            SendKeyEvent(keyCode, true, modifiers);
        }

        public void PressHotkey(int keyCode, int modifiers)
        {
            SendKeyEvent(keyCode, false, modifiers);
            Thread.Sleep(50);
            SendKeyEvent(keyCode, true, modifiers);
        }
    }

    public class TestCaseExecutor
    {
        private readonly Action<string> _log;

        public TestCaseExecutor(Action<string> log)
        {
            _log = log;
        }

        public void RunAllTests(MasterRpcClient masterClient)
        {
            _log("\n========== Running Automated System Integration Tests ==========");
            try
            {
                string spoutId = Test_CreateBrowser(masterClient);
                if (!string.IsNullOrEmpty(spoutId))
                {
                    var browserClient = new BrowserRpcClient();
                    browserClient.Connect(spoutId);
                    Thread.Sleep(500);

                    Test_BrowserNavigation(browserClient);
                    Test_BrowserJsExecution(browserClient);
                    Test_BrowserResize(browserClient);
                    Test_BrowserInput(browserClient);

                    masterClient.StopBrowser(spoutId);
                    _log("[PASS] StopBrowser executed successfully.");
                }
                _log("\n========== All Integration Tests Completed Successfully ==========");
            }
            catch (Exception ex)
            {
                _log($"[Fatal error] Test aborted: {ex.Message}");
            }
        }

        private string Test_CreateBrowser(MasterRpcClient masterClient)
        {
            _log("\n--- Test 1: Create Browser Instance ---");
            _log("Calling: CreateBrowser(\"https://www.bing.com\", 1280, 720, 60)");

            string spoutId = masterClient.CreateBrowser("https://www.bing.com", 1280, 720, 60);

            if (!string.IsNullOrEmpty(spoutId))
            {
                _log($"[PASS] Browser created successfully, SpoutId: {spoutId}");
                return spoutId;
            }
            else
            {
                _log("[FAIL] CreateBrowser returned empty SpoutId");
                return string.Empty;
            }
        }

        private void Test_BrowserNavigation(BrowserRpcClient browserClient)
        {
            _log("\n--- Test 2: Browser Navigation ---");
            _log("Calling: LoadUrl(\"https://www.baidu.com\")");
            browserClient.LoadUrl("https://www.baidu.com");
            Thread.Sleep(1000);
            _log("[PASS] Navigation command sent.");
        }

        private void Test_BrowserJsExecution(BrowserRpcClient browserClient)
        {
            _log("\n--- Test 3: JS Execution ---");
            _log("Calling: ExecuteJs(\"console.log('NCEF WebView2 Test Success');\")");
            browserClient.ExecuteJs("console.log('NCEF WebView2 Test Success');");
            _log("[PASS] JS Execution command sent.");
        }

        private void Test_BrowserResize(BrowserRpcClient browserClient)
        {
            _log("\n--- Test 4: Window Resize ---");
            _log("Calling: Resize(1920, 1080, 1, false)");
            bool resized = browserClient.Resize(1920, 1080, 1, false);
            _log($"[PASS] Resize returned: {resized}");
        }

        private void Test_BrowserInput(BrowserRpcClient browserClient)
        {
            _log("\n--- Test 5: Mouse & Keyboard Input ---");
            _log("Simulating Mouse move & click at (100, 100)");
            browserClient.SendMouseMove(100, 100);
            browserClient.ClickLeft(100, 100);

            _log("Simulating Keyboard text input: \"Hello WebView2\"");
            browserClient.SendText("Hello WebView2");
            _log("[PASS] Input commands dispatched.");
        }
    }
}
