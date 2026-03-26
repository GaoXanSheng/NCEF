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

            _controller = new MasterControllerImpl();
#if DEBUG
            _controller.CreateAndStartSession("https://testufo.com/", "NCEF_DUMMY_BROWSER", 60).Wait();
#endif
            _controller.CreateAndStartSession("about:blank", "NCEF_DUMMY_BROWSER", 1).Wait();
            Console.WriteLine("NCEF Master Ready.");
        }
        private void ApplyExtraCommandLineArgs(CefSettings settings)
        {
            string cliString = Config.CefCommandLineArgs; 
            if (string.IsNullOrWhiteSpace(cliString)) return;
            var args = cliString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var arg in args)
            {
                string cleanArg = arg.TrimStart('-');
                if (string.IsNullOrEmpty(cleanArg)) continue;
                if (cleanArg.Contains("="))
                {
                    var parts = cleanArg.Split(new[] { '=' }, 2);
                    settings.CefCommandLineArgs.Add(parts[0], parts[1]);
                }
                else
                {
                    settings.CefCommandLineArgs.Add(cleanArg, "1");
                }
            }
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
            ApplyExtraCommandLineArgs(settings);
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