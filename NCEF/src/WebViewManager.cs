using System;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using CefSharp;
using CefSharp.OffScreen;
using NCEF.Handler;
using NCEF.JavascriptRegister;

namespace NCEF
{
    #region WebViewManager

    public class WebViewManager
    {
        public ChromiumWebBrowser Browser { get; private set; }
        public int MaxFPS { get; }
        public string InitialUrl { get; }

        public WebViewManager(string url, int maxFPS)
        {
            InitialUrl = url;
            MaxFPS = maxFPS;
        }

        public async Task InitializeAsync(int debugPort)
        {
            string userDataPath = Path.Combine(Environment.CurrentDirectory, "User Data");
            
            // Check if User Data folder is locked/occupied BEFORE any CEF initialization
            if (!Cef.IsInitialized.GetValueOrDefault() && IsDirectoryLocked(userDataPath))
            {
                Console.Error.WriteLine("User Data folder is already in use by another NCEF instance!");
                Environment.Exit(-1);
                return; // Safety return, though Exit should terminate
            }

            if (!Cef.IsInitialized.GetValueOrDefault())
            {
                var settings = new CefSettings
                {
                    CachePath = userDataPath,
                    LogFile = Path.Combine(Environment.CurrentDirectory, "cef.log"),
                    WindowlessRenderingEnabled = true
                };
                settings.CefCommandLineArgs.Add("remote-debugging-port", debugPort.ToString());
                settings.CefCommandLineArgs.Add("proprietary-codecs", "1");
                settings.CefCommandLineArgs.Add("enable-media-stream", "1");
                settings.EnableAudio();

                if (!Cef.Initialize(settings))
                {
                    Console.Error.WriteLine("CefSharp initialization failed!");
                    Environment.Exit(-1);
                }
            }

            var browserSettings = new BrowserSettings
            {
                WindowlessFrameRate = MaxFPS
            };
            var jsBindingSettings = new CefSharp.JavascriptBinding.JavascriptBindingSettings()
            {
                LegacyBindingEnabled = true // 必须在浏览器创建前设置
            };
            Browser = new ChromiumWebBrowser(InitialUrl, browserSettings)
            {
                LifeSpanHandler = new LifeSpanHandler(),
                AudioHandler = new VolumeAudioHandler(),
                JsDialogHandler = new JsDialogManager(),
            };
            Browser.FrameLoadEnd += OnFrameLoadEnd;
            Browser.JavascriptObjectRepository.Register(
                "JsAudioController", // JS 端访问名
                new JsAudioController(Browser), // C# 对象实例
                isAsync: false, // JS 调用返回 Promise
                options: BindingOptions.DefaultBinder
            );
            await Browser.WaitForInitialLoadAsync();
        }

        private bool IsDirectoryLocked(string directoryPath)
        {
            // If directory doesn't exist, it's not locked
            if (!Directory.Exists(directoryPath))
            {
                return false;
            }

            // First, check for other NCEF or CefSharp processes using the same User Data directory
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var cefProcesses = Process.GetProcessesByName("NCEF")
                    .Concat(Process.GetProcessesByName("CefSharp.BrowserSubprocess"))
                    .Where(p => p.Id != currentProcess.Id)
                    .ToList();

                if (cefProcesses.Any())
                {
                    Console.WriteLine($"Found {cefProcesses.Count} other CEF/NCEF process(es) running");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking for processes: {ex.Message}");
            }

            // Check multiple lock files that Chromium/CEF creates
            string[] lockFiles = new string[]
            {
                Path.Combine(directoryPath, "SingletonLock"),
                Path.Combine(directoryPath, "SingletonSocket"),
                Path.Combine(directoryPath, "SingletonCookie"),
                Path.Combine(directoryPath, "Cookies"),
                Path.Combine(directoryPath, "Cookies-journal"),
                Path.Combine(directoryPath, "Local State"),
                Path.Combine(directoryPath, "lockfile")
            };

            foreach (string lockFilePath in lockFiles)
            {
                if (File.Exists(lockFilePath))
                {
                    try
                    {
                        // Try to open the file with exclusive access
                        using (FileStream fs = File.Open(lockFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            // If we can open it exclusively, continue checking other files
                        }
                    }
                    catch (IOException)
                    {
                        // File is locked by another process
                        Console.WriteLine($"Detected locked file: {Path.GetFileName(lockFilePath)}");
                        return true;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // File is locked or we don't have permission
                        Console.WriteLine($"Access denied to file: {Path.GetFileName(lockFilePath)}");
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (!e.Frame.IsValid) return;
            if (!e.Frame.IsMain) return;
            string script = @"document.addEventListener(""mousedown"",function(e){
                const s=e.target.closest(""select"");if(!s)return;e.preventDefault();
                const o=Array.from(s.options).map(o=>({value:o.value,text:o.text}));
                let old=document.getElementById(""custom-select-list"");if(old)old.remove();
                const r=s.getBoundingClientRect(),ul=document.createElement(""ul"");
                ul.id=""custom-select-list"";
                ul.style.position=""absolute"";
                ul.style.left=r.left+window.scrollX+""px"";
                ul.style.top=r.bottom+window.scrollY+""px"";
                ul.style.background=""#fff"";
                ul.style.border=""1px solid #ccc"";
                ul.style.padding=""0"";
                ul.style.margin=""0"";
                ul.style.listStyle=""none"";
                ul.style.zIndex=9999;
                o.forEach(opt=>{
                    const li=document.createElement(""li"");
                    li.textContent=opt.text;
                    li.style.padding=""5px 10px"";
                    li.style.cursor=""pointer"";
                    li.addEventListener(""mouseenter"",()=>li.style.background=""#eee"");
                    li.addEventListener(""mouseleave"",()=>li.style.background=""#fff"");
                    li.addEventListener(""click"",()=>{s.value=opt.value;ul.remove();s.dispatchEvent(new Event(""change"",{bubbles:true}))});
                    ul.appendChild(li)
                });
                document.body.appendChild(ul)
            });
            document.addEventListener(""click"",e=>{
                const ul=document.getElementById(""custom-select-list"");
                if(ul&&!ul.contains(e.target)&&e.target.tagName.toLowerCase()!==""select"")ul.remove()
            });";
            e.Frame.ExecuteJavaScriptAsync(script);
        }
    }

    #endregion
}