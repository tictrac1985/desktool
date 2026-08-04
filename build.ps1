# DeskMate build script — compiles with VS BuildTools Roslyn csc against .NET Framework 4.8 GAC assemblies.
param(
    [switch]$SkipTests,
    [switch]$SkipInstaller,
    [switch]$UiSmoke,
    # Authenticode signing. Pass -SignThumbprint with a cert installed in Cert:\CurrentUser\My,
    # or -SignPfx + -SignPfxPassword. Unsigned builds still work; they just trip SmartScreen.
    [string]$SignThumbprint,
    [string]$SignPfx,
    [string]$SignPfxPassword,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$csc  = "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe"
$fw   = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
if (-not (Test-Path $csc)) { throw "csc not found: $csc" }

$dist = Join-Path $root "dist"
$obj  = Join-Path $root "obj"
New-Item -ItemType Directory -Force $dist | Out-Null
New-Item -ItemType Directory -Force $obj  | Out-Null
New-Item -ItemType Directory -Force (Join-Path $root "assets") | Out-Null

# ---------------- Icon generation ----------------
function New-AppIcon([string]$icoPath) {
    Add-Type -AssemblyName System.Drawing
    $sizes = 16, 24, 32, 48, 64, 256
    $pngs = @()
    foreach ($s in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($s, $s)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        # rounded dark square
        $r = [float]($s * 0.22); $w = [float]($s - 1)
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc(0, 0, 2*$r, 2*$r, 180, 90)
        $path.AddArc($w - 2*$r, 0, 2*$r, 2*$r, 270, 90)
        $path.AddArc($w - 2*$r, $w - 2*$r, 2*$r, 2*$r, 0, 90)
        $path.AddArc(0, $w - 2*$r, 2*$r, 2*$r, 90, 90)
        $path.CloseFigure()
        $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.Point(0,0)), (New-Object System.Drawing.Point(0,$s)),
            [System.Drawing.Color]::FromArgb(255, 34, 41, 58), [System.Drawing.Color]::FromArgb(255, 15, 19, 28))
        $g.FillPath($bg, $path)
        # dial
        $cx = [float]($s / 2.0); $cy = [float]($s / 2.0); $rad = [float]($s * 0.33)
        $penDial = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(215, 255, 255, 255), [Math]::Max(1.0, $s * 0.05))
        $g.DrawEllipse($penDial, $cx - $rad, $cy - $rad, 2*$rad, 2*$rad)
        # hands at 10:10
        $penH = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 198, 110), [Math]::Max(1.0, $s * 0.07))
        $penH.StartCap = "Round"; $penH.EndCap = "Round"
        $penM = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [Math]::Max(1.0, $s * 0.055))
        $penM.StartCap = "Round"; $penM.EndCap = "Round"
        $aH = -150 * [Math]::PI / 180.0   # hour hand toward 10
        $aM = -30 * [Math]::PI / 180.0    # minute hand toward 2
        $g.DrawLine($penH, $cx, $cy, [float]($cx + [Math]::Sin($aH) * $rad * 0.55), [float]($cy - [Math]::Cos($aH) * $rad * 0.55))
        $g.DrawLine($penM, $cx, $cy, [float]($cx + [Math]::Sin($aM) * $rad * 0.8),  [float]($cy - [Math]::Cos($aM) * $rad * 0.8))
        $dotR = [Math]::Max(1.0, $s * 0.045)
        $g.FillEllipse((New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 198, 110))), $cx - $dotR, $cy - $dotR, 2*$dotR, 2*$dotR)
        $g.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $pngs += ,@($s, $ms.ToArray())
    }
    # pack PNG-ICO
    $fs = [System.IO.File]::Create($icoPath)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$pngs.Count)
    $offset = 6 + 16 * $pngs.Count
    foreach ($p in $pngs) {
        $s = $p[0]; $data = $p[1]
        $b = 0; if ($s -lt 256) { $b = $s }
        $bw.Write([byte]$b); $bw.Write([byte]$b); $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$data.Length); $bw.Write([uint32]$offset)
        $offset += $data.Length
    }
    foreach ($p in $pngs) { $bw.Write([byte[]]$p[1]) }
    $bw.Close(); $fs.Close()
}

$ico = Join-Path $root "assets\app.ico"
if (-not (Test-Path $ico)) {
    Write-Host "Generating app.ico ..."
    New-AppIcon $ico
}

# ---------------- References ----------------
$xaml = Join-Path $fw "System.Xaml.dll"
if (-not (Test-Path $xaml)) { $xaml = Join-Path $fw "WPF\System.Xaml.dll" }
$refsApp = @(
    "$fw\mscorlib.dll", "$fw\System.dll", "$fw\System.Core.dll", "$fw\System.Xml.dll",
    "$fw\System.Drawing.dll", "$fw\System.Windows.Forms.dll", "$fw\Microsoft.CSharp.dll",
    "$fw\System.Security.dll",
    $xaml, "$fw\WPF\WindowsBase.dll", "$fw\WPF\PresentationCore.dll", "$fw\WPF\PresentationFramework.dll"
)
# System.Security carries ProtectedData (DPAPI), used by Core\Secret.cs
$refsPure = @("$fw\mscorlib.dll", "$fw\System.dll", "$fw\System.Core.dll", "$fw\System.Xml.dll",
              "$fw\System.Security.dll")

# ---------------- Authenticode signing ----------------
function Get-SignTool {
    $roots = @("${env:ProgramFiles(x86)}\Windows Kits\10\bin", "$env:ProgramFiles\Windows Kits\10\bin")
    foreach ($r in $roots) {
        if (-not (Test-Path $r)) { continue }
        $found = Get-ChildItem $r -Recurse -Filter signtool.exe -ErrorAction SilentlyContinue |
                 Where-Object { $_.FullName -match '\\x64\\' } |
                 Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

function Invoke-Sign([string]$path) {
    if (-not $SignThumbprint -and -not $SignPfx) { return }   # unsigned build: nothing to do
    $signtool = Get-SignTool
    if (-not $signtool) { throw "signtool.exe not found - install the Windows 10/11 SDK 'Signing Tools' feature" }
    $a = @("sign", "/fd", "SHA256", "/td", "SHA256", "/tr", $TimestampUrl)
    if ($SignThumbprint) { $a += @("/sha1", $SignThumbprint) }
    else {
        $a += @("/f", $SignPfx)
        if ($SignPfxPassword) { $a += @("/p", $SignPfxPassword) }
    }
    $a += $path
    & $signtool @a
    if ($LASTEXITCODE -ne 0) { throw "signing failed for $path" }
    Write-Host "Signed -> $path" -ForegroundColor Green
}

function Invoke-Csc([string]$out, [string]$target, [string[]]$sources, [string[]]$refs, [string[]]$extra) {
    $args = @("/nologo", "/codepage:65001", "/langversion:latest", "/optimize+", "/platform:anycpu",
              "/target:$target", "/out:$out", "/noconfig", "/nostdlib+")
    foreach ($r in $refs) { $args += "/reference:$r" }
    if ($extra) { $args += $extra }
    $args += $sources
    & $csc @args
    if ($LASTEXITCODE -ne 0) { throw "csc failed for $out" }
}

# ---------------- Self tests ----------------
$pureSources = @(Get-ChildItem (Join-Path $root "src\Core") -Filter *.cs | ForEach-Object { $_.FullName })
$logicDir = Join-Path $root "src\Logic"
if (Test-Path $logicDir) { $pureSources += @(Get-ChildItem $logicDir -Filter *.cs | ForEach-Object { $_.FullName }) }
$selfTest = Join-Path $root "tests\SelfTest.cs"
if ((-not $SkipTests) -and (Test-Path $selfTest)) {
    Write-Host "Building SelfTest ..."
    $stExe = Join-Path $obj "SelfTest.exe"
    Invoke-Csc $stExe "exe" (@($selfTest) + $pureSources) $refsPure $null
    Write-Host "Running SelfTest ..."
    & $stExe
    if ($LASTEXITCODE -ne 0) { throw "Self tests FAILED" }
    Write-Host "Self tests passed." -ForegroundColor Green
}

# ---------------- Main app ----------------
Write-Host "Building DeskMate.exe ..."
$appSources = @(Get-ChildItem (Join-Path $root "src") -Recurse -Filter *.cs | ForEach-Object { $_.FullName })
$appExe   = Join-Path $dist "DeskMate.exe"
$manifest = Join-Path $root "src\app.manifest"
$appExtra = @("/win32icon:$ico")
if (Test-Path $manifest) { $appExtra += "/win32manifest:$manifest" }
Invoke-Csc $appExe "winexe" $appSources $refsApp $appExtra
# sign before embedding: the installer carries this exact file as its payload
Invoke-Sign $appExe
Write-Host "OK -> $appExe" -ForegroundColor Green

# ---------------- Installer ----------------
$setupSrc = Join-Path $root "installer\Setup.cs"
if ((-not $SkipInstaller) -and (Test-Path $setupSrc)) {
    Write-Host "Building DeskMateSetup.exe ..."
    $setupExe = Join-Path $dist "DeskMateSetup.exe"
    $refsSetup = @("$fw\mscorlib.dll", "$fw\System.dll", "$fw\System.Core.dll",
                   "$fw\System.Drawing.dll", "$fw\System.Windows.Forms.dll")
    $setupExtra = @("/win32icon:$ico", "/resource:$appExe,DeskMate.Payload")
    if (Test-Path $manifest) { $setupExtra += "/win32manifest:$manifest" }
    # AppVersion.cs is compiled in too: one version source for app and installer alike.
    # SetupAssemblyInfo.cs turns that constant into real file metadata — without it the
    # installer shipped as 0.0.0.0 in Explorer's Details tab.
    Invoke-Csc $setupExe "winexe" @($setupSrc,
        (Join-Path $root "src\Core\AppVersion.cs"),
        (Join-Path $root "installer\SetupAssemblyInfo.cs")) $refsSetup $setupExtra
    Invoke-Sign $setupExe
    Write-Host "OK -> $setupExe" -ForegroundColor Green
}
# ---------------- UI smoke screenshots (optional) ----------------
$uiSmokeSrc = Join-Path $root "tests\UiSmoke.cs"
if ($UiSmoke -and (Test-Path $uiSmokeSrc)) {
    Write-Host "Building UiSmoke ..."
    $smokeExe = Join-Path $obj "UiSmoke.exe"
    Invoke-Csc $smokeExe "winexe" ($appSources + @($uiSmokeSrc)) $refsApp @("/main:UiSmoke", "/win32icon:$ico")
    $shots = Join-Path $obj "uishots"
    Write-Host "Capturing UI screenshots -> $shots"
    & $smokeExe $shots | Out-Host
    # the smoke run now reports every swallowed failure through its exit code
    if ($LASTEXITCODE -ne 0) { throw "UiSmoke FAILED ($LASTEXITCODE) - see FAIL lines above" }
}

Write-Host "Build complete." -ForegroundColor Green
