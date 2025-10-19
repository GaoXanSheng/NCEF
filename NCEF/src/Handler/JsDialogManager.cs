using CefSharp;
using NCEF;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Windows.Forms;

namespace NCEF.Handler
{
    public class JsDialogManager : IJsDialogHandler
    {
        private readonly ConcurrentQueue<(CefJsDialogType type, string msg, string promptDefault, IJsDialogCallback callback)> queue
            = new ConcurrentQueue<(CefJsDialogType, string, string, IJsDialogCallback)>();

        public JsDialogManager()
        {
            var uiThread = new Thread(ProcessQueue)
            {
                IsBackground = true
            };
            uiThread.SetApartmentState(ApartmentState.STA);
            uiThread.Start();
        }

        public bool OnJSDialog(IWebBrowser browserControl, IBrowser browser, string originUrl, CefJsDialogType dialogType, string messageText, string defaultPromptText, IJsDialogCallback callback, ref bool suppressMessage)
        {
            queue.Enqueue((dialogType, messageText, defaultPromptText, callback));
            return true;
        }

        public bool OnBeforeUnloadDialog(IWebBrowser browserControl, IBrowser browser, string messageText, bool isReload, IJsDialogCallback callback)
        {
            callback.Continue(true);
            return true;
        }

        public void OnResetDialogState(IWebBrowser browserControl, IBrowser browser)
        {
        }

        public void OnDialogClosed(IWebBrowser browserControl, IBrowser browser)
        {
        }

        private void ProcessQueue()
        {
            while (true)
            {
                if (queue.TryDequeue(out var item))
                {
                    ShowDialog(item.type, item.msg, item.promptDefault, item.callback);
                }
                else
                {
                    Thread.Sleep(50);
                }
            }
        }

        private void ShowDialog(CefJsDialogType type, string message, string defaultPrompt, IJsDialogCallback callback)
        {
            switch (type)
            {
                case CefJsDialogType.Alert:
                    MessageBox.Show(message, "Alert", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    callback.Continue(true);
                    break;

                case CefJsDialogType.Confirm:
                    var result = MessageBox.Show(message, "Confirm", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    callback.Continue(result == DialogResult.Yes);
                    break;

                case CefJsDialogType.Prompt:
                    using (var prompt = new PromptForm(message, defaultPrompt))
                    {
                        if (prompt.ShowDialog() == DialogResult.OK)
                            callback.Continue(true, prompt.InputText);
                        else
                            callback.Continue(false);
                    }
                    break;
            }
        }
    }
}
