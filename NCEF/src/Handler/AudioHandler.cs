using System;
using System.Runtime.InteropServices;
using log4net;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NCEF.Handler
{
    public class AudioHandler : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(AudioHandler));
        private BufferedWaveProvider _waveProvider;
        private WasapiOut _waveOut; 
        private volatile float _volume = 1.0f;

        public AudioHandler()
        {
        }

        public void SetVolume(float vol)
        {
            if (vol < 0f) vol = 0f;
            if (vol > 1f) vol = 1f;
            _volume = vol;
        }

        private void CleanupAudio()
        {
            try
            {
                _waveOut?.Stop();
                _waveOut?.Dispose();
                _waveOut = null;
                _waveProvider = null;
            }
            catch (Exception ex)
            {
                Log.Error("CleanupAudio Failed: " + ex.Message, ex);
            }
        }

        public void Dispose()
        {
            CleanupAudio();
        }
    }
}