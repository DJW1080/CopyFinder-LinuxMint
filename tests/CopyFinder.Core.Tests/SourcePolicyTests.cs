using Xunit;

namespace CopyFinder.Core.Tests;

public sealed class SourcePolicyTests
{
    [Fact]
    public void MaintainedSource_DoesNotReferenceWindowsDesktopApis()
    {
        var root = RepositoryRoot.Find();
        var source = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) ||
                           path.EndsWith(".axaml", StringComparison.Ordinal))
            .Where(path => !path.Contains(
                $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal))
            .Select(File.ReadAllText);
        var combined = string.Join('\n', source);

        Assert.DoesNotContain("Microsoft.UI", combined);
        Assert.DoesNotContain("Windows.Storage", combined);
        Assert.DoesNotContain("explorer.exe", combined);
        Assert.DoesNotContain("powershell.exe", combined);
        Assert.DoesNotContain("UseWinUI", combined);
    }
}
