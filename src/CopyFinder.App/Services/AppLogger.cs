namespace CopyFinder.Services;

public interface IAppLogger
{
    void Log(string category, string message, Exception? exception = null);
}

public sealed class AppLogger(IXdgPaths paths) : IAppLogger
{
    public void Log(string category, string message, Exception? exception = null)
    {
        try
        {
            var directory = Path.GetDirectoryName(paths.LogFile);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var exceptionText = exception is null
                ? string.Empty
                : $" {exception.GetType().Name}: {exception.Message}";
            File.AppendAllText(
                paths.LogFile,
                $"{DateTimeOffset.Now:O} [{category}] {message}{exceptionText}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never prevent CopyFinder from starting or protecting files.
        }
    }
}
