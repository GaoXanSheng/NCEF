namespace NCEF.Controller
{
    public interface IMasterController
    {
        string CreateBrowser(string url, int w, int h, int fps);
        void StopBrowser(string spoutId);
    }
}