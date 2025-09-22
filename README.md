WindowsLayoutSnapshot
=====================

Ever switch monitor configs on your laptop and all of your windows are squished down in size and in the wrong position?  Ever want to minimize all windows but save their layouts and min-max'ed states for later?

WindowsLayoutSnapshot is a modern .NET 8 Windows app to remember and restore window positions.

## Building and Running

To build the application:
```bash
dotnet build
```

To run the application:
```bash
dotnet run
```

The built executable will be located at `bin/Debug/net8.0-windows/WindowsLayoutSnapshot.exe`. There's no installer; just run the executable or put it in your Startup folder to have it start automatically.

## Features

The app takes a "snapshot" of your windows layouts every thirty minutes. You can see the list of snapshots when you click on the tray menu icon.

## Requirements

- .NET 8.0 Runtime (Windows)
- Windows 10 or later


#### Tips
* When there are many stored snapshots, all very recent ones are shown, plus more spaced-out but distant past snapshots.
* Automatically taken snapshots are shown in normal text.  Manually taken snapshots (`Take Snapshot`) command are shown in **bold**.
* As you mouse over each snapshot, it is restored, so it's easy to find the layout you want.
* Snapshots keep track of, and restore, the "normal size" of windows even if they're currently minimized or maximized.
* This app makes sure all windows fit inside a currently-visible display when restoring snapshots.  Because of that, if you ever have a window that's off-screen because of a bug in other software, just restore the "(Just now)" snapshot.
* Snapshots are stored during the current session (not persisted across app restarts)
* Automatic snapshots are saved every 30 minutes, with intelligent spacing for older snapshots


#### Screenshot
<img src="https://raw.github.com/adamsmith/WindowsLayoutSnapshot/master/screenshot.png" />


#### License
This app and its source code are released into the public domain.