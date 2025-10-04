using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NCEF
{
    internal class Program
    {
        private static MainWindow main;

        [STAThread]
        public static void Main(string[] args)
        {
            var parent = GetParentProcess();
            if (parent == null || parent.HasExited)
            {
                Console.WriteLine("TheProgramIsSelfExited");
                return;
            }

            Console.WriteLine($"ParentProcess: {parent.ProcessName} (PID: {parent.Id})");

            main = new MainWindow();
            main.InitAsync().GetAwaiter().GetResult();

            // 父进程监控 (兼容 .NET Framework)
            Task.Run(() =>
            {
                while (!parent.HasExited)
                {
                    Thread.Sleep(1000); // 每 0.5 秒检查一次
                }

                Console.WriteLine("TheProgramIsSelfExited");
                main.OnClosed(EventArgs.Empty);
                Environment.Exit(0);
            });

            // 阻止主线程退出
            Thread.Sleep(Timeout.Infinite);
        }

        #region Parent Process Helper

        [StructLayout(LayoutKind.Sequential)]
        struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr Reserved1;
            public IntPtr PebBaseAddress;
            public IntPtr Reserved2_0;
            public IntPtr Reserved2_1;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation, uint processInformationLength, out uint returnLength);

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