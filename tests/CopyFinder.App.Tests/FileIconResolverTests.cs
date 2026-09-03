using CopyFinder.Services;
using Xunit;

namespace CopyFinder.Tests;

public sealed class FileIconResolverTests
{
    [Theory]
    [InlineData("song.flac", "file-audio.png")]
    [InlineData("document.pdf", "file-pdf.png")]
    [InlineData("source.cs", "file-code.png")]
    [InlineData("archive.zip", "file-archive.png")]
    [InlineData("movie.mkv", "file-video.png")]
    [InlineData("unknown.bin", "file-generic.png")]
    public void Resolve_UsesOriginalIconCategories(string path, string expectedAsset)
    {
        Assert.EndsWith(expectedAsset, FileIconResolver.Resolve(path));
    }

    [Fact]
    public void Resolve_UsesFileUriForSupportedImages()
    {
        var result = FileIconResolver.Resolve("/tmp/photo.png");

        Assert.StartsWith("file:///", result, StringComparison.Ordinal);
    }
}
