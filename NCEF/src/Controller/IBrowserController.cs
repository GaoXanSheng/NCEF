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
        void ShowDevTools();
        void SendMouseMove(int x, int y, bool mouseLeave = false, bool leftButtonPressed = false);
        void SendMouseClick(int x, int y, int button, bool mouseUp);
        void SendMouseWheel(int x, int y, int deltaX, int deltaY);
        void SendKeyEvent(int windowsKeyCode, bool isUp,int modifiers);
        void SendText(string text);
        int GetCursorType();
        void ResolveJsPromise(string reqId, object result);
    }
}