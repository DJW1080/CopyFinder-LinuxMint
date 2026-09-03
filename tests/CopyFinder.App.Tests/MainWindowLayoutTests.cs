using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using CopyFinder.Views;
using Xunit;

namespace CopyFinder.Tests;

public sealed class MainWindowLayoutTests
{
    [AvaloniaFact]
    public void MainWindow_ContainsTheOriginalPrimaryWorkflowControls()
    {
        var window = new MainWindow();

        Assert.NotNull(window.FindControl<TextBox>("FolderPathBox"));
        Assert.NotNull(window.FindControl<Button>("BrowseButton"));
        Assert.NotNull(window.FindControl<Button>("ScanButton"));
        Assert.NotNull(window.FindControl<Button>("CancelButton"));
        Assert.NotNull(window.FindControl<Expander>("SettingsPanel"));
        Assert.NotNull(window.FindControl<ItemsControl>("DuplicateGroupList"));
        Assert.NotNull(window.FindControl<Button>("ExportButton"));
        Assert.NotNull(window.FindControl<Button>("DeleteButton"));
    }
}
