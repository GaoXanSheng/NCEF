using System;
using System.Threading.Tasks;

namespace NCEF
{
    public class AppController
    {
        private readonly AppCore _appCore;

        public AppController(AppCore appCore)
        {
            _appCore = appCore;
        }

        public async Task CreateBrowser(string url, int width, int height, string spoutId, int maxFps)
        {
            try
            {
                await _appCore.CreateAndStartSession(url, width, height, spoutId, maxFps);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating browser session: {ex}");
            }
        }

        public void CloseBrowser(string spoutId)
        {
            try
            {
                _appCore.StopSession(spoutId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error closing browser session: {ex}");
            }
        }
    }
}