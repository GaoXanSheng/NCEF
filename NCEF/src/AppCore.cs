using System;
using System.IO;
using System.Threading.Tasks;
using NCEF.Controller;

namespace NCEF
{
    public class AppCore
    {
        public async Task InitAsync()
        {
            var _controller = new MasterControllerImpl();
            await _controller.CreateAndStartSession("about:blank", "NCEF_DUMMY_BROWSER", 1);
            Program.Logger.Info("NCEF Master Ready.");
        }

        public void OnClosed()
        {
        }
    }
}