namespace CopyFinder.Services;

public sealed record TrashResult(bool Succeeded, string Path, string Message);

public interface ITrashService
{
    Task<TrashResult> TrashAsync(string path, CancellationToken cancellationToken);
}

public sealed class LinuxTrashService(IProcessRunner processRunner) : ITrashService
{
    public async Task<TrashResult> TrashAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(path);
        var result = await processRunner.RunAsync(
                "/usr/bin/gio",
                ["trash", "--", fullPath],
                cancellationToken)
            .ConfigureAwait(false);

        return result.ExitCode == 0
            ? new TrashResult(true, fullPath, "Moved to Trash.")
            : new TrashResult(
                false,
                fullPath,
                string.IsNullOrWhiteSpace(result.StandardError)
                    ? "Trash operation failed. The file was not deleted."
                    : result.StandardError.Trim());
    }
}
