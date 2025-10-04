using System;
using CefSharp;
using CefSharp.Handler;

namespace NCEF.Handler
{
    public class VolumeAudioHandler: AudioHandler
    {
        private static double _volume;
        public VolumeAudioHandler(double initialVolume = 1.0)
        {
            _volume = initialVolume;
        }
        /// <summary>
        /// 设置音量，范围 0.0 ~ 1.0
        /// </summary>
        public static void SetVolume(double value)
        {
            if (value < 0) value = 0;
            if (value > 1) value = 1;
            _volume = value;
            Console.WriteLine($"TheVolumeIsSetTo: {_volume * 100}%");
        }
        protected override void OnAudioStreamPacket(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr data, int noOfFrames, long pts)
        {
            float[] buffer = new float[noOfFrames];
            System.Runtime.InteropServices.Marshal.Copy(data, buffer, 0, noOfFrames);

            // 调整音量
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= (float)_volume;
            }
            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, data, noOfFrames);
        }

    }
}