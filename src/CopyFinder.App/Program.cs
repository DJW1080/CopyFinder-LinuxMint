using Avalonia;

namespace CopyFinder;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }
}
