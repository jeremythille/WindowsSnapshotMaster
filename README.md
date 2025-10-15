Simple utility that:

- Memorizes all your programs locations and sizes
- Allows to restore them if they have moved

## Features

WindowsLayoutMaster is a modern .NET 8 Windows system tray utility that remembers and restores window positions, with persistent storage.

- **Persistent Storage**: Snapshots are saved to disk (in a JSON file) and survive app restarts
- **System Tray Interface**: Runs quietly in the background with left-click menu access
- **Automatic Snapshots**: Takes snapshots every 30 minutes automatically
- **Manual Snapshots**: Take snapshots on-demand via the tray menu
- **Preview on Hover**: Preview layouts by hovering over snapshot menu items
- **Single Instance**: Prevents multiple instances from running simultaneously
- **Multi-Monitor Support**: Full support for complex multi-monitor setups, as well as virtual desktops

## Usage

1. Run `WindowsLayoutMaster.cmd` (or `./program/WindowsLayoutMaster.exe`) - it will then appear in your system tray.
There's no installer; just run `WindowsLayoutMaster.cmd` or put it in your Startup folder to have it start automatically.

	### Note: 
	Windows might ask you to install the Microsoft .NET 8.0 Runtime, please accept, as it is required by the program to run.

2. Click the tray icon to access the menu
3. Use "Take Snapshot" to manually save your current window layout
4. Hover over any snapshot in the menu to preview/restore it
5. Snapshots are automatically saved every 30 minutes



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


## Requirements

- .NET 8.0 Runtime (Windows)
- Windows 10 or later



## Storage Location

Snapshots are stored in `program/snapshots.json` (in the same folder as the executable)

## Requirements

- .NET 8.0 Runtime (Windows)
- Windows 10 or later


#### Tips
* When there are many stored snapshots, all very recent ones are shown, plus more spaced-out but distant past snapshots.
* Automatically taken snapshots are shown in normal text. Manually taken snapshots are shown in **bold**.
* As you mouse over each snapshot, it is restored, so it's easy to find the layout you want.
* Snapshots keep track of, and restore, the "normal size" of windows even if they're currently minimized or maximized.
* This app makes sure all windows fit inside a currently-visible display when restoring snapshots. Because of that, if you ever have a window that's off-screen because of a bug in other software, just restore the most recent snapshot.
* Snapshots are **persistent** - they survive app restarts and system reboots
* Automatic snapshots are saved every 30 minutes, with intelligent spacing for older snapshots


#### License
This app and its source code are released into the public domain.