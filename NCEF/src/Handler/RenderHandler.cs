using System;
using CefSharp;
using CefSharp.Enums;
using CefSharp.OffScreen;
using CefSharp.Structs;
using NCEF.Utils;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Device1 = SharpDX.Direct3D11.Device1;

namespace NCEF.Handler
{
    public class RenderHandler : IRenderHandler, IDisposable
    {
        private Device1 _dxDevice;
        private SpoutDX _spoutSender;
        private Texture2D _compositeTexture; 
        private Texture2D _popupCopy;
        private bool _isPopupShowing;
        private Rect _popupRect;

        public CursorType CurrentCursor { get; private set; } = CursorType.Pointer;

        public RenderHandler(string spoutId)
        {
            InitializeDirectX(spoutId);
        }

        private void InitializeDirectX(string spoutId)
        {
            var device0 = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            _dxDevice = device0.QueryInterface<Device1>();
            device0.Dispose();
            _spoutSender = new SpoutDX();
            _spoutSender.SetSenderName(spoutId);
            _spoutSender.OpenDirectX(_dxDevice.NativePointer);
        }

        private Rect _viewRect = new Rect(0, 0, Config.Bounds.Width, Config.Bounds.Height);

        public Rect GetViewRect() => _viewRect;

        public void SetViewRect(Rect viewRect) => _viewRect = viewRect;

        public ScreenInfo? GetScreenInfo() => null;

        public bool GetScreenPoint(int vx, int vy, out int sx, out int sy)
        {
            sx = vx;
            sy = vy;
            return false;
        }

        public void OnCursorChange(IntPtr cursor, CursorType type, CursorInfo customCursorInfo)
        {
            CurrentCursor = type;
        }

        public void OnPaint(PaintElementType type, Rect dirtyRect, IntPtr buffer, int width, int height)
        {
        }

        public void OnAcceleratedPaint(PaintElementType type, Rect _dirtyRect, AcceleratedPaintInfo info)
        {
            if (info.SharedTextureHandle == IntPtr.Zero || _dxDevice == null)
                return;

            try
            {
                if (type == PaintElementType.Popup)
                {
                    HandlePopupPaint(info);
                    return;
                }

                HandleViewPaint(info);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnAcceleratedPaint error: {ex.Message}");
            }
        }

        private void HandlePopupPaint(AcceleratedPaintInfo info)
        {
            using (var sharedPopup = _dxDevice.OpenSharedResource1<Texture2D>(info.SharedTextureHandle))
            {
                if (sharedPopup == null || sharedPopup.NativePointer == IntPtr.Zero)
                    return;
                _popupCopy?.Dispose();
                var desc = sharedPopup.Description;
                desc.BindFlags = BindFlags.None;
                desc.OptionFlags = ResourceOptionFlags.None;
                desc.Usage = ResourceUsage.Default;
                desc.CpuAccessFlags = CpuAccessFlags.None;

                _popupCopy = new Texture2D(_dxDevice, desc);
                _dxDevice.ImmediateContext.CopyResource(sharedPopup, _popupCopy);
            }
        }

        private void HandleViewPaint(AcceleratedPaintInfo info)
        {
            using (var cefTex = _dxDevice.OpenSharedResource1<Texture2D>(info.SharedTextureHandle))
            {
                if (cefTex == null || cefTex.NativePointer == IntPtr.Zero)
                    return;
                var desc = cefTex.Description;
                EnsureCompositeTexture(desc.Width, desc.Height, desc.Format);

                var ctx = _dxDevice.ImmediateContext;
                ctx.CopyResource(cefTex, _compositeTexture);
                if (_isPopupShowing
                    && _popupCopy != null
                    && !_popupCopy.IsDisposed
                    && _popupCopy.NativePointer != IntPtr.Zero
                    && _popupRect.Width > 0
                    && _popupRect.Height > 0)
                {
                    var srcBox = new ResourceRegion(
                        0, 0, 0,
                        Math.Min(_popupRect.Width, _popupCopy.Description.Width),
                        Math.Min(_popupRect.Height, _popupCopy.Description.Height),
                        1
                    );

                    ctx.CopySubresourceRegion(
                        _popupCopy,
                        0,
                        srcBox,
                        _compositeTexture,
                        0,
                        _popupRect.X,
                        _popupRect.Y,
                        0
                    );
                }

                _spoutSender.SendTexture(_compositeTexture.NativePointer);
            }
        }

        private void EnsureCompositeTexture(int width, int height, Format format)
        {
            if (_compositeTexture != null
                && !_compositeTexture.IsDisposed
                && _compositeTexture.Description.Width == width
                && _compositeTexture.Description.Height == height)
            {
                return;
            }

            _compositeTexture?.Dispose();
            _compositeTexture = new Texture2D(_dxDevice, new Texture2DDescription
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = format,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.None,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.Shared
            });
        }

        public void OnPopupShow(bool show)
        {
            _isPopupShowing = show;
            if (!show)
            {
                _popupCopy?.Dispose();
                _popupCopy = null;
            }
        }

        public void OnPopupSize(Rect rect)
        {
            _popupRect = rect;
        }

        public void OnImeCompositionRangeChanged(Range r, Rect[] b)
        {
        }

        public void OnVirtualKeyboardRequested(IBrowser b, TextInputMode m)
        {
        }

        public bool StartDragging(IDragData d, DragOperationsMask m, int x, int y) => false;

        public void UpdateDragCursor(DragOperationsMask m)
        {
        }

        public void Dispose()
        {
            _popupCopy?.Dispose();
            _compositeTexture?.Dispose();
            _spoutSender?.Dispose();
            _dxDevice?.Dispose();
            _popupCopy = null;
            _compositeTexture = null;
            _spoutSender = null;
            _dxDevice = null;
        }
    }
}