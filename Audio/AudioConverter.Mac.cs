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

        // 1) Core Audio directly (audio files)
        if (TryAfconvert(input, output, errors) && Usable(output, errors, "afconvert")) return;

        // 2) AVFoundation extracts the audio track first: video files, and fragmented MP4 from YouTube
        var m4a = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, $"extracted-{Guid.NewGuid():N}.m4a");
        try
        {
            var (exit, err) = AudioTools.Run(Avconvert, "--source", input, "--preset", "PresetAppleM4A", "--output", m4a);
            if (exit != 0) errors.Add("avconvert: " + err);
            if (exit == 0 && File.Exists(m4a) && TryAfconvert(m4a, output, errors) && Usable(output, errors, "avconvert")) return;
        }
        finally
        {
            try { File.Delete(m4a); } catch { /* best effort */ }
        }

        // 3) ffmpeg, if installed (mkv, webm, …)
        if (AudioTools.TryFfmpeg(input, output) && Usable(output, errors, "ffmpeg")) return;

        throw new InvalidOperationException(L.T("ConvertFailed") + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private static bool Usable(string wav, List<string> errors, string tool)
    {
        try
        {
            AudioTools.NormalizeWav(wav);
            if (!AudioTools.LooksSilent(wav)) return true;
            errors.Add($"{tool}: output is silent");
        }
        catch (Exception ex)
        {
            errors.Add($"{tool}: {ex.Message}");
        }
        return false;
    }

    private static bool TryAfconvert(string input, string output, List<string> errors)
    {
        var (exit, err) = AudioTools.Run(Afconvert, "-f", "WAVE", "-d", $"LEI16@{AudioTools.SampleRate}", "-c", "1", input, output);
        if (exit == 0 && File.Exists(output) && new FileInfo(output).Length > 44) return true;
        errors.Add("afconvert: " + err);
        return false;
    }
}
