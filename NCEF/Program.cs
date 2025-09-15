using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NCEF
{
    internal class Program
    {
        private static MainWindow main;
        private static TaskCompletionSource<bool> exitTcs = new TaskCompletionSource<bool>();

        [STAThread]
        public static void Main(string[] args)
        {
            var parent = GetParentProcess();
            if (parent == null || parent.HasExited)
            {
                Console.WriteLine("父进程不存在或已退出，程序自退出。");
                return;
            }

            Console.WriteLine("父进程: " + parent.ProcessName + " (PID: " + parent.Id + ")");

            main = new MainWindow();
            main.InitWebViewAsync().GetAwaiter().GetResult();

            // 启动一个任务监控父进程
            Task.Run(() =>
            {
                parent.WaitForExit();  // 阻塞直到父进程退出
                Console.WriteLine("父进程退出，程序自退出。");
                Environment.Exit(0);
            });

            exitTcs.Task.GetAwaiter().GetResult();
            main.OnClosed(EventArgs.Empty);
        }

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
        static extern int NtQueryInformationProcess(IntPtr processHandle, int processInformationClass,
            ref PROCESS_BASIC_INFORMATION processInformation, uint processInformationLength, out uint returnLength);

        static Process GetParentProcess()
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
    }
}