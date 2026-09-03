using CopyFinder.Services;
using Xunit;

namespace CopyFinder.Tests;

public sealed class XdgPathsTests
{
    [Fact]
    public void XdgPaths_UsesEnvironmentOverrides()
    {
        var paths = new XdgPaths(
            name => name switch
            {
                "XDG_CONFIG_HOME" => "/tmp/config",
                "XDG_STATE_HOME" => "/tmp/state",
                "XDG_CACHE_HOME" => "/tmp/cache",
                _ => null
            },
            homeDirectory: "/home/tester");

        Assert.Equal("/tmp/config/CopyFinder/settings.json", paths.SettingsFile);
        Assert.Equal("/tmp/state/CopyFinder/logs/copyfinder.log", paths.LogFile);
        Assert.Equal("/tmp/cache/CopyFinder", paths.CacheDirectory);
    }

    [Fact]
    public void XdgPaths_FallsBackToHomeDirectories()
    {
        var paths = new XdgPaths(_ => null, "/home/tester");

        Assert.Equal("/home/tester/.config/CopyFinder/settings.json", paths.SettingsFile);
        Assert.Equal("/home/tester/.local/state/CopyFinder/logs/copyfinder.log", paths.LogFile);
        Assert.Equal("/home/tester/.cache/CopyFinder", paths.CacheDirectory);
    }
}
