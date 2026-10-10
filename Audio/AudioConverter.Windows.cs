using NAudio.MediaFoundation;
using NAudio.Wave;

namespace YtPrepis;

/// <summary>Windows: converts downloaded audio to 16 kHz mono WAV with Media Foundation (ffmpeg as fallback).</summary>
internal static class AudioConverter
{
    public static void ToWav16k(string input, string output)
    {
        try
        {
            MediaFoundationApi.Startup();
            using var reader = new MediaFoundationReader(input);
            using var resampler = new MediaFoundationResampler(reader, new WaveFormat(AudioTools.SampleRate, 16, 1))
            {
                ResamplerQuality = 60
            };
            WaveFileWriter.CreateWaveFile(output, resampler);
        }
        catch (Exception mfEx)
        {
            if (!AudioTools.TryFfmpeg(input, output))
                throw new InvalidOperationException(L.T("ConvertFailed"), mfEx);
        }

        AudioTools.NormalizeWav(output);
    }
}
