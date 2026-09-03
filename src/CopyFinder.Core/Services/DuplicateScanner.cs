using System.Collections.Concurrent;
using CopyFinder.Models;
using MetadataExtractor;

namespace CopyFinder.Services;

public sealed record ScanProgress(string Message);

public interface IDuplicateScanner
{
    Task<DuplicateScanResult> FindDuplicatesAsync(
        string rootDirectory,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}

public sealed class DuplicateScanner : IDuplicateScanner
{
    private readonly IFileAccess _fileAccess;

    public DuplicateScanner(IFileAccess? fileAccess = null)
    {
        _fileAccess = fileAccess ?? new SystemFileAccess();
    }

    public async Task<DuplicateScanResult> FindDuplicatesAsync(
        string rootDirectory,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(options);

        var candidatesBySize = EnumerateFiles(rootDirectory, options, progress, cancellationToken)
            .GroupBy(file => file.Length)
            .Where(group => group.Key > 0 && group.Count() > 1)
            .ToList();

        var duplicateFiles = new List<DuplicateFile>();
        var groupId = 1;
        var duplicateFileCount = 0;
        var limitReached = false;
        var maximumDuplicates = Math.Max(1, options.MaxDuplicateFiles);

        foreach (var sizeGroup in candidatesBySize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hashGroups = await HashFilesAsync(
                    sizeGroup,
                    options.HashParallelism,
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            foreach (var hashGroup in hashGroups)
            {
                if (duplicateFileCount >= maximumDuplicates)
                {
                    limitReached = true;
                    break;
                }

                var metadata = await ReadImageMetadataAsync(hashGroup.Files, cancellationToken)
                    .ConfigureAwait(false);
                var ordered = hashGroup.Files
                    .OrderBy(file => GetPrimaryKeepScore(file, options, metadata))
                    .ThenBy(GetCopyNameScore)
                    .ThenBy(file => Path.GetFileNameWithoutExtension(file.Name).Length)
                    .ThenBy(file => file.LastWriteTimeUtc)
                    .ThenBy(file => file.FullName, LinuxPathRules.PathComparer)
                    .ToList();

                var slotsRemaining = maximumDuplicates - duplicateFileCount;
                var filesToReturn = ordered.Take(slotsRemaining + 1).ToList();
                for (var index = 0; index < filesToReturn.Count; index++)
                {
                    var file = filesToReturn[index];
                    metadata.TryGetValue(file.FullName, out var imageMetadata);
                    duplicateFiles.Add(new DuplicateFile(
                        groupId,
                        file.FullName,
                        file.Length,
                        hashGroup.Hash,
                        file.LastWriteTime,
                        imageMetadata.Width,
                        imageMetadata.Height,
                        index == 0));
                }

                duplicateFileCount += Math.Max(0, filesToReturn.Count - 1);
                limitReached = filesToReturn.Count < ordered.Count;
                groupId++;
            }

            if (duplicateFileCount >= maximumDuplicates)
            {
                limitReached = true;
                break;
            }
        }

        return new DuplicateScanResult(duplicateFiles, limitReached, duplicateFileCount);
    }

    private async Task<IReadOnlyList<HashGroup>> HashFilesAsync(
        IEnumerable<FileInfo> files,
        int parallelism,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var hashes = new ConcurrentDictionary<string, ConcurrentBag<FileInfo>>(
            StringComparer.OrdinalIgnoreCase);
        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Clamp(parallelism, 1, 16)
        };

        await Parallel.ForEachAsync(files, parallelOptions, async (file, token) =>
        {
            progress?.Report(new ScanProgress($"Hashing {file.FullName}"));
            try
            {
                var hash = await _fileAccess.ComputeSha256Async(file.FullName, token)
                    .ConfigureAwait(false);
                hashes.GetOrAdd(hash, _ => []).Add(file);
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                progress?.Report(new ScanProgress($"Skipped file: {file.FullName}"));
            }
        }).ConfigureAwait(false);

        return hashes
            .Where(group => group.Value.Count > 1)
            .Select(group => new HashGroup(group.Key, group.Value.ToList()))
            .ToList();
    }

    private IEnumerable<FileInfo> EnumerateFiles(
        string rootDirectory,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        var visited = new HashSet<string>(LinuxPathRules.PathComparer);
        pending.Push(rootDirectory);
        var seen = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pending.Pop();
            if (ShouldSkipDirectory(directory, progress) || !TryMarkVisited(directory, visited))
            {
                continue;
            }

            var subdirectories = _fileAccess.EnumerateDirectories(directory);
            if (subdirectories.ErrorMessage is not null)
            {
                progress?.Report(new ScanProgress($"Skipped folder: {directory}"));
                continue;
            }

            foreach (var subdirectory in subdirectories.Paths)
            {
                if (!ShouldSkipDirectory(subdirectory, progress))
                {
                    pending.Push(subdirectory);
                }
            }

            var files = _fileAccess.EnumerateFiles(directory);
            if (files.ErrorMessage is not null)
            {
                progress?.Report(new ScanProgress($"Skipped folder: {directory}"));
                continue;
            }

            foreach (var path in files.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                FileInfo file;
                try
                {
                    file = _fileAccess.GetFileInfo(path);
                    if (!file.Exists || ShouldSkipFile(file, options))
                    {
                        continue;
                    }
                }
                catch (Exception exception) when (
                    exception is ArgumentException or IOException or UnauthorizedAccessException)
                {
                    progress?.Report(new ScanProgress($"Skipped file: {path}"));
                    continue;
                }

                seen++;
                if (seen % 100 == 0)
                {
                    progress?.Report(new ScanProgress($"Found {seen:N0} files"));
                }

                yield return file;
            }
        }
    }

    private static bool TryMarkVisited(string directory, ISet<string> visited)
    {
        try
        {
            return visited.Add(Path.GetFullPath(directory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool ShouldSkipDirectory(
        string directory,
        IProgress<ScanProgress>? progress)
    {
        try
        {
            if (!LinuxPathRules.IsLinkedDirectory(directory))
            {
                return false;
            }

            progress?.Report(new ScanProgress($"Skipped linked folder: {directory}"));
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            progress?.Report(new ScanProgress($"Skipped folder: {directory}"));
            return true;
        }
    }

    private static bool ShouldSkipFile(FileInfo file, ScanOptions options)
    {
        if (file.Length < options.MinimumFileSizeBytes)
        {
            return true;
        }

        if (options.SkipHiddenFiles && LinuxPathRules.IsHidden(file))
        {
            return true;
        }

        var extension = file.Extension.TrimStart('.');
        return options.ExcludedExtensions.Any(excluded =>
            string.Equals(
                excluded.TrimStart('.'),
                extension,
                StringComparison.OrdinalIgnoreCase));
    }

    private static long GetPrimaryKeepScore(
        FileInfo file,
        ScanOptions options,
        IReadOnlyDictionary<string, ImageMetadata> metadata)
    {
        return options.KeepRule switch
        {
            KeepRule.PreferOriginalName => GetCopyNameScore(file),
            KeepRule.PreferShortestName => Path.GetFileNameWithoutExtension(file.Name).Length,
            KeepRule.PreferOldestFile => file.LastWriteTimeUtc.Ticks,
            KeepRule.PreferNewestFile => -file.LastWriteTimeUtc.Ticks,
            KeepRule.PreferFolder => IsInPreferredFolder(file, options.PreferredFolder) ? 0 : 1,
            KeepRule.PreferHighestResolution => metadata.TryGetValue(file.FullName, out var image)
                ? -image.PixelCount
                : long.MaxValue,
            _ => GetCopyNameScore(file)
        };
    }

    private static int GetCopyNameScore(FileInfo file)
    {
        var name = Path.GetFileNameWithoutExtension(file.Name).Trim().ToLowerInvariant();
        if (name.EndsWith(" - copy", StringComparison.Ordinal) ||
            name.EndsWith(" copy", StringComparison.Ordinal) ||
            name.EndsWith("_copy", StringComparison.Ordinal))
        {
            return 10;
        }

        var opening = name.LastIndexOf('(');
        return opening >= 0 && name.EndsWith(')') &&
               name[(opening + 1)..^1].All(char.IsDigit)
            ? 20
            : 0;
    }

    private static bool IsInPreferredFolder(FileInfo file, string preferredFolder)
    {
        if (string.IsNullOrWhiteSpace(preferredFolder))
        {
            return false;
        }

        try
        {
            var folder = Path.GetFullPath(preferredFolder)
                .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return file.FullName.StartsWith(folder, LinuxPathRules.PathComparison);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<Dictionary<string, ImageMetadata>> ReadImageMetadataAsync(
        IEnumerable<FileInfo> files,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ImageMetadata>(LinuxPathRules.PathComparer);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[file.FullName] = await TryReadImageMetadataAsync(file.FullName, cancellationToken)
                .ConfigureAwait(false);
        }

        return result;
    }

    private static async Task<ImageMetadata> TryReadImageMetadataAsync(
        string path,
        CancellationToken cancellationToken)
    {
        if (Path.GetExtension(path).ToLowerInvariant() is not
            (".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp"))
        {
            return ImageMetadata.Empty;
        }

        try
        {
            return await Task.Run(() =>
            {
                var directories = ImageMetadataReader.ReadMetadata(path);
                var width = FindDimension(directories, "Image Width", "Width");
                var height = FindDimension(directories, "Image Height", "Height");
                return new ImageMetadata(width, height);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or ImageProcessingException)
        {
            return ImageMetadata.Empty;
        }
    }

    private static int? FindDimension(
        IReadOnlyList<MetadataExtractor.Directory> directories,
        params string[] tagNames)
    {
        foreach (var directory in directories)
        {
            foreach (var tag in directory.Tags)
            {
                if (tagNames.Contains(tag.Name, StringComparer.OrdinalIgnoreCase) &&
                    directory.TryGetInt32(tag.Type, out var value) &&
                    value > 0)
                {
                    return value;
                }
            }
        }

        return null;
    }

    private readonly record struct ImageMetadata(int? Width, int? Height)
    {
        public static ImageMetadata Empty { get; } = new(null, null);

        public long PixelCount =>
            Width is null || Height is null ? 0 : (long)Width.Value * Height.Value;
    }

    private sealed record HashGroup(string Hash, List<FileInfo> Files);
}
