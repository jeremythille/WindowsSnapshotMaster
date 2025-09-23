using System.Text.Json;

namespace WindowsLayoutMaster;

/// <summary>
/// Application configuration settings
/// </summary>
public class AppConfig
{
    public int AutoSnapshotIntervalMinutes { get; set; } = 30;
    public int MaxSnapshotsToKeep { get; set; } = 20;
    public bool StartWithWindows { get; set; } = true;
    public bool EnableSounds { get; set; } = false;
    public bool SaveSnapshotsToDisk { get; set; } = false;
    public string SnapshotsDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowsLayoutMaster",
        "config.json");

    /// <summary>
    /// Loads configuration from disk or returns default
    /// </summary>
    public static async Task<AppConfig> LoadAsync()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = await File.ReadAllTextAsync(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                return config ?? new AppConfig();
            }
        }
        catch (Exception ex)
        {
            // Log error in real application
            Console.WriteLine($"Error loading config: {ex.Message}");
        }

        return new AppConfig();
    }

    /// <summary>
    /// Saves configuration to disk
    /// </summary>
    public async Task SaveAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(ConfigPath, json);
        }
        catch (Exception ex)
        {
            // Log error in real application
            Console.WriteLine($"Error saving config: {ex.Message}");
        }
    }
}

/// <summary>
/// Simple logging utility
/// </summary>
public static class Logger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowsLayoutMaster",
        "app.log");

    public static async Task LogAsync(string level, string message, Exception? exception = null)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var logEntry = $"[{timestamp}] [{level}] {message}";
            
            if (exception != null)
            {
                logEntry += $"\n{exception}";
            }
            
            logEntry += "\n";

            await File.AppendAllTextAsync(LogPath, logEntry);
        }
        catch
        {
            // Ignore logging errors to prevent cascading failures
        }
    }

    public static async Task InfoAsync(string message) => await LogAsync("INFO", message);
    public static async Task WarnAsync(string message) => await LogAsync("WARN", message);
    public static async Task ErrorAsync(string message, Exception? exception = null) => 
        await LogAsync("ERROR", message, exception);
}