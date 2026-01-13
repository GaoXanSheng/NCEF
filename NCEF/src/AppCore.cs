using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using CefSharp;
using CefSharp.OffScreen;
using NCEF.IController;
using NCEF.Manager;
using NCEF.RPC;

namespace NCEF
{
    public class AppCore : IMasterController
    {
        private Dictionary<string, RenderSession> _sessions = new Dictionary<string, RenderSession>();
        private RpcServer<IMasterController> _masterRpc;

        public async Task InitAsync()
        {
            int debugPort = EnvManager.GetInt("BROWSER_PORT", 9222);
            InitGlobalCef(debugPort);
            string masterId = EnvManager.GetString("MASTER_RPC_ID", "GLOBAL_NCEF");
            _masterRpc = new RpcServer<IMasterController>(masterId, this);
            var bounds = Screen.PrimaryScreen.Bounds;
            // Create a dummy session to keep CEF alive
            CreateAndStartSession("about:blank", bounds.Width, bounds.Height, "NCEF_DUMMY_BROWSER", 1).Wait();
            Console.WriteLine("NCEF Master Ready.");
        }

        public string CreateBrowser(string url, int w, int h, int fps)
        {
            string spoutName = "BRW_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (_sessions.ContainsKey(spoutName))
                return spoutName;
            var session = new RenderSession(url, w, h, spoutName, fps);
            _sessions.Add(spoutName, session);
            _ = Task.Run(async () =>
            {
                try
                {
                    await session.StartAsync(w, h);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"StartAsync failed [{spoutName}]: {e}");
                    _sessions.Remove(spoutName);
                }
            });

            return spoutName;
        }

        public void StopBrowser(string spoutId) => StopSession(spoutId);
        public void Shutdown() => Environment.Exit(0);
        
        public async Task CreateAndStartSession(string url, int w, int h, string spoutName, int fps)
        {
            if (_sessions.ContainsKey(spoutName)) return;
            var renderSession = new RenderSession(url, w, h, spoutName, fps);
            await renderSession.StartAsync(w,h);
            _sessions.Add(spoutName, renderSession);
        }

        public void StopSession(string spoutName)
        {
            if (_sessions.TryGetValue(spoutName, out var session))
            {
                session.Dispose();
                _sessions.Remove(spoutName);
                Console.WriteLine($"RPC Dispose: {spoutName}");
            }
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