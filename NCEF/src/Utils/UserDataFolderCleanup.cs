using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NCEF.Utils
{
    public class UserDataFolderCleanup
    {
        public static void CleanupLockedUserDataFolder()
        {
            if (!Directory.Exists(Config.UserDataPath))
            {
                Console.WriteLine("User Data folder does not exist, no cleanup needed.");
                return;
            }
            Console.WriteLine("Checking for processes locking User Data folder...");
            var currentProcess = Process.GetCurrentProcess();
            var lockingProcesses = Process.GetProcessesByName("NCEF")
                .Where(p => p.Id != currentProcess.Id)
                .ToList();

            if (lockingProcesses.Any())
            {
                Console.WriteLine($"Found {lockingProcesses.Count} process(es) that may be locking User Data folder:");
                foreach (var process in lockingProcesses)
                {
                    try
                    {
                        Console.WriteLine($"  - {process.ProcessName} (PID: {process.Id})");
                        Console.WriteLine($"    Killing process {process.Id}...");
                        process.Kill();
                        process.WaitForExit(5000);
                        Console.WriteLine($"    Process {process.Id} terminated successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Failed to kill process {process.Id}: {ex.Message}");
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
                        Console.WriteLine($"Deleted lock file: {Path.GetFileName(lockFile)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Could not delete lock file {Path.GetFileName(lockFile)}: {ex.Message}");
                    }
                }
            }

            Console.WriteLine("User Data folder cleanup completed.");
        }
    }
}