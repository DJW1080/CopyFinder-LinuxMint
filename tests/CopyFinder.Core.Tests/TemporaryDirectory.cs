namespace CopyFinder.Core.Tests;

public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"CopyFinderTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string PathFor(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath);

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}
