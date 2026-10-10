using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace YtTranscript;

/// <summary>Platform-independent audio helpers shared by the Windows and macOS converters.</summary>
internal static class AudioTools
{
    public const int SampleRate = 16000;

    /// <summary>Runs an external tool. Returns exit code -1 if the tool cannot be started.</summary>
    public static (int Exit, string Error) Run(string file, params string[] args)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        try
        {
            using var proc = Process.Start(psi);
            if (proc is null) return (-1, $"{file} did not start");
            var stdout = proc.StandardOutput.ReadToEndAsync();
            var stderr = proc.StandardError.ReadToEndAsync();
            proc.WaitForExit();
            return (proc.ExitCode, (stderr.Result + stdout.Result).Trim());
        }
        catch (Win32Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    /// <summary>Converts any audio file with ffmpeg, if it is installed. Set YTTRANSCRIPT_NO_FFMPEG=1 to disable.</summary>
    public static bool TryFfmpeg(string input, string output)
    {
        if (Environment.GetEnvironmentVariable("YTTRANSCRIPT_NO_FFMPEG") == "1") return false;

        foreach (var ffmpeg in new[] { "ffmpeg", "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg" })
        {
            var (exit, _) = Run(ffmpeg, "-y", "-loglevel", "error", "-i", input,
                "-ar", SampleRate.ToString(), "-ac", "1", "-c:a", "pcm_s16le", output);
            if (exit == 0 && File.Exists(output)) return true;
        }
        return false;
    }

    /// <summary>
    /// True if a 16-bit PCM WAV file seems to contain no sound. Samples up to 64 blocks spread over the file,
    /// so it stays fast for multi-hour recordings.
    /// </summary>
    public static bool LooksSilent(string path, int threshold = 300)
    {
        using var fs = File.OpenRead(path);
        var length = fs.Length - 44;
        if (length <= 0) return true;

        const int block = 64 * 1024;
        var buf = new byte[block];
        var blocks = (int)Math.Min(64, Math.Max(1, length / block));
        for (var b = 0; b < blocks; b++)
        {
            fs.Position = 44 + (length / blocks * b & ~1L);
            var n = fs.Read(buf, 0, block) & ~1;
            for (var i = 0; i < n; i += 2)
                if (Math.Abs((int)BitConverter.ToInt16(buf, i)) > threshold) return false;
        }
        return true;
    }

    /// <summary>Approximate duration of a 16 kHz, 16-bit mono WAV file.</summary>
    public static double WavDurationSeconds(string path) =>
        Math.Max(0, new FileInfo(path).Length - 44) / (double)(SampleRate * 2);

    /// <summary>
    /// Rewrites a WAV file with a canonical 44-byte PCM header (some converters add extra chunks
    /// or use WAVE_FORMAT_EXTENSIBLE). Throws if the audio is not 16 kHz, 16-bit mono.
    /// </summary>
    public static void NormalizeWav(string path)
    {
        long dataOffset, dataLength;
        bool canonical;

        using (var fs = File.OpenRead(path))
        using (var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true))
        {
            if (new string(br.ReadChars(4)) != "RIFF") throw new InvalidDataException("Not a WAV file.");
            br.ReadUInt32();
            if (new string(br.ReadChars(4)) != "WAVE") throw new InvalidDataException("Not a WAV file.");

            int channels = 0, rate = 0, bits = 0;
            ushort format = 0;
            long fmtOffset = -1;
            dataOffset = -1;
            dataLength = 0;

            while (fs.Position + 8 <= fs.Length)
            {
                var id = new string(br.ReadChars(4));
                long size = br.ReadUInt32();
                var start = fs.Position;

                if (id == "fmt ")
                {
                    fmtOffset = start - 8;
                    format = br.ReadUInt16();
                    channels = br.ReadUInt16();
                    rate = br.ReadInt32();
                    br.ReadInt32(); // byte rate
                    br.ReadUInt16(); // block align
                    bits = br.ReadUInt16();
                }
                else if (id == "data")
                {
                    dataOffset = start;
                    dataLength = Math.Min(size, fs.Length - start);
                    break;
                }

                fs.Position = start + size + (size & 1);
            }

            if (dataOffset < 0 || fmtOffset < 0) throw new InvalidDataException("WAV file has no audio data.");
            if (channels != 1 || rate != SampleRate || bits != 16 || (format != 1 && format != 0xFFFE))
                throw new InvalidDataException($"Unexpected WAV format: {channels} ch, {rate} Hz, {bits} bit, format {format}.");

            canonical = format == 1 && fmtOffset == 12 && dataOffset == 44 && dataLength == fs.Length - 44;
        }

        if (canonical) return;

        var tmp = path + ".tmp";
        using (var src = File.OpenRead(path))
        using (var dst = File.Create(tmp))
        using (var bw = new BinaryWriter(dst))
        {
            WriteHeader(bw, (uint)dataLength);
            src.Position = dataOffset;
            CopyBytes(src, dst, dataLength);
        }
        File.Move(tmp, path, true);
    }

    private static void WriteHeader(BinaryWriter w, uint dataLength)
    {
        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataLength);
        w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        w.Write(16);                // fmt chunk size
        w.Write((ushort)1);         // PCM
        w.Write((ushort)1);         // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);    // byte rate
        w.Write((ushort)2);         // block align
        w.Write((ushort)16);        // bits per sample
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataLength);
    }

    private static void CopyBytes(Stream src, Stream dst, long count)
    {
        var buf = new byte[1 << 20];
        while (count > 0)
        {
            var n = src.Read(buf, 0, (int)Math.Min(buf.Length, count));
            if (n <= 0) break;
            dst.Write(buf, 0, n);
            count -= n;
        }
    }
}
