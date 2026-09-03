using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CopyFinder.Services;
using CopyFinder.ViewModels;
using CopyFinder.Views;

namespace CopyFinder;

public sealed partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var paths = new XdgPaths();
            var logger = new AppLogger(paths);
            var settings = new SettingsService(paths);
            var processRunner = new SystemProcessRunner();
            var fileAccess = new SystemFileAccess();
            var window = new MainWindow();
            var viewModel = new MainWindowViewModel(
                new DuplicateScanner(fileAccess),
                fileAccess,
                new DuplicateDeleteValidator(fileAccess),
                new LinuxTrashService(processRunner),
                new LinuxShellService(processRunner),
                settings,
                window);

            window.DataContext = viewModel;
            desktop.MainWindow = window;
            logger.Log("Startup", "CopyFinder Linux started.");
            _ = LoadSettingsAsync(viewModel, logger);
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static async Task LoadSettingsAsync(
        MainWindowViewModel viewModel,
        IAppLogger logger)
    {
        try
        {
            await viewModel.LoadSettingsAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.Log("Settings", "Could not load saved settings.", exception);
        }
    }
}
