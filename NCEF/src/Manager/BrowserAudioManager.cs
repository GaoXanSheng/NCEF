using System;
using CefSharp;
using CefSharp.Enums;
using CefSharp.Structs;
using NAudio.Wave;
using System.Runtime.InteropServices; // 必须引用这个

namespace NCEF.Manager
{
    public class BrowserAudioManager : IAudioHandler, IDisposable
    {
        private BufferedWaveProvider _waveProvider;
        private WaveOutEvent _waveOut;
        private volatile float _volume = 1.0f;

        public BrowserAudioManager()
        {
            _waveOut = new WaveOutEvent();
        }

        public void SetVolume(float vol)
        {
            if (vol < 0) vol = 0;
            if (vol > 1) vol = 1;
            _volume = vol;
        }

        public bool GetAudioParameters(IWebBrowser chromiumWebBrowser, IBrowser browser, ref AudioParameters parameters)
        {
            parameters.ChannelLayout = ChannelLayout.LayoutStereo;
            parameters.SampleRate = 44100;
            parameters.FramesPerBuffer = 1024;
            return true;
        }

        public void OnAudioStreamStarted(IWebBrowser chromiumWebBrowser, IBrowser browser, AudioParameters parameters, int channels)
        {
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(parameters.SampleRate, channels);

            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromSeconds(2),
                DiscardOnBufferOverflow = true
            };

            _waveOut.Init(_waveProvider);
            _waveOut.Play();
        }
        
        public void OnAudioStreamPacket(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr data, int noOfFrames, long pts)
        {
            if (_waveProvider == null || data == IntPtr.Zero) return;

            int channels = 2; 
            
            IntPtr ptrLeft = Marshal.ReadIntPtr(data, 0 * IntPtr.Size);
            IntPtr ptrRight = Marshal.ReadIntPtr(data, 1 * IntPtr.Size);
            
            float[] leftBuffer = new float[noOfFrames];
            float[] rightBuffer = new float[noOfFrames];
            Marshal.Copy(ptrLeft, leftBuffer, 0, noOfFrames);
            Marshal.Copy(ptrRight, rightBuffer, 0, noOfFrames);
            float[] interleavedSamples = new float[noOfFrames * channels];

            for (int i = 0; i < noOfFrames; i++)
            {
                interleavedSamples[i * 2] = leftBuffer[i] * _volume;
                interleavedSamples[i * 2 + 1] = rightBuffer[i] * _volume;
            }
            
            int totalBytes = interleavedSamples.Length * 4;
            byte[] byteBuffer = new byte[totalBytes];
            Buffer.BlockCopy(interleavedSamples, 0, byteBuffer, 0, totalBytes);

            _waveProvider.AddSamples(byteBuffer, 0, byteBuffer.Length);
        }

        public void OnAudioStreamStopped(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            _waveOut?.Stop();
        }

        public void OnAudioStreamError(IWebBrowser chromiumWebBrowser, IBrowser browser, string errorMessage)
        {
            
        }

        public void Dispose()
        {
            _waveOut?.Dispose();
            _waveOut = null;
        }
    }
}