namespace YtTranscript;

/// <summary>
/// macOS: converts downloaded audio to 16 kHz mono WAV with the tools built into macOS
/// (afconvert, and avconvert for formats afconvert cannot read). ffmpeg is the last fallback.
/// </summary>
internal static class AudioConverter
{
    private const string Afconvert = "/usr/bin/afconvert";
    private const string Avconvert = "/usr/bin/avconvert";

    public static void ToWav16k(string input, string output)
    {
        var errors = new List<string>();

        if (!TryAfconvert(input, output, errors))
        {
            // YouTube serves fragmented MP4; AVFoundation reads it even when Core Audio does not.
            var m4a = Path.Combine(Path.GetDirectoryName(output)!, "remuxed.m4a");
            var (exit, err) = AudioTools.Run(Avconvert, "--source", input, "--preset", "PresetAppleM4A", "--output", m4a);
            if (exit != 0) errors.Add("avconvert: " + err);

            var ok = exit == 0 && File.Exists(m4a) && TryAfconvert(m4a, output, errors);
            if (!ok && !AudioTools.TryFfmpeg(input, output))
                throw new InvalidOperationException(L.T("ConvertFailed") + Environment.NewLine + string.Join(Environment.NewLine, errors));
        }

        AudioTools.NormalizeWav(output);
    }

    private static bool TryAfconvert(string input, string output, List<string> errors)
    {
        var (exit, err) = AudioTools.Run(Afconvert, "-f", "WAVE", "-d", $"LEI16@{AudioTools.SampleRate}", "-c", "1", input, output);
        if (exit == 0 && File.Exists(output) && new FileInfo(output).Length > 44) return true;
        errors.Add("afconvert: " + err);
        return false;
    }
}
