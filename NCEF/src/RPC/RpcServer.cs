using System;
using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NCEF.RPC
{
    public class RpcServer<T> : IDisposable
    {
        private readonly T _impl;
        private readonly MemoryMappedFile _rpcMmf;
        private readonly MemoryMappedViewAccessor _rpcAccessor;
        private readonly EventWaitHandle _reqEvt;
        private readonly EventWaitHandle _resEvt;
        private readonly MemoryMappedFile _evtMmf;   
        private readonly MemoryMappedViewAccessor _evtAccessor;
        private readonly EventWaitHandle _evtDataReady;  
        private readonly EventWaitHandle _evtDataAck;   
        private readonly BlockingCollection<RpcPacket> _eventQueue = new BlockingCollection<RpcPacket>(); // 内部缓冲队列
        
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private const int RPC_BUF_SIZE = 1024 * 1024;
        private const int EVT_BUF_SIZE = 1024 * 512 ;  

        public RpcServer(string rpcId, T implementation)
        {
            _impl = implementation;
            _rpcMmf = MemoryMappedFile.CreateOrOpen($"NCEF_RPC_{rpcId}", RPC_BUF_SIZE);
            _rpcAccessor = _rpcMmf.CreateViewAccessor();
            _reqEvt = new EventWaitHandle(false, EventResetMode.AutoReset, $"NCEF_REQ_{rpcId}");
            _resEvt = new EventWaitHandle(false, EventResetMode.AutoReset, $"NCEF_RES_{rpcId}");
            _evtMmf = MemoryMappedFile.CreateOrOpen($"NCEF_EVT_{rpcId}", EVT_BUF_SIZE);
            _evtAccessor = _evtMmf.CreateViewAccessor();
            _evtDataReady = new EventWaitHandle(false, EventResetMode.AutoReset, $"NCEF_EVT_READY_{rpcId}");
            _evtDataAck = new EventWaitHandle(false, EventResetMode.AutoReset, $"NCEF_EVT_ACK_{rpcId}");
            Task.Run(ListenForRpcLoop, _cts.Token);
            Task.Run(ProcessEventQueueLoop, _cts.Token);

            Console.WriteLine($"[RPC] Server started for ID: {rpcId}");
        }
        public void SendEvent(string method, params object[] args)
        {
            if (_cts.IsCancellationRequested) return;
            _eventQueue.Add(new RpcPacket { Method = method, Args = args });
        }
        private void ProcessEventQueueLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    RpcPacket packet = _eventQueue.Take(_cts.Token);
                    string json = JsonConvert.SerializeObject(packet);
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    _evtAccessor.Write(0, data.Length);
                    _evtAccessor.WriteArray(4, data, 0, data.Length);
                    _evtDataReady.Set();
                    if (!_evtDataAck.WaitOne(2000)) 
                    {
                        Console.WriteLine("[RPC] Warning: Java timed out accepting event.");
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RPC] Event Loop Error: {ex.Message}");
                }
            }
        }
        private void ListenForRpcLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_reqEvt.WaitOne(1000))
                {
                    try
                    {
                        int len = _rpcAccessor.ReadInt32(0);
                        byte[] data = new byte[len];
                        _rpcAccessor.ReadArray(4, data, 0, len);
                        var req = JsonConvert.DeserializeObject<RpcPacket>(Encoding.UTF8.GetString(data));
                        
                        object result = null;
                        var method = typeof(T).GetMethod(req.Method);
                        if (method != null)
                        {
                            var @params = method.GetParameters();
                            if (req.Args != null && req.Args.Length == @params.Length)
                            {
                                for (int i = 0; i < req.Args.Length; i++)
                                {
                                    if (req.Args[i] != null)
                                    {
                                        Type targetType = @params[i].ParameterType;
                                        if (req.Args[i] is Newtonsoft.Json.Linq.JObject jo)
                                            req.Args[i] = jo.ToObject(targetType);
                                        else if (req.Args[i] is Newtonsoft.Json.Linq.JArray ja)
                                            req.Args[i] = ja.ToObject(targetType);
                                        else
                                            req.Args[i] = Convert.ChangeType(req.Args[i], targetType);
                                    }
                                }
                            }
                            result = method.Invoke(_impl, req.Args);
                        }
                        string resJson = JsonConvert.SerializeObject(result);
                        byte[] resBytes = Encoding.UTF8.GetBytes(resJson);
                        _rpcAccessor.Write(512 * 1024, resBytes.Length); 
                        _rpcAccessor.WriteArray((512 * 1024) + 4, resBytes, 0, resBytes.Length);
                        _resEvt.Set();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"RPC Invoke Error: {ex.Message}");
                        _resEvt.Set();
                    }
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _rpcAccessor?.Dispose();
            _rpcMmf?.Dispose();
            _reqEvt?.Dispose();
            _resEvt?.Dispose();

            _evtAccessor?.Dispose();
            _evtMmf?.Dispose();
            _evtDataReady?.Dispose();
            _evtDataAck?.Dispose();
        }

        private class RpcPacket { public string Method; public object[] Args; }
    }
}