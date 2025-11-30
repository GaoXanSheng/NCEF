using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms; // 如果需要获取屏幕大小
using CefSharp;
using CefSharp.OffScreen;

namespace NCEF
{
    public class AppCore
    {
        // 管理所有会话的列表
        private Dictionary<string, RenderSession> _sessions = new Dictionary<string, RenderSession>();
        private AppController _appController;
        private int _debugPort; // Store the debug port

        public async Task InitAsync()
        {
            _appController = new AppController(this);
            _debugPort = EnvManager.GetInt("BROWSER_PORT", 9222); // Get debug port here
            InitGlobalCef(_debugPort);
            int maxFps = EnvManager.GetInt("MAXFPS", 60);
            var bounds = Screen.PrimaryScreen.Bounds;
            string mainUrl = EnvManager.GetString("CUSTOMIZE_LOADING_SCREEN_URL", "https://google.com");
            string mainSpoutId = EnvManager.GetString("SPOUT_ID", "NCEF");
            await CreateAndStartSession(mainUrl, bounds.Width, bounds.Height, mainSpoutId, maxFps);
        }

        public async Task CreateAndStartSession(string url, int w, int h, string spoutName, int fps)
        {
            if (_sessions.ContainsKey(spoutName)) return;
            var renderSession = new RenderSession(url, w, h, spoutName, fps, _appController);
            await renderSession.StartAsync(0);
            _sessions.Add(spoutName, renderSession);
            Console.WriteLine($"Started Session: {spoutName} -> {url}");
        }

        public Task StopSession(string spoutName)
        {
            if (_sessions.ContainsKey(spoutName))
            {
                _sessions[spoutName].Dispose();
                _sessions.Remove(spoutName);
                Console.WriteLine($"Stopped Session: {spoutName}");
            }
            return Task.CompletedTask;
        }

        private void InitGlobalCef(int debugPort)
        {
            if (Cef.IsInitialized.GetValueOrDefault()) return;
            string userDataPath = Path.Combine(Environment.CurrentDirectory, "User Data");
            var settings = new CefSettings
            {
                CachePath = userDataPath,
                LogFile = Path.Combine(Environment.CurrentDirectory, "cef.log"),
                WindowlessRenderingEnabled = true,
                MultiThreadedMessageLoop = true
            };

            settings.CefCommandLineArgs.Add("remote-debugging-port", debugPort.ToString());
            settings.CefCommandLineArgs.Add("proprietary-codecs", "1");
            settings.CefCommandLineArgs.Add("enable-media-stream", "1");
            settings.EnableAudio();

            if (!Cef.Initialize(settings))
            {
                throw new Exception("CefSharp initialization failed!");
            }
        }

        public void OnClosed()
        {
            _sessions.Clear();
            Cef.Shutdown();
        }
    }
}