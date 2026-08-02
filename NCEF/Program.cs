using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using log4net;
using log4net.Config;
using NCEF.Utils;

namespace NCEF
{
    internal class Program
    {
        private static AppCore _appCore;
        public static readonly ILog Logger = LogManager.GetLogger(typeof(Program));
        public static SynchronizationContext MainSyncContext { get; private set; }

        public static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            var parent = ParentProcessHelper.GetParentProcess();
            if (parent == null || parent.HasExited)
            {
                Logger.Info("Parent process does not exist. Exiting.");
                return;
            }

            Logger.Info($"ParentProcess: {parent.ProcessName} (PID: {parent.Id})");
            UserDataFolderCleanup.CleanupLockedUserDataFolder();

            // Run WebView2 initialization and message loop on a dedicated STA thread
            var staThread = new Thread(() =>
            {
                var syncContext = new WindowsFormsSynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(syncContext);
                MainSyncContext = syncContext;
                _appCore = new AppCore();
                _appCore.InitAsync().ContinueWith(t =>
                {
                    if (t.IsFaulted && t.Exception != null)
                    {
                        Logger.Error("InitAsync Failed", t.Exception.InnerException ?? t.Exception);
                    }
                });

                _ = ParentProcessHelper.MonitorParentProcess(parent, _appCore);
                Application.Run();
            });

            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join();
        }
    }
}