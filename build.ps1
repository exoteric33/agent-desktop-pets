# Builds aipets.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
#   .\build.ps1                    compile (sprites come from pets\<id>\sprites\)
#   .\build.ps1 -OutputDirectory C:\Temp\aipets-build   isolated build, never stops or starts aipets
# A running aipets is stopped only after compilation succeeds and restarted after replacement.
param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$isolated = -not [string]::IsNullOrWhiteSpace($OutputDirectory)
if ($isolated) {
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    if ($OutputDirectory.TrimEnd('\') -eq $root.TrimEnd('\')) { throw 'OutputDirectory must differ from the source folder' }
    New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
} else { $OutputDirectory = $root }
$exe = Join-Path $OutputDirectory 'aipets.exe'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }   # 32-bit Windows
if (-not (Test-Path $csc)) { throw 'C# compiler of .NET Framework 4 not found (Windows feature ".NET Framework 4.8")' }
$candidate = Join-Path $OutputDirectory ('aipets-build-' + [guid]::NewGuid().ToString('N') + '.exe')
$running = @()
try {
    & $csc /nologo /target:winexe /optimize+ /warnaserror+ /codepage:65001 /utf8output `
        "/out:$candidate" "/win32icon:$root\art\aipets.ico" "/resource:$root\art\aipets.ico,aipets.ico" `
        /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
        "$root\src\*.cs"
    if ($LASTEXITCODE -ne 0) { throw 'csc failed; the installed executable was not changed' }
    if (-not $isolated) {
        # Stop the installed app only after a successful compilation.
        $running = @(Get-Process aipets -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })
        $running | Stop-Process -Force
        $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
    }
    # [NullString]::Value: PowerShell would pass $null as "" (invalid backup path)
    if ([IO.File]::Exists($exe)) { [IO.File]::Replace($candidate, $exe, [NullString]::Value) }
    else { [IO.File]::Move($candidate, $exe) }
    Write-Host "built $exe"
} finally {
    if (Test-Path -LiteralPath $candidate) { Remove-Item -LiteralPath $candidate }
    if ($running.Count -gt 0 -and (Test-Path -LiteralPath $exe)) {
        # Through explorer, so the tray does not inherit this shell's environment.
        Start-Process explorer.exe "`"$exe`"" -WindowStyle Hidden
        Write-Host 'aipets restarted'
    }
}
