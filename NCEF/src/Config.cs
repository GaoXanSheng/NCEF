using System;
using System.IO;
using System.Windows.Forms;
using NCEF.Utils;

namespace NCEF
{
    public class Config
    {
        public static readonly int DebugPort = Env.GetInt("BROWSER_PORT", 9222);
        public static readonly string MasterId  = Env.GetString("MASTER_RPC_ID", "GLOBAL_NCEF");
        public static readonly string UserDataPath = Path.Combine(Environment.CurrentDirectory, "User Data");
        public static readonly string CefLogFile = Path.Combine(Environment.CurrentDirectory, "cef.log");
        public static readonly System.Drawing.Rectangle Bounds = Screen.PrimaryScreen.Bounds;
    }
}