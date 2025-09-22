WindowsLayoutMaster
===================

Ever switch monitor configs on your laptop and all of your windows are squished down in size and in the wrong position? Ever want to minimize all windows but save their layouts and min-max'ed states for later?

WindowsLayoutMaster is a modern .NET 8 Windows system tray application that remembers and restores window positions with persistent storage.

## Usage

1. Run `WindowsLayoutMaster.exe` - it will appear in your system tray
2. Right-click the tray icon to access the menu
3. Use "Take Snapshot" to manually save your current window layout
4. Hover over any snapshot in the menu to preview/restore it
5. Snapshots are automatically saved every 30 minutes


## Running

To run WindowsLayoutMaster, simply double-click the `WindowsLayoutMaster.exe.lnk` shortcut in the root folder. This launches the executable located in the `program/` folder and the app will appear in your system tray.

There's no installer; just run WindowsLayoutMaster.exe or put it in your Startup folder to have it start automatically.

## Project Structure

- `source/` - Contains all source code and development files
- `program/` - Contains the final executable ready for distribution


## Build (optional, for developers)

If you want to modify the source code and rebuild the application:

```powershell
cd source
dotnet build WindowsLayoutMaster.csproj
```

The build automatically outputs to the `program/` folder for immediate use.

## Distribution

The final executable is located in the `program/` folder:
- `WindowsLayoutMaster.exe` - Main executable (use the shortcut in root folder)
- `WindowsLayoutMaster.dll` - Application assembly  
- `WindowsLayoutMaster.runtimeconfig.json` - Runtime configuration


## Features

- **Persistent Storage**: Snapshots are saved to disk and survive app restarts
- **System Tray Interface**: Runs quietly in the background with right-click menu access
- **Automatic Snapshots**: Takes snapshots every 30 minutes automatically
- **Manual Snapshots**: Take snapshots on-demand via the tray menu
- **Preview on Hover**: Preview layouts by hovering over snapshot menu items
- **Single Instance**: Prevents multiple instances from running simultaneously
- **Custom Icon**: Clean, modern icon design
- **Multi-Monitor Support**: Full support for complex multi-monitor setups

## Requirements

- .NET 8.0 Runtime (Windows)
- Windows 10 or later



## Storage Location

Snapshots are stored in: `%APPDATA%\WindowsLayoutMaster\snapshots.json`

## Requirements

- .NET 8.0 Runtime (Windows)
- Windows 10 or later


#### Tips
* When there are many stored snapshots, all very recent ones are shown, plus more spaced-out but distant past snapshots.
* Automatically taken snapshots are shown in normal text. Manually taken snapshots are shown in **bold**.
* As you mouse over each snapshot, it is restored, so it's easy to find the layout you want.
* Snapshots keep track of, and restore, the "normal size" of windows even if they're currently minimized or maximized.
* This app makes sure all windows fit inside a currently-visible display when restoring snapshots. Because of that, if you ever have a window that's off-screen because of a bug in other software, just restore the most recent snapshot.
* Snapshots are now **persistent** - they survive app restarts and system reboots
* Automatic snapshots are saved every 30 minutes, with intelligent spacing for older snapshots


#### License
This app and its source code are released into the public domain.