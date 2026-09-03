namespace CopyFinder.Services;

public interface IXdgPaths
{
    string SettingsFile { get; }
    string LogFile { get; }
    string CacheDirectory { get; }
}

public sealed class XdgPaths : IXdgPaths
{
    public XdgPaths(
        Func<string, string?>? getEnvironment = null,
        string? homeDirectory = null)
    {
        getEnvironment ??= Environment.GetEnvironmentVariable;
        homeDirectory ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var config = GetRoot(
            getEnvironment("XDG_CONFIG_HOME"),
            Path.Combine(homeDirectory, ".config"));
        var state = GetRoot(
            getEnvironment("XDG_STATE_HOME"),
            Path.Combine(homeDirectory, ".local", "state"));
        var cache = GetRoot(
            getEnvironment("XDG_CACHE_HOME"),
            Path.Combine(homeDirectory, ".cache"));

        SettingsFile = Path.Combine(config, "CopyFinder", "settings.json");
        LogFile = Path.Combine(state, "CopyFinder", "logs", "copyfinder.log");
        CacheDirectory = Path.Combine(cache, "CopyFinder");
    }

    public string SettingsFile { get; }

    public string LogFile { get; }

    public string CacheDirectory { get; }

    private static string GetRoot(string? configured, string fallback) =>
        string.IsNullOrWhiteSpace(configured) ? fallback : configured;
}
