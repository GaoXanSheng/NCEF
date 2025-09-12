using System;
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
            Program.main = new MainWindow();
            Program.main.InitWebViewAsync().GetAwaiter().GetResult();
            Program.exitTcs.Task.GetAwaiter().GetResult();
            Program.main.OnClosed(EventArgs.Empty);
        }
    }

}