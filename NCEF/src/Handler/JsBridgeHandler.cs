using System.Collections.Generic;
using NCEF.Controller;
using NCEF.RPC;

namespace NCEF.Handler
{
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    public class JsBridgeHandler
    {
        private readonly RpcServer<IBrowserController> _rpcServer;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingRequests 
            = new ConcurrentDictionary<string, TaskCompletionSource<object>>();

        public JsBridgeHandler(RpcServer<IBrowserController> rpcServer)
        {
            _rpcServer = rpcServer;
        }
        /**
         * JAVA Void Emit(String eventName)
         */
        public void Emit(string eventName, params object[] args)
        {
            _rpcServer.SendEvent(eventName, args);
        }
        /**
         * JAVA Object Call(String methodName, Object... args)
         */
        public Task<object> Call(string methodName, params object[] args)
        {
            var tcs = new TaskCompletionSource<object>();
            string reqId = System.Guid.NewGuid().ToString();
            _pendingRequests.TryAdd(reqId, tcs);
            var payload = new List<object> { reqId, methodName };
            payload.AddRange(args);
        
            _rpcServer.SendEvent("__RPC_CALL__", payload.ToArray());
            return tcs.Task;
        }
        public void CompleteRequest(string reqId, object result)
        {
            if (_pendingRequests.TryRemove(reqId, out var tcs))
            {
                tcs.TrySetResult(result);
            }
        }
    }
}