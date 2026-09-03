namespace CopyFinder.Services;

public static class FileIconResolver
{
    private const string AssetRoot = "avares://CopyFinder/Assets/FileIcons/";

    public static string Resolve(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or
            ".tif" or ".tiff" or ".webp")
        {
            return new Uri(Path.GetFullPath(path)).AbsoluteUri;
        }

        var icon = extension switch
        {
            ".mp3" or ".flac" or ".wav" or ".m4a" or ".aac" or ".ogg" or
                ".wma" or ".aiff" or ".alac" => "file-audio.png",
            ".doc" or ".docx" or ".rtf" or ".odt" => "file-word.png",
            ".pdf" => "file-pdf.png",
            ".xls" or ".xlsx" or ".csv" or ".ods" => "file-spreadsheet.png",
            ".ppt" or ".pptx" or ".odp" => "file-presentation.png",
            ".mp4" or ".mkv" or ".mov" or ".avi" or ".wmv" or ".m4v" or
                ".webm" => "file-video.png",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".bz2" =>
                "file-archive.png",
            ".txt" or ".md" or ".log" or ".nfo" or ".ini" or ".cfg" =>
                "file-text.png",
            ".cs" or ".js" or ".ts" or ".json" or ".xml" or ".html" or
                ".css" or ".ps1" or ".py" or ".sql" => "file-code.png",
            _ => "file-generic.png"
        };

        return AssetRoot + icon;
    }
}
