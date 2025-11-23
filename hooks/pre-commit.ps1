#Requires -Version 5.1
<#
.SYNOPSIS
    Pre-commit hook for RebelShip Browser
.DESCRIPTION
    Runs linting and security checks before allowing commits
#>

$ErrorActionPreference = "Continue"

Write-Host ""
Write-Host "Running pre-commit checks..." -ForegroundColor Cyan
Write-Host ""

# Get the root directory
$RootDir = git rev-parse --show-toplevel
Set-Location $RootDir

$Failed = $false

# ==========================================
# Build with security analyzers
# ==========================================
Write-Host "==========================================" -ForegroundColor White
Write-Host "  Building with analyzers enabled..." -ForegroundColor White
Write-Host "==========================================" -ForegroundColor White

# Security rules to treat as errors
$SecurityErrors = @(
    "CA5389", "CA5390", "CA5391", "CA5392", "CA5393", "CA5394", "CA5395",
    "CA5396", "CA5397", "CA5398", "CA5399", "CA5400", "CA5401", "CA5402", "CA5403",
    "CA3001", "CA3002", "CA3003", "CA3004", "CA3005", "CA3006", "CA3007",
    "CA3008", "CA3009", "CA3010", "CA3011", "CA3012", "CA2100"
) -join ","

$BuildOutput = dotnet build RebelShipBrowser.sln -warnaserror:$SecurityErrors 2>&1
$BuildExitCode = $LASTEXITCODE

if ($BuildExitCode -ne 0) {
    Write-Host "BUILD FAILED" -ForegroundColor Red
    Write-Host $BuildOutput
    $Failed = $true
} else {
    Write-Host "Build successful" -ForegroundColor Green

    # Check for security warnings
    $SecurityWarnings = $BuildOutput | Where-Object { $_ -match "(CA5[0-9]{3}|CA3[0-9]{3}|SCS[0-9]{4})" -and $_ -match "warning" }
    if ($SecurityWarnings) {
        Write-Host ""
        Write-Host "Security warnings found:" -ForegroundColor Yellow
        $SecurityWarnings | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
        Write-Host ""
        Write-Host "Review these warnings before committing." -ForegroundColor Yellow
    }
}

# ==========================================
# Check for sensitive data
# ==========================================
Write-Host ""
Write-Host "==========================================" -ForegroundColor White
Write-Host "  Checking for sensitive data..." -ForegroundColor White
Write-Host "==========================================" -ForegroundColor White

$StagedFiles = git diff --cached --name-only --diff-filter=ACM

if ($StagedFiles) {
    $SecretPatterns = "password|secret|api_key|apikey|access_token|auth_token|credentials|private_key"

    foreach ($File in $StagedFiles) {
        if (Test-Path $File) {
            # Skip binary files
            if ($File -match "\.(exe|dll|pdb|ico|png|jpg|gif)$") {
                continue
            }

            $Content = Get-Content $File -ErrorAction SilentlyContinue
            $Matches = $Content | Select-String -Pattern $SecretPatterns -AllMatches | Select-Object -First 5

            if ($Matches) {
                Write-Host "Potential secrets in $File`:" -ForegroundColor Yellow
                $Matches | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
                Write-Host ""
            }
        }
    }
}

Write-Host "Sensitive data check complete" -ForegroundColor Green

# ==========================================
# Check for forbidden patterns
# ==========================================
Write-Host ""
Write-Host "==========================================" -ForegroundColor White
Write-Host "  Checking for forbidden patterns..." -ForegroundColor White
Write-Host "==========================================" -ForegroundColor White

$ForbiddenFound = $false

foreach ($File in $StagedFiles) {
    if ((Test-Path $File) -and ($File -match "\.cs$")) {
        $Content = Get-Content $File -Raw -ErrorAction SilentlyContinue

        # Check for direct HTTP client usage
        if ($Content -match "new HttpClient|new WebClient|WebRequest\.Create") {
            Write-Host "Forbidden: Direct HTTP client usage in $File" -ForegroundColor Red
            $ForbiddenFound = $true
        }

        # Check for hardcoded URLs (except allowed)
        $UrlMatches = [regex]::Matches($Content, 'https?://[^"'' ]+')
        foreach ($Match in $UrlMatches) {
            $Url = $Match.Value
            if ($Url -notmatch "shippingmanager\.cc" -and
                $Url -notmatch "github\.com/justonlyforyou" -and
                $Url -notmatch "schemas\.microsoft\.com" -and
                $Url -notmatch "learn\.microsoft\.com") {
                Write-Host "Review: URL found in $File`: $Url" -ForegroundColor Yellow
            }
        }
    }
}

if ($ForbiddenFound) {
    $Failed = $true
}

Write-Host "Pattern check complete" -ForegroundColor Green

# ==========================================
# Result
# ==========================================
Write-Host ""
Write-Host "==========================================" -ForegroundColor White

if ($Failed) {
    Write-Host "Pre-commit checks FAILED" -ForegroundColor Red
    Write-Host "Please fix the issues above before committing." -ForegroundColor Red
    exit 1
} else {
    Write-Host "All pre-commit checks passed!" -ForegroundColor Green
    exit 0
}
