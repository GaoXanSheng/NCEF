using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using log4net;

namespace NCEF.Utils
{
    public class UserDataFolderCleanup
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(UserDataFolderCleanup));

        public static void CleanupLockedUserDataFolder()
        {
            if (!Directory.Exists(Config.UserDataPath))
            {
                Log.Info("User Data folder does not exist, no cleanup needed.");
                return;
            }
            Log.Info("Checking for processes locking User Data folder...");
            var currentProcess = Process.GetCurrentProcess();
            var lockingProcesses = Process.GetProcessesByName("NCEF")
                .Where(p => p.Id != currentProcess.Id)
                .ToList();

            if (lockingProcesses.Any())
            {
                Log.Info($"Found {lockingProcesses.Count} process(es) that may be locking User Data folder:");
                foreach (var process in lockingProcesses)
                {
                    try
                    {
                        Log.Info($"  - {process.ProcessName} (PID: {process.Id})");
                        Log.Info($"    Killing process {process.Id}...");
                        process.Kill();
                        process.WaitForExit(5000);
                        Log.Info($"    Process {process.Id} terminated successfully.");
                    }
                    catch (Exception ex)
                    {
                        Log.Info($"    Failed to kill process {process.Id}: {ex.Message}");
                    }
                }
                System.Threading.Thread.Sleep(500);
            }
            string[] lockFiles = new string[]
            {
                Path.Combine(Config.UserDataPath, "SingletonLock"),
                Path.Combine(Config.UserDataPath, "SingletonSocket"),
                Path.Combine(Config.UserDataPath, "SingletonCookie"),
                Path.Combine(Config.UserDataPath, "lockfile")
            };

            foreach (string lockFile in lockFiles)
            {
                if (File.Exists(lockFile))
                {
                    try
                    {
                        File.Delete(lockFile);
                        Log.Info($"Deleted lock file: {Path.GetFileName(lockFile)}");
                    }
                    catch (Exception ex)
                    {
                        Log.Info($"Could not delete lock file {Path.GetFileName(lockFile)}: {ex.Message}");
                    }
                }
            }

            Log.Info("User Data folder cleanup completed.");
        }
    }
}