using Xunit;

namespace CopyFinder.Core.Tests;

public sealed class ReleaseDocumentationTests
{
    [Fact]
    public void ReleaseDocumentation_IsLinuxMintOnlyAndCreditsTheCollaboration()
    {
        var root = RepositoryRoot.Find();
        var readme = ReadOrEmpty(root, "README.md");
        var install = ReadOrEmpty(root, "INSTALL.md");
        var desktopEntry = ReadOrEmpty(
            root,
            "packaging",
            "io.github.DJW1080.CopyFinder.desktop");
        var publishScript = ReadOrEmpty(root, "publish.sh");

        Assert.Contains("Linux Mint 22.3", readme);
        Assert.Contains("Human-AI collaboration", readme);
        Assert.Contains("Dean John Weiniger", readme);
        Assert.Contains("ChatGPT by OpenAI", readme);
        Assert.DoesNotContain("Windows 11", readme);
        Assert.Contains("sha256sum -c", install);
        Assert.Contains("Exec=", desktopEntry);
        Assert.Contains("dotnet publish", publishScript);
        Assert.Contains("linux-x64", publishScript);
        Assert.Contains("sha256sum", publishScript);
    }

    private static string ReadOrEmpty(string root, params string[] segments)
    {
        var path = segments.Aggregate(root, Path.Combine);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
