# Code signing policy

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).

Release installers are built automatically by GitHub Actions ([`.github/workflows/release.yml`](.github/workflows/release.yml)) from the source code in this repository, then signed through SignPath. Only binaries built from this repository by that workflow are signed.

## Team roles

| Role | Members |
| --- | --- |
| Committers and reviewers | [shivoulis](https://github.com/shivoulis) |
| Approvers (release signing) | [shivoulis](https://github.com/shivoulis) |

## Privacy policy

This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

The only network connections it makes are:

- **Update check** — only if you allow it (the app asks on first start; you can change it any time in Settings). It reads the latest release information from the GitHub API. No personal data, recordings or usage information are sent.
- **Speech model download** — only when you turn on subtitles and confirm the download. Models are downloaded from Hugging Face.
- **Background effect model download** — only when you choose a camera background effect. The model (under 1 MB) is downloaded from Qualcomm AI Hub's public storage.
- **Update download** — only when you click *Update*. The installer is downloaded from this repository's GitHub releases.

Recordings, subtitles and settings stay on your computer. Speech recognition and camera effects run locally.
