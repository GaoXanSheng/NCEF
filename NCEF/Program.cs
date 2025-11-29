using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NCEF
{
    internal class Program
    {
        private static AppCore _appCore;
        public static Thread MainThread { get; private set; }

        [STAThread]
        public static async Task Main(string[] args)
        {
            MainThread = Thread.CurrentThread;
            
            var parent = GetParentProcess();
            if (parent == null || parent.HasExited)
            {
                Console.WriteLine("Parent process does not exist. Exiting.");
                return;
            }

            Console.WriteLine($"ParentProcess: {parent.ProcessName} (PID: {parent.Id})");
            
            CleanupLockedUserDataFolder();

            _appCore = new AppCore();
            await _appCore.InitAsync();

            _ = MonitorParentProcess(parent);

            await Task.Delay(-1);
        }
        
        private static Task MonitorParentProcess(Process parent)
        {
            return Task.Run(async () =>
            {
                int parentPid = parent.Id;
                Console.WriteLine($"Starting parent process monitor for PID: {parentPid}");

                try
                {
                    while (true)
                    {
                        await Task.Delay(1000);

                        try
                        {
                            parent.Refresh();
                            if (parent.HasExited)
                            {
                                Console.WriteLine("Parent process has exited (detected via Refresh/HasExited).");
                                break;
                            }
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("Parent process is no longer accessible (Refresh failed).");
                            break;
                        }

                        try
                        {
                            Process checkProcess = Process.GetProcessById(parentPid);
                            if (checkProcess.ProcessName != parent.ProcessName)
                            {
                                Console.WriteLine($"Parent process PID {parentPid} has been reused by different process.");
                                break;
                            }
                        }
                        catch (ArgumentException)
                        {
                            Console.WriteLine($"Parent process with PID {parentPid} no longer exists.");
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in parent process monitoring: {ex.Message}");
                }

                Console.WriteLine("Parent process exited. Closing NCEF...");
                try
                {
                    _appCore?.OnClosed();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup: {ex.Message}");
                }
                Environment.Exit(0);
            });
        }

        private static void CleanupLockedUserDataFolder()
        {
            string userDataPath = Path.Combine(Environment.CurrentDirectory, "User Data");

            if (!Directory.Exists(userDataPath))
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
                Path.Combine(userDataPath, "SingletonLock"),
                Path.Combine(userDataPath, "SingletonSocket"),
                Path.Combine(userDataPath, "SingletonCookie"),
                Path.Combine(userDataPath, "lockfile")
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

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation,
            uint processInformationLength,
            out uint returnLength);

        private static Process GetParentProcess()
        {
            try
            {
                var pbi = new PROCESS_BASIC_INFORMATION();
                uint retLen;
                int status = NtQueryInformationProcess(
                    Process.GetCurrentProcess().Handle, 
                    0, 
                    ref pbi,
                    (uint)Marshal.SizeOf(pbi), 
                    out retLen);

                if (status != 0)
                {
                    Console.WriteLine($"NtQueryInformationProcess failed with status: {status}");
                    return null;
                }

                int parentPid = pbi.InheritedFromUniqueProcessId.ToInt32();
                Console.WriteLine($"Detected parent process PID: {parentPid}");

                try
                {
                    Process parentProcess = Process.GetProcessById(parentPid);
                    Console.WriteLine($"Parent process found: {parentProcess.ProcessName}");
                    return parentProcess;
                }
                catch (ArgumentException ex)
                {
                    Console.WriteLine($"Parent process {parentPid} not found: {ex.Message}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting parent process: {ex.Message}");
                return null;
            }
        }
    }
}