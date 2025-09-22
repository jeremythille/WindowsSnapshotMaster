using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace WindowsLayoutMaster;

/// <summary>
/// Represents a window with its position, size, and state information
/// </summary>
public record WindowInfo
{
    [JsonIgnore]
    public IntPtr Handle { get; init; }
    
    public required string Title { get; init; }
    public required Rectangle Bounds { get; init; }
    public required WindowState State { get; init; }
    public required DateTime CapturedAt { get; init; }
}

/// <summary>
/// Window state enumeration
/// </summary>
public enum WindowState
{
    Normal = 1,
    Minimized = 2,
    Maximized = 3
}

/// <summary>
/// Modern window management utilities
/// </summary>
public static class WindowManager
{
    // Win32 API imports
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
    
    [DllImport("user32.dll")]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
    
    [DllImport("user32.dll")]
    private static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hwnd, GetAncestorFlags gaFlags);
    
    [DllImport("user32.dll")]
    private static extern IntPtr GetLastActivePopup(IntPtr hWnd);
    
    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080L;
    private const long WS_EX_APPWINDOW = 0x00040000L;

    private enum GetAncestorFlags : uint
    {
        GetParent = 1,
        GetRoot = 2,
        GetRootOwner = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public Point ptMinPosition;
        public Point ptMaxPosition;
        public RECT rcNormalPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        
        public Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        
        public static RECT FromRectangle(Rectangle rect) => new()
        {
            Left = rect.Left,
            Top = rect.Top,
            Right = rect.Right,
            Bottom = rect.Bottom
        };
    }

    /// <summary>
    /// Gets all visible windows that should appear in Alt+Tab
    /// </summary>
    public static List<WindowInfo> GetAllWindows()
    {
        var windows = new List<WindowInfo>();
        var captureTime = DateTime.UtcNow;
        
        EnumWindows((hWnd, lParam) =>
        {
            if (IsAltTabWindow(hWnd))
            {
                var title = GetWindowTitle(hWnd);
                var placement = GetWindowPlacement(hWnd);
                
                if (placement.HasValue)
                {
                    var windowInfo = new WindowInfo
                    {
                        Handle = hWnd,
                        Title = title,
                        Bounds = placement.Value.rcNormalPosition.ToRectangle(),
                        State = (WindowState)placement.Value.showCmd,
                        CapturedAt = captureTime
                    };
                    
                    windows.Add(windowInfo);
                }
            }
            
            return true; // Continue enumeration
        }, IntPtr.Zero);
        
        return windows;
    }

    /// <summary>
    /// Restores a window to its specified position and state
    /// </summary>
    public static bool RestoreWindow(WindowInfo windowInfo)
    {
        IntPtr targetHandle = windowInfo.Handle;
        
        // If handle is null/invalid (e.g., loaded from JSON), try to find by title
        if (targetHandle == IntPtr.Zero || !IsWindowVisible(targetHandle))
        {
            targetHandle = FindWindowByTitle(windowInfo.Title);
            if (targetHandle == IntPtr.Zero)
                return false;
        }

        var placement = new WINDOWPLACEMENT
        {
            length = Marshal.SizeOf(typeof(WINDOWPLACEMENT)),
            showCmd = (int)windowInfo.State,
            rcNormalPosition = RECT.FromRectangle(EnsureWindowFitsScreen(windowInfo.Bounds))
        };

        return SetWindowPlacement(targetHandle, ref placement);
    }

    /// <summary>
    /// Finds a window by its title
    /// </summary>
    private static IntPtr FindWindowByTitle(string title)
    {
        var foundHandle = IntPtr.Zero;
        
        EnumWindows((hWnd, lParam) =>
        {
            if (IsWindowVisible(hWnd) && !IsIconic(hWnd))
            {
                var windowTitle = GetWindowTitle(hWnd);
                if (windowTitle == title)
                {
                    foundHandle = hWnd;
                    return false; // Stop enumeration
                }
            }
            return true; // Continue enumeration
        }, IntPtr.Zero);
        
        return foundHandle;
    }

    private static string GetWindowTitle(IntPtr hWnd)
    {
        var titleBuilder = new System.Text.StringBuilder(256);
        GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
        return titleBuilder.ToString();
    }

    private static WINDOWPLACEMENT? GetWindowPlacement(IntPtr hWnd)
    {
        var placement = new WINDOWPLACEMENT
        {
            length = Marshal.SizeOf(typeof(WINDOWPLACEMENT))
        };
        
        return GetWindowPlacement(hWnd, ref placement) ? placement : null;
    }

    private static bool IsAltTabWindow(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd))
            return false;

        var exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        
        // Has WS_EX_APPWINDOW style
        if ((exStyle & WS_EX_APPWINDOW) != 0)
            return true;
            
        // Has WS_EX_TOOLWINDOW style (exclude these)
        if ((exStyle & WS_EX_TOOLWINDOW) != 0)
            return false;

        // Check if this is the active window for its owner
        var hwndTry = GetAncestor(hWnd, GetAncestorFlags.GetRootOwner);
        var hwndWalk = IntPtr.Zero;
        
        while (hwndTry != hwndWalk)
        {
            hwndWalk = hwndTry;
            hwndTry = GetLastActivePopup(hwndWalk);
            if (IsWindowVisible(hwndTry))
                break;
        }
        
        return hwndWalk == hWnd;
    }

    private static Rectangle EnsureWindowFitsScreen(Rectangle windowBounds)
    {
        var workingArea = Screen.GetWorkingArea(windowBounds);
        
        // Ensure the window fits within the screen bounds
        var width = Math.Min(windowBounds.Width, workingArea.Width);
        var height = Math.Min(windowBounds.Height, workingArea.Height);
        
        var x = Math.Max(workingArea.Left, 
                Math.Min(workingArea.Right - width, windowBounds.X));
        var y = Math.Max(workingArea.Top, 
                Math.Min(workingArea.Bottom - height, windowBounds.Y));
        
        return new Rectangle(x, y, width, height);
    }
}