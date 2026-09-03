using System.Security.Cryptography;
using System.Text;

namespace CopyFinder.Services;

public sealed class SystemFileAccess : IFileAccess
{
    public SafeDirectoryEnumerationResult EnumerateDirectories(string directory) =>
        Enumerate(directory, Directory.EnumerateDirectories);

    public SafeDirectoryEnumerationResult EnumerateFiles(string directory) =>
        Enumerate(directory, Directory.EnumerateFiles);

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public FileInfo GetFileInfo(string path) => new(Path.GetFullPath(path));

    public async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    public async Task WriteAllTextAsync(
        string path,
        string contents,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(fullPath, contents, encoding, cancellationToken)
            .ConfigureAwait(false);
    }

    private static SafeDirectoryEnumerationResult Enumerate(
        string directory,
        Func<string, IEnumerable<string>> enumerate)
    {
        try
        {
            return new SafeDirectoryEnumerationResult(enumerate(directory).ToArray(), null);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new SafeDirectoryEnumerationResult([], exception.Message);
        }
    }
}
