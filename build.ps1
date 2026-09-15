# Builds aipets.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
#   .\build.ps1                    compile (sprites come from pets\<id>\sprites\)
#   .\build.ps1 -Art               regenerate the app icon and all pet sprites first (Python with numpy + Pillow)
#   .\build.ps1 -Art -Pet hermes   only this pet's sprites
# A running aipets from this folder is stopped for the build and started again afterwards.
param([switch]$Art, [string[]]$Pet)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$exe = Join-Path $root 'aipets.exe'

if ($Art) {
    if (-not $Pet) {
        python "$root\art\app_icon.py"
        if ($LASTEXITCODE -ne 0) { throw 'app_icon.py failed' }
    }
    foreach ($dir in Get-ChildItem "$root\pets" -Directory) {
        if ($Pet -and $Pet -notcontains $dir.Name) { continue }
        python "$($dir.FullName)\art\make_sprites.py"
        if ($LASTEXITCODE -ne 0) { throw "make_sprites.py failed for $($dir.Name)" }
    }
}

# the tray and its pets lock the exe
$running = @(Get-Process aipets -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })
$running | Stop-Process -Force
$running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }   # 32-bit Windows
if (-not (Test-Path $csc)) { throw 'C# compiler of .NET Framework 4 not found (Windows feature ".NET Framework 4.8")' }
& $csc /nologo /target:winexe /optimize+ /codepage:65001 /utf8output `
    "/out:$exe" "/win32icon:$root\art\aipets.ico" "/resource:$root\art\aipets.ico,aipets.ico" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "$root\src\*.cs"
if ($LASTEXITCODE -ne 0) { throw 'csc failed' }
Write-Host "built $exe"

if ($running.Count -gt 0) {
    # through explorer, so the tray does not inherit this shell's environment
    Start-Process explorer.exe "`"$exe`""
    Write-Host 'aipets restarted'
}
