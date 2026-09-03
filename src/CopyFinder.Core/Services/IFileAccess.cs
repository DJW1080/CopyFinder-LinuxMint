using System.Text;

namespace CopyFinder.Services;

public sealed record SafeDirectoryEnumerationResult(
    IReadOnlyList<string> Paths,
    string? ErrorMessage);

public interface IFileAccess
{
    SafeDirectoryEnumerationResult EnumerateDirectories(string directory);
    SafeDirectoryEnumerationResult EnumerateFiles(string directory);
    bool FileExists(string path);
    FileInfo GetFileInfo(string path);
    Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken);
    Task WriteAllTextAsync(
        string path,
        string contents,
        Encoding encoding,
        CancellationToken cancellationToken);
}
