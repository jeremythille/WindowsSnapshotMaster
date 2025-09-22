using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsLayoutMaster;

/// <summary>
/// Represents a snapshot of window layouts at a specific point in time
/// </summary>
public class Snapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime TimeTaken { get; init; }
    public bool UserInitiated { get; init; }
    public List<WindowInfo> Windows { get; init; } = [];
    public List<MonitorInfo> Monitors { get; init; } = [];
    
    [JsonIgnore]
    public string Description => GenerateDescription();

    [JsonIgnore]
    public TimeSpan Age => DateTime.UtcNow - TimeTaken;

    /// <summary>
    /// Takes a snapshot of the current window layout
    /// </summary>
    public static async Task<Snapshot> TakeSnapshotAsync(bool userInitiated = false)
    {
        return await Task.Run(() =>
        {
            var windows = WindowManager.GetAllWindows();
            var monitors = GetCurrentMonitors();
            
            return new Snapshot
            {
                TimeTaken = DateTime.UtcNow,
                UserInitiated = userInitiated,
                Windows = windows,
                Monitors = monitors
            };
        });
    }

    /// <summary>
    /// Restores all windows to their positions from this snapshot
    /// </summary>
    public async Task<int> RestoreAsync()
    {
        return await Task.Run(() =>
        {
            int restoredCount = 0;
            
            foreach (var windowInfo in Windows)
            {
                if (WindowManager.RestoreWindow(windowInfo))
                {
                    restoredCount++;
                }
            }
            
            return restoredCount;
        });
    }

    /// <summary>
    /// Saves this snapshot to a JSON file
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads a snapshot from a JSON file
    /// </summary>
    public static async Task<Snapshot?> LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return null;
            
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<Snapshot>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static List<MonitorInfo> GetCurrentMonitors()
    {
        return Screen.AllScreens.Select((screen, index) => new MonitorInfo
        {
            Index = index,
            Bounds = screen.Bounds,
            WorkingArea = screen.WorkingArea,
            Primary = screen.Primary,
            DeviceName = screen.DeviceName ?? $"Monitor{index + 1}"
        }).ToList();
    }

    private string GenerateDescription()
    {
        var timeString = TimeTaken.ToLocalTime().ToString("MMM dd, h:mm tt");
        var windowCount = Windows.Count;
        var monitorCount = Monitors.Count;
        
        var suffix = UserInitiated ? " (Manual)" : "";
        return $"{timeString} - {windowCount} windows, {monitorCount} monitor{(monitorCount != 1 ? "s" : "")}{suffix}";
    }
}

/// <summary>
/// Information about a monitor/display
/// </summary>
public record MonitorInfo
{
    public required int Index { get; init; }
    public required Rectangle Bounds { get; init; }
    public required Rectangle WorkingArea { get; init; }
    public required bool Primary { get; init; }
    public required string DeviceName { get; init; }
    
    [JsonIgnore]
    public long PixelCount => (long)Bounds.Width * Bounds.Height;
    
    public float RelativeSize(long maxPixels) => maxPixels > 0 ? (float)Math.Sqrt((double)PixelCount / maxPixels) : 1.0f;
}