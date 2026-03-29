# Log Launcher

Launch tree logs and item stacks with a hotkey. Point at a log or ground item, press T to hoist it over your shoulder, then press T again to send it flying.

## Features
- Two-stage throw — first press grabs, second press launches
- Works on tree logs and item stacks on the ground
- Configurable launch force, range, and upward angle
- Protects you from launching while in menus, chats, or shops

## Configuration
All settings are available in the BepInEx Configuration Manager (F1 in-game).

| Setting | Default | Description |
|---|---|---|
| LaunchHotkey | T | Grab and launch key |
| LaunchForce | 800 | How hard the object is launched |
| LaunchRange | 10 | Max grab distance in meters |
| UpwardAngle | 15 | Degrees upward added to launch direction |
| EnableLogs | true | Allow launching tree logs |
| EnableItemStacks | true | Allow launching ground item stacks |

## Changelog
See CHANGELOG.md
```

**README.txt** (Nexus)
```
Log Launcher
============
Launch tree logs and item stacks with a hotkey.

Point at a log or ground item, press T to hoist it over your shoulder,
then press T again to send it flying.

INSTALLATION
------------
1. Install BepInEx for Valheim (BepInExPack_Valheim)
2. Drop LogLauncher.dll into BepInEx/plugins/

CONFIGURATION
-------------
All settings are in BepInEx/config/com.custom.loglauncher.cfg
or use the BepInEx Configuration Manager in-game (F1).

LaunchHotkey   - Default: T
LaunchForce    - Default: 800
LaunchRange    - Default: 10 meters
UpwardAngle    - Default: 15 degrees
EnableLogs     - Default: true
EnableItemStacks - Default: true