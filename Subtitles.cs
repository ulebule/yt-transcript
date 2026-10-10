using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace YtTranscript;

/// <summary>Builds SubRip (.srt) subtitles from transcript segments.</summary>
public static class Subtitles
{
    /// <summary>Maximum characters per subtitle line (common broadcast / streaming guideline).</summary>
    public const int MaxLineLength = 42;
    public const int MaxLines = 2;

    private static readonly TimeSpan MinDuration = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MinGap = TimeSpan.FromMilliseconds(40);

    public static string ToSrt(IEnumerable<Segment> segments)
    {
        var cues = Normalize(segments).SelectMany(SplitLongCue).ToList();
        FixTiming(cues);

        var sb = new StringBuilder();
        for (var i = 0; i < cues.Count; i++)
        {
            var c = cues[i];
            sb.Append(i + 1).Append("\r\n")
              .Append(Timestamp(c.Start)).Append(" --> ").Append(Timestamp(c.End)).Append("\r\n")
              .Append(string.Join("\r\n", Wrap(c.Text))).Append("\r\n\r\n");
        }
        return sb.ToString();
    }

    private static IEnumerable<Segment> Normalize(IEnumerable<Segment> segments) =>
        segments
            .Select(s => s with { Text = Regex.Replace(s.Text, @"\s+", " ").Trim() })
            .Where(s => s.Text.Length > 0)
            .OrderBy(s => s.Start);

    /// <summary>Splits a cue that would need more than two lines into several cues, sharing the time by text length.</summary>
    private static IEnumerable<Segment> SplitLongCue(Segment s)
    {
        const int maxChars = MaxLineLength * MaxLines;
        if (s.Text.Length <= maxChars && Wrap(s.Text).Count <= MaxLines)
        {
            yield return s;
            yield break;
        }

        // greedy word packing into chunks that fit in two lines
        var chunks = new List<string>();
        var current = "";
        foreach (var word in s.Text.Split(' '))
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && (candidate.Length > maxChars || Wrap(candidate).Count > MaxLines))
            {
                chunks.Add(current);
                current = word;
            }
            else current = candidate;
        }
        if (current.Length > 0) chunks.Add(current);

        var total = chunks.Sum(c => c.Length);
        var duration = s.End - s.Start;
        var start = s.Start;
        for (var i = 0; i < chunks.Count; i++)
        {
            var end = i == chunks.Count - 1
                ? s.End
                : start + TimeSpan.FromTicks(duration.Ticks * chunks[i].Length / Math.Max(1, total));
            yield return new Segment(start, end, chunks[i]);
            start = end;
        }
    }

    /// <summary>Removes overlaps (common in auto-generated YouTube captions) and gives short cues a readable minimum duration.</summary>
    private static void FixTiming(List<Segment> cues)
    {
        for (var i = 0; i < cues.Count; i++)
        {
            var c = cues[i];
            var end = c.End;
            if (end - c.Start < MinDuration) end = c.Start + MinDuration;
            if (i + 1 < cues.Count && end > cues[i + 1].Start - MinGap)
                end = cues[i + 1].Start - MinGap;
            if (end <= c.Start) end = c.Start + TimeSpan.FromMilliseconds(500);
            cues[i] = c with { End = end };
        }
    }

    /// <summary>Wraps text into lines of at most <see cref="MaxLineLength"/> characters, balancing two-line cues.</summary>
    public static List<string> Wrap(string text)
    {
        if (text.Length <= MaxLineLength) return new() { text };

        // two lines: break at the space closest to the middle that keeps both lines short enough
        var best = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != ' ') continue;
            if (i > MaxLineLength || text.Length - i - 1 > MaxLineLength) continue;
            if (best < 0 || Math.Abs(i - text.Length / 2) < Math.Abs(best - text.Length / 2)) best = i;
        }
        if (best > 0) return new() { text[..best], text[(best + 1)..] };

        // longer: plain greedy wrap
        var lines = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            if (line.Length > 0 && line.Length + 1 + word.Length > MaxLineLength)
            {
                lines.Add(line);
                line = word;
            }
            else line = line.Length == 0 ? word : line + " " + word;
        }
        if (line.Length > 0) lines.Add(line);
        return lines;
    }

    private static string Timestamp(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00},{3:000}",
            (int)t.TotalHours, t.Minutes, t.Seconds, t.Milliseconds);
    }
}
