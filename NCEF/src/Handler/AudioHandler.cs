using System;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace NCEF.Handler
{
    public class AudioHandler : IDisposable
    {
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
                Program.Logger.Error("CleanupAudio Failed: " + ex.Message);
            }
        }

        public void Dispose()
        {
            CleanupAudio();
        }
    }
}