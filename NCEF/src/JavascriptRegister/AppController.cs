using System;

namespace NCEF
{
    public class AppController
    {
        private readonly AppCore _appCore;

        public AppController(AppCore appCore)
        {
            _appCore = appCore;
        }

        public void CreateBrowser(string url, int width, int height, string spoutId, int maxFps)
        {
            _appCore.CreateAndStartSession(url, width, height, spoutId, maxFps).ContinueWith(task =>
            {
                if (task.IsFaulted)
                {
                    Console.WriteLine($"Error creating browser session: {task.Exception}");
                }
            });
        }
    }
}