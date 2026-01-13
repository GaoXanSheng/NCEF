using System;
using CefSharp;
using CefSharp.Enums;
using CefSharp.OffScreen;
using CefSharp.Structs; 
using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;

namespace NCEF.Manager
{
    public class BrowserRender : IRenderHandler, IDisposable
    {
        // --- 渲染与 Spout 资源 ---
        private Device _dxDevice;
        private Texture2D _mainTexture;
        private Texture2D _popupTexture; // 处理下拉菜单等弹出层
        private SpoutDX _spoutSender;
        private readonly object _renderLock = new object();
        private Rect _popupRect;

        // --- CefSharp 状态 ---
        private int _width;
        private int _height;
        public CursorType CurrentCursor { get; private set; } = CursorType.Pointer;

        public BrowserRender(string spoutId, int width, int height)
        {
            _width = width;
            _height = height;

            // 初始化 DirectX 和 Spout
            InitializeDirectX(spoutId, width, height);
        }

        private void InitializeDirectX(string spoutId, int width, int height)
        {
            _dxDevice = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport);
            _mainTexture = CreateTexture(width, height);
            
            _spoutSender = new SpoutDX();
            _spoutSender.OpenDirectX(_dxDevice.NativePointer);
            _spoutSender.SetSenderName(spoutId);
        }

        private Texture2D CreateTexture(int width, int height)
        {
            return new Texture2D(_dxDevice, new Texture2DDescription
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

        /// <summary>
        /// 调整大小：不仅要更新 CefSharp 视口，还要重建 DirectX 纹理
        /// </summary>
        public void Resize(int width, int height)
        {
            lock (_renderLock)
            {
                _width = width;
                _height = height;

                // 重建主纹理
                _mainTexture?.Dispose();
                _mainTexture = CreateTexture(width, height);
            }
        }

        // ========================================================================
        //  IRenderHandler 核心实现
        // ========================================================================

        public Rect GetViewRect()
        {
            return new Rect(0, 0, _width, _height);
        }

        public void OnCursorChange(IntPtr cursor, CursorType type, CursorInfo customCursorInfo)
        {
            CurrentCursor = type;
        }

        public void OnPaint(PaintElementType type, Rect dirtyRect, IntPtr buffer, int width, int height)
        {
            lock (_renderLock)
            {
                // 1. 基础校验
                if (_dxDevice == null || _mainTexture == null || _mainTexture.IsDisposed || buffer == IntPtr.Zero)
                    return;

                var context = _dxDevice.ImmediateContext;

                // 2. 判断是主画面还是弹窗(Popup)
                if (type == PaintElementType.Popup)
                {
                    HandlePopupPaint(context, dirtyRect, buffer, width, height);
                }
                else // PaintElementType.View
                {
                    HandleViewPaint(context, buffer, width, height);
                }

                // 3. 如果有弹窗，将其合成到主画面上
                if (_popupTexture != null && !_popupTexture.IsDisposed)
                {
                    // 简单的区域拷贝
                    var srcRegion = new ResourceRegion(0, 0, 0, _popupTexture.Description.Width, _popupTexture.Description.Height, 1);
                    // 注意：这里 _popupRect 可能超出主画面范围，实际生产中最好做一下裁切计算
                    context.CopySubresourceRegion(_popupTexture, 0, srcRegion, _mainTexture, 0, _popupRect.X, _popupRect.Y, 0);
                }

                // 4. 发送给 Spout
                _spoutSender.SendTexture(_mainTexture.NativePointer);
            }
        }

        // --- 内部渲染逻辑 ---

        private void HandleViewPaint(DeviceContext context, IntPtr buffer, int width, int height)
        {
            // 尺寸校验，如果 CefSharp 传来的数据和纹理不一致，等待 Resize 重建
            if (width != _mainTexture.Description.Width || height != _mainTexture.Description.Height) return;

            var dataBox = context.MapSubresource(_mainTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
            try
            {
                int sourcePitch = width * 4;
                if (dataBox.RowPitch == sourcePitch)
                {
                    Utilities.CopyMemory(dataBox.DataPointer, buffer, height * sourcePitch);
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        Utilities.CopyMemory(
                            dataBox.DataPointer + y * dataBox.RowPitch,
                            buffer + y * sourcePitch,
                            sourcePitch
                        );
                    }
                }
            }
            finally
            {
                context.UnmapSubresource(_mainTexture, 0);
            }
        }

        private void HandlePopupPaint(DeviceContext context, Rect dirtyRect, IntPtr buffer, int width, int height)
        {
            _popupRect = dirtyRect;
            // 注意：width/height 在 Popup 模式下是 Popup 自身的宽高

            if (width == 0 || height == 0) return;

            // 检查是否需要重建 Popup 纹理
            if (_popupTexture == null || _popupTexture.IsDisposed ||
                _popupTexture.Description.Width != width ||
                _popupTexture.Description.Height != height)
            {
                _popupTexture?.Dispose();
                _popupTexture = CreateTexture(width, height);
            }

            var dataBox = context.MapSubresource(_popupTexture, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
            try
            {
                int sourcePitch = width * 4;
                int destPitch = dataBox.RowPitch;
                int copyBytes = Math.Min(sourcePitch, destPitch);

                if (sourcePitch == destPitch)
                {
                    Utilities.CopyMemory(dataBox.DataPointer, buffer, height * sourcePitch);
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        Utilities.CopyMemory(
                            dataBox.DataPointer + y * destPitch,
                            buffer + y * sourcePitch,
                            copyBytes
                        );
                    }
                }
            }
            finally
            {
                context.UnmapSubresource(_popupTexture, 0);
            }
        }

        // ========================================================================
        //  资源清理
        // ========================================================================
        public void Dispose()
        {
            lock (_renderLock)
            {
                _mainTexture?.Dispose();
                _popupTexture?.Dispose();
                _spoutSender?.Dispose();
                _dxDevice?.Dispose();
                
                _mainTexture = null;
                _popupTexture = null;
                _spoutSender = null;
                _dxDevice = null;
            }
        }

        // ========================================================================
        //  其他 IRenderHandler 默认实现 (留空)
        // ========================================================================
        public ScreenInfo? GetScreenInfo() => null;
        public bool GetScreenPoint(int viewX, int viewY, out int screenX, out int screenY) { screenX = viewX; screenY = viewY; return false; }
        public void OnAcceleratedPaint(PaintElementType type, Rect dirtyRect, AcceleratedPaintInfo acceleratedPaintInfo) { }
        public void OnImeCompositionRangeChanged(Range selectedRange, Rect[] characterBounds) { }
        public void OnPopupShow(bool show) { }
        public void OnPopupSize(Rect rect) { }
        public void OnVirtualKeyboardRequested(IBrowser browser, TextInputMode inputMode) { }
        public bool StartDragging(IDragData dragData, DragOperationsMask mask, int x, int y) => false;
        public void UpdateDragCursor(DragOperationsMask operationMask) { }
    }
}