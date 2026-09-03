using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(CopyFinder.Tests.AppBuilderFactory))]

namespace CopyFinder.Tests;

public static class AppBuilderFactory
{
    public static AppBuilder BuildAvaloniaApp() =>
        global::CopyFinder.App.BuildAvaloniaApp()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
