# YT Transcript

A simple desktop app for **Windows and macOS** that turns a YouTube video or a local audio/video file into a transcript (.txt) or subtitles (.srt).

## How it works

1. **Input:** paste a YouTube link, or choose a local audio/video file (*Browse …* or drag and drop it onto the window). Supported: mp3, m4a, wav, flac, mp4, mov and more; anything else works if `ffmpeg` is installed.
2. For YouTube links it first tries the video's **existing captions** (manually created captions take priority over auto-generated ones).
3. Otherwise (or if *Always use Whisper* is checked, or for local files) it **transcribes the audio locally with Whisper** ([whisper.cpp](https://github.com/ggerganov/whisper.cpp) via [Whisper.net](https://github.com/sandrohanea/whisper.net)).
4. **Output:**
   - **.txt**: plain text split into paragraphs by pauses in speech (UTF-8). You can edit it in the app before saving.
   - **.srt**: SubRip subtitles with timecodes, at most 2 lines of 42 characters per subtitle. Long segments are split, and overlapping auto-captions are fixed.

Nothing is sent to the cloud – everything runs on your own computer.

The user interface is available in English and Slovenian. It follows your system language by default and can be switched in the top-right corner; the choice is remembered.

## Installation (for users)

Download the right file from [Releases](https://github.com/ulebule/yt-transcript/releases/latest):

| System | File |
|---|---|
| Windows 10/11 | `YtTranscript-Setup-x.y.z.exe` |
| Mac with Apple Silicon (M1 or newer) | `YtTranscript-x.y.z-mac-apple-silicon.dmg` |
| Mac with Intel | `YtTranscript-x.y.z-mac-intel.dmg` |

No admin rights and no .NET installation are required.

**Windows:** run the installer. It is not code-signed, so SmartScreen may show a warning: click *More info* → *Run anyway*.

**macOS:** open the .dmg and drag *YT Transcript* to *Applications*. The app is not notarized by Apple, so the first launch is blocked. Open it once, then go to *System Settings → Privacy & Security* and click *Open Anyway*. Alternatively, run this in Terminal:

```
xattr -dr com.apple.quarantine "/Applications/YT Transcript.app"
```

On Apple Silicon, Whisper runs on the GPU (Metal), so transcription is much faster than on a typical Windows laptop.

## Releasing a new version

Everything is built by GitHub Actions. To publish a new release, bump `<Version>` in `Directory.Build.props` (e.g. to `1.1.1`) and push to `main`. The workflow builds the Windows installer and both macOS .dmg files, runs a macOS smoke test (local video file → Whisper → TXT/SRT), and creates the release with all files attached. Pushing a `v*` tag works too.

## Project layout

| Path | What |
|---|---|
| `TranscriptService.cs`, `Subtitles.cs`, `Loc.cs`, `Audio/` | shared core: YouTube, Whisper, SRT export, audio conversion, UI strings |
| `YtTranscript.csproj`, `MainForm.cs` | Windows app (WinForms) |
| `mac/` | macOS app (Avalonia) |
| `tests/SmokeTest/` | macOS end-to-end check used by CI |
| `installer/` | Inno Setup script (Windows), .app/.dmg packaging (macOS) |

## Running from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

Windows:

```
dotnet run --project YtTranscript.csproj -c Release
```

macOS:

```
dotnet run --project mac/YtTranscript.Mac.csproj -c Release
```

## Whisper models

The selected model is downloaded automatically from Hugging Face on first use and stored in `%LOCALAPPDATA%\YtTranscript\models`.

| Model | Size | Note |
|---|---|---|
| tiny | 75 MB | very fast, lower quality |
| base | 142 MB | |
| small | 466 MB | default |
| medium | 1.5 GB | noticeably better for Slovenian |
| large-v3-turbo | 1.6 GB | best quality, slower on CPU |

## Notes

- Audio conversion uses built-in system tools: Media Foundation on Windows, `afconvert`/`avconvert` on macOS. If that fails, the app falls back to `ffmpeg` (if installed).
- If YouTube changes something and downloads stop working, update the package: `dotnet add package YoutubeExplode`.

## Libraries

- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) – captions and audio download
- [Whisper.net](https://github.com/sandrohanea/whisper.net) – speech recognition
- [NAudio](https://github.com/naudio/NAudio) – audio conversion (Windows)
- [Avalonia](https://avaloniaui.net) – macOS user interface
