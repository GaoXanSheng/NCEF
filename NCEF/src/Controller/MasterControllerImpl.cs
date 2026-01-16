using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NCEF.Browser;
using NCEF.RPC;

namespace NCEF.Controller
{
    public class MasterControllerImpl : IMasterController
    {
        private readonly RpcServer<IMasterController>  _masterRpc;
        private readonly Dictionary<string, BrowserSession> _sessions = new Dictionary<string, BrowserSession>();

        public MasterControllerImpl()
        {
            _masterRpc = new RpcServer<IMasterController>(Config.MasterId, this);
        }

        public async Task CreateAndStartSession(string url, string spoutName, int fps)
        {
            if (_sessions.ContainsKey(spoutName)) return;
            var renderSession = new BrowserSession(url, spoutName, fps);
            await renderSession.StartAsync(Config.Bounds.Width, Config.Bounds.Height);
            _sessions.Add(spoutName, renderSession);
        }
        public string CreateBrowser(string url, int w, int h, int fps)
        {
            string spoutName = "N_NCEF_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            if (_sessions.ContainsKey(spoutName))
                return spoutName;
            var session = new BrowserSession(url, spoutName, fps);
            _sessions.Add(spoutName, session);
            session.StartAsync(w,h).Wait();
            return spoutName;
        }

        public void StopBrowser(string spoutName)
        {
            if (_sessions.TryGetValue(spoutName, out var session))
            {
                session.Dispose();
                _sessions.Remove(spoutName);
                Console.WriteLine($"RPC Dispose: {spoutName}");
            }
        }

        public void OnClosed()
        {
            _sessions.Clear();
            _masterRpc.Dispose();
        }
    }
}