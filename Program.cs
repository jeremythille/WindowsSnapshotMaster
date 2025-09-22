using WindowsLayoutSnapshot;

try
{
    // Configure application
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.SetHighDpiMode(HighDpiMode.SystemAware);
    
    // Set up unhandled exception handling
    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
    Application.ThreadException += OnThreadException;
    AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

    // Load configuration
    var config = await AppConfig.LoadAsync();
    await Logger.InfoAsync("Application starting");

    // Run the application
    using var form = new TrayIconForm(config);
    Application.Run(form);
}
catch (Exception ex)
{
    await Logger.ErrorAsync("Fatal error during application startup", ex);
    MessageBox.Show($"A fatal error occurred: {ex.Message}", "Windows Layout Snapshot", 
        MessageBoxButtons.OK, MessageBoxIcon.Error);
}

static async void OnThreadException(object sender, ThreadExceptionEventArgs e)
{
    await Logger.ErrorAsync("Unhandled thread exception", e.Exception);
}

static async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
{
    if (e.ExceptionObject is Exception ex)
    {
        await Logger.ErrorAsync("Unhandled application exception", ex);
    }
}