namespace CopyFinder.Services;

public interface IShellService
{
    Task<ProcessResult> OpenContainingFolderAsync(
        string path,
        CancellationToken cancellationToken);
}

public sealed class LinuxShellService(IProcessRunner processRunner) : IShellService
{
    public Task<ProcessResult> OpenContainingFolderAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Directory.Exists(fullPath)
            ? fullPath
            : Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException(
                "Could not determine the containing folder.",
                nameof(path));
        }

        return processRunner.RunAsync(
            "/usr/bin/xdg-open",
            [directory],
            cancellationToken);
    }
}
