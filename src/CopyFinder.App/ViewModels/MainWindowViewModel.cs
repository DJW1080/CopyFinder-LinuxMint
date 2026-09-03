using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using CopyFinder.Models;
using CopyFinder.Services;

namespace CopyFinder.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IDuplicateScanner _scanner;
    private readonly IFileAccess _fileAccess;
    private readonly DuplicateDeleteValidator _deleteValidator;
    private readonly ITrashService _trashService;
    private readonly IShellService _shellService;
    private readonly ISettingsService _settingsService;
    private readonly IUserInteractionService _interaction;

    private CancellationTokenSource? _scanCancellation;
    private string _folderPath = string.Empty;
    private KeepRule _keepRule = KeepRule.PreferOriginalName;
    private string _preferredFolder = string.Empty;
    private int _hashWorkers = 2;
    private int _scanLimit = 500;
    private long _minimumSizeKb;
    private bool _skipHiddenFiles = true;
    private string _excludedExtensionsText = string.Empty;
    private bool _isScanning;
    private string _summaryText = "Choose a folder to begin.";
    private string _statusText = "Ready";

    public MainWindowViewModel(
        IDuplicateScanner scanner,
        IFileAccess fileAccess,
        DuplicateDeleteValidator deleteValidator,
        ITrashService trashService,
        IShellService shellService,
        ISettingsService settingsService,
        IUserInteractionService interaction)
    {
        _scanner = scanner;
        _fileAccess = fileAccess;
        _deleteValidator = deleteValidator;
        _trashService = trashService;
        _shellService = shellService;
        _settingsService = settingsService;
        _interaction = interaction;
    }

    public ObservableCollection<DuplicateGroupViewModel> DuplicateGroups { get; } = [];

    public IReadOnlyList<DuplicateFileViewModel> AllFiles =>
        DuplicateGroups.SelectMany(group => group.Files).ToList();

    public IReadOnlyList<KeepRule> KeepRules { get; } = Enum.GetValues<KeepRule>();

    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value);
    }

    public KeepRule KeepRule
    {
        get => _keepRule;
        set => SetProperty(ref _keepRule, value);
    }

    public string PreferredFolder
    {
        get => _preferredFolder;
        set => SetProperty(ref _preferredFolder, value);
    }

    public int HashWorkers
    {
        get => _hashWorkers;
        set => SetProperty(ref _hashWorkers, Math.Clamp(value, 1, 16));
    }

    public int ScanLimit
    {
        get => _scanLimit;
        set => SetProperty(ref _scanLimit, Math.Max(1, value));
    }

    public long MinimumSizeKb
    {
        get => _minimumSizeKb;
        set => SetProperty(ref _minimumSizeKb, Math.Max(0, value));
    }

    public bool SkipHiddenFiles
    {
        get => _skipHiddenFiles;
        set => SetProperty(ref _skipHiddenFiles, value);
    }

    public string ExcludedExtensionsText
    {
        get => _excludedExtensionsText;
        set => SetProperty(ref _excludedExtensionsText, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                RefreshActionState();
            }
        }
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool CanDelete => !IsScanning && AllFiles.Any(file => file.IsSelected);
    public bool CanExport => !IsScanning && DuplicateGroups.Count > 0;
    public bool CanSelectAll => !IsScanning && AllFiles.Any(file => file.IsDuplicate && !file.IsSelected);
    public bool CanDeselectAll => !IsScanning && AllFiles.Any(file => file.IsDuplicate && file.IsSelected);

    public async Task LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken).ConfigureAwait(false);
        FolderPath = settings.LastFolder;
        ScanLimit = settings.ScanDuplicateLimit;
        KeepRule = settings.ScanOptions.KeepRule;
        PreferredFolder = settings.ScanOptions.PreferredFolder;
        HashWorkers = settings.ScanOptions.HashParallelism;
        MinimumSizeKb = settings.ScanOptions.MinimumFileSizeBytes / 1024;
        SkipHiddenFiles = settings.ScanOptions.SkipHiddenFiles;
        ExcludedExtensionsText = string.Join(", ", settings.ScanOptions.ExcludedExtensions);
    }

    public async Task ScanAsync()
    {
        if (IsScanning)
        {
            return;
        }

        if (!Directory.Exists(FolderPath))
        {
            await _interaction.ShowMessageAsync(
                "CopyFinder",
                "Choose an existing folder before scanning.",
                CancellationToken.None);
            return;
        }

        _scanCancellation = new CancellationTokenSource();
        var cancellationToken = _scanCancellation.Token;
        IsScanning = true;
        ClearGroups();
        StatusText = "Scanning…";

        try
        {
            var options = BuildScanOptions();
            await _settingsService
                .SaveAsync(BuildSettings(options), cancellationToken)
                .ConfigureAwait(false);
            var progress = new InlineProgress<ScanProgress>(value => StatusText = value.Message);
            var result = await _scanner.FindDuplicatesAsync(
                    FolderPath,
                    options,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var files in result.Files.GroupBy(file => file.GroupId))
            {
                var group = new DuplicateGroupViewModel(files.Key, files);
                group.PropertyChanged += GroupOnPropertyChanged;
                DuplicateGroups.Add(group);
            }

            RefreshSummary(result.LimitReached ? "Scan stopped for review." : "Scan complete.");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan canceled.";
            SummaryText = "Scan canceled.";
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            StatusText = "Scan failed.";
            await _interaction.ShowMessageAsync(
                "CopyFinder",
                exception.Message,
                CancellationToken.None);
        }
        finally
        {
            _scanCancellation.Dispose();
            _scanCancellation = null;
            IsScanning = false;
            RefreshActionState();
        }
    }

    public void CancelScan() => _scanCancellation?.Cancel();

    public void SelectAllDuplicates()
    {
        foreach (var group in DuplicateGroups)
        {
            group.SetDuplicateSelection(true);
        }

        RefreshActionState();
    }

    public void DeselectAll()
    {
        foreach (var group in DuplicateGroups)
        {
            group.SetDuplicateSelection(false);
        }

        RefreshActionState();
    }

    public void SelectGroup(DuplicateGroupViewModel group)
    {
        EnsureKnownGroup(group);
        group.SetDuplicateSelection(true);
        RefreshActionState();
    }

    public void DeselectGroup(DuplicateGroupViewModel group)
    {
        EnsureKnownGroup(group);
        group.SetDuplicateSelection(false);
        RefreshActionState();
    }

    public void KeepFile(DuplicateFileViewModel file)
    {
        var group = DuplicateGroups.Single(candidate => candidate.GroupId == file.GroupId);
        group.SetOriginal(file);
        RefreshSummary(StatusText);
    }

    public async Task ExportAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var report = CreateReportFiles();
        var content = string.Equals(
            Path.GetExtension(path),
            ".json",
            StringComparison.OrdinalIgnoreCase)
            ? DuplicateReportFormatter.BuildJsonReport(report)
            : DuplicateReportFormatter.BuildCsvReport(report);

        await _fileAccess
            .WriteAllTextAsync(path, content, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
        StatusText = $"Report exported: {path}";
    }

    public async Task OpenFileLocationAsync(string path, CancellationToken cancellationToken)
    {
        var result = await _shellService
            .OpenContainingFolderAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            var message = string.IsNullOrWhiteSpace(result.StandardError)
                ? "The file manager could not open that location."
                : result.StandardError.Trim();
            await _interaction.ShowMessageAsync(
                "CopyFinder",
                message,
                cancellationToken);
        }
    }

    public async Task DeleteSelectedAsync(CancellationToken cancellationToken = default)
    {
        var selectedFiles = AllFiles.Where(file => file.IsDuplicate && file.IsSelected).ToList();
        if (selectedFiles.Count == 0)
        {
            return;
        }

        if (!await _interaction
                .ConfirmDeleteAsync(selectedFiles.Count, cancellationToken)
                .ConfigureAwait(false))
        {
            StatusText = "Move to Trash canceled.";
            return;
        }

        var failures = new List<string>();
        var moved = 0;
        foreach (var selected in selectedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var group = DuplicateGroups.Single(item => item.GroupId == selected.GroupId);
            var kept = group.Files.Single(file => file.IsOriginal);
            var candidate = new DuplicateDeleteCandidate(
                selected.GroupId,
                selected.Path,
                kept.Path,
                selected.Size,
                selected.Hash);
            var validation = await _deleteValidator
                .ValidateAsync(candidate, cancellationToken)
                .ConfigureAwait(false);
            if (!validation.CanDelete)
            {
                failures.Add(validation.FailureMessage ?? $"Could not validate {selected.Path}.");
                continue;
            }

            var trashResult = await _trashService
                .TrashAsync(selected.Path, cancellationToken)
                .ConfigureAwait(false);
            if (!trashResult.Succeeded)
            {
                failures.Add($"{selected.Path}: {trashResult.Message}");
                continue;
            }

            RemoveFileFromGroups(selected);
            moved++;
        }

        StatusText = failures.Count == 0
            ? $"{moved} file{(moved == 1 ? string.Empty : "s")} moved to Trash."
            : $"{moved} moved to Trash; {failures.Count} could not be moved.";
        RefreshSummary(StatusText);

        if (failures.Count > 0)
        {
            await _interaction.ShowMessageAsync(
                "Some files were not moved",
                string.Join(Environment.NewLine, failures),
                cancellationToken);
        }
    }

    private ScanOptions BuildScanOptions() => new()
    {
        MaxDuplicateFiles = ScanLimit,
        HashParallelism = HashWorkers,
        KeepRule = KeepRule,
        PreferredFolder = PreferredFolder,
        MinimumFileSizeBytes = MinimumSizeKb * 1024,
        SkipHiddenFiles = SkipHiddenFiles,
        ExcludedExtensions = ExcludedExtensionsText
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.TrimStart('.'))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
    };

    private AppSettings BuildSettings(ScanOptions options) => new()
    {
        LastFolder = FolderPath,
        ScanDuplicateLimit = ScanLimit,
        ScanOptions = options
    };

    private IReadOnlyList<DuplicateReportFile> CreateReportFiles() =>
        AllFiles.Select(file => new DuplicateReportFile(
                file.GroupId,
                file.Role,
                file.IsSelected,
                file.IsOriginal,
                file.Size,
                file.Hash,
                file.ImageWidth,
                file.ImageHeight,
                file.LastWriteTime,
                file.Path))
            .ToList();

    private void RemoveFileFromGroups(DuplicateFileViewModel file)
    {
        var group = DuplicateGroups.Single(item => item.GroupId == file.GroupId);
        group.RemoveFile(file);
        if (!group.HasDuplicates())
        {
            group.PropertyChanged -= GroupOnPropertyChanged;
            DuplicateGroups.Remove(group);
        }

        OnPropertyChanged(nameof(AllFiles));
        RefreshActionState();
    }

    private void ClearGroups()
    {
        foreach (var group in DuplicateGroups)
        {
            group.PropertyChanged -= GroupOnPropertyChanged;
        }

        DuplicateGroups.Clear();
        OnPropertyChanged(nameof(AllFiles));
        RefreshActionState();
    }

    private void RefreshSummary(string status)
    {
        var duplicateCount = AllFiles.Count(file => file.IsDuplicate);
        var duplicateBytes = AllFiles.Where(file => file.IsDuplicate).Sum(file => file.Size);
        var groupWord = DuplicateGroups.Count == 1 ? "duplicate group" : "duplicate groups";
        var fileWord = duplicateCount == 1 ? "duplicate file" : "duplicate files";
        SummaryText = $"{DuplicateGroups.Count} {groupWord} • {duplicateCount} {fileWord} • " +
                      $"{FormatSize(duplicateBytes)} recoverable";
        StatusText = status;
        OnPropertyChanged(nameof(AllFiles));
        RefreshActionState();
    }

    private void RefreshActionState()
    {
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanExport));
        OnPropertyChanged(nameof(CanSelectAll));
        OnPropertyChanged(nameof(CanDeselectAll));
    }

    private void GroupOnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(DuplicateGroupViewModel.SelectedCount))
        {
            RefreshActionState();
        }
    }

    private void EnsureKnownGroup(DuplicateGroupViewModel group)
    {
        if (!DuplicateGroups.Contains(group))
        {
            throw new ArgumentException("Group does not belong to this scan.", nameof(group));
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
