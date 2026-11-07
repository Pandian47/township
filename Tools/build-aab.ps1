# Builds the signed Google Play bundle (.aab) for TownBuilder.
# Run it yourself in PowerShell. Passwords are asked interactively, passed to Unity only through
# environment variables for this one process, and never written to disk.
#
#   cd D:\01-Projects\05-Township\TownBuilder
#   powershell -ExecutionPolicy Bypass -File .\Tools\build-aab.ps1
#
# Close the Unity Editor for this project first (batch mode can't open a project that is already open).

param(
    [string]$Keystore = "$env:USERPROFILE\keys\townbuilder-upload.keystore",
    [string]$Alias = "upload",
    [string]$Unity = "C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Unity.exe"
)

$ErrorActionPreference = "Stop"
$project = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path $Keystore)) { throw "Keystore not found: $Keystore  (see Documentation/AndroidRelease.md, step 1)" }
if (-not (Test-Path $Unity)) { throw "Unity not found: $Unity" }

function Read-Secret([string]$prompt) {
    $secure = Read-Host -AsSecureString $prompt
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr) }
}

$storePass = Read-Secret "Keystore password"
$keyPass = Read-Secret "Key password (press Enter if same as keystore password)"
if ([string]::IsNullOrEmpty($keyPass)) { $keyPass = $storePass }

$env:TOWNBUILDER_KEYSTORE = $Keystore
$env:TOWNBUILDER_KEYSTORE_PASS = $storePass
$env:TOWNBUILDER_KEY_ALIAS = $Alias
$env:TOWNBUILDER_KEY_PASS = $keyPass

New-Item -ItemType Directory -Force "$project\Logs" | Out-Null
$log = "$project\Logs\build-aab.log"
Write-Host "Building AAB... (log: $log)"
try {
    $p = Start-Process -FilePath $Unity -Wait -PassThru -ArgumentList @(
        "-batchmode", "-quit", "-projectPath", "`"$project`"",
        "-executeMethod", "TownBuilder.EditorTools.BuildTools.BuildAabFromCommandLine",
        "-logFile", "`"$log`"")
}
finally {
    Remove-Item Env:TOWNBUILDER_KEYSTORE_PASS, Env:TOWNBUILDER_KEY_PASS -ErrorAction SilentlyContinue
}

Select-String -Path $log -Pattern "\[Build\]" | ForEach-Object { $_.Line }
if ($p.ExitCode -eq 0) {
    Write-Host "Done. Upload this file to Google Play Console:" -ForegroundColor Green
    Get-ChildItem "$project\Builds\Android\*.aab" | Sort-Object LastWriteTime -Descending | Select-Object -First 1 | ForEach-Object { $_.FullName }
} else {
    Write-Host "Build failed (exit $($p.ExitCode)). See $log" -ForegroundColor Red
    exit 1
}
