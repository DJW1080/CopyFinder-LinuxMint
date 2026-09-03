using CopyFinder.Models;
using CopyFinder.Services;
using Xunit;

namespace CopyFinder.Core.Tests;

public sealed class DuplicateScannerTests
{
    [Fact]
    public async Task FindDuplicatesAsync_GroupsFilesWithSameSizeAndHash()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(fixture.PathFor("original.txt"), "same-content");
        await File.WriteAllTextAsync(fixture.PathFor("copy.txt"), "same-content");
        await File.WriteAllTextAsync(fixture.PathFor("different.txt"), "different");

        var result = await new DuplicateScanner().FindDuplicatesAsync(
            fixture.Path,
            new ScanOptions { MaxDuplicateFiles = 50, SkipHiddenFiles = false },
            progress: null,
            CancellationToken.None);

        Assert.Equal(2, result.Files.Count);
        Assert.Single(result.Files.Select(file => file.GroupId).Distinct());
        Assert.Equal(1, result.Files.Count(file => file.IsOriginal));
    }
}
