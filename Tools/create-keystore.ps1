# Creates the Google Play UPLOAD key for TownBuilder (run once).
# keytool asks for the password and your details interactively; nothing is stored in the project.
#
#   powershell -ExecutionPolicy Bypass -File .\Tools\create-keystore.ps1
#
# BACK UP the resulting .keystore file and its password (password manager + offline copy).

param(
    [string]$Keystore = "$env:USERPROFILE\keys\townbuilder-upload.keystore",
    [string]$Alias = "upload"
)

$ErrorActionPreference = "Stop"
$keytool = "C:\Program Files\Unity\Hub\Editor\6000.6.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\OpenJDK\bin\keytool.exe"

if (Test-Path $Keystore) {
    Write-Host "A keystore already exists at $Keystore - not overwriting it." -ForegroundColor Yellow
    exit 0
}

New-Item -ItemType Directory -Force (Split-Path $Keystore) | Out-Null
Write-Host ""
Write-Host "Creating upload key: $Keystore  (alias: $Alias)" -ForegroundColor Cyan
Write-Host "1) Choose a strong password (min 6 chars). Typing is invisible - that's normal."
Write-Host "2) Answer the name/organisation questions (any honest values; 2-letter country code e.g. IN)."
Write-Host "3) Type 'yes' to confirm."
Write-Host ""

& $keytool -genkeypair -v -keystore $Keystore -alias $Alias -keyalg RSA -keysize 2048 -validity 10000

if ($LASTEXITCODE -eq 0 -and (Test-Path $Keystore)) {
    Write-Host ""
    Write-Host "Keystore created: $Keystore" -ForegroundColor Green
    Write-Host "Back it up now, together with the password. Losing it blocks future Play Store updates until Google resets the key."
} else {
    Write-Host "Keystore was not created." -ForegroundColor Red
    exit 1
}
