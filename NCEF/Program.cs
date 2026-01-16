using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NCEF.Utils;

namespace NCEF
{
    internal class Program
    {
        private static AppCore _appCore;

        [STAThread]
        public static async Task Main(string[] args)
        {
            var parent = ParentProcessHelper.GetParentProcess();
            if (parent == null || parent.HasExited)
            {
                Console.WriteLine("Parent process does not exist. Exiting.");
                return;
            }

            Console.WriteLine($"ParentProcess: {parent.ProcessName} (PID: {parent.Id})");
            UserDataFolderCleanup.CleanupLockedUserDataFolder();
            _appCore = new AppCore();
            await _appCore.InitAsync();
            _ = ParentProcessHelper.MonitorParentProcess(parent,_appCore);
            await Task.Delay(-1);
        }
    }
}