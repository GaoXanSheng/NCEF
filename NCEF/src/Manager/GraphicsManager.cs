using System;
using CefSharp.OffScreen;
using CefSharp.Structs;
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace NCEF
{
    public class GraphicsManager : IDisposable
    {
        public Device DxDevice { get; private set; }
        public Texture2D MainTexture { get; private set; }
        private Texture2D _popupTexture = null;
        private Rect _popupRect;
        private SpoutDX _spoutSender;
        private readonly object _renderLock = new object();

        public GraphicsManager(int width, int height, string spoutId)
        {
            DxDevice = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            MainTexture = CreateTexture(width, height);
            _spoutSender = new SpoutDX();
            _spoutSender.OpenDirectX(DxDevice.NativePointer);
            _spoutSender.SetSenderName("WebViewSpoutCapture_" + spoutId);
        }

        private Texture2D CreateTexture(int width, int height)
        {
            return new Texture2D(DxDevice, new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Dynamic,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.Write
            });
        }

        public void HandleBrowserPaint(OnPaintEventArgs e)
        {
            lock (_renderLock)
            {
                if (MainTexture == null || MainTexture.IsDisposed || _spoutSender == null || e.BufferHandle == IntPtr.Zero)
                    return;

                var context = DxDevice.ImmediateContext;

                if (e.IsPopup)
                {
                    _popupRect = e.DirtyRect;
                    int popupWidth = _popupRect.Width;
                    int popupHeight = _popupRect.Height;

                    if (popupWidth == 0 || popupHeight == 0) return;

                    if (_popupTexture == null || _popupTexture.IsDisposed || _popupTexture.Description.Width != popupWidth || _popupTexture.Description.Height != popupHeight)
                    {
                        _popupTexture?.Dispose();
                        _popupTexture = CreateTexture(popupWidth, popupHeight);
                    }

                    var dataBox = context.MapSubresource(_popupTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
                    try
                    {
                        int sourceRowPitch = e.Width * 4;
                        int destRowPitch = dataBox.RowPitch;
                        int bytesToCopyPerRow = Math.Min(sourceRowPitch, destRowPitch);

                        if (sourceRowPitch == destRowPitch)
                        {
                            Utilities.CopyMemory(dataBox.DataPointer, e.BufferHandle, sourceRowPitch * popupHeight);
                        }
                        else
                        {
                            for (int y = 0; y < popupHeight; y++)
                            {
                                IntPtr src = e.BufferHandle + y * sourceRowPitch;
                                IntPtr dest = dataBox.DataPointer + y * destRowPitch;
                                Utilities.CopyMemory(dest, src, bytesToCopyPerRow);
                            }
                        }
                    }
                    finally
                    {
                        context.UnmapSubresource(_popupTexture, 0);
                    }
                }
                else
                {
                    int width = e.Width;
                    int height = e.Height;

                    if (width != MainTexture.Description.Width || height != MainTexture.Description.Height)
                    {
                        return;
                    }

                    var mainDataBox = context.MapSubresource(MainTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
                    try
                    {
                        int sourceRowPitch = width * 4;
                        if (mainDataBox.RowPitch == sourceRowPitch)
                        {
                            Utilities.CopyMemory(mainDataBox.DataPointer, e.BufferHandle, height * sourceRowPitch);
                        }
                        else
                        {
                            for (int y = 0; y < height; y++)
                            {
                                IntPtr src = e.BufferHandle + y * sourceRowPitch;
                                IntPtr dest = mainDataBox.DataPointer + y * mainDataBox.RowPitch;
                                Utilities.CopyMemory(dest, src, sourceRowPitch);
                            }
                        }
                    }
                    finally
                    {
                        context.UnmapSubresource(MainTexture, 0);
                    }
                }

                if (_popupTexture != null && !_popupTexture.IsDisposed)
                {
                    var srcRegion = new ResourceRegion(0, 0, 0, _popupTexture.Description.Width, _popupTexture.Description.Height, 1);
                    context.CopySubresourceRegion(_popupTexture, 0, srcRegion, MainTexture, 0, _popupRect.X, _popupRect.Y, 0);
                }

                _spoutSender.SendTexture(MainTexture.NativePointer);
            }
        }

        public void Dispose()
        {
            lock (_renderLock)
            {
                MainTexture?.Dispose();
                _popupTexture?.Dispose();
                _spoutSender?.Dispose();
                DxDevice?.Dispose();
                MainTexture = null;
                _popupTexture = null;
                _spoutSender = null;
                DxDevice = null;
            }
        }
    }
}