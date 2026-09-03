using CopyFinder.Services;
using Xunit;

namespace CopyFinder.Tests;

public sealed class LinuxShellServiceTests
{
    [Fact]
    public async Task OpenContainingFolderAsync_PassesParentAsSingleArgument()
    {
        var runner = new RecordingProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new LinuxShellService(runner);
        const string path = "/tmp/folder;echo-no/file.txt";

        await service.OpenContainingFolderAsync(path, CancellationToken.None);

        Assert.Equal("/usr/bin/xdg-open", runner.FileName);
        Assert.Equal(["/tmp/folder;echo-no"], runner.Arguments);
    }
}
