# Make my Web Screen Recorder

A simple, modern screen recorder for Windows 10/11.

- Record the **entire screen**, a **single window**, a **selected area** (drag to choose), or a **browser tab** (Chrome, Edge, Brave — records just the page, without the browser toolbars)
- **Microphone** and **system audio** ("what you hear"), each with an on/off switch
- **Webcam bubble**: round, draggable camera overlay in the corner of the recording (S/M/L), with **background blur**, **scenes** (built-in or your own picture) and **colour filters** — person segmentation runs locally on Windows ML
- **Automatic subtitles**: local speech-to-text with Whisper (language detected automatically) as a switchable subtitle track + `.srt` file, or burned into the video
- Floating **control bar** while recording: timer, pause/resume, mute mic, discard, stop — hidden from the recording itself
- Red **frame** around the recorded area, optional **3-2-1 countdown**, optional **click highlights**
- Saves small, **visually lossless MP4** files that play everywhere (captured losslessly, then compressed with H.264 CRF 18)
- Optional: keep the truly lossless original; export to high-quality MP4, extra-small MP4 or lossless MKV
- **Automatic updates** from GitHub Releases (verified with the SHA-256 checksum GitHub publishes)

## Install

Download `MakeMyWebScreenRecorder-Setup-x.y.z.exe` from the Releases page and run it. No admin rights needed.
Windows may show "Windows protected your PC" because the installer isn't code-signed: click **More info → Run anyway**.

## Build from source

Requirements (no Visual Studio needed):

- Windows 10/11 with .NET Framework 4.8 (built in) — the C# compiler `csc.exe` that ships with Windows is used
- FFmpeg on `PATH`: `winget install Gyan.FFmpeg`
- Inno Setup for the installer: `winget install JRSoftware.InnoSetup`

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1               # app + installer -> dist\
powershell -ExecutionPolicy Bypass -File build.ps1 -NoInstaller  # app only -> build\
```

The build copies `ffmpeg.exe` and `ffplay.exe` next to the app, so the installer works on PCs without FFmpeg.

## Releasing an update

1. Bump `AssemblyVersion` / `AssemblyFileVersion` in `src/Program.cs` (the only place the version lives) and commit.
2. Write release notes in a Markdown file.
3. Run `powershell -ExecutionPolicy Bypass -File release.ps1 -NotesFile notes.md`. It saves the notes to `release-notes/`, tags the version and pushes; GitHub Actions then builds, signs (see [CODE_SIGNING.md](CODE_SIGNING.md)) and publishes the release.

Installed copies that allow update checks offer the update with one click.

## How it works

| File | What it does |
| --- | --- |
| `src/Program.cs` | Entry point, FFmpeg helpers, settings, Win32 helpers (windows, monitors), system-audio capture (WASAPI loopback), click highlights |
| `src/BrowserTabs.cs` | Lists browser tabs and finds the page area via UI Automation |
| `src/Recorder.cs` | Recording session (FFmpeg `ddagrab` + lossless H.264 RGB) and the compression step (cuts pauses, silences mic mutes, mixes audio) |
| `src/MainWindow.cs` | Main window (WPF) |
| `src/Overlays.cs` | Control bar, countdown and recording frame (excluded from capture) |
| `src/AreaPicker.cs` | Drag-to-select overlay for recording part of the screen |
| `src/Theme.cs` | Dark theme and control styles |
| `src/Camera.cs` | Webcam bubble (camera frames via FFmpeg) |
| `src/Subtitles.cs` | Whisper transcription (FFmpeg's `whisper` filter), model download, embedding/burning subtitles |
| `src/Effects.cs`, `src/EffectsWindow.cs` | Camera background blur/scenes (MediaPipe selfie segmentation on Windows ML) and filters, with live preview |
| `src/OnnxFix.cs` | Converts the downloaded ONNX model to a form Windows ML can load |
| `src/Updater.cs` | Checks GitHub Releases, downloads and verifies the installer, runs it silently |
| `release.ps1` | Builds and publishes a GitHub release |
| `installer.iss` | Inno Setup installer script |

Speech models are downloaded on first use to `%LOCALAPPDATA%\MakeMyWebScreenRecorder\models` (from Hugging Face: whisper.cpp `ggml-base` / `ggml-large-v3-turbo-q5_0` and the Silero VAD model).

Settings are stored in `%APPDATA%\MakeMyWebScreenRecorder\settings.ini`; crashes are logged to `error.log` in the same folder.
For screenshots while developing, set `MMWSR_SHOW_OVERLAYS=1` to stop the overlays being hidden from capture.

## Code signing and privacy

See [CODE_SIGNING.md](CODE_SIGNING.md).

## Licence

Copyright (C) 2026 shivoulis

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. It is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY. See [LICENSE](LICENSE) for the full text.

## Third-party software

The installer bundles [FFmpeg](https://ffmpeg.org) (GPL build by [gyan.dev](https://www.gyan.dev/ffmpeg/builds/)); its licence is installed in `ffmpeg\LICENSE.txt`.
