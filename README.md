# YT Prepis

Preprosta Windows aplikacija (.NET 8, WinForms), ki iz YouTube videa naredi prepis v navadnem besedilu (.txt).

## Kako deluje

1. Najprej poskusi vzeti **obstoječe YouTube podnapise** (ročni imajo prednost pred samodejnimi).
2. Če jih ni (ali če obkljukaš *Vedno uporabi Whisper*), prenese zvok in ga **lokalno prepiše z Whisperjem** ([whisper.cpp](https://github.com/ggerganov/whisper.cpp) prek [Whisper.net](https://github.com/sandrohanea/whisper.net)).
3. Besedilo razdeli na odstavke glede na premore v govoru in ga lahko kopiraš ali shraniš kot .txt (UTF-8).

Nič ne gre v oblak – vse teče na tvojem računalniku.

## Zagon

Potrebuješ [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) in Windows 10/11 (x64).

```
dotnet run -c Release
```

ali pa naredi samostojen .exe:

```
dotnet publish -c Release -r win-x64 --self-contained
```

## Whisper modeli

Model se ob prvi uporabi samodejno prenese s Hugging Face v `%LOCALAPPDATA%\YtPrepis\models`.

| Model | Velikost | Opomba |
|---|---|---|
| tiny | 75 MB | zelo hitro, slabše |
| base | 142 MB | |
| small | 466 MB | privzeto |
| medium | 1,5 GB | precej boljše za slovenščino |
| large-v3-turbo | 1,6 GB | najboljše, počasneje na CPU |

## Opombe

- Pretvorba zvoka uporablja Windows Media Foundation. Če ta odpove, aplikacija poskusi z `ffmpeg` (če je v PATH).
- Če YouTube kaj spremeni in prenos neha delovati, posodobi paket: `dotnet add package YoutubeExplode`.

## Knjižnice

- [YoutubeExplode](https://github.com/Tyrrrz/YoutubeExplode) – podnapisi in prenos zvoka
- [Whisper.net](https://github.com/sandrohanea/whisper.net) – prepoznava govora
- [NAudio](https://github.com/naudio/NAudio) – pretvorba zvoka
