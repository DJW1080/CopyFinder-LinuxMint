using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using CopyFinder.Services;
using CopyFinder.ViewModels;

namespace CopyFinder.Views;

public sealed partial class MainWindow : Window, IUserInteractionService
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async Task<bool> ConfirmDeleteAsync(
        int selectedFileCount,
        CancellationToken cancellationToken)
    {
        var dialog = CreateDialogWindow("Move duplicates to Trash", 480);
        var moveButton = new Button
        {
            Content = "Move to Trash",
            Classes = { "danger" },
            MinWidth = 130
        };
        var cancelButton = new Button { Content = "Cancel", MinWidth = 95 };
        moveButton.Click += (_, _) => dialog.Close(true);
        cancelButton.Click += (_, _) => dialog.Close(false);
        dialog.Content = CreateDialogContent(
            $"Move {selectedFileCount} selected duplicate" +
            $"{(selectedFileCount == 1 ? string.Empty : "s")} to Trash?\n\n" +
            "Every file will be checked against the scan again first. Nothing is permanently deleted.",
            cancelButton,
            moveButton);

        using var registration = cancellationToken.Register(() => dialog.Close(false));
        return await dialog.ShowDialog<bool>(this);
    }

    public async Task ShowMessageAsync(
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        var dialog = CreateDialogWindow(title, 520);
        var closeButton = new Button
        {
            Content = "OK",
            Classes = { "primary" },
            MinWidth = 90
        };
        closeButton.Click += (_, _) => dialog.Close();
        dialog.Content = CreateDialogContent(message, closeButton);

        using var registration = cancellationToken.Register(dialog.Close);
        await dialog.ShowDialog(this);
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a folder to scan",
            AllowMultiple = false
        });

        if (result.Count == 1 &&
            result[0].TryGetLocalPath() is { } path &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.FolderPath = path;
        }
    }

    private async void PreferredFolderButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the folder whose copy should be kept",
            AllowMultiple = false
        });

        if (result.Count == 1 &&
            result[0].TryGetLocalPath() is { } path &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.PreferredFolder = path;
        }
    }

    private async void ScanButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ScanAsync();
        }
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.CancelScan();
        }
    }

    private void SelectAllButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectAllDuplicates();
        }
    }

    private void DeselectAllButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.DeselectAll();
        }
    }

    private void SelectGroupButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Control { DataContext: DuplicateGroupViewModel group } &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SelectGroup(group);
        }
    }

    private void DeselectGroupButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Control { DataContext: DuplicateGroupViewModel group } &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.DeselectGroup(group);
        }
    }

    private void KeepButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Control { DataContext: DuplicateFileViewModel file } &&
            DataContext is MainWindowViewModel viewModel)
        {
            viewModel.KeepFile(file);
        }
    }

    private async void OpenButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Control { DataContext: DuplicateFileViewModel file } &&
            DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.OpenFileLocationAsync(file.Path, CancellationToken.None);
        }
    }

    private async void ExportButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export duplicate report",
            SuggestedFileName = $"CopyFinder-report-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExtension = "csv",
            FileTypeChoices =
            [
                new FilePickerFileType("CSV report") { Patterns = ["*.csv"] },
                new FilePickerFileType("JSON report") { Patterns = ["*.json"] }
            ]
        });

        if (file?.TryGetLocalPath() is { } path &&
            DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ExportAsync(path, CancellationToken.None);
        }
    }

    private async void DeleteButton_Click(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.DeleteSelectedAsync();
        }
    }

    private static Window CreateDialogWindow(string title, double width) => new()
    {
        Title = title,
        Width = width,
        MinHeight = 210,
        SizeToContent = SizeToContent.Height,
        CanResize = false,
        ShowInTaskbar = false,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        Background = new SolidColorBrush(Color.Parse("#111214"))
    };

    private static Control CreateDialogContent(string message, params Button[] buttons)
    {
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        foreach (var button in buttons)
        {
            buttonPanel.Children.Add(button);
        }

        return new Border
        {
            Padding = new Thickness(24),
            Child = new StackPanel
            {
                Spacing = 24,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 15,
                        Foreground = new SolidColorBrush(Color.Parse("#F2F2F2"))
                    },
                    buttonPanel
                }
            }
        };
    }
}
