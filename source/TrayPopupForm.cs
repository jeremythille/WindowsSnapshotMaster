using System.ComponentModel;

namespace WindowsLayoutMaster;

/// <summary>
/// Custom popup form that acts like a context menu but with reliable behavior
/// </summary>
public partial class TrayPopupForm : Form
{
    private readonly Action<string> _onMenuItemClick;
    private readonly List<(string text, string action, List<MonitorInfo>? monitors, long maxPixels, bool isManual)> _menuItems = [];
    private Snapshot? _originalSnapshot; // Store original layout for preview restoration
    private readonly Action<Snapshot?> _onPreviewSnapshot; // Callback for preview
    private readonly Func<int, Snapshot?> _getSnapshotByIndex; // Callback to get snapshot by index
    private bool _isClickInProgress; // Flag to prevent MouseLeave from interfering with clicks

    public TrayPopupForm(Action<string> onMenuItemClick, Action<Snapshot?> onPreviewSnapshot, Func<int, Snapshot?> getSnapshotByIndex)
    {
        _onMenuItemClick = onMenuItemClick;
        _onPreviewSnapshot = onPreviewSnapshot;
        _getSnapshotByIndex = getSnapshotByIndex;
        InitializeForm();
    }

    private void InitializeForm()
    {
        // Configure form as popup
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = SystemColors.Menu;
        Font = SystemFonts.MenuFont;
        
        // Handle deactivation to close popup and restore original layout
        Deactivate += (s, e) => 
        {
            // Only restore original layout if we're not in the middle of processing a click
            if (!_isClickInProgress)
            {
                RestoreOriginalLayout();
            }
            Hide();
        };
        
        // Handle click outside to close popup and restore original layout
        LostFocus += (s, e) => 
        {
            // Only restore original layout if we're not in the middle of processing a click
            if (!_isClickInProgress)
            {
                RestoreOriginalLayout();
            }
            Hide();
        };
    }

    private void RestoreOriginalLayout()
    {
        if (_originalSnapshot != null)
        {
            _onPreviewSnapshot(null); // Signal to restore original layout
        }
    }

    /// <summary>
    /// Manually reset the click state (useful for cleanup)
    /// </summary>
    public void ResetClickState()
    {
        _isClickInProgress = false;
    }

    public void ClearItems()
    {
        _menuItems.Clear();
        Controls.Clear();
    }

    public void AddMenuItem(string text, string action, List<MonitorInfo>? monitors = null, long maxPixels = 0, bool isManual = false)
    {
        _menuItems.Add((text, action, monitors, maxPixels, isManual));
    }

    public void AddSeparator()
    {
        _menuItems.Add(("---", "", null, 0, false));
    }

    public void BuildMenu()
    {
        Controls.Clear();
        
        int y = 2;
        int minWidth = 250; // Reduced since text is shorter now
        int maxWidth = minWidth;
        
        // Calculate the actual width needed by measuring text
        using (var g = CreateGraphics())
        {
            foreach (var item in _menuItems)
            {
                if (item.text != "---")
                {
                    var textSize = g.MeasureString(item.text, Font);
                    var neededWidth = (int)textSize.Width + 80; // Extra space for icons and padding
                    if (neededWidth > maxWidth)
                        maxWidth = neededWidth;
                }
            }
        }
        
        // Ensure reasonable bounds
        maxWidth = Math.Max(minWidth, Math.Min(maxWidth, 400)); // Between 250-400px
        
        foreach (var item in _menuItems)
        {
            if (item.text == "---")
            {
                // Separator
                var separator = new Panel
                {
                    Height = 1,
                    Width = maxWidth - 4,
                    Left = 2,
                    Top = y + 2,
                    BackColor = SystemColors.ControlDark
                };
                Controls.Add(separator);
                y += 5;
            }
            else
            {
                // Menu item with monitor icons
                var menuItem = item.monitors != null && item.monitors.Count > 0 
                    ? new MonitorAwareLabel(item.text, item.monitors, item.maxPixels)
                    : new Label { Text = item.text };
                
                // Apply bold font for manual snapshots
                if (item.isManual)
                {
                    menuItem.Font = new Font(menuItem.Font, FontStyle.Bold);
                }
                
                menuItem.Left = 8;
                menuItem.Top = y;
                menuItem.Height = 22;
                menuItem.Width = maxWidth - 16;
                menuItem.TextAlign = ContentAlignment.MiddleLeft;
                menuItem.Cursor = Cursors.Hand;
                menuItem.Tag = item.action;
                
                // Add hover effect and preview functionality
                menuItem.MouseEnter += (s, e) => 
                {
                    menuItem.BackColor = SystemColors.Highlight;
                    menuItem.ForeColor = SystemColors.HighlightText;
                    
                    // Preview snapshot on hover (only for restore actions)
                    if (item.action.StartsWith("restore-"))
                    {
                        var indexStr = item.action.Substring("restore-".Length);
                        if (int.TryParse(indexStr, out int index))
                        {
                            _onPreviewSnapshot(GetSnapshotByIndex(index));
                        }
                    }
                };
                
                menuItem.MouseLeave += (s, e) => 
                {
                    menuItem.BackColor = SystemColors.Menu;
                    menuItem.ForeColor = SystemColors.MenuText;
                    
                    // Only restore original layout when not clicking (to prevent click interference)
                    if (!_isClickInProgress)
                    {
                        RestoreOriginalLayout();
                    }
                };
                
                // Handle click
                menuItem.Click += (s, e) =>
                {
                    _isClickInProgress = true; // Prevent MouseLeave from interfering
                    
                    // Store the action to execute after hiding
                    var actionToExecute = item.action;
                    
                    // Hide the form first
                    Hide();
                    
                    // Use a small delay to ensure the form is hidden and events have settled
                    // before executing the action and resetting the flag
                    Task.Run(async () =>
                    {
                        await Task.Delay(50); // Small delay to let events settle
                        
                        // Execute the action on the UI thread
                        Invoke(() =>
                        {
                            _onMenuItemClick(actionToExecute);
                            // Reset flag after a longer delay to prevent interference from late events
                            Task.Run(async () =>
                            {
                                await Task.Delay(200);
                                _isClickInProgress = false;
                            });
                        });
                    });
                };
                
                Controls.Add(menuItem);
                y += 22;
            }
        }
        
        // Set form size
        Size = new Size(maxWidth, y + 2);
        
        // Add border
        Paint += (s, e) =>
        {
            ControlPaint.DrawBorder(e.Graphics, ClientRectangle, 
                SystemColors.ControlDark, ButtonBorderStyle.Solid);
        };
    }

    public void ShowAt(Point location)
    {
        // Capture the current layout before showing the menu
        Task.Run(async () =>
        {
            _originalSnapshot = await Snapshot.TakeSnapshotAsync(userInitiated: false);
        });
        
        // Get screen bounds to adjust position if needed
        var screen = Screen.FromPoint(location);
        var workingArea = screen.WorkingArea;
        
        // Calculate popup size first (we need this for positioning)
        var popupSize = Size;
        
        // Adjust X position if popup would go off right edge
        if (location.X + popupSize.Width > workingArea.Right)
        {
            location.X = workingArea.Right - popupSize.Width;
        }
        
        // Adjust Y position if popup would go off bottom edge (common for tray icons)
        if (location.Y + popupSize.Height > workingArea.Bottom)
        {
            // Show above the cursor instead of below
            location.Y = location.Y - popupSize.Height - 10; // Extra 10px margin
        }
        
        // Ensure we don't go off the top edge either
        if (location.Y < workingArea.Top)
        {
            location.Y = workingArea.Top;
        }
        
        Location = location;
        Show();
        Activate(); // Ensure it gets focus for proper deactivation
    }

    private Snapshot? GetSnapshotByIndex(int index)
    {
        return _getSnapshotByIndex(index);
    }

    public Snapshot? GetOriginalSnapshot()
    {
        return _originalSnapshot;
    }
}

/// <summary>
/// Custom label that can paint monitor icons like the original MonitorAwareToolStripMenuItem
/// </summary>
public class MonitorAwareLabel : Label
{
    private readonly List<MonitorInfo> _monitors;
    private readonly long _maxPixels;

    public MonitorAwareLabel(string text, List<MonitorInfo> monitors, long maxPixels) : base()
    {
        Text = text;
        _monitors = monitors;
        _maxPixels = maxPixels;
    }

    private static Image? _monitorIcon;
    private static void EnsureMonitorIconLoaded()
    {
        if (_monitorIcon == null)
        {
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "monitor.png");
                if (File.Exists(iconPath))
                {
                    _monitorIcon = Image.FromFile(iconPath);
                }
            }
            catch { /* ignore */ }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        EnsureMonitorIconLoaded();
        var icon = _monitorIcon;
        var iconSize = 16;
        var margin = 8;
        var currentX = Width - margin;

        if (icon != null)
        {
            // Draw one icon per monitor (or just one if only one monitor)
            foreach (var monitor in _monitors.OrderByDescending(m => m.Primary))
            {
                var relativeSize = monitor.RelativeSize(_maxPixels);
                var scaledSize = (int)(iconSize * relativeSize);
                var rect = new Rectangle(
                    currentX - scaledSize,
                    (Height - scaledSize) / 2,
                    scaledSize,
                    scaledSize);
                e.Graphics.DrawImage(icon, rect);
                currentX -= scaledSize + 2;
            }
            // If only one monitor, still draw one icon
            if (_monitors.Count == 1)
            {
                var rect = new Rectangle(
                    currentX - iconSize,
                    (Height - iconSize) / 2,
                    iconSize,
                    iconSize);
                e.Graphics.DrawImage(icon, rect);
            }
        }
        else
        {
            // Fallback: draw a gray rectangle if icon not found
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
            if (_monitors.Count == 1)
            {
                var rect = new Rectangle(
                    currentX - iconSize,
                    (Height - iconSize) / 2,
                    iconSize,
                    iconSize);
                e.Graphics.FillRectangle(brush, rect);
                e.Graphics.DrawRectangle(Pens.Gray, rect);
            }
        }
    }
}