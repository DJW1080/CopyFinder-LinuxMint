using System.Text.Json;
using CopyFinder.Models;

namespace CopyFinder.Services;

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}

public sealed class SettingsService(IXdgPaths paths) : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.SettingsFile))
        {
            return new AppSettings();
        }

        await using var stream = File.OpenRead(paths.SettingsFile);
        return await JsonSerializer
                   .DeserializeAsync<AppSettings>(stream, JsonOptions, cancellationToken)
                   .ConfigureAwait(false) ??
               new AppSettings();
    }

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = Path.GetDirectoryName(paths.SettingsFile) ??
            throw new InvalidOperationException("The settings directory could not be determined.");
        Directory.CreateDirectory(directory);

        var temporaryFile = paths.SettingsFile + ".tmp";
        await using (var stream = new FileStream(
                         temporaryFile,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 4096,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    JsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryFile, paths.SettingsFile, overwrite: true);
    }
}
