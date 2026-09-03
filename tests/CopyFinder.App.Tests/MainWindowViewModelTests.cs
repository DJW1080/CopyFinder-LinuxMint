using CopyFinder.Models;
using CopyFinder.Services;
using CopyFinder.ViewModels;
using Xunit;

namespace CopyFinder.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public async Task ScanAsync_PopulatesGroupsAndEnablesReviewActions()
    {
        using var fixture = new TemporaryDirectory();
        var scanner = FakeDuplicateScanner.Returning(TwoGroups(fixture.Path));
        var viewModel = CreateViewModel(scanner: scanner);
        viewModel.FolderPath = fixture.Path;

        await viewModel.ScanAsync();

        Assert.Equal(2, viewModel.DuplicateGroups.Count);
        Assert.True(viewModel.CanExport);
        Assert.False(viewModel.IsScanning);
        Assert.Contains("2 duplicate groups", viewModel.SummaryText);
    }

    [Fact]
    public async Task DeleteSelectedAsync_ValidatesBeforeCallingTrash()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(fixture.PathFor("kept.txt"), "same");
        await File.WriteAllTextAsync(fixture.PathFor("duplicate.txt"), "same");
        var scanResult = await new DuplicateScanner().FindDuplicatesAsync(
            fixture.Path,
            new ScanOptions { SkipHiddenFiles = false },
            null,
            CancellationToken.None);
        var trash = new RecordingTrashService();
        var viewModel = CreateViewModel(trash: trash, scanResult: scanResult);
        viewModel.FolderPath = fixture.Path;
        await viewModel.ScanAsync();
        viewModel.SelectAllDuplicates();

        await viewModel.DeleteSelectedAsync();

        Assert.Equal(scanResult.DuplicateFileCount, trash.Paths.Count);
        Assert.All(
            trash.Paths,
            path => Assert.DoesNotContain(viewModel.AllFiles, file => file.Path == path));
    }

    [Fact]
    public async Task DeleteSelectedAsync_DoesNotTrashAChangedFile()
    {
        using var fixture = new TemporaryDirectory();
        await File.WriteAllTextAsync(fixture.PathFor("kept.txt"), "same");
        await File.WriteAllTextAsync(fixture.PathFor("duplicate.txt"), "same");
        var scanResult = await new DuplicateScanner().FindDuplicatesAsync(
            fixture.Path,
            new ScanOptions { SkipHiddenFiles = false },
            null,
            CancellationToken.None);
        var trash = new RecordingTrashService();
        var viewModel = CreateViewModel(trash: trash, scanResult: scanResult);
        viewModel.FolderPath = fixture.Path;
        await viewModel.ScanAsync();
        viewModel.SelectAllDuplicates();
        await File.WriteAllTextAsync(
            Assert.Single(viewModel.AllFiles, file => file.IsSelected).Path,
            "changed");

        await viewModel.DeleteSelectedAsync();

        Assert.Empty(trash.Paths);
        Assert.Contains("could not be moved", viewModel.StatusText);
    }

    [Fact]
    public async Task ExportAsync_WritesJsonWhenJsonExtensionIsRequested()
    {
        using var fixture = new TemporaryDirectory();
        var viewModel = CreateViewModel(
            scanner: FakeDuplicateScanner.Returning(TwoGroups(fixture.Path)));
        viewModel.FolderPath = fixture.Path;
        await viewModel.ScanAsync();
        var reportPath = fixture.PathFor("report.json");

        await viewModel.ExportAsync(reportPath, CancellationToken.None);

        var contents = await File.ReadAllTextAsync(reportPath);
        Assert.Contains("\"GroupId\"", contents);
        Assert.Contains("\"Path\"", contents);
    }

    private static DuplicateScanResult TwoGroups(string root) => new(
    [
        new(1, Path.Combine(root, "a.txt"), 4, "AA", DateTime.Now, null, null, true),
        new(1, Path.Combine(root, "b.txt"), 4, "AA", DateTime.Now, null, null, false),
        new(2, Path.Combine(root, "c.txt"), 5, "BB", DateTime.Now, null, null, true),
        new(2, Path.Combine(root, "d.txt"), 5, "BB", DateTime.Now, null, null, false)
    ],
    false,
    2);

    private static MainWindowViewModel CreateViewModel(
        IDuplicateScanner? scanner = null,
        ITrashService? trash = null,
        DuplicateScanResult? scanResult = null)
    {
        scanner ??= FakeDuplicateScanner.Returning(scanResult ?? new([], false, 0));
        var files = new SystemFileAccess();
        return new MainWindowViewModel(
            scanner,
            files,
            new DuplicateDeleteValidator(files),
            trash ?? new RecordingTrashService(),
            new RecordingShellService(),
            new InMemorySettingsService(),
            new FakeUserInteractionService());
    }
}

internal sealed class FakeDuplicateScanner(DuplicateScanResult result) : IDuplicateScanner
{
    public static FakeDuplicateScanner Returning(DuplicateScanResult result) => new(result);

    public Task<DuplicateScanResult> FindDuplicatesAsync(
        string rootDirectory,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken) => Task.FromResult(result);
}

internal sealed class FakeUserInteractionService(bool confirmDelete = true)
    : IUserInteractionService
{
    public List<string> Messages { get; } = [];

    public Task<bool> ConfirmDeleteAsync(
        int selectedFileCount,
        CancellationToken cancellationToken) => Task.FromResult(confirmDelete);

    public Task ShowMessageAsync(
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        Messages.Add($"{title}: {message}");
        return Task.CompletedTask;
    }
}

internal sealed class RecordingTrashService : ITrashService
{
    public List<string> Paths { get; } = [];

    public Task<TrashResult> TrashAsync(string path, CancellationToken cancellationToken)
    {
        Paths.Add(path);
        return Task.FromResult(new TrashResult(true, path, "Moved to Trash."));
    }
}

internal sealed class InMemorySettingsService : ISettingsService
{
    public AppSettings Settings { get; private set; } = new();

    public Task<AppSettings> LoadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Settings);

    public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingShellService : IShellService
{
    public Task<ProcessResult> OpenContainingFolderAsync(
        string path,
        CancellationToken cancellationToken) =>
        Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"CopyFinderAppTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string PathFor(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath);

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
