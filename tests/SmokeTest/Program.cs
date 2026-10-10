// macOS end-to-end check used by CI.
// Usage: SmokeTest <local audio/video file> <expected word>
using System.Text.RegularExpressions;
using YtTranscript;

var (input, expected) = (args[0], args[1]);
var failures = new List<string>();

// 0) conversion diagnostics (shown as annotations, because CI logs are not always readable)
var probe = Path.Combine(Path.GetTempPath(), $"probe-{Guid.NewGuid():N}.wav");
AudioConverter.ToWav16k(input, probe);
var pcm = File.ReadAllBytes(probe);
int peak = 0;
for (var i = 44; i + 1 < pcm.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(pcm, i)));
Console.WriteLine($"::notice::Converted WAV: {AudioTools.WavDurationSeconds(probe):F1} s, peak {peak}");
if (peak < 500) failures.Add($"Converted audio is silent (peak {peak}).");

// 1) full pipeline on a local file: model download, conversion, Whisper
var result = await new TranscriptService().RunAsync(
    new TranscriptOptions(input, "en", "tiny", ForceWhisper: false),
    new Progress<ProgressInfo>(_ => { }),
    CancellationToken.None);

Console.WriteLine($"Title: {result.Title}");
Console.WriteLine($"Source: {result.Source}");
Console.WriteLine($"::notice::Transcript ({result.Segments.Count} segments): {result.Text}");
if (!result.Text.Contains(expected, StringComparison.OrdinalIgnoreCase))
    failures.Add($"Transcript does not contain '{expected}': {result.Text}");

// 2) SRT from the real segments
var srt = Subtitles.ToSrt(result.Segments);
Console.WriteLine("SRT:\n" + srt);
if (!Regex.IsMatch(srt, @"^1\r\n\d\d:\d\d:\d\d,\d{3} --> \d\d:\d\d:\d\d,\d{3}\r\n\S"))
    failures.Add("SRT does not start with a valid cue.");

// 3) SRT rules on synthetic segments: line length, max two lines, no overlaps
var longText = string.Join(" ", Enumerable.Repeat("Danes bomo govorili o objektivih in kameri obscuri.", 5));
var synthetic = new List<Segment>
{
    new(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(20), longText),
    new(TimeSpan.FromSeconds(19), TimeSpan.FromSeconds(21), "Prekrivanje s prejšnjim."),
    new(TimeSpan.FromSeconds(21.1), TimeSpan.FromSeconds(21.2), "Kratko."),
};
var cues = Subtitles.ToSrt(synthetic).Split("\r\n\r\n", StringSplitOptions.RemoveEmptyEntries);
TimeSpan prevEnd = TimeSpan.MinValue;
foreach (var cue in cues)
{
    var lines = cue.Split("\r\n");
    var times = Regex.Match(lines[1], @"(\S+) --> (\S+)");
    var start = TimeSpan.ParseExact(times.Groups[1].Value, @"hh\:mm\:ss\,fff", null);
    var end = TimeSpan.ParseExact(times.Groups[2].Value, @"hh\:mm\:ss\,fff", null);
    var text = lines.Skip(2).ToArray();

    if (text.Length is < 1 or > Subtitles.MaxLines) failures.Add($"Cue {lines[0]} has {text.Length} lines.");
    if (text.Any(l => l.Length > Subtitles.MaxLineLength)) failures.Add($"Cue {lines[0]} has a line over {Subtitles.MaxLineLength} chars.");
    if (end <= start) failures.Add($"Cue {lines[0]} ends before it starts.");
    if (start < prevEnd) failures.Add($"Cue {lines[0]} overlaps the previous cue.");
    prevEnd = end;
}
Console.WriteLine($"Synthetic SRT: {cues.Length} cues");
if (cues.Length < 5) failures.Add($"Long segment was not split (only {cues.Length} cues).");

foreach (var f in failures) Console.WriteLine($"::error::{f}");
return failures.Count == 0 ? 0 : 1;
