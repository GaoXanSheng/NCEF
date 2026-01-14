using System;
using CefSharp;
using CefSharp.Enums;
using CefSharp.Structs;
using NAudio.Wave;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace NCEF.Manager
{
    public class BrowserAudioManager : IAudioHandler, IDisposable
    {
        private BufferedWaveProvider _waveProvider;
        private WasapiOut _waveOut; 
        private volatile float _volume = 1.0f;
        
        private float[] _leftBuffer = new float[0];
        private float[] _rightBuffer = new float[0];
        private float[] _interleavedBuffer = new float[0];
        private byte[] _byteBuffer = new byte[0];

        public BrowserAudioManager()
        {
            
        }

        public void SetVolume(float vol)
        {
            if (vol < 0f) vol = 0f;
            if (vol > 1f) vol = 1f;
            _volume = vol;
        }

        public bool GetAudioParameters(IWebBrowser chromiumWebBrowser, IBrowser browser, ref AudioParameters parameters)
        {
            parameters.ChannelLayout = ChannelLayout.LayoutStereo;
            parameters.SampleRate = 44100;
            parameters.FramesPerBuffer = 441;
            return true;
        }

        public void OnAudioStreamStarted(IWebBrowser chromiumWebBrowser, IBrowser browser, AudioParameters parameters, int channels)
        {
            CleanupAudio();

            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(parameters.SampleRate, channels);

            _waveProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(200),
                DiscardOnBufferOverflow = true
            };

            try
            {
                _waveOut = new WasapiOut(AudioClientShareMode.Shared, 50);
                _waveOut.Init(_waveProvider);
                _waveOut.Play();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WASAPI Init Failed: " + ex.Message);
            }
        }

        public void OnAudioStreamPacket(IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr data, int noOfFrames, long pts)
        {
            if (_waveProvider == null || _waveOut == null || data == IntPtr.Zero) return;

            if (_waveProvider.BufferedDuration.TotalMilliseconds > 60)
            {
                _waveProvider.ClearBuffer();
            }

            int channels = 2;
            int totalSamples = noOfFrames * channels;

            if (_leftBuffer.Length < noOfFrames)
            {
                _leftBuffer = new float[noOfFrames];
                _rightBuffer = new float[noOfFrames];
                _interleavedBuffer = new float[totalSamples];
                _byteBuffer = new byte[totalSamples * 4];
            }

            IntPtr ptrLeft = Marshal.ReadIntPtr(data, 0 * IntPtr.Size);
            IntPtr ptrRight = Marshal.ReadIntPtr(data, 1 * IntPtr.Size);

            Marshal.Copy(ptrLeft, _leftBuffer, 0, noOfFrames);
            Marshal.Copy(ptrRight, _rightBuffer, 0, noOfFrames);

            for (int i = 0; i < noOfFrames; i++)
            {
                _interleavedBuffer[i * 2] = _leftBuffer[i] * _volume;
                _interleavedBuffer[i * 2 + 1] = _rightBuffer[i] * _volume;
            }

            Buffer.BlockCopy(_interleavedBuffer, 0, _byteBuffer, 0, totalSamples * 4);
            _waveProvider.AddSamples(_byteBuffer, 0, totalSamples * 4);
        }

        public void OnAudioStreamStopped(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            CleanupAudio();
        }

        public void OnAudioStreamError(IWebBrowser chromiumWebBrowser, IBrowser browser, string errorMessage)
        {
            CleanupAudio();
        }

        private void CleanupAudio()
        {
            try
            {
                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }
                _waveProvider = null;
            }
            catch { }
        }

        public void Dispose()
        {
            CleanupAudio();
        }
    }
}