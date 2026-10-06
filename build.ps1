# Builds Make my Web Screen Recorder:
#   1. draws the app icon (assets\app.ico) if missing
#   2. compiles src\Program.cs into build\MakeMyWebScreenRecorder.exe with the C# compiler built into Windows
#   3. copies FFmpeg next to it (build\ffmpeg\)
#   4. builds the installer with Inno Setup -> dist\MakeMyWebScreenRecorder-Setup-<version>.exe
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1 [-NoInstaller]

param([switch]$NoInstaller)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$build = Join-Path $root 'build'
$assets = Join-Path $root 'assets'
New-Item -ItemType Directory -Force $build, $assets, (Join-Path $build 'ffmpeg') | Out-Null

# --- 1. Icon ----------------------------------------------------------------
$ico = Join-Path $assets 'app.ico'
if (-not (Test-Path $ico)) {
    Add-Type -AssemblyName System.Drawing
    $pngs = foreach ($size in 16, 24, 32, 48, 64, 128, 256) {
        $bmp = New-Object System.Drawing.Bitmap $size, $size
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = 'AntiAlias'
        $g.Clear([System.Drawing.Color]::Transparent)
        $s = $size / 256.0
        # dark rounded tile
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $r = 56 * $s; $m = 8 * $s; $w = $size - 2 * $m
        $path.AddArc($m, $m, $r, $r, 180, 90); $path.AddArc($m + $w - $r, $m, $r, $r, 270, 90)
        $path.AddArc($m + $w - $r, $m + $w - $r, $r, $r, 0, 90); $path.AddArc($m, $m + $w - $r, $r, $r, 90, 90)
        $path.CloseFigure()
        $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 32, 36, 44))), $path)
        # white ring + red record dot
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(235, 255, 255, 255)), ([Math]::Max(1.5, 14 * $s))
        $g.DrawEllipse($pen, 52 * $s, 52 * $s, 152 * $s, 152 * $s)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 235, 45, 55))), 84 * $s, 84 * $s, 88 * $s, 88 * $s)
        $g.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        , @($size, $ms.ToArray())
    }
    # ICO file: header, directory, then PNG images
    $out = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $out
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$pngs.Count)
    $offset = 6 + 16 * $pngs.Count
    foreach ($p in $pngs) {
        $dim = if ($p[0] -ge 256) { 0 } else { $p[0] }
        $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$p[1].Length); $bw.Write([uint32]$offset)
        $offset += $p[1].Length
    }
    foreach ($p in $pngs) { $bw.Write([byte[]]$p[1]) }
    $bw.Flush()
    [IO.File]::WriteAllBytes($ico, $out.ToArray())
    Write-Host "Created icon $ico"
}

# --- 2. Compile ---------------------------------------------------------------
$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$csc = Join-Path $fw 'csc.exe'
$exe = Join-Path $build 'MakeMyWebScreenRecorder.exe'
& $csc /nologo /target:winexe /optimize+ /platform:anycpu "/out:$exe" "/win32icon:$ico" "/lib:$fw\WPF" `
    /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Xaml.dll `
    /r:WindowsBase.dll /r:PresentationCore.dll /r:PresentationFramework.dll `
    /r:UIAutomationClient.dll /r:UIAutomationTypes.dll /r:Microsoft.VisualBasic.dll `
    (Join-Path $root 'src\*.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compile failed' }
Write-Host "Compiled $exe"

# --- 3. FFmpeg --------------------------------------------------------------
$ff = (Get-Command ffmpeg.exe -ErrorAction SilentlyContinue).Source
if (-not $ff) { throw 'FFmpeg not found on PATH (winget install Gyan.FFmpeg)' }
$ffbin = Split-Path $ff
foreach ($f in 'ffmpeg.exe', 'ffplay.exe') {
    $src = Join-Path $ffbin $f
    $dst = Join-Path $build "ffmpeg\$f"
    if ((Test-Path $src) -and (-not (Test-Path $dst) -or (Get-Item $src).LastWriteTime -ne (Get-Item $dst).LastWriteTime)) {
        Copy-Item $src $dst -Force
    }
}
$lic = Join-Path (Split-Path $ffbin) 'LICENSE'
if (Test-Path $lic) { Copy-Item $lic (Join-Path $build 'ffmpeg\LICENSE.txt') -Force }
Write-Host 'Copied FFmpeg'

# --- 4. Installer -----------------------------------------------------------
if ($NoInstaller) { return }
$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw 'Inno Setup not found (winget install JRSoftware.InnoSetup)' }
& $iscc /Q (Join-Path $root 'installer.iss')
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }
Get-ChildItem (Join-Path $root 'dist') -Filter *.exe | ForEach-Object { Write-Host ("Installer: {0} ({1:N0} MB)" -f $_.FullName, ($_.Length / 1MB)) }
