using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace WindowsLayoutSnapshot;

/// <summary>
/// Modern tray icon form with async operations and better UX
/// </summary>
public partial class TrayIconForm : Form
{
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly System.Windows.Forms.Timer _autoSnapshotTimer;
    private readonly List<Snapshot> _snapshots = [];
    private readonly SemaphoreSlim _snapshotSemaphore = new(1, 1);
    private readonly AppConfig _config;
    
    private Snapshot? _previewSnapshot;

    public TrayIconForm(AppConfig config)
    {
        _config = config;
        _ = Logger.InfoAsync($"Initializing TrayIconForm with AutoSnapshotIntervalMinutes: {config.AutoSnapshotIntervalMinutes}");
        
        InitializeComponent();
        
        // Configure form
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        Visible = false;
        
        // Initialize tray icon
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "monitor-window-3d-shadow.ico");
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
            Text = "Windows Layout Snapshot",
            Visible = true
        };
        
        _ = Logger.InfoAsync("Tray icon initialized and set to visible");
        
        // Initialize context menu
        _contextMenu = new ContextMenuStrip();
        _trayIcon.ContextMenuStrip = _contextMenu;
        
        // Configure events
        _trayIcon.MouseClick += OnTrayIconClick;
        _contextMenu.Opening += OnContextMenuOpening;
        
        // Force tray icon to refresh (sometimes needed on Windows)
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
        
        // Take initial snapshot
        _ = TakeSnapshotAsync(userInitiated: false);
    }

    private async void OnTrayIconClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            // Take a preview snapshot for "Just Now"
            _previewSnapshot = await Snapshot.TakeSnapshotAsync(userInitiated: false);
            
            // Show context menu on left click
            var method = typeof(NotifyIcon).GetMethod("ShowContextMenu", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method?.Invoke(_trayIcon, null);
        }
    }

    private void OnContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        UpdateContextMenu();
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

    private void UpdateContextMenu()
    {
        _contextMenu.Items.Clear();
        
        // Manual snapshot option
        var takeSnapshotItem = new ToolStripMenuItem("Take Snapshot Now")
        {
            Font = new Font(_contextMenu.Font, FontStyle.Bold)
        };
        takeSnapshotItem.Click += async (s, e) => await TakeSnapshotAsync(userInitiated: true);
        _contextMenu.Items.Add(takeSnapshotItem);
        
        _contextMenu.Items.Add(new ToolStripSeparator());
        
        // "Just Now" option if preview snapshot exists
        if (_previewSnapshot != null)
        {
            var justNowItem = new ToolStripMenuItem("Restore Current Layout")
            {
                ToolTipText = "Restore the layout as it was when you opened this menu"
            };
            justNowItem.Click += async (s, e) => await RestoreSnapshotAsync(_previewSnapshot);
            justNowItem.MouseEnter += async (s, e) => await PreviewSnapshotAsync(_previewSnapshot);
            _contextMenu.Items.Add(justNowItem);
            
            _contextMenu.Items.Add(new ToolStripSeparator());
        }
        
        // Snapshot history
        var condensedSnapshots = CondenseSnapshots(_snapshots, _config.MaxSnapshotsToKeep);
        var maxPixels = condensedSnapshots.SelectMany(s => s.Monitors).Max(m => m.PixelCount);
        
        foreach (var snapshot in condensedSnapshots.OrderByDescending(s => s.TimeTaken))
        {
            var item = new MonitorAwareToolStripMenuItem(snapshot.Description, snapshot.Monitors, maxPixels)
            {
                Tag = snapshot,
                Font = snapshot.UserInitiated 
                    ? new Font(_contextMenu.Font, FontStyle.Bold) 
                    : _contextMenu.Font
            };
            
            item.Click += async (s, e) => await RestoreSnapshotAsync(snapshot);
            item.MouseEnter += async (s, e) => await PreviewSnapshotAsync(snapshot);
            
            _contextMenu.Items.Add(item);
        }
        
        // Additional options
        if (_snapshots.Count > 0)
        {
            _contextMenu.Items.Add(new ToolStripSeparator());
            
            var clearItem = new ToolStripMenuItem("Clear All Snapshots");
            clearItem.Click += (s, e) => _snapshots.Clear();
            _contextMenu.Items.Add(clearItem);
        }
        
        _contextMenu.Items.Add(new ToolStripSeparator());
        
        // Exit option
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => Application.Exit();
        _contextMenu.Items.Add(exitItem);
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

    private async Task PreviewSnapshotAsync(Snapshot snapshot)
    {
        // In the original, this was used to preview layouts
        // For now, we'll just restore directly as the preview mechanism was complex
        await RestoreSnapshotAsync(snapshot);
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
            _contextMenu?.Dispose();
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
        Text = "Windows Layout Snapshot";
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
        var margin = 4;
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