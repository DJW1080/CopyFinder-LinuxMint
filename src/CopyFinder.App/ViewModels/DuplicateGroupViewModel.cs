using System.Collections.ObjectModel;
using System.ComponentModel;
using CopyFinder.Models;

namespace CopyFinder.ViewModels;

public sealed class DuplicateGroupViewModel : ObservableObject
{
    public DuplicateGroupViewModel(int groupId, IEnumerable<DuplicateFile> files)
    {
        GroupId = groupId;
        Files = new ObservableCollection<DuplicateFileViewModel>(
            files.Select((file, index) => new DuplicateFileViewModel(file, index)));

        foreach (var file in Files)
        {
            file.PropertyChanged += FileOnPropertyChanged;
        }
    }

    public int GroupId { get; }
    public ObservableCollection<DuplicateFileViewModel> Files { get; }
    public int SelectedCount => Files.Count(file => file.IsDuplicate && file.IsSelected);
    public string HashPreview => Files.FirstOrDefault()?.HashPreview ?? string.Empty;
    public string HeaderText => $"Group {GroupId}";
    public string ConfidenceText => $"Same size and SHA-256 hash {HashPreview}";

    public void SetOriginal(DuplicateFileViewModel file)
    {
        if (!Files.Contains(file))
        {
            throw new ArgumentException("File does not belong to this group.", nameof(file));
        }

        foreach (var candidate in Files)
        {
            candidate.SetOriginal(ReferenceEquals(candidate, file));
        }

        OnPropertyChanged(nameof(SelectedCount));
    }

    public void SetDuplicateSelection(bool selected)
    {
        foreach (var file in Files.Where(file => file.IsDuplicate))
        {
            file.IsSelected = selected;
        }

        OnPropertyChanged(nameof(SelectedCount));
    }

    public void RemoveFile(DuplicateFileViewModel file)
    {
        if (Files.Remove(file))
        {
            file.PropertyChanged -= FileOnPropertyChanged;
            OnPropertyChanged(nameof(SelectedCount));
        }
    }

    public bool HasDuplicates() => Files.Any(file => file.IsDuplicate);

    private void FileOnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName is nameof(DuplicateFileViewModel.IsSelected) or
            nameof(DuplicateFileViewModel.IsOriginal))
        {
            OnPropertyChanged(nameof(SelectedCount));
        }
    }
}
