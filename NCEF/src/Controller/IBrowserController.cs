namespace NCEF.Controller
{
    public interface IBrowserController
    {
        string GetSpoutId();
        void LoadUrl(string url);
        void ExecuteJs(string script);
        string GetUrl();
        bool Resize(int width, int height, int deviceScaleFactor, bool mobile);
        void SetAudioMuted(bool b);
        void SetVolume(float vol);
        // --- 新增功能 ---

        /// <summary>
        /// 发送鼠标移动事件
        /// </summary>
        void SendMouseMove(int x, int y, bool mouseLeave = false, bool leftButtonPressed = false);

        /// <summary>
        /// 发送鼠标点击事件
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="button">0: 左键, 2: 中键, 1: 右键</param>
        /// <param name="mouseUp">true为抬起，false为按下</param>
        void SendMouseClick(int x, int y, int button, bool mouseUp);

        /// <summary>
        /// 发送鼠标滚轮事件
        /// </summary>
        void SendMouseWheel(int x, int y, int deltaX, int deltaY);

        /// <summary>
        /// 发送键盘按键事件
        /// </summary>
        void SendKeyEvent(int windowsKeyCode, bool isUp);

        /// <summary>
        /// 发送文本输入
        /// </summary>
        void SendText(string text);

        /// <summary>
        /// 获取当前光标样式 (需配合 RenderSession 实现)
        /// </summary>
        /// <returns>IntPtr 句柄 或 CefSharp.Enums.CursorType 枚举的整数值</returns>
        int GetCursorType();

        void ResolveJsPromise(string reqId, object result);
    }
}