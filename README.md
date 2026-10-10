# YT Transcript

A simple desktop app for **Windows and macOS** that turns a YouTube video into a plain-text transcript (.txt).

## How it works

1. It first tries to use the video's **existing YouTube captions** (manually created captions take priority over auto-generated ones).
2. If there are none (or if *Always use Whisper* is checked), it downloads the audio and **transcribes it locally with Whisper** ([whisper.cpp](https://github.com/ggerganov/whisper.cpp) via [Whisper.net](https://github.com/sandrohanea/whisper.net)).
3. The text is split into paragraphs based on pauses in speech, and you can copy it or save it as a .txt file (UTF-8).

Nothing is sent to the cloud – everything runs on your own computer.

The user interface is available in English and Slovenian. It follows your system language by default and can be switched in the top-right corner; the choice is remembered.

## Installation (for users)

Download the right file from [Releases](https://github.com/ulebule/yt-transcript/releases/latest):

| System | File |
|---|---|
| Windows 10/11 | `YtPrepis-Setup-x.y.z.exe` |
| Mac with Apple Silicon (M1–M4) | `YtPrepis-x.y.z-mac-apple-silicon.dmg` |
| Mac with Intel | `YtPrepis-x.y.z-mac-intel.dmg` |

No admin rights and no .NET installation are required.

**Windows:** run the installer. It is not code-signed, so SmartScreen may show a warning: click *More info* → *Run anyway*.

**macOS:** open the .dmg and drag *YT Prepis* to *Applications*. The app is not notarized by Apple, so the first launch is blocked. Open it once, then go to *System Settings → Privacy & Security* and click *Open Anyway*. Alternatively, run this in Terminal:

```
xattr -dr com.apple.quarantine "/Applications/YT Prepis.app"
```

On Apple Silicon, Whisper runs on the GPU (Metal), so transcription is much faster than on a typical Windows laptop.

## Releasing a new version

Everything is built by GitHub Actions. To publish a new release, bump `<Version>` in `Directory.Build.props` (e.g. to `1.1.1`) and push to `main`. The workflow builds the Windows installer and both macOS .dmg files, runs a macOS smoke test (audio conversion + Whisper), and creates the release with all files attached. Pushing a `v*` tag works too.

## Project layout

| Path | What |
|---|---|
| `TranscriptService.cs`, `Loc.cs`, `Audio/` | shared core: YouTube, Whisper, audio conversion, UI strings |
| `YtPrepis.csproj`, `MainForm.cs` | Windows app (WinForms) |
| `mac/` | macOS app (Avalonia) |
| `tests/SmokeTest/` | macOS end-to-end check used by CI |
| `installer/` | Inno Setup script (Windows), .app/.dmg packaging (macOS) |

## Running from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

Windows:

```
dotnet run --project YtPrepis.csproj -c Release
```

macOS:

```
dotnet run --project mac/YtPrepis.Mac.csproj -c Release
```

## Whisper models

The selected model is downloaded automatically from Hugging Face on first use and stored in `%LOCALAPPDATA%\YtPrepis\models`.

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
