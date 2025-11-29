using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.OffScreen;

namespace NCEF
{
    public class AppCore
    {
        private List<RenderSession> _sessions = new List<RenderSession>();
        private bool _isCefInitialized = false;
        public AppController AppController { get; private set; }

        public AppCore()
        {
            AppController = new AppController(this);
        }

        public async Task InitAsync()
        {
            InitGlobalCef(EnvManager.GetInt("BROWSER_PORT", 9222));
            int maxFps = EnvManager.GetInt("MAXFPS", 60);
            var bounds = Screen.PrimaryScreen.Bounds;
            string mainUrl = EnvManager.GetString("CUSTOMIZE_LOADING_SCREEN_URL", "https://google.com");
            string mainSpoutId = EnvManager.GetString("SPOUT_ID", "NCEF");
            
            await CreateAndStartSession(mainUrl, bounds.Width, bounds.Height, mainSpoutId, maxFps);
        }
        
        public async Task CreateAndStartSession(string url, int w, int h, string spoutName, int fps)
        {
            var session = new RenderSession(this, url, w, h, spoutName, fps);
            _sessions.Add(session);
            await session.StartAsync(0);
            Console.WriteLine($"Started Session: {spoutName} -> {url}");
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
            _isCefInitialized = true;
        }

        public void OnClosed()
        {
            foreach (var session in _sessions)
            {
                session.Dispose();
            }
            _sessions.Clear();
            Cef.Shutdown();
        }
    }
}