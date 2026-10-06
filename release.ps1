# Publishes a new version to GitHub Releases, where the app's built-in updater finds it.
#
#   1. Bump AssemblyVersion / AssemblyFileVersion in src\Program.cs (e.g. 2.2.0.0) and commit.
#   2. Write the release notes in a Markdown file.
#   3. powershell -ExecutionPolicy Bypass -File release.ps1 -NotesFile notes.md
#
# Requires the GitHub CLI (winget install GitHub.cli) signed in with `gh auth login`.

param([Parameter(Mandatory = $true)][string]$NotesFile)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repo = 'shivoulis/Make-my-Web-Screen-Recorder'

$gh = (Get-Command gh.exe -ErrorAction SilentlyContinue).Source
if (-not $gh) {
    $gh = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter gh.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $gh) { throw 'GitHub CLI not found (winget install GitHub.cli)' }
if (-not (Test-Path $NotesFile)) { throw "Notes file not found: $NotesFile" }

$version = [regex]::Match((Get-Content (Join-Path $root 'src\Program.cs') -Raw), 'AssemblyVersion\("(\d+\.\d+\.\d+)').Groups[1].Value
$tag = "v$version"
if (git -C $root status --porcelain) { throw 'Commit your changes first (git status is not clean).' }
$ErrorActionPreference = 'Continue'     # "release not found" on stderr is the expected answer here
& $gh release view $tag --repo $repo 2>&1 | Out-Null
$exists = $LASTEXITCODE -eq 0
$ErrorActionPreference = 'Stop'
if ($exists) { throw "Release $tag already exists. Bump the version in src\Program.cs first." }

& (Join-Path $root 'build.ps1')
$installer = Join-Path $root "dist\MakeMyWebScreenRecorder-Setup-$version.exe"
if (-not (Test-Path $installer)) { throw "Installer not found: $installer" }

git -C $root push
& $gh release create $tag $installer --repo $repo --target main --title "Make my Web Screen Recorder $version" --notes-file $NotesFile
if ($LASTEXITCODE -ne 0) { throw 'Creating the release failed' }
Write-Host "Published $tag - installed copies will offer the update next time they start."
