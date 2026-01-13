using System;
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
        private readonly MemoryMappedFile _mmf;
        private readonly MemoryMappedViewAccessor _accessor;
        private readonly EventWaitHandle _reqEvt;
        private readonly EventWaitHandle _resEvt;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private const int BUF_SIZE = 1024 * 1024; // 1MB
        private const int RES_OFFSET = 512 * 1024; // 返回值存放在 512KB 处

        public RpcServer(string rpcId, T implementation)
        {
            _impl = implementation;
            _mmf = MemoryMappedFile.CreateOrOpen($"NCEF_RPC_{rpcId}", BUF_SIZE);
            _accessor = _mmf.CreateViewAccessor();
            _reqEvt = new EventWaitHandle(false, EventResetMode.AutoReset, $"NCEF_REQ_{rpcId}");
            _resEvt = new EventWaitHandle(false, EventResetMode.AutoReset, $"NCEF_RES_{rpcId}");

            Task.Run(ListenLoop, _cts.Token);
            Console.WriteLine($"[RPC] Server started for ID: {rpcId}");
        }

        private void ListenLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                if (_reqEvt.WaitOne(1000))
                {
                    try
                    {
                        int len = _accessor.ReadInt32(0);
                        byte[] data = new byte[len];
                        _accessor.ReadArray(4, data, 0, len);
                        var req = JsonConvert.DeserializeObject<RpcPacket>(Encoding.UTF8.GetString(data));
                        var method = typeof(T).GetMethod(req.Method);
                        object result = null;
                        if (method != null)
                        {
                            var @params = method.GetParameters();
                            if (req.Args != null && req.Args.Length == @params.Length)
                            {
                                for (int i = 0; i < req.Args.Length; i++)
                                    req.Args[i] = Convert.ChangeType(req.Args[i], @params[i].ParameterType);
                            }
                            result = method.Invoke(_impl, req.Args);
                        }
                        string resJson = JsonConvert.SerializeObject(result);
                        byte[] resBytes = Encoding.UTF8.GetBytes(resJson);
                        _accessor.Write(RES_OFFSET, resBytes.Length);
                        _accessor.WriteArray(RES_OFFSET + 4, resBytes, 0, resBytes.Length);
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
            _accessor.Dispose();
            _mmf.Dispose();
            _reqEvt.Dispose();
            _resEvt.Dispose();
        }

        private class RpcPacket { public string Method; public object[] Args; }
    }
}