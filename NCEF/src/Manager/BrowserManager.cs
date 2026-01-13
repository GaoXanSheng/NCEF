using System;
using System.IO;
using System.Threading.Tasks;
using CefSharp;
using CefSharp.OffScreen;
using NCEF.Controller;
using NCEF.Handler;

namespace NCEF.Manager
{
    #region BrowserManager

    public class BrowserManager
    {
        public ChromiumWebBrowser chromiumWebBrowser { get; private set; }
        public int MaxFPS { get; }
        public string InitialUrl { get; }
        private readonly string _spoutId;
        private readonly Action _onClose;

        public BrowserManager(string url, int maxFPS, string spoutId, Action onClose)
        {
            InitialUrl = url;
            MaxFPS = maxFPS;
            _spoutId = spoutId;
            _onClose = onClose;
        }

        public async Task InitializeAsync(BrowserControllerImpl _controller)
        {
            string userDataPath = Path.Combine(Environment.CurrentDirectory, "User Data");
            CefSharpSettings.ConcurrentTaskExecution = true; 
            if (!Cef.IsInitialized.GetValueOrDefault())
            {
                var settings = new CefSettings
                {
                    CachePath = userDataPath,
                    LogFile = Path.Combine(Environment.CurrentDirectory, "cef.log"),
                    WindowlessRenderingEnabled = true,
                    MultiThreadedMessageLoop = true,
                };
                settings.CefCommandLineArgs.Add("proprietary-codecs", "1");
                settings.CefCommandLineArgs.Add("enable-media-stream", "1");
                settings.EnableAudio();
            }

            var browserSettings = new BrowserSettings
            {
                WindowlessFrameRate = MaxFPS,
            };
            chromiumWebBrowser = new ChromiumWebBrowser(InitialUrl, browserSettings)
            {
                LifeSpanHandler = new LifeSpanHandler(_onClose),
                JsDialogHandler = new JsDialogManager(),
            };
            _controller.BindJsBridge();
            chromiumWebBrowser.FrameLoadEnd += OnFrameLoadEnd;
            await chromiumWebBrowser.WaitForInitialLoadAsync();

        }


        private void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            if (!e.Frame.IsValid) return;
            if (!e.Frame.IsMain) return;
            e.Frame.ExecuteJavaScriptAsync($"document.title = '{_spoutId}';");
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