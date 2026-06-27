# FluidScroll

FluidScroll is a background service for Windows that provides smooth, inertial scrolling for mouse wheels.

## Features

- **Smooth Scrolling:** Adds inertial physics to standard mouse wheel events.
- **Acceleration:** Adaptive scroll speed based on wheel velocity.
- **Horizontal Support:** Works with horizontal wheel events.
- **Lightweight:** Runs as a background process with no UI.
- **Single Instance:** Only one instance runs at a time.
- **Auto-Startup:** Automatically adds itself to Windows startup on first run.

## Usage

Install `FluidScrollSetup.exe` from the latest GitHub Release. FluidScroll runs in the background and appears as a tray icon in the notification area. Right-click the tray icon to open settings, toggle auto-start, or exit.

## WinGet

After a release is published with the installer asset, FluidScroll can be installed with:

```powershell
winget install --id alf16d.FluidScroll -e
```

Uninstall with:

```powershell
winget uninstall --id alf16d.FluidScroll -e
```
