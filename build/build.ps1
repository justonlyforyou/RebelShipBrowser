# Build script for RebelShip Browser
# Usage: .\build\build.ps1
# Version is read from VERSION file in root directory

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $RootDir "publish"
$AppDir = Join-Path $PublishDir "app"
$InstallerDir = Join-Path $PublishDir "installer"

# Read version from VERSION file
$VersionFile = Join-Path $RootDir "VERSION"
if (-not (Test-Path $VersionFile)) {
    Write-Host "ERROR: VERSION file not found at $VersionFile" -ForegroundColor Red
    exit 1
}
$Version = (Get-Content $VersionFile -Raw).Trim()

if ([string]::IsNullOrEmpty($Version)) {
    Write-Host "ERROR: VERSION file is empty" -ForegroundColor Red
    exit 1
}

Write-Host "Building RebelShip Browser v$Version" -ForegroundColor Cyan
Write-Host "=======================================" -ForegroundColor Cyan

# Clean
Write-Host "`n[1/5] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $AppDir -Force | Out-Null
New-Item -ItemType Directory -Path $InstallerDir -Force | Out-Null

# Restore
Write-Host "`n[2/5] Restoring dependencies..." -ForegroundColor Yellow
dotnet restore "$RootDir\RebelShipBrowser.sln"

# Build Main Application
Write-Host "`n[3/5] Building main application..." -ForegroundColor Yellow
dotnet publish "$RootDir\src\RebelShipBrowser\RebelShipBrowser.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -o $AppDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Create payload zip
Write-Host "`n[4/5] Creating installer payload..." -ForegroundColor Yellow
$PayloadDir = Join-Path $RootDir "src\RebelShipBrowser.Installer\Resources"
if (-not (Test-Path $PayloadDir)) {
    New-Item -ItemType Directory -Path $PayloadDir -Force | Out-Null
}
$PayloadZip = Join-Path $PayloadDir "app-payload.zip"
Compress-Archive -Path "$AppDir\*" -DestinationPath $PayloadZip -Force

# Build Installer
Write-Host "`n[5/5] Building installer..." -ForegroundColor Yellow
dotnet publish "$RootDir\src\RebelShipBrowser.Installer\RebelShipBrowser.Installer.csproj" `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version `
    -p:AssemblyVersion="$Version.0" `
    -p:FileVersion="$Version.0" `
    -o $InstallerDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Installer build failed!" -ForegroundColor Red
    exit 1
}

# Rename
$FinalName = "RebelShipBrowser-Setup-v$Version.exe"
Move-Item "$InstallerDir\Setup.exe" "$PublishDir\$FinalName" -Force

Write-Host "`n=======================================" -ForegroundColor Cyan
Write-Host "Build complete!" -ForegroundColor Green
Write-Host "Output: $PublishDir\$FinalName" -ForegroundColor Green
