# Builds ClaudePet.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
#   .\build.ps1        compile (sprites from art\build\)
#   .\build.ps1 -Art   regenerate the sprites first (Python with numpy + Pillow)
param([switch]$Art)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if ($Art) {
    python "$root\art\make_sprites.py"
    if ($LASTEXITCODE -ne 0) { throw 'make_sprites.py failed' }
}

# a running pet locks the exe
Get-Process ClaudePet -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq "$root\ClaudePet.exe" } | Stop-Process -Force

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $csc /nologo /target:winexe /optimize+ /codepage:65001 /utf8output `
    "/out:$root\ClaudePet.exe" "/win32icon:$root\art\build\icon.ico" `
    "/resource:$root\art\build\atlas.png,ClaudePet.atlas.png" `
    "/resource:$root\art\build\atlas.txt,ClaudePet.atlas.txt" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "$root\src\*.cs"
if ($LASTEXITCODE -ne 0) { throw 'csc failed' }
Write-Host "built $root\ClaudePet.exe"
