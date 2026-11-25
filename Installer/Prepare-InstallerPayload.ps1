param()

$ErrorActionPreference = 'Stop'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = 'true'
$env:DOTNET_NOLOGO = 'true'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = 'true'

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectFile = Join-Path $projectRoot 'LightBulb\LightBulb.csproj'
$payloadDir = Join-Path $projectRoot 'artifacts\InstallerPayload'
$installedMarker = Join-Path $PSScriptRoot '.installed'
$licenseFile = Join-Path $projectRoot 'License.txt'

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET SDK was not found. Install the .NET 10 SDK and compile the installer again.'
}

if (-not (Test-Path $projectFile)) {
    throw "LightBulb project was not found: $projectFile"
}

if (Test-Path $payloadDir) {
    Remove-Item $payloadDir -Recurse -Force
}
New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null

Write-Host 'Publishing LightBulb 2.7.1-forked-0.21 (win-x64, self-contained)...'
& dotnet publish $projectFile `
    -p:CSharpier_Bypass=true `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $payloadDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Copy-Item $installedMarker (Join-Path $payloadDir '.installed') -Force
Copy-Item $licenseFile (Join-Path $payloadDir 'License.txt') -Force

$exe = Join-Path $payloadDir 'LightBulb.exe'
if (-not (Test-Path $exe)) {
    throw "Published payload is incomplete: $exe was not generated."
}

Write-Host "Installer payload ready: $payloadDir"
