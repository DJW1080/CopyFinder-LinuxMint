namespace CopyFinder.Services;

public sealed record DuplicateDeleteCandidate(
    int GroupId,
    string DuplicatePath,
    string KeptPath,
    long ExpectedSize,
    string ExpectedHash);

public sealed record DuplicateDeleteValidation(bool CanDelete, string? FailureMessage)
{
    public static DuplicateDeleteValidation Success { get; } = new(true, null);

    public static DuplicateDeleteValidation Fail(string message) => new(false, message);
}

public sealed class DuplicateDeleteValidator
{
    private readonly IFileAccess _fileAccess;

    public DuplicateDeleteValidator(IFileAccess? fileAccess = null)
    {
        _fileAccess = fileAccess ?? new SystemFileAccess();
    }

    public async Task<DuplicateDeleteValidation> ValidateAsync(
        DuplicateDeleteCandidate candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            var duplicatePath = Path.GetFullPath(candidate.DuplicatePath);
            var keptPath = Path.GetFullPath(candidate.KeptPath);
            if (string.Equals(duplicatePath, keptPath, LinuxPathRules.PathComparison))
            {
                return DuplicateDeleteValidation.Fail(
                    $"Group {candidate.GroupId}: duplicate path is the kept file.");
            }

            if (!_fileAccess.FileExists(duplicatePath) || !_fileAccess.FileExists(keptPath))
            {
                return DuplicateDeleteValidation.Fail(
                    $"Group {candidate.GroupId}: one of the scanned files no longer exists.");
            }

            if (_fileAccess.GetFileInfo(duplicatePath).Length != candidate.ExpectedSize ||
                _fileAccess.GetFileInfo(keptPath).Length != candidate.ExpectedSize)
            {
                return DuplicateDeleteValidation.Fail(
                    $"Group {candidate.GroupId}: a file changed since scan.");
            }

            var duplicateHash = await _fileAccess
                .ComputeSha256Async(duplicatePath, cancellationToken)
                .ConfigureAwait(false);
            var keptHash = await _fileAccess
                .ComputeSha256Async(keptPath, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(duplicateHash, candidate.ExpectedHash, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(keptHash, candidate.ExpectedHash, StringComparison.OrdinalIgnoreCase))
            {
                return DuplicateDeleteValidation.Fail(
                    $"Group {candidate.GroupId}: a file changed since scan.");
            }

            return DuplicateDeleteValidation.Success;
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return DuplicateDeleteValidation.Fail(
                $"Group {candidate.GroupId}: validation failed before deletion: {exception.Message}");
        }
    }
}
