# build_win.ps1 — Build distributable Windows client from Content/
# Usage: .\client\build_win.ps1 [-Server "ip"] [-Release]
# Prerequisites: run build_content.py first
param(
    [string]$Server,
    [switch]$Release
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$ContentDir = "$RepoRoot\GameFiles"
$OutDir = "$RepoRoot\SpellBinder-win"
$ZipName = "$RepoRoot\SpellBinder-win.zip"

# --- Validate ---
if (-not (Test-Path "$ContentDir\game.dll")) {
    Write-Error "GameFiles/ not found. Run 'python build_content.py' first."
    exit 1
}

# --- Compile Play.exe ---
Write-Host "=== Compiling Play.exe ==="
$csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $csc)) {
    Write-Error "csc.exe not found. Install .NET Framework 4.8."
}

Push-Location $ScriptDir
& $csc /target:winexe /out:Play.exe `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll `
    Play.cs
if ($LASTEXITCODE -ne 0) { Pop-Location; Write-Error "Compile failed"; exit 1 }
Pop-Location

# --- Assemble ---
Write-Host "=== Assembling $OutDir ==="
if (Test-Path $OutDir) { Remove-Item -Recurse -Force $OutDir }
New-Item -ItemType Directory -Path "$OutDir\game" | Out-Null

# Play.exe at root
Copy-Item "$ScriptDir\Play.exe" "$OutDir\Play.exe"

# Game files into game/ (skip installer/RE artifacts)
$skip = @("manifest.json", "spells.json", "spells_summary.txt", "UNWISE.EXE",
          "spell.exe", "game.dll.orig", "game.dll.clean")
Get-ChildItem $ContentDir | Where-Object { $skip -notcontains $_.Name } | ForEach-Object {
    Copy-Item -Recurse $_.FullName "$OutDir\game\$($_.Name)"
}

# dgVoodoo DLLs + config
if (Test-Path "$ScriptDir\dgvoodoo") {
    Get-ChildItem "$ScriptDir\dgvoodoo" | ForEach-Object {
        Copy-Item $_.FullName "$OutDir\game\$($_.Name)" -Force
    }
}
if (Test-Path "$ScriptDir\dgVoodoo.conf") {
    Copy-Item "$ScriptDir\dgVoodoo.conf" "$OutDir\game\dgVoodoo.conf" -Force
}

# Default keybinds
if (Test-Path "$ScriptDir\defaults") {
    Get-ChildItem "$ScriptDir\defaults" | ForEach-Object {
        Copy-Item $_.FullName "$OutDir\game\$($_.Name)" -Force
    }
}

# Set server address
if ($Server) {
    Write-Host "Setting server to $Server"
    $mainDat = "$OutDir\game\main.dat"
    if (Test-Path $mainDat) {
        (Get-Content $mainDat -Raw) -replace "address=.*", "address=$Server" | Set-Content $mainDat -NoNewline
    }
}

Write-Host ""
Write-Host "=== Built $OutDir ==="
Write-Host "  Play.exe   (double-click to play)"
Write-Host "  game\      (game files + dgVoodoo)"

if ($Release) {
    Write-Host ""
    Write-Host "=== Creating zip ==="
    if (Test-Path $ZipName) { Remove-Item $ZipName }
    Compress-Archive -Path $OutDir -DestinationPath $ZipName
    $size = [math]::Round((Get-Item $ZipName).Length / 1MB, 1)
    Write-Host "Created $ZipName ($size MB)"
}
