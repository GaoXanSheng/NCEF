using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NCEF
{
    internal class Program
    {
        private static MainWindow main;

        [STAThread]
        public static async Task Main(string[] args)
        {

            var parent = GetParentProcess();
            if (parent == null || parent.HasExited)
            {
                Console.WriteLine("Parent process does not exist. Exiting.");
                return;
            }

            Console.WriteLine($"ParentProcess: {parent.ProcessName} (PID: {parent.Id})");

            // Clean up locked User Data folder before initialization
            CleanupLockedUserDataFolder();

            main = new MainWindow();
            await main.InitAsync();

            // 当父进程退出时关闭程序
            _ = MonitorParentProcess(parent);

            // 阻止主线程退出
            await Task.Delay(-1);
        }
        

        #region Parent Process Monitoring

        private static Task MonitorParentProcess(Process parent)
        {
            return Task.Run(async () =>
            {
                int parentPid = parent.Id;
                Console.WriteLine($"Starting parent process monitor for PID: {parentPid}");

                try
                {
                    // 使用轮询方式检查父进程，更可靠
                    while (true)
                    {
                        await Task.Delay(1000); // 每秒检查一次

                        // 方法1: 尝试刷新进程信息
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
                            // 如果无法刷新，说明进程可能已经不存在
                            Console.WriteLine("Parent process is no longer accessible (Refresh failed).");
                            break;
                        }

                        // 方法2: 尝试通过PID重新获取进程
                        try
                        {
                            Process checkProcess = Process.GetProcessById(parentPid);
                            // 如果能获取到进程，检查进程名是否匹配（防止PID被重用）
                            if (checkProcess.ProcessName != parent.ProcessName)
                            {
                                Console.WriteLine($"Parent process PID {parentPid} has been reused by different process.");
                                break;
                            }
                        }
                        catch (ArgumentException)
                        {
                            // 进程不存在
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
                    main?.OnClosed(EventArgs.Empty);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup: {ex.Message}");
                }
                Environment.Exit(0);
            });
        }


        #endregion

        #region User Data Folder Cleanup

        private static void CleanupLockedUserDataFolder()
        {
            string userDataPath = Path.Combine(Environment.CurrentDirectory, "User Data");

            // If directory doesn't exist, nothing to clean up
            if (!Directory.Exists(userDataPath))
            {
                Console.WriteLine("User Data folder does not exist, no cleanup needed.");
                return;
            }

            Console.WriteLine("Checking for processes locking User Data folder...");

            // Find all NCEF and CefSharp processes (excluding current process)
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
                        process.WaitForExit(5000); // Wait up to 5 seconds for process to exit
                        Console.WriteLine($"    Process {process.Id} terminated successfully.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Failed to kill process {process.Id}: {ex.Message}");
                    }
                }

                // Give system time to release file locks
                System.Threading.Thread.Sleep(500);
            }

            // Additionally check for lock files
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

        #endregion

        #region Parent Process Helper

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

        #endregion
    }
}
