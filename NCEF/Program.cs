using System;
using System.Diagnostics;
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
            return Task.Run(() =>
            {
                try
                {
                    parent.WaitForExit(); // 同步阻塞，但在独立线程中
                }
                catch
                {
                    // 忽略异常
                }

                Console.WriteLine("Parent process exited. Closing program.");
                main.OnClosed(EventArgs.Empty);
                Environment.Exit(0);
            });
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
            var pbi = new PROCESS_BASIC_INFORMATION();
            uint retLen;
            int status = NtQueryInformationProcess(Process.GetCurrentProcess().Handle, 0, ref pbi,
                (uint)Marshal.SizeOf(pbi), out retLen);

            if (status != 0) return null;

            try
            {
                return Process.GetProcessById(pbi.InheritedFromUniqueProcessId.ToInt32());
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
