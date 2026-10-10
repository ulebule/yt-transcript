# YT Prepis

A simple Windows app (.NET 8, WinForms) that turns a YouTube video into a plain-text transcript (.txt).

## How it works

1. It first tries to use the video's **existing YouTube captions** (manually created captions take priority over auto-generated ones).
2. If there are none (or if *Always use Whisper* is checked), it downloads the audio and **transcribes it locally with Whisper** ([whisper.cpp](https://github.com/ggerganov/whisper.cpp) via [Whisper.net](https://github.com/sandrohanea/whisper.net)).
3. The text is split into paragraphs based on pauses in speech, and you can copy it or save it as a .txt file (UTF-8).

Nothing is sent to the cloud – everything runs on your own computer.

The user interface is available in English and Slovenian. It follows your Windows language by default and can be switched in the top-right corner; the choice is remembered.

## Installation (for users)

Download `YtPrepis-Setup-x.y.z.exe` from [Releases](https://github.com/ulebule/yt-transcript/releases/latest) and run it. No admin rights and no .NET installation are required.

The installer is not code-signed, so Windows SmartScreen may show a warning on first run. Click *More info* → *Run anyway*.

## Releasing a new version

The installer is built by GitHub Actions. To publish a new release, bump `<Version>` in `YtPrepis.csproj` (e.g. to `1.0.1`) and push to `main`. The workflow builds the installer, creates the `v1.0.1` tag and release, and attaches the installer automatically. Pushing a `v*` tag works too.

## Running from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) and Windows 10/11 (x64).

```
dotnet run -c Release
```

To build a self-contained version:

```
dotnet publish -c Release -r win-x64 --self-contained -o publish
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

- Audio conversion uses Windows Media Foundation. If that fails, the app falls back to `ffmpeg` (if it is on your PATH).
- If YouTube changes something and downloads stop working, update the package: `dotnet add package YoutubeExplode`.

## Libraries

- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) – captions and audio download
- [Whisper.net](https://github.com/sandrohanea/whisper.net) – speech recognition
- [NAudio](https://github.com/naudio/NAudio) – audio conversion
