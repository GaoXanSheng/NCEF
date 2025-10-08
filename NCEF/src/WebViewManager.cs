using System;
using System.IO;
using System.Threading.Tasks;
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

        private int userIntDataId = 0;

        private string GetUserDataPath()
        {
            string basePath = Path.Combine(Environment.CurrentDirectory, "UserData_"+userIntDataId);
            string lockFile = Path.Combine(basePath, "LOCK");
            if (File.Exists(lockFile))
            {
                userIntDataId++;
                return GetUserDataPath();
            }

            return basePath;
        }

        public async Task InitializeAsync(int debugPort)
        {
            var settings = new CefSettings
            {
                CachePath = GetUserDataPath(),

                LogFile = Path.Combine(Environment.CurrentDirectory, "cef.log"),
                WindowlessRenderingEnabled = true
            };
            settings.CefCommandLineArgs.Add("remote-debugging-port", debugPort.ToString());
            settings.CefCommandLineArgs.Add("proprietary-codecs", "1");
            settings.CefCommandLineArgs.Add("enable-media-stream", "1");
            settings.EnableAudio();

            if (!Cef.IsInitialized.GetValueOrDefault())
            {
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
                AudioHandler = new VolumeAudioHandler()
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