using CopyFinder.Models;
using CopyFinder.Services;
using Xunit;

namespace CopyFinder.Core.Tests;

public sealed class LinuxScanSemanticsTests
{
    [Fact]
    public async Task Scan_SkipsLeadingDotFilesWhenRequested()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(fixture.PathFor(".hidden"), "duplicate");
        await File.WriteAllTextAsync(fixture.PathFor("visible"), "duplicate");

        var result = await new DuplicateScanner().FindDuplicatesAsync(
            fixture.Path,
            new ScanOptions { SkipHiddenFiles = true },
            null,
            CancellationToken.None);

        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task Scan_DoesNotFollowSymbolicLinkDirectories()
    {
        using var fixture = new TemporaryDirectory();
        var real = Directory.CreateDirectory(fixture.PathFor("real")).FullName;
        await File.WriteAllTextAsync(System.IO.Path.Combine(real, "a.txt"), "same");
        Directory.CreateSymbolicLink(fixture.PathFor("link"), real);

        var result = await new DuplicateScanner().FindDuplicatesAsync(
            fixture.Path,
            new ScanOptions { SkipHiddenFiles = false },
            null,
            CancellationToken.None);

        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task PreferFolder_UsesCaseSensitivePathIdentity()
    {
        using var fixture = new TemporaryDirectory();
        var upper = Directory.CreateDirectory(fixture.PathFor("Folder")).FullName;
        var lower = Directory.CreateDirectory(fixture.PathFor("folder")).FullName;
        await File.WriteAllTextAsync(System.IO.Path.Combine(upper, "a.txt"), "same");
        await File.WriteAllTextAsync(System.IO.Path.Combine(lower, "b.txt"), "same");

        var result = await new DuplicateScanner().FindDuplicatesAsync(
            fixture.Path,
            new ScanOptions
            {
                KeepRule = KeepRule.PreferFolder,
                PreferredFolder = lower,
                SkipHiddenFiles = false
            },
            null,
            CancellationToken.None);

        Assert.StartsWith(
            lower + System.IO.Path.DirectorySeparatorChar,
            Assert.Single(result.Files, file => file.IsOriginal).Path,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scan_RespectsMinimumSizeAndExcludedExtensions()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(fixture.PathFor("small-a.txt"), "x");
        await File.WriteAllTextAsync(fixture.PathFor("small-b.txt"), "x");
        await File.WriteAllTextAsync(fixture.PathFor("large-a.tmp"), "duplicate");
        await File.WriteAllTextAsync(fixture.PathFor("large-b.tmp"), "duplicate");

        var result = await new DuplicateScanner().FindDuplicatesAsync(
            fixture.Path,
            new ScanOptions
            {
                MinimumFileSizeBytes = 2,
                ExcludedExtensions = ["tmp"],
                SkipHiddenFiles = false
            },
            null,
            CancellationToken.None);

        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task Scan_ObservesCancellation()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(fixture.PathFor("a.txt"), "same");
        await File.WriteAllTextAsync(fixture.PathFor("b.txt"), "same");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new DuplicateScanner().FindDuplicatesAsync(
                fixture.Path,
                new ScanOptions { SkipHiddenFiles = false },
                null,
                cancellation.Token));
    }
}
