// Usage: SmokeTest <audio file> <ggml model> <expected word>
using YtTranscript;

var (input, model, expected) = (args[0], args[1], args[2]);
var wav = Path.Combine(Path.GetTempPath(), $"smoke-{Guid.NewGuid():N}.wav");

AudioConverter.ToWav16k(input, wav);
Console.WriteLine($"Converted to WAV: {AudioTools.WavDurationSeconds(wav):F1} s");

var text = await TranscriptService.TranscribeWavAsync(
    wav, "en", model, new Progress<ProgressInfo>(_ => { }), CancellationToken.None);
Console.WriteLine($"Transcript: {text}");

if (!text.Contains(expected, StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine($"::error::Transcript does not contain '{expected}': {text}");
    return 1;
}
return 0;
