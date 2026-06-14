# BackupPilot distribution build
# Usage: .\build_dist.ps1
# Output: dist\v{version}\BackupPilot_Setup_v{version}.exe

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$csproj = Join-Path $root "BackupPilot.csproj"
$version = ([regex]::Match((Get-Content $csproj -Raw), '<Version>([^<]+)</Version>')).Groups[1].Value
if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Failed to read Version from BackupPilot.csproj."
}

$versionDir = Join-Path $root (Join-Path "dist" ("v" + $version))
$stagingDir = Join-Path $versionDir "win-x64"
$payloadZip = Join-Path $versionDir "payload.zip"
$setupExe = Join-Path $versionDir ("BackupPilot_Setup_v" + $version + ".exe")
$installerProject = Join-Path $root "installer\build\BackupPilotInstaller.csproj"
$installerOut = Join-Path $versionDir "installer_out"

Write-Host "Building BackupPilot v$version ..."

Stop-Process -Name BackupPilot -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

if (Test-Path $versionDir) {
    Remove-Item $versionDir -Recurse -Force
}
New-Item -ItemType Directory $versionDir | Out-Null

dotnet publish $csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Platform=x64 `
    -p:WindowsAppSDKSelfContained=true `
    -p:PublishSingleFile=false `
    -o $stagingDir
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Compress-Archive -Path (Join-Path $stagingDir "*") -DestinationPath $payloadZip -CompressionLevel Optimal -Force
Remove-Item $stagingDir -Recurse -Force

$installerPayload = Join-Path $root "installer\build\payload.zip"
Copy-Item $payloadZip $installerPayload -Force
dotnet publish $installerProject `
    -c Release `
    -p:AppVersion=$version `
    -o $installerOut
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$builtSetup = Join-Path $installerOut ("BackupPilot_Setup_v" + $version + ".exe")
Move-Item $builtSetup $setupExe -Force
Remove-Item $payloadZip -Force
Remove-Item $installerOut -Recurse -Force
Remove-Item $installerPayload -Force -ErrorAction SilentlyContinue

$distRoot = Join-Path $root "dist"
Get-ChildItem $distRoot -File -ErrorAction SilentlyContinue | Remove-Item -Force
Get-ChildItem $distRoot -Directory |
    Where-Object { $_.Name -notlike "v*" } |
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force }

$sizeMb = [math]::Round((Get-Item $setupExe).Length / 1MB, 1)
Write-Host "Done: $setupExe (${sizeMb} MB)"
