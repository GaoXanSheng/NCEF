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
 #region GraphicsManager
    public class GraphicsManager : IDisposable
    {
        public Device DxDevice { get; private set; }
        public Texture2D MainTexture { get; private set; }
        private Texture2D _popupTexture = null;
        private Rect _popupRect;
        private SpoutDX _spoutSender;

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
            if (MainTexture == null || _spoutSender == null || e.BufferHandle == IntPtr.Zero)
                return;

            var context = DxDevice.ImmediateContext;
            int width = e.Width;
            int height = e.Height;
            int rowPitch = width * 4;

            if (e.IsPopup)
            {
                _popupRect = e.DirtyRect;
                int popupWidth = _popupRect.Width;
                int popupHeight = _popupRect.Height;

                if (_popupTexture == null || _popupTexture.Description.Width != popupWidth ||
                    _popupTexture.Description.Height != popupHeight)
                {
                    _popupTexture?.Dispose();
                    _popupTexture = CreateTexture(popupWidth, popupHeight);
                }

                var dataBox = context.MapSubresource(_popupTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
                try
                {
                    IntPtr srcBase = e.BufferHandle + (_popupRect.Y * rowPitch) + (_popupRect.X * 4);
                    IntPtr destBase = dataBox.DataPointer;
                    int copyRowPitch = popupWidth * 4;
                    for (int y = 0; y < popupHeight; y++)
                    {
                        Utilities.CopyMemory(destBase + y * dataBox.RowPitch, srcBase + y * rowPitch, copyRowPitch);
                    }
                }
                finally
                {
                    context.UnmapSubresource(_popupTexture, 0);
                }
                return;
            }

            var mainDataBox = context.MapSubresource(MainTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
            try
            {
                for (int y = 0; y < height; y++)
                {
                    IntPtr src = e.BufferHandle + y * rowPitch;
                    IntPtr dest = mainDataBox.DataPointer + y * mainDataBox.RowPitch;
                    Utilities.CopyMemory(dest, src, rowPitch);
                }
            }
            finally
            {
                context.UnmapSubresource(MainTexture, 0);
            }

            if (_popupTexture != null)
            {
                var srcRegion = new ResourceRegion
                {
                    Left = 0,
                    Top = 0,
                    Front = 0,
                    Right = _popupTexture.Description.Width,
                    Bottom = _popupTexture.Description.Height,
                    Back = 1
                };

                context.CopySubresourceRegion(
                    _popupTexture, 0, srcRegion,
                    MainTexture, 0,
                    _popupRect.X, _popupRect.Y, 0
                );
            }

            _spoutSender.SendTexture(MainTexture.NativePointer);
        }

        public void Dispose()
        {
            MainTexture?.Dispose();
            _popupTexture?.Dispose();
            _spoutSender?.Dispose();
            DxDevice?.Dispose();
        }
    }
    #endregion

}