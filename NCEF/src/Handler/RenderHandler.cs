using System;
using CefSharp;
using CefSharp.Enums;
using CefSharp.OffScreen;
using CefSharp.Structs;
using NCEF.Utils;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;
using Device1 = SharpDX.Direct3D11.Device1;

namespace NCEF.Handler
{
    public class RenderHandler : IRenderHandler, IDisposable
    {
        private Device1 _dxDevice;
        private SpoutDX _spoutSender;
        public CursorType CurrentCursor { get; private set; } = CursorType.Pointer;

        public RenderHandler(string spoutId)
        {
            InitializeDirectX(spoutId);
        }

        private void InitializeDirectX(string spoutId)
        {
            var device0 = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            _dxDevice = device0.QueryInterface<Device1>();
            _spoutSender = new SpoutDX();
            _spoutSender.SetSenderName(spoutId);
            _spoutSender.OpenDirectX(_dxDevice.NativePointer);
        }

        public void OnCursorChange(IntPtr cursor, CursorType type, CursorInfo customCursorInfo)
        {
            CurrentCursor = type;
        }

        public void OnPaint(PaintElementType type, Rect dirtyRect, IntPtr buffer, int width, int height)
        {
        }

        public void Dispose()
        {
            _spoutSender?.Dispose();
            _dxDevice?.Dispose();
            _spoutSender = null;
            _dxDevice = null;
        }


        public ScreenInfo? GetScreenInfo() => null;

        public Rect GetViewRect()
        {
            return new Rect(0, 0, Config.Bounds.Width, Config.Bounds.Height);
        }

        public bool GetScreenPoint(int vx, int vy, out int sx, out int sy)
        {
            sx = vx;
            sy = vy;
            return false;
        }

        public void OnAcceleratedPaint(PaintElementType type, Rect _dirtyRect, AcceleratedPaintInfo info)
        {
            if (info.SharedTextureHandle == IntPtr.Zero)
                return;

            if (type == PaintElementType.Popup)
            {
             return;   
            }
            using (Texture2D cefTex = _dxDevice.OpenSharedResource1<Texture2D>(info.SharedTextureHandle))
            {
                _spoutSender.SendTexture(cefTex.NativePointer);
            }
        }


        public void OnImeCompositionRangeChanged(Range r, Rect[] b)
        {
        }

        public void OnPopupShow(bool show)
        {
        }

        private Rect popupRect;

        public void OnPopupSize(Rect rect)
        {
            popupRect = rect;
        }

        public void OnVirtualKeyboardRequested(IBrowser b, TextInputMode m)
        {
        }

        public bool StartDragging(IDragData d, DragOperationsMask m, int x, int y) => false;

        public void UpdateDragCursor(DragOperationsMask m)
        {
        }
    }
}