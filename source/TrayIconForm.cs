using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace WindowsLayoutMaster;

/// <summary>
/// Modern tray icon form with async operations and better UX
/// </summary>
public partial class TrayIconForm : Form
{
    private readonly NotifyIcon _trayIcon;
    private readonly TrayPopupForm _popup; // Replace ContextMenuStrip with custom popup
    private readonly System.Windows.Forms.Timer _autoSnapshotTimer;
    private readonly List<Snapshot> _snapshots = [];
    private readonly SemaphoreSlim _snapshotSemaphore = new(1, 1);
    private readonly AppConfig _config;
    private readonly string _snapshotsFilePath;

    // Windows API constants for tray icon messages
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int WM_RBUTTONUP = 0x0205;

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    public TrayIconForm(AppConfig config)
    {
        _config = config;
        _snapshotsFilePath = Path.Combine(_config.SnapshotsDirectory, "snapshots.json");
        _ = Logger.InfoAsync($"Initializing TrayIconForm with AutoSnapshotIntervalMinutes: {config.AutoSnapshotIntervalMinutes}");

        // Initialize custom popup with preview functionality
        _popup = new TrayPopupForm(OnMenuItemClick, OnPreviewSnapshot, GetSnapshotByIndex);

        InitializeComponent();        // Configure form
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        Visible = false;
        
        // Initialize tray icon
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
        Icon? trayIconIcon = null;
        
        try
        {
            if (File.Exists(iconPath))
            {
                trayIconIcon = new Icon(iconPath);
                _ = Logger.InfoAsync($"Loaded custom icon from: {iconPath}");
            }
            else
            {
                _ = Logger.InfoAsync($"Custom icon not found at: {iconPath}, using system icon");
                trayIconIcon = SystemIcons.Application;
            }
        }
        catch (Exception ex)
        {
            _ = Logger.ErrorAsync($"Error loading icon from {iconPath}", ex);
            trayIconIcon = SystemIcons.Application;
        }
        
        _trayIcon = new NotifyIcon
        {
            Icon = trayIconIcon,
            Text = "Windows Layout Master",
            Visible = true
        };
        
        _ = Logger.InfoAsync("Tray icon initialized and set to visible");

        // Configure events for simple click handling
        _trayIcon.MouseDown += OnTrayIconMouseDown;        // Force tray icon to refresh (sometimes needed on Windows)
        Task.Run(async () =>
        {
            await Task.Delay(500); // Small delay to ensure everything is initialized
            this.Invoke(() =>
            {
                _trayIcon.Visible = false;
                _trayIcon.Visible = true;
                _ = Logger.InfoAsync("Tray icon refreshed");
            });
        });
        
        // Initialize auto-snapshot timer with validation
        var intervalMinutes = Math.Max(_config.AutoSnapshotIntervalMinutes, 1); // Ensure at least 1 minute
        var intervalMs = (int)TimeSpan.FromMinutes(intervalMinutes).TotalMilliseconds;
        
        _autoSnapshotTimer = new System.Windows.Forms.Timer
        {
            Interval = intervalMs,
            Enabled = true
        };
        
        _ = Logger.InfoAsync($"Auto-snapshot timer set to {intervalMinutes} minutes ({intervalMs} ms)");
        _autoSnapshotTimer.Tick += OnAutoSnapshotTimer;
        
        // Load existing snapshots from disk
        _ = LoadSnapshotsAsync();
        
        // Take initial snapshot
        _ = TakeSnapshotAsync(userInitiated: false);
    }

    private void OnTrayIconMouseDown(object? sender, MouseEventArgs e)
    {
        // Handle both left and right clicks with our custom popup
        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
        {
            if (_popup.Visible)
            {
                _popup.Hide();
            }
            else
            {
                UpdatePopupMenu();
                var cursorPos = Cursor.Position;
                
                // For tray icons, show the menu near the tray icon position
                // Since tray is usually at bottom-right, we want to show above and slightly left
                var menuLocation = new Point(cursorPos.X - 50, cursorPos.Y - 20);
                _popup.ShowAt(menuLocation);
            }
        }
    }

    private void OnMenuItemClick(string action)
    {
        switch (action)
        {
            case "take-snapshot":
                _ = TakeSnapshotAsync(userInitiated: true);
                break;
            case "clear-all":
                _snapshots.Clear();
                _ = SaveSnapshotsAsync();
                break;
            case "exit":
                Application.Exit();
                break;
            default:
                if (action.StartsWith("restore-"))
                {
                    var indexStr = action.Substring("restore-".Length);
                    if (int.TryParse(indexStr, out int index) && index >= 0 && index < _snapshots.Count)
                    {
                        var snapshot = _snapshots[index];
                        _ = RestoreSnapshotAsync(snapshot);
                    }
                }
                break;
        }
    }

    private void OnPreviewSnapshot(Snapshot? snapshot)
    {
        if (snapshot != null)
        {
            // Apply preview snapshot - only on current virtual desktop
            _ = RestoreSnapshotForPreviewAsync(snapshot);
        }
        else
        {
            // Restore original layout - we need to get the original from the popup
            var originalSnapshot = _popup.GetOriginalSnapshot();
            if (originalSnapshot != null)
            {
                _ = RestoreSnapshotForPreviewAsync(originalSnapshot);
            }
        }
    }

    private async Task RestoreSnapshotForPreviewAsync(Snapshot snapshot)
    {
        try
        {
            // For preview, get the current virtual desktop and only restore windows on it
            await Task.Run(() =>
            {
                // Get all currently visible windows on this virtual desktop
                var currentDesktopWindows = GetCurrentDesktopWindows();
                
                foreach (var windowInfo in snapshot.Windows)
                {
                    IntPtr targetHandle = windowInfo.Handle;
                    
                    // If handle is null/invalid, try to find by title
                    if (targetHandle == IntPtr.Zero || !WindowManager.IsWindowVisiblePublic(targetHandle))
                    {
                        targetHandle = WindowManager.FindWindowByTitle(windowInfo.Title);
                        if (targetHandle == IntPtr.Zero)
                            continue;
                    }

                    // Only restore if window is currently on this virtual desktop
                    if (currentDesktopWindows.Contains(targetHandle))
                    {
                        WindowManager.RestoreWindow(windowInfo);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _ = Logger.ErrorAsync("Error during preview restore", ex);
        }
    }

    private HashSet<IntPtr> GetCurrentDesktopWindows()
    {
        var currentWindows = new HashSet<IntPtr>();
        
        // Enumerate all top-level windows and check if they're visible and not minimized
        WindowManager.EnumWindows((hWnd, lParam) =>
        {
            if (WindowManager.IsWindowVisiblePublic(hWnd) && 
                !WindowManager.IsWindowIconic(hWnd) && // Not minimized
                WindowManager.GetWindowTitleLength(hWnd) > 0) // Has a title
            {
                currentWindows.Add(hWnd);
            }
            return true;
        }, IntPtr.Zero);
        
        return currentWindows;
    }

    private Snapshot? GetSnapshotByIndex(int index)
    {
        return index >= 0 && index < _snapshots.Count ? _snapshots[index] : null;
    }

    private async void OnAutoSnapshotTimer(object? sender, EventArgs e)
    {
        await TakeSnapshotAsync(userInitiated: false);
    }

    private async Task TakeSnapshotAsync(bool userInitiated)
    {
        if (!await _snapshotSemaphore.WaitAsync(100)) // Quick timeout to avoid blocking UI
            return;

        try
        {
            var snapshot = await Snapshot.TakeSnapshotAsync(userInitiated);
            _snapshots.Add(snapshot);
            
            // Keep only the most recent snapshots
            while (_snapshots.Count > _config.MaxSnapshotsToKeep)
            {
                _snapshots.RemoveAt(0);
            }
            
            // Save snapshots to disk
            _ = SaveSnapshotsAsync();
        }
        catch (Exception ex)
        {
            await Logger.ErrorAsync("Error taking snapshot", ex);
            // Optionally show user notification for critical errors
        }
        finally
        {
            _snapshotSemaphore.Release();
        }
    }

    private async Task SaveSnapshotsAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_snapshotsFilePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(_snapshots, options);
            await File.WriteAllTextAsync(_snapshotsFilePath, json);
            
            await Logger.InfoAsync($"Saved {_snapshots.Count} snapshots to {_snapshotsFilePath}");
        }
        catch (Exception ex)
        {
            await Logger.ErrorAsync("Error saving snapshots to disk", ex);
        }
    }

    private async Task LoadSnapshotsAsync()
    {
        try
        {
            if (!File.Exists(_snapshotsFilePath))
            {
                await Logger.InfoAsync("No existing snapshots file found, starting fresh");
                return;
            }

            var json = await File.ReadAllTextAsync(_snapshotsFilePath);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var loadedSnapshots = JsonSerializer.Deserialize<List<Snapshot>>(json, options);
            if (loadedSnapshots != null)
            {
                _snapshots.Clear();
                _snapshots.AddRange(loadedSnapshots);
                await Logger.InfoAsync($"Loaded {_snapshots.Count} snapshots from {_snapshotsFilePath}");
            }
        }
        catch (Exception ex)
        {
            await Logger.ErrorAsync("Error loading snapshots from disk", ex);
        }
    }

    private void UpdatePopupMenu()
    {
        _popup.ClearItems();
        
        // Snapshot history
        var condensedSnapshots = CondenseSnapshots(_snapshots, _config.MaxSnapshotsToKeep);
        var maxPixels = condensedSnapshots.Any() 
            ? condensedSnapshots.SelectMany(s => s.Monitors).Max(m => m.PixelCount)
            : 0;
        
        for (int i = 0; i < condensedSnapshots.Count; i++)
        {
            var snapshot = condensedSnapshots.OrderByDescending(s => s.TimeTaken).ToList()[i];
            _popup.AddMenuItem(snapshot.Description, $"restore-{_snapshots.IndexOf(snapshot)}", snapshot.Monitors, maxPixels);
        }
        
        // Additional options
        if (_snapshots.Count > 0)
        {
            _popup.AddSeparator();
            _popup.AddMenuItem("Clear All Snapshots", "clear-all");
        }
        
        _popup.AddSeparator();
        _popup.AddMenuItem("Take Snapshot", "take-snapshot");
        _popup.AddSeparator();
        _popup.AddMenuItem("Exit", "exit");
        
        _popup.BuildMenu();
    }

    private async Task RestoreSnapshotAsync(Snapshot snapshot)
    {
        try
        {
            var restoredCount = await snapshot.RestoreAsync();
            
            // Update tray icon tooltip temporarily
            var originalText = _trayIcon.Text;
            _trayIcon.Text = $"Restored {restoredCount} windows";
            
            // Reset tooltip after 3 seconds
            var resetTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            resetTimer.Tick += (s, e) =>
            {
                _trayIcon.Text = originalText;
                resetTimer.Stop();
                resetTimer.Dispose();
            };
            resetTimer.Start();
        }
        catch (Exception ex)
        {
            await Logger.ErrorAsync("Error restoring snapshot", ex);
            // Show user notification for restore errors as they're more critical
            MessageBox.Show($"Error restoring snapshot: {ex.Message}", "Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static List<Snapshot> CondenseSnapshots(List<Snapshot> snapshots, int maxSnapshots = 20)
    {
        if (snapshots.Count <= maxSnapshots)
            return new List<Snapshot>(snapshots);

        // Prioritize recent snapshots and manually taken ones
        var result = new List<Snapshot>();
        var sortedSnapshots = snapshots.OrderByDescending(s => s.TimeTaken).ToList();
        
        // Always include the most recent ones
        result.AddRange(sortedSnapshots.Take(5));
        
        // Include manual snapshots
        var manualSnapshots = sortedSnapshots.Where(s => s.UserInitiated && !result.Contains(s)).Take(5);
        result.AddRange(manualSnapshots);
        
        // Fill remaining slots with diverse snapshots
        var remaining = sortedSnapshots.Where(s => !result.Contains(s)).ToList();
        while (result.Count < maxSnapshots && remaining.Count > 0)
        {
            result.Add(remaining[0]);
            remaining.RemoveAt(0);
        }
        
        return result.OrderByDescending(s => s.TimeTaken).ToList();
    }

    protected override void SetVisibleCore(bool value)
    {
        // Prevent the form from becoming visible
        base.SetVisibleCore(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon?.Dispose();
            _popup?.Dispose();
            _autoSnapshotTimer?.Dispose();
            _snapshotSemaphore?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(0, 0);
        FormBorderStyle = FormBorderStyle.None;
        Name = "TrayIconForm";
        ShowIcon = false;
        ShowInTaskbar = false;
        Text = "Windows Layout Master";
        WindowState = FormWindowState.Minimized;
        
        ResumeLayout(false);
    }
}

/// <summary>
/// Custom menu item that displays monitor icons
/// </summary>
public class MonitorAwareToolStripMenuItem : ToolStripMenuItem
{
    private readonly List<MonitorInfo> _monitors;
    private readonly long _maxPixels;

    public MonitorAwareToolStripMenuItem(string text, List<MonitorInfo> monitors, long maxPixels) 
        : base(text)
    {
        _monitors = monitors;
        _maxPixels = maxPixels;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (_monitors.Count <= 1) return;

        // Load monitor icon (we'll create a simple rectangle for now since we don't have the original icon)
        var iconSize = 16;
        var margin = 8; // Normal margin with wider menu providing space
        var currentX = Width - margin;

        using var brush = new SolidBrush(Color.FromArgb(128, ForeColor));
        
        foreach (var monitor in _monitors.OrderByDescending(m => m.Primary))
        {
            var relativeSize = monitor.RelativeSize(_maxPixels);
            var scaledSize = (int)(iconSize * relativeSize);
            var rect = new Rectangle(
                currentX - scaledSize, 
                (Height - scaledSize) / 2, 
                scaledSize, 
                scaledSize);

            e.Graphics.FillRectangle(brush, rect);
            e.Graphics.DrawRectangle(Pens.Gray, rect);

            currentX -= scaledSize + 2;
        }
    }
}