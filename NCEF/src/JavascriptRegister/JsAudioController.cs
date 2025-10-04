using System;
using System.Threading.Tasks;
using CefSharp.OffScreen;
using NCEF.Handler;

namespace NCEF.JavascriptRegister
{
    public class JsAudioController
    {
        private readonly ChromiumWebBrowser _browser;
        public JsAudioController(ChromiumWebBrowser browser)
        {
            _browser = browser;
        }
        public static void SetVolume(double value)
        {
            if (value < 0) value = 0;
            if (value > 1) value = 1;
            VolumeAudioHandler.SetVolume(value);
        }
    }
}