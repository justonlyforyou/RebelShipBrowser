<img src="logo.png" width="100" height="100" alt="RebelShip Browser Logo">

# RebelShip Browser

This tiny tool is gift to all my friends and steam players of the game [Shippingmanager.cc](https://shippingmanager.cc)
to provide a possiblity playing the Steam-Version in a browser without the stupid T-Stroke bug.
So you can rename everthing or chat with others again, without any problems.

## Features

- **Auto-Login**: Extracts session cookie from Steam and logs you in automatically
- **System Tray**: Minimizes to tray, runs in background
- **Quick Access**: Double-click tray icon to restore window
- **Re-Login**: Easy re-extraction of session if needed

## Requirements

- Windows 10/11
- .NET 8.0 Runtime (included in installer)
- Steam installed and logged into shippingmanager.cc

## How it Works

**Pre-Task**: Start Steam and start Shippingmanager once and close it
afterwards the login was successful. This pre-task is required so Steam
is able to store your Auth-Cookie.

1. On startup, the app extracts your session Auth-Cookie from Steam's browser cache
2. Steam is temporarily stopped if running (then restarted)
3. The cookie is injected into the built-in browser before loading the page
4. You are automatically logged in - no manual login required
5. Exit deletes all cookies and cache

## What not does

1. Does not talk to somewhere else, execpt shippingmanager.cc and their related URL's!
2. Does not enable AD's people have on the mobile app version!
3. Does not enable any other feature!

Just play your game in a browser without the bugs you're faced
with while using the official steam version provided by Trophy Games.

## Installation

1. Download the latest `RebelShipBrowser-Setup-vX.X.X.exe` from Releases
2. Run the installer
3. Launch from Start Menu or Desktop shortcut

## Building from Source

### Prerequisites

- .NET 8.0 SDK
- Windows 10/11

### Build

```powershell
# Clone the repository
git clone https://github.com/your-repo/rebelship-browser.git
cd rebelship-browser

# Build using the build script (reads version from VERSION file)
.\build\build.ps1

# Or build manually (version will be 0.0.0)
dotnet build RebelShipBrowser.sln -c Release
```

### Output

- `publish/RebelShipBrowser-Setup-vX.X.X.exe` - Installer
- `publish/app/` - Application files

## License

[Licence](.\LICENSE)

## Screenshot
<img src="screenshot.png" width="100%" alt="RebelShip Browser Screenshot">
