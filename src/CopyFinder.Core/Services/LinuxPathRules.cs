namespace CopyFinder.Services;

public static class LinuxPathRules
{
    public static StringComparer PathComparer { get; } = StringComparer.Ordinal;

    public const StringComparison PathComparison = StringComparison.Ordinal;

    public static bool IsHidden(FileInfo file) =>
        file.Name.StartsWith(".", StringComparison.Ordinal);

    public static bool IsLinkedDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        return info.LinkTarget is not null ||
               info.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }
}
