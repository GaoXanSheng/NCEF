using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NCEF.Utils
{
    public class ParentProcessHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessBasicInformation
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
            ref ProcessBasicInformation processInformation,
            uint processInformationLength,
            out uint returnLength);

        public static Process GetParentProcess()
        {
            try
            {
                var pbi = new ProcessBasicInformation();
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

        public static Task MonitorParentProcess(Process parent, AppCore appCore)
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
                                Console.WriteLine(
                                    $"Parent process PID {parentPid} has been reused by different process.");
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
                    appCore?.OnClosed();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup: {ex.Message}");
                }

                Environment.Exit(0);
            });
        }
    }
}