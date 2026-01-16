using System;
using System.Collections.Concurrent;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NCEF.RPC
{
    public class RpcServer<T> : IDisposable
    {
        private const string PREFIX_RPC_MAP = "NCEF_RPC_";
        private const string PREFIX_REQ_EVENT = "NCEF_REQ_";
        private const string PREFIX_RES_EVENT = "NCEF_RES_";
        private const string PREFIX_EVT_MAP = "NCEF_EVT_";
        private const string PREFIX_EVT_READY = "NCEF_EVT_READY_";
        private const string PREFIX_EVT_ACK = "NCEF_EVT_ACK_";

        private const int RPC_MAP_SIZE = 1024 * 1024 * 8;
        private const int EVT_MAP_SIZE = RPC_MAP_SIZE / 2;
        private const int REQ_OFFSET = 0;
        private const int RES_OFFSET = RPC_MAP_SIZE / 2;
        private const int DATA_HEADER_SIZE = 4;

        private const int RPC_WAIT_TIMEOUT_MS = 10000;
        private const int EVENT_ACK_TIMEOUT_MS = 10000;

        private const string LOG_TAG = "[RPC Server] ";

        private readonly T _impl;
        private readonly MemoryMappedFile _rpcMmf;
        private readonly MemoryMappedViewAccessor _rpcAccessor;
        private readonly EventWaitHandle _reqEvt;
        private readonly EventWaitHandle _resEvt;

        private readonly MemoryMappedFile _evtMmf;
        private readonly MemoryMappedViewAccessor _evtAccessor;
        private readonly EventWaitHandle _evtDataReady;
        private readonly EventWaitHandle _evtDataAck;

        private readonly BlockingCollection<RpcPacket> _eventQueue = new BlockingCollection<RpcPacket>(new ConcurrentQueue<RpcPacket>());
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public RpcServer(string rpcId, T implementation)
        {
            _impl = implementation;

            _rpcMmf = MemoryMappedFile.CreateOrOpen(PREFIX_RPC_MAP + rpcId, RPC_MAP_SIZE);
            _rpcAccessor = _rpcMmf.CreateViewAccessor();
            _reqEvt = new EventWaitHandle(false, EventResetMode.AutoReset, PREFIX_REQ_EVENT + rpcId);
            _resEvt = new EventWaitHandle(false, EventResetMode.AutoReset, PREFIX_RES_EVENT + rpcId);

            _evtMmf = MemoryMappedFile.CreateOrOpen(PREFIX_EVT_MAP + rpcId, EVT_MAP_SIZE);
            _evtAccessor = _evtMmf.CreateViewAccessor();
            _evtDataReady = new EventWaitHandle(false, EventResetMode.AutoReset, PREFIX_EVT_READY + rpcId);
            _evtDataAck = new EventWaitHandle(false, EventResetMode.AutoReset, PREFIX_EVT_ACK + rpcId);

            Task.Run(ListenForRpcLoop, _cts.Token);
            Task.Run(ProcessEventQueueLoop, _cts.Token);

            Console.WriteLine($"{LOG_TAG}Started for ID: {rpcId}");
        }

        public void SendEvent(string method, params object[] args)
        {
            if (_cts.IsCancellationRequested || _eventQueue.IsAddingCompleted)
                return;

            _eventQueue.Add(new RpcPacket { Method = method, Args = args });
        }


        private void ProcessEventQueueLoop()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    if (!_eventQueue.TryTake(out var packet, 100))
                        continue;

                    string json = JsonConvert.SerializeObject(packet);
                    byte[] data = Encoding.UTF8.GetBytes(json);

                    _evtAccessor.Write(0, data.Length);
                    _evtAccessor.WriteArray(DATA_HEADER_SIZE, data, 0, data.Length);

                    _evtDataReady.Set();

                    if (!_evtDataAck.WaitOne(EVENT_ACK_TIMEOUT_MS))
                    {
                        Console.WriteLine($"{LOG_TAG}Warning: Java client timed out accepting event.");
                    }
                }
            }
            catch (ArgumentNullException e)
            {
                // Thrown when Dispose is called on the collection.
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{LOG_TAG}Event Loop Fatal Error!");
                Console.WriteLine(ex);
            }
        }


        private void ListenForRpcLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_reqEvt.WaitOne(RPC_WAIT_TIMEOUT_MS))
                {
                    try
                    {
                        int len = _rpcAccessor.ReadInt32(REQ_OFFSET);
                        byte[] data = new byte[len];
                        _rpcAccessor.ReadArray(REQ_OFFSET + DATA_HEADER_SIZE, data, 0, len);

                        var req = JsonConvert.DeserializeObject<RpcPacket>(Encoding.UTF8.GetString(data));
                        if (req == null) continue;

                        object result = null;
                        MethodInfo method = typeof(T).GetMethod(req.Method);

                        if (method != null)
                        {
                            ParameterInfo[] parameters = method.GetParameters();
                            if (req.Args != null && req.Args.Length == parameters.Length)
                            {
                                for (int i = 0; i < req.Args.Length; i++)
                                {
                                    if (req.Args[i] != null)
                                    {
                                        Type targetType = parameters[i].ParameterType;
                                        if (req.Args[i] is JObject jo)
                                            req.Args[i] = jo.ToObject(targetType);
                                        else if (req.Args[i] is JArray ja)
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

                        _rpcAccessor.Write(RES_OFFSET, resBytes.Length);
                        _rpcAccessor.WriteArray(RES_OFFSET + DATA_HEADER_SIZE, resBytes, 0, resBytes.Length);

                        _resEvt.Set();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"{LOG_TAG}Invoke Error: {ex.Message}");
                    }
                    finally
                    {
                        _resEvt.Set();
                    }
                }
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _eventQueue.CompleteAdding();
            _evtDataReady?.Set();

            _rpcAccessor?.Dispose();
            _rpcMmf?.Dispose();
            _reqEvt?.Dispose();
            _resEvt?.Dispose();

            _evtAccessor?.Dispose();
            _evtMmf?.Dispose();
            _evtDataReady?.Dispose();
            _evtDataAck?.Dispose();

            _eventQueue?.Dispose();
            _cts?.Dispose();
        }


        private class RpcPacket
        {
            public string Method { get; set; }
            public object[] Args { get; set; }
        }
    }
}