using CopyFinder.Services;

namespace CopyFinder.Tests;

public sealed class RecordingProcessRunner(ProcessResult result) : IProcessRunner
{
    public string? FileName { get; private set; }

    public IReadOnlyList<string> Arguments { get; private set; } = [];

    public Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        FileName = fileName;
        Arguments = arguments.ToArray();
        return Task.FromResult(result);
    }
}
