using CopyFinder.Models;
using CopyFinder.Services;

namespace CopyFinder.ViewModels;

public sealed class DuplicateFileViewModel : ObservableObject
{
    private bool _isOriginal;
    private bool _isSelected;

    public DuplicateFileViewModel(DuplicateFile file, int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(file);
        GroupId = file.GroupId;
        Path = file.Path;
        Size = file.Size;
        Hash = file.Hash;
        LastWriteTime = file.LastWriteTime;
        ImageWidth = file.ImageWidth;
        ImageHeight = file.ImageHeight;
        RowIndex = rowIndex;
        _isOriginal = file.IsOriginal;
        _isSelected = !file.IsOriginal;
        IconAsset = FileIconResolver.Resolve(file.Path);
    }

    public int GroupId { get; }
    public string Path { get; }
    public string FileName => System.IO.Path.GetFileName(Path);
    public long Size { get; }
    public string SizeText => FormatSize(Size);
    public string Hash { get; }
    public string HashPreview => Hash.Length > 12 ? Hash[..12] : Hash;
    public DateTime LastWriteTime { get; }
    public string LastWriteText => LastWriteTime.ToString("dd/MM/yy HH:mm");
    public int? ImageWidth { get; }
    public int? ImageHeight { get; }
    public string? ResolutionText => ImageWidth is null || ImageHeight is null
        ? null
        : $"{ImageWidth} × {ImageHeight}";
    public int RowIndex { get; }
    public bool IsOddRow => RowIndex % 2 != 0;
    public string IconAsset { get; }

    public bool IsOriginal => _isOriginal;
    public bool IsDuplicate => !IsOriginal;
    public string Role => IsOriginal ? "Keep" : "Duplicate";

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, IsDuplicate && value);
    }

    internal void SetOriginal(bool value)
    {
        if (!SetProperty(ref _isOriginal, value, nameof(IsOriginal)))
        {
            return;
        }

        if (value)
        {
            IsSelected = false;
        }

        OnPropertyChanged(nameof(IsDuplicate));
        OnPropertyChanged(nameof(Role));
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
