# Build script for RebelShip Browser
# Usage: .\build\build.ps1
# Version is read from VERSION file in root directory

$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $RootDir "publish"
$AppDir = Join-Path $PublishDir "app"
$InstallerDir = Join-Path $PublishDir "installer"

# GitHub repo for userscripts
$GitHubRepo = "justonlyforyou/shippingmanager_user_scripts"
$GitHubApiUrl = "https://api.github.com/repos/$GitHubRepo/contents"
$GitHubRawUrl = "https://raw.githubusercontent.com/$GitHubRepo/main"

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
Write-Host "`n[1/6] Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $AppDir -Force | Out-Null
New-Item -ItemType Directory -Path $InstallerDir -Force | Out-Null

# Fetch userscripts from GitHub
Write-Host "`n[2/6] Fetching userscripts from GitHub..." -ForegroundColor Yellow
$BundledDir = Join-Path $RootDir "userscripts\bundled"

# Clear existing bundled scripts
if (Test-Path $BundledDir) {
    Remove-Item "$BundledDir\*.js" -Force -ErrorAction SilentlyContinue
}
if (-not (Test-Path $BundledDir)) {
    New-Item -ItemType Directory -Path $BundledDir -Force | Out-Null
}

# Fetch file list from GitHub API
try {
    $Headers = @{ "User-Agent" = "RebelShipBrowser-Build" }
    $Response = Invoke-RestMethod -Uri $GitHubApiUrl -Headers $Headers -Method Get

    $ScriptCount = 0
    foreach ($File in $Response) {
        if ($File.name -like "*.user.js") {
            $DownloadUrl = "$GitHubRawUrl/$($File.name)"
            $DestPath = Join-Path $BundledDir $File.name

            Write-Host "  Downloading: $($File.name)" -ForegroundColor Gray
            Invoke-WebRequest -Uri $DownloadUrl -OutFile $DestPath -Headers $Headers
            $ScriptCount++
        }
    }

    Write-Host "  Downloaded $ScriptCount userscripts from GitHub" -ForegroundColor Green
} catch {
    Write-Host "  WARNING: Failed to fetch scripts from GitHub: $_" -ForegroundColor Yellow
    Write-Host "  Using existing local scripts" -ForegroundColor Yellow
}

# Restore
Write-Host "`n[3/6] Restoring dependencies..." -ForegroundColor Yellow
dotnet restore "$RootDir\RebelShipBrowser.sln"

# Build Main Application
Write-Host "`n[4/6] Building main application..." -ForegroundColor Yellow
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
Write-Host "`n[5/6] Creating installer payload..." -ForegroundColor Yellow

# Copy userscripts to app folder
$UserScriptsSource = Join-Path $RootDir "userscripts"
$UserScriptsDest = Join-Path $AppDir "userscripts"
if (Test-Path $UserScriptsSource) {
    Copy-Item -Path $UserScriptsSource -Destination $UserScriptsDest -Recurse -Force
    Write-Host "  Copied userscripts folder" -ForegroundColor Gray
}

$PayloadDir = Join-Path $RootDir "src\RebelShipBrowser.Installer\Resources"
if (-not (Test-Path $PayloadDir)) {
    New-Item -ItemType Directory -Path $PayloadDir -Force | Out-Null
}
$PayloadZip = Join-Path $PayloadDir "app-payload.zip"
Compress-Archive -Path "$AppDir\*" -DestinationPath $PayloadZip -Force

# Build Installer
Write-Host "`n[6/6] Building installer..." -ForegroundColor Yellow
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
