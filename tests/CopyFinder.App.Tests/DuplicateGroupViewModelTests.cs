using CopyFinder.Models;
using CopyFinder.ViewModels;
using Xunit;

namespace CopyFinder.Tests;

public sealed class DuplicateGroupViewModelTests
{
    [Fact]
    public void SetOriginal_MakesExactlyOneFileTheKeptFile()
    {
        var first = Duplicate(1, "/tmp/a.txt", isOriginal: true);
        var second = Duplicate(1, "/tmp/b.txt", isOriginal: false);
        var group = new DuplicateGroupViewModel(1, [first, second]);

        group.SetOriginal(group.Files[1]);

        Assert.False(group.Files[0].IsOriginal);
        Assert.True(group.Files[1].IsOriginal);
        Assert.False(group.Files[1].IsSelected);
        Assert.Single(group.Files, file => file.IsOriginal);
    }

    [Fact]
    public void SelectDuplicates_NeverSelectsKeptFile()
    {
        var group = CreateTwoFileGroup();

        group.SetDuplicateSelection(true);

        Assert.False(Assert.Single(group.Files, file => file.IsOriginal).IsSelected);
        Assert.True(Assert.Single(group.Files, file => file.IsDuplicate).IsSelected);
        Assert.Equal(1, group.SelectedCount);
    }

    [Fact]
    public void Rows_AlternateWithinAGroup()
    {
        var group = CreateTwoFileGroup();

        Assert.False(group.Files[0].IsOddRow);
        Assert.True(group.Files[1].IsOddRow);
    }

    private static DuplicateFile Duplicate(int groupId, string path, bool isOriginal) =>
        new(groupId, path, 4, "AA", DateTime.UnixEpoch, null, null, isOriginal);

    private static DuplicateGroupViewModel CreateTwoFileGroup() =>
        new(1,
        [
            Duplicate(1, "/tmp/a.txt", isOriginal: true),
            Duplicate(1, "/tmp/b.txt", isOriginal: false)
        ]);
}
