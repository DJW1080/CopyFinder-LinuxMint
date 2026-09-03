using CopyFinder.Services;
using Xunit;

namespace CopyFinder.Tests;

public sealed class LinuxTrashServiceTests
{
    [Fact]
    public async Task TrashAsync_PassesDangerousLookingPathAsOneArgument()
    {
        var runner = new RecordingProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new LinuxTrashService(runner);
        const string path = "/tmp/name; touch SHOULD_NOT_RUN.txt";

        var result = await service.TrashAsync(path, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("/usr/bin/gio", runner.FileName);
        Assert.Equal(["trash", "--", Path.GetFullPath(path)], runner.Arguments);
    }

    [Fact]
    public async Task TrashAsync_ReturnsFailureWithoutDeletingWhenGioFails()
    {
        var runner = new RecordingProcessRunner(new ProcessResult(2, string.Empty, "not supported"));
        var service = new LinuxTrashService(runner);

        var result = await service.TrashAsync("/tmp/example", CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("not supported", result.Message);
    }
}
