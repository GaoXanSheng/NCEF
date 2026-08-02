using System;
using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using Device = SharpDX.Direct3D11.Device;
using Device1 = SharpDX.Direct3D11.Device1;

namespace NCEF_Test.TestCases
{
    public class SpoutReceiver : IDisposable
    {
        public SpoutDXWrapper.SpoutReceiver _receiver { get; private set; }
        private Device1? _dxDevice;
        private Texture2D? _sharedTexture;
        private bool _disposed;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public double Fps { get; private set; }
        public bool HasNewFrame { get; private set; }
        public IntPtr SharedTexturePtr { get; private set; }
        public bool IsConnected => _receiver != null;

        public SpoutReceiver()
        {
            _receiver = new SpoutDXWrapper.SpoutReceiver();
            Width = 0;
            Height = 0;
            HasNewFrame = false;
        }

        public void Connect(string senderName)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SpoutReceiver));

            _dxDevice?.Dispose();
            _receiver?.Dispose();

            var device0 = new Device(SharpDX.Direct3D.DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            _dxDevice = device0.QueryInterface<Device1>();
            device0.Dispose();

            _receiver = new SpoutDXWrapper.SpoutReceiver();
            _receiver.OpenDirectX(_dxDevice.NativePointer);
            _receiver.SetReceiverName(senderName);
            Width = 0;
            Height = 0;
            HasNewFrame = false;
        }

        public void Disconnect()
        {
            _sharedTexture?.Dispose();
            _sharedTexture = null;
            _dxDevice?.Dispose();
            _dxDevice = null;
            _receiver?.Dispose();
            _receiver = null!;
            Width = 0;
            Height = 0;
            HasNewFrame = false;
            SharedTexturePtr = IntPtr.Zero;
        }

        public void Update()
        {
            if (_receiver == null || _dxDevice == null) return;

            try
            {
                IntPtr texPtr = _receiver.ReceiveTexturePtr();
                if (texPtr != IntPtr.Zero)
                {
                    Width = (int)_receiver.GetWidth();
                    Height = (int)_receiver.GetHeight();
                    Fps = _receiver.GetFps();
                    SharedTexturePtr = texPtr;
                    HasNewFrame = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpoutReceiver] Update error: {ex.Message}");
            }
        }

        public byte[]? CaptureFrame()
        {
            if (SharedTexturePtr == IntPtr.Zero || Width == 0 || Height == 0 || _dxDevice == null)
                return null;

            try
            {
                var sharedTexture = new Texture2D(SharedTexturePtr);
                var desc = new Texture2DDescription
                {
                    Width = Width,
                    Height = Height,
                    MipLevels = 1,
                    ArraySize = 1,
                    Format = Format.B8G8R8A8_UNorm,
                    SampleDescription = new SampleDescription(1, 0),
                    Usage = ResourceUsage.Staging,
                    BindFlags = BindFlags.None,
                    CpuAccessFlags = CpuAccessFlags.Read,
                    OptionFlags = ResourceOptionFlags.None
                };
                using var stagingTexture = new Texture2D(_dxDevice, desc);
                _dxDevice.ImmediateContext.CopyResource(sharedTexture, stagingTexture);
                var dataBox = _dxDevice.ImmediateContext.MapSubresource(stagingTexture, 0, MapMode.Read, MapFlags.None);

                byte[] data = new byte[Width * Height * 4];
                for (int y = 0; y < Height; y++)
                {
                    int srcOffset = y * dataBox.RowPitch;
                    int dstOffset = y * Width * 4;
                    System.Runtime.InteropServices.Marshal.Copy(dataBox.DataPointer + srcOffset, data, dstOffset,
                        Width * 4);
                }

                _dxDevice.ImmediateContext.UnmapSubresource(stagingTexture, 0);
                sharedTexture.Dispose();
                return data;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpoutReceiver] Capture error: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            Disconnect();
            _disposed = true;
        }
    }
}