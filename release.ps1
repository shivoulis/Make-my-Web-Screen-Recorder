# Publishes a new version. GitHub Actions builds, signs and releases it; the app's updater then offers it.
#
#   1. Bump AssemblyVersion / AssemblyFileVersion in src\Program.cs (e.g. 2.4.0.0) and commit.
#   2. Write the release notes in a Markdown file.
#   3. powershell -ExecutionPolicy Bypass -File release.ps1 -NotesFile notes.md
#
# -Local builds and publishes from this PC instead (unsigned; needs the GitHub CLI signed in).

param([Parameter(Mandatory = $true)][string]$NotesFile, [switch]$Local)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$repo = 'shivoulis/Make-my-Web-Screen-Recorder'

if (-not (Test-Path $NotesFile)) { throw "Notes file not found: $NotesFile" }
$version = [regex]::Match((Get-Content (Join-Path $root 'src\Program.cs') -Raw), 'AssemblyVersion\("(\d+\.\d+\.\d+)').Groups[1].Value
$tag = "v$version"
if (git -C $root status --porcelain) { throw 'Commit your changes first (git status is not clean).' }
if (git -C $root tag --list $tag) { throw "Tag $tag already exists. Bump the version in src\Program.cs first." }

if (-not $Local) {
    $dest = Join-Path $root "release-notes\$tag.md"
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    Copy-Item $NotesFile $dest
    git -C $root add $dest
    git -C $root commit -q -m "Release notes for $tag"
    git -C $root tag $tag
    git -C $root push
    git -C $root push origin $tag
    Write-Host "Pushed $tag. GitHub Actions is building and publishing it:"
    Write-Host "  https://github.com/$repo/actions"
    return
}

$gh = (Get-Command gh.exe -ErrorAction SilentlyContinue).Source
if (-not $gh) {
    $gh = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -Filter gh.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $gh) { throw 'GitHub CLI not found (winget install GitHub.cli)' }
& (Join-Path $root 'build.ps1')
$installer = Join-Path $root "dist\MakeMyWebScreenRecorder-Setup-$version.exe"
git -C $root push
& $gh release create $tag $installer --repo $repo --target main --title "Make my Web Screen Recorder $version" --notes-file $NotesFile
if ($LASTEXITCODE -ne 0) { throw 'Creating the release failed' }
Write-Host "Published $tag (unsigned, built locally)."
