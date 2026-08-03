using System;
using System.IO;
using System.Threading.Tasks;
using log4net;
using NCEF.Controller;

namespace NCEF
{
    public class AppCore
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AppCore));

        public async Task InitAsync()
        {
            var _controller = new MasterControllerImpl();
            await _controller.CreateAndStartSession("about:blank", "NCEF_DUMMY_BROWSER", 1);
            Log.Info("NCEF Master Ready.");
        }

        public void OnClosed()
        {
        }
    }
}