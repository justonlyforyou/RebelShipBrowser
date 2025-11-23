@echo off
REM Install git hooks for RebelShip Browser
REM Run this script from the repository root

echo Installing git hooks...

REM Check if we're in a git repository
if not exist ".git" (
    echo Error: Not in a git repository root. Please run from the repository root.
    pause
    exit /b 1
)

REM Create hooks directory if it doesn't exist
if not exist ".git\hooks" mkdir ".git\hooks"

REM Create the pre-commit hook that calls PowerShell
(
echo #!/bin/sh
echo # Pre-commit hook - calls PowerShell script
echo powershell.exe -ExecutionPolicy Bypass -NoProfile -File "./hooks/pre-commit.ps1"
echo exit $?
) > ".git\hooks\pre-commit"

echo.
echo Git hooks installed successfully!
echo.
echo The pre-commit hook will now run before each commit to:
echo   - Build the solution with security analyzers
echo   - Check for potential secrets in staged files
echo   - Check for forbidden patterns (unauthorized network calls)
echo.
pause
