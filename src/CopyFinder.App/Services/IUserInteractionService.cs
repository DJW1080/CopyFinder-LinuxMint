namespace CopyFinder.Services;

public interface IUserInteractionService
{
    Task<bool> ConfirmDeleteAsync(
        int selectedFileCount,
        CancellationToken cancellationToken);

    Task ShowMessageAsync(
        string title,
        string message,
        CancellationToken cancellationToken);
}
