using CopyFinder.Services;
using Xunit;

namespace CopyFinder.Core.Tests;

public sealed class DuplicateDeleteValidatorTests
{
    [Fact]
    public async Task ValidateAsync_RejectsDuplicateChangedAfterScan()
    {
        using var fixture = new TemporaryDirectory();
        var kept = fixture.PathFor("kept.txt");
        var duplicate = fixture.PathFor("duplicate.txt");
        await File.WriteAllTextAsync(kept, "same");
        await File.WriteAllTextAsync(duplicate, "same");
        var hash = await new SystemFileAccess()
            .ComputeSha256Async(kept, CancellationToken.None);
        var candidate = new DuplicateDeleteCandidate(1, duplicate, kept, 4, hash);
        await File.WriteAllTextAsync(duplicate, "changed");

        var result = await new DuplicateDeleteValidator()
            .ValidateAsync(candidate, CancellationToken.None);

        Assert.False(result.CanDelete);
        Assert.Contains("changed since scan", result.FailureMessage);
    }
}
