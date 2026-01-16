using System;
using System.IO;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.OffScreen;
using NCEF.Controller;

namespace NCEF
{
    public class AppCore
    {
        private MasterControllerImpl _controller;

        public async Task InitAsync()
        {
            InitGlobalCef();
            Console.WriteLine("NCEF Master Ready.");
            _controller = new MasterControllerImpl();
#if DEBUG
            _controller.CreateAndStartSession("https://testufo.com/", "NCEF_DUMMY_BROWSER", 60).Wait();
#endif
            // CEF preheat
            _controller.CreateAndStartSession("about:blank", "NCEF_DUMMY_BROWSER", 1).Wait();
        }

        private void InitGlobalCef()
        {
            if (Cef.IsInitialized.GetValueOrDefault()) return;
            var settings = new CefSettings
            {
                CachePath = Config.UserDataPath,
                LogFile = Path.Combine(Environment.CurrentDirectory, ""),
                WindowlessRenderingEnabled = true,
                MultiThreadedMessageLoop = true
            };

            if (Config.DebugPort != 0)
            {
                settings.CefCommandLineArgs.Add("remote-debugging-port", Config.DebugPort.ToString());
            }

            settings.EnableAudio();

            if (!Cef.Initialize(settings))
            {
                throw new Exception("CefSharp initialization failed!");
            }
        }

        public void OnClosed()
        {
            _controller.OnClosed();
            Cef.Shutdown();
        }
    }
}