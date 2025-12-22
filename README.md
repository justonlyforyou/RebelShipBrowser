<img src="logo.png" width="100" height="100" alt="RebelShip Browser Logo">

# 🏴‍☠️ The RebelShip Browser 🏴‍☠️

**Avast, ye scurvy dogs!** This tiny tool be a treasure gifted to all me hearties and Steam-sailors of the game **Shippingmanager.cc**. It lets ye play the Steam version in a browser without that cursed **T-Stroke bug** (blimey!). Now ye can finally christen yer ships proper-like or parley with other captains in the chat without yer quill breakin' or the ink runnin' dry!

### 💎 The Booty (Features)

* **Auto-Login (Boarding Party):** We plunder the session cookie straight from Steam and get ye aboard automatically. No secret codes needed!
* **System Tray (The Crow's Nest):** Keeps a weather eye open in the background. Minimizes to the tray so it don't clutter yer deck.
* **Quick Access:** Double-tap the icon to bring her about and restore the window in a flash.
* **Re-Login:** If the seas get rough, we grab the key again. Easy peasy.
* **Premium Map Themes:** All premium map styles unlocked - Dark, Light, Street, Satellite, City, and Sky. No doubloons required!
* **Tanker Operations:** Build tankers even without the achievement. The seas be open to all cargo types!
* **Metropolis Routes:** Access metropolis port routes without purchase. Chart yer course to the biggest harbors!

### 📜 The Ship’s Articles (Requirements)

* A sturdy hull running **Windows 10/11**.
* The right rigging: **.NET 8.0 Runtime** (we packed it in the crate for ye).
* **Steam** must be docked (installed) and ye must have sailed **shippingmanager.cc** at least once.

### 🧭 How to Navigate (How it Works)

**Before we weigh anchor:**
Fire up Steam and launch Shippingmanager *once*. Close it down after ye see the harbor (successful login). We need Steam to stash yer **Auth-Cookie** in the chest first!

1.  **On Startup:** The app raids Steam’s browser cache to find yer shiny session cookie.
2.  **The Switch:** We briefly keelhaul Steam (stop it) and hoist it back up.
3.  **Injection:** We slip that cookie into our browser like a thief in the night.
4.  **Full Sail:** Ye be logged in automatically—no need to lift a finger!
5.  **Abandon Ship (Exit):** Closing the app scrubs the deck clean. All cookies and cache are sent to Davy Jones' locker.

### 🚫 The Pirate Code (What it DOESN'T do)

* **No Blabberin':** We don't talk to foreign powers! We only signal to `shippingmanager.cc` and their related URL's - everything stays on your computer!
* **No Sirens with Loot**: Sadly, we can't lure those mobile ads aboard this vessel. That means no watching ads for free bonus points here. Ye’ll have to earn yer gold the hard way, alas!
* **No Black Magic:** No cheats, no extra cannons.
* **Just Smooth Sailing:** Play yer game in a browser without the barnacles and bugs ye faced using the official Steam tub provided by Trophy Games. Yarrr! 🦜

## 🔨 Raising the Flag (Installation)

1.  Snatch the latest `RebelShipBrowser-Setup-vX.X.X.exe` from the [Releases](https://github.com/justonlyforyou/RebelShipBrowser/releases/) cove.
2.  Crack open the installer keg (Run it).
3.  Grab the helm from the Start Menu or yer Desktop shortcut.

## ⚔️ Forging Yer Own Ship (Building from Source)

### 🧰 Shipwright Tools (Prerequisites)

- **.NET 8.0 SDK** (Essential tools for the trade)
- **Windows 10/11** (A proper shipyard)

### 🏗️ Constructing the Vessel (Build)

```powershell
# Hijack the blueprints (Clone the repository)
git clone https://github.com/justonlyforyou/RebelShipBrowser.git
cd RebelShipBrowser

# Hammer the ship together using the script (reads version from VERSION file)
.\build\build.ps1

# Or build it by hand like a true craftsman (version will be 0.0.0)
dotnet build RebelShipBrowser.sln -c Release
```

## Questions or Problems?

Join the [Discord](https://discord.gg/rw4yKxp7Mv)


## License

[Licence](.\LICENSE)

## Screenshot
<img src="screenshot.png" width="100%" alt="RebelShip Browser Screenshot">
