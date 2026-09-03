# CopyFinder Linux Mint Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver a full-featured Linux Mint 22.3 edition of CopyFinder that matches the original interface and safely sends validated duplicates to Trash.

**Architecture:** Extract the scanner, models, reporting, and delete validation into a UI-independent .NET 10 core library. Build an Avalonia 12.1.1 application around that core, with Linux-specific XDG storage, process execution, folder opening, and GIO Trash adapters behind injectable interfaces.

**Tech Stack:** .NET 10, C# 14, Avalonia 12.1.1, SixLabors.ImageSharp 4.1.1, xUnit 2.9.3, Avalonia.Headless.XUnit 12.1.1, Bash, GIO, xdg-open.

**Spec:** `docs/superpowers/specs/2026-09-02-linux-mint-port-design.md`

## Global Constraints

- Target Linux Mint 22.3 x86-64 only; do not retain a Windows build.
- Target `net10.0` and pin Avalonia packages to `12.1.1`.
- Preserve the original interface hierarchy, dark palette, blue accents, banner, grouped duplicate cards, keep rule choices, and complete review workflow.
- Treat Linux paths as case-sensitive and never recursively follow symbolic-link directories.
- Send files to Trash through GIO and never fall back automatically to permanent deletion.
- Store settings, logs, and cache data under the XDG config, state, and cache roots respectively.
- Preserve existing uncommitted work until each file is deliberately replaced; do not reset the worktree wholesale.
- Keep Core independent of Avalonia and Linux process APIs.
- Use the approved human-AI credit wording verbatim in README and release-facing documentation.

---

## File Structure

Create this maintained source layout:

```text
Directory.Build.props
CopyFinder.sln
src/
  CopyFinder.Core/
    CopyFinder.Core.csproj
    Models/
    Services/
  CopyFinder.App/
    CopyFinder.App.csproj
    Program.cs
    App.axaml
    App.axaml.cs
    Assets/
    Services/
    ViewModels/
    Views/
tests/
  CopyFinder.Core.Tests/
  CopyFinder.App.Tests/
packaging/
  io.github.DJW1080.CopyFinder.desktop
  install.sh
publish.sh
```

The root WinUI/XAML files and Windows services are removed only after the Avalonia and Linux implementations compile and cover their behavior.

### Task 1: Extract a Buildable .NET 10 Core with a Real Regression Test

**Files:**
- Create: `Directory.Build.props`
- Create: `src/CopyFinder.Core/CopyFinder.Core.csproj`
- Move: `Models/*.cs` to `src/CopyFinder.Core/Models/`
- Move: `Services/DuplicateScanner.cs` to `src/CopyFinder.Core/Services/DuplicateScanner.cs`
- Move: `Services/DuplicateReportFormatter.cs` to `src/CopyFinder.Core/Services/DuplicateReportFormatter.cs`
- Move: `Services/DuplicateDeleteValidator.cs` to `src/CopyFinder.Core/Services/DuplicateDeleteValidator.cs`
- Create: `src/CopyFinder.Core/Services/IFileAccess.cs`
- Create: `src/CopyFinder.Core/Services/SystemFileAccess.cs`
- Create: `tests/CopyFinder.Core.Tests/CopyFinder.Core.Tests.csproj`
- Create: `tests/CopyFinder.Core.Tests/TemporaryDirectory.cs`
- Create: `tests/CopyFinder.Core.Tests/DuplicateScannerTests.cs`
- Modify: `CopyFinder.sln`

**Interfaces:**
- Produces: `IFileAccess`, `SystemFileAccess`, `IDuplicateScanner`, and `DuplicateScanner` for every later task.
- Produces: `TemporaryDirectory` for filesystem tests.

- [ ] **Step 1: Write the failing duplicate-scan test**

```csharp
[Fact]
public async Task FindDuplicatesAsync_GroupsFilesWithSameSizeAndHash()
{
    using var fixture = new TemporaryDirectory();
    await File.WriteAllTextAsync(fixture.PathFor("original.txt"), "same-content");
    await File.WriteAllTextAsync(fixture.PathFor("copy.txt"), "same-content");
    await File.WriteAllTextAsync(fixture.PathFor("different.txt"), "different");

    var result = await new DuplicateScanner().FindDuplicatesAsync(
        fixture.Path,
        new ScanOptions { MaxDuplicateFiles = 50, SkipHiddenFiles = false },
        progress: null,
        CancellationToken.None);

    Assert.Equal(2, result.Files.Count);
    Assert.Single(result.Files.Select(file => file.GroupId).Distinct());
    Assert.Equal(1, result.Files.Count(file => file.IsOriginal));
}
```

- [ ] **Step 2: Run the new test to prove the migrated project is not present yet**

Run: `dotnet test tests/CopyFinder.Core.Tests/CopyFinder.Core.Tests.csproj --filter FindDuplicatesAsync_GroupsFilesWithSameSizeAndHash`

Expected: FAIL because `CopyFinder.Core.Tests.csproj` or the referenced Core types do not exist.

- [ ] **Step 3: Add the common build properties and pinned Core dependency**

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

```xml
<!-- src/CopyFinder.Core/CopyFinder.Core.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="SixLabors.ImageSharp" Version="4.1.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Introduce injectable file access and migrate the scanner**

```csharp
public interface IFileAccess
{
    SafeDirectoryEnumerationResult EnumerateDirectories(string directory);
    SafeDirectoryEnumerationResult EnumerateFiles(string directory);
    bool FileExists(string path);
    FileInfo GetFileInfo(string path);
    Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken);
    Task WriteAllTextAsync(string path, string contents, Encoding encoding, CancellationToken cancellationToken);
}

public sealed record SafeDirectoryEnumerationResult(
    IReadOnlyList<string> Paths,
    string? ErrorMessage);

public interface IDuplicateScanner
{
    Task<DuplicateScanResult> FindDuplicatesAsync(
        string rootDirectory,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}
```

Implement `SystemFileAccess` with guarded `Directory.EnumerateDirectories`, `Directory.EnumerateFiles`, `FileInfo`, asynchronous SHA-256 streaming, and asynchronous text writing. Change `DuplicateScanner` from static `SafeFile` calls to a constructor-injected `IFileAccess`, defaulting to `SystemFileAccess`.

```csharp
public sealed class SystemFileAccess : IFileAccess
{
    public SafeDirectoryEnumerationResult EnumerateDirectories(string directory) => Enumerate(directory, Directory.EnumerateDirectories);
    public SafeDirectoryEnumerationResult EnumerateFiles(string directory) => Enumerate(directory, Directory.EnumerateFiles);
    public bool FileExists(string path) => File.Exists(path);
    public FileInfo GetFileInfo(string path) => new(Path.GetFullPath(path));

    public async Task<string> ComputeSha256Async(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token));
    }

    public async Task WriteAllTextAsync(string path, string contents, Encoding encoding, CancellationToken token)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, contents, encoding, token);
    }

    private static SafeDirectoryEnumerationResult Enumerate(
        string directory,
        Func<string, IEnumerable<string>> enumerate)
    {
        try { return new(enumerate(directory).ToArray(), null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return new([], ex.Message); }
    }
}
```

- [ ] **Step 5: Add the xUnit project and fixture helper**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.9.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <ProjectReference Include="../../src/CopyFinder.Core/CopyFinder.Core.csproj" />
  </ItemGroup>
</Project>
```

```csharp
public sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"CopyFinderTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }
    public string PathFor(string relative) => System.IO.Path.Combine(Path, relative);
    public void Dispose() => Directory.Delete(Path, recursive: true);
}
```

- [ ] **Step 6: Run Core tests and the solution build**

Run: `dotnet test tests/CopyFinder.Core.Tests/CopyFinder.Core.Tests.csproj`

Expected: PASS, including `FindDuplicatesAsync_GroupsFilesWithSameSizeAndHash`.

Run: `dotnet build CopyFinder.sln -warnaserror`

Expected: PASS after the solution references only the new buildable projects.

- [ ] **Step 7: Commit the Core extraction**

```bash
git add Directory.Build.props CopyFinder.sln src/CopyFinder.Core tests/CopyFinder.Core.Tests Models Services
git commit -m "refactor: extract cross-platform duplicate scanner core"
```

### Task 2: Make Scanning Correct for Linux Paths, Dotfiles, and Links

**Files:**
- Create: `src/CopyFinder.Core/Services/LinuxPathRules.cs`
- Modify: `src/CopyFinder.Core/Services/DuplicateScanner.cs`
- Modify: `src/CopyFinder.Core/Services/DuplicateDeleteValidator.cs`
- Modify: `src/CopyFinder.Core/Models/AppSettings.cs`
- Modify: `src/CopyFinder.Core/Models/ScanOptions.cs`
- Create: `tests/CopyFinder.Core.Tests/LinuxScanSemanticsTests.cs`
- Create: `tests/CopyFinder.Core.Tests/DuplicateDeleteValidatorTests.cs`

**Interfaces:**
- Consumes: `IFileAccess`, `DuplicateScanner`, `ScanOptions`.
- Produces: `LinuxPathRules.PathComparer`, `LinuxPathRules.PathComparison`, and an injectable `DuplicateDeleteValidator`.

- [ ] **Step 1: Write failing tests for Linux-specific behavior**

```csharp
[Fact]
public async Task Scan_SkipsLeadingDotFilesWhenRequested()
{
    using var fixture = new TemporaryDirectory();
    await File.WriteAllTextAsync(fixture.PathFor(".hidden"), "duplicate");
    await File.WriteAllTextAsync(fixture.PathFor("visible"), "duplicate");

    var result = await new DuplicateScanner().FindDuplicatesAsync(
        fixture.Path,
        new ScanOptions { SkipHiddenFiles = true }, null, CancellationToken.None);

    Assert.Empty(result.Files);
}

[Fact]
public async Task Scan_DoesNotFollowSymbolicLinkDirectories()
{
    using var fixture = new TemporaryDirectory();
    var real = Directory.CreateDirectory(fixture.PathFor("real")).FullName;
    await File.WriteAllTextAsync(System.IO.Path.Combine(real, "a.txt"), "same");
    Directory.CreateSymbolicLink(fixture.PathFor("link"), real);

    var result = await new DuplicateScanner().FindDuplicatesAsync(
        fixture.Path,
        new ScanOptions { SkipHiddenFiles = false }, null, CancellationToken.None);

    Assert.Empty(result.Files);
}

[Fact]
public async Task PreferFolder_UsesCaseSensitivePathIdentity()
{
    using var fixture = new TemporaryDirectory();
    var upper = Directory.CreateDirectory(fixture.PathFor("Folder")).FullName;
    var lower = Directory.CreateDirectory(fixture.PathFor("folder")).FullName;
    await File.WriteAllTextAsync(System.IO.Path.Combine(upper, "a.txt"), "same");
    await File.WriteAllTextAsync(System.IO.Path.Combine(lower, "b.txt"), "same");

    var result = await new DuplicateScanner().FindDuplicatesAsync(
        fixture.Path,
        new ScanOptions { KeepRule = KeepRule.PreferFolder, PreferredFolder = lower },
        null,
        CancellationToken.None);

    Assert.StartsWith(lower + System.IO.Path.DirectorySeparatorChar,
        Assert.Single(result.Files.Where(file => file.IsOriginal)).Path,
        StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run Linux semantic tests and verify at least the dotfile or case test fails**

Run: `dotnet test tests/CopyFinder.Core.Tests/CopyFinder.Core.Tests.csproj --filter LinuxScanSemanticsTests`

Expected: FAIL because the inherited implementation uses Windows attributes and case-insensitive path comparisons.

- [ ] **Step 3: Centralize Linux path rules**

```csharp
public static class LinuxPathRules
{
    public static StringComparer PathComparer { get; } = StringComparer.Ordinal;
    public const StringComparison PathComparison = StringComparison.Ordinal;

    public static bool IsHidden(FileInfo file) =>
        file.Name.StartsWith('.', StringComparison.Ordinal);

    public static bool IsLinkedDirectory(string path)
    {
        var info = new DirectoryInfo(path);
        return info.LinkTarget is not null || info.Attributes.HasFlag(FileAttributes.ReparsePoint);
    }
}
```

Use these rules for visited directories, image metadata keys, preferred-folder matching, final tie-breaking, and duplicate-path equality. Continue using case-insensitive comparison only for SHA-256 hex strings and extension filters.

Remove `AppSettings.LastCompatibilityReportVersion` and `ScanOptions.SkipSystemFiles`; both represented Windows-only deployment concepts. Keep `SkipHiddenFiles` and implement it through `LinuxPathRules.IsHidden`.

- [ ] **Step 4: Make delete validation injectable and case-correct**

```csharp
public sealed class DuplicateDeleteValidator(IFileAccess fileAccess)
{
    public DuplicateDeleteValidator() : this(new SystemFileAccess()) { }

    public async Task<DuplicateDeleteValidation> ValidateAsync(
        DuplicateDeleteCandidate candidate,
        CancellationToken cancellationToken)
    {
        try
        {
            var duplicatePath = Path.GetFullPath(candidate.DuplicatePath);
            var keptPath = Path.GetFullPath(candidate.KeptPath);
            if (string.Equals(duplicatePath, keptPath, LinuxPathRules.PathComparison))
                return DuplicateDeleteValidation.Fail($"Group {candidate.GroupId}: duplicate path is the kept file.");
            if (!fileAccess.FileExists(duplicatePath))
                return DuplicateDeleteValidation.Fail($"Group {candidate.GroupId}: duplicate file no longer exists: {duplicatePath}");
            if (!fileAccess.FileExists(keptPath))
                return DuplicateDeleteValidation.Fail($"Group {candidate.GroupId}: kept file no longer exists: {keptPath}");
            if (fileAccess.GetFileInfo(duplicatePath).Length != candidate.ExpectedSize ||
                fileAccess.GetFileInfo(keptPath).Length != candidate.ExpectedSize)
                return DuplicateDeleteValidation.Fail($"Group {candidate.GroupId}: a file changed since scan.");

            var duplicateHash = await fileAccess.ComputeSha256Async(duplicatePath, cancellationToken);
            var keptHash = await fileAccess.ComputeSha256Async(keptPath, cancellationToken);
            return string.Equals(duplicateHash, candidate.ExpectedHash, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(keptHash, candidate.ExpectedHash, StringComparison.OrdinalIgnoreCase)
                ? DuplicateDeleteValidation.Success
                : DuplicateDeleteValidation.Fail($"Group {candidate.GroupId}: a file changed since scan.");
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return DuplicateDeleteValidation.Fail($"Group {candidate.GroupId}: validation failed before deletion: {ex.Message}");
        }
    }
}
```

- [ ] **Step 5: Add pre-delete mutation coverage**

```csharp
[Fact]
public async Task ValidateAsync_RejectsDuplicateChangedAfterScan()
{
    using var fixture = new TemporaryDirectory();
    var kept = fixture.PathFor("kept.txt");
    var duplicate = fixture.PathFor("duplicate.txt");
    await File.WriteAllTextAsync(kept, "same");
    await File.WriteAllTextAsync(duplicate, "same");
    var hash = await new SystemFileAccess().ComputeSha256Async(kept, CancellationToken.None);
    var candidate = new DuplicateDeleteCandidate(1, duplicate, kept, 4, hash);
    await File.WriteAllTextAsync(duplicate, "changed");

    var result = await new DuplicateDeleteValidator().ValidateAsync(candidate, CancellationToken.None);

    Assert.False(result.CanDelete);
    Assert.Contains("changed since scan", result.FailureMessage);
}
```

- [ ] **Step 6: Run all Core tests**

Run: `dotnet test tests/CopyFinder.Core.Tests/CopyFinder.Core.Tests.csproj`

Expected: PASS for duplicate grouping, dotfiles, links, case-sensitive preferred folders, cancellation, exclusions, minimum size, and delete mutation.

- [ ] **Step 7: Commit Linux scan semantics**

```bash
git add src/CopyFinder.Core tests/CopyFinder.Core.Tests
git commit -m "fix: apply Linux filesystem semantics"
```

### Task 3: Implement XDG Storage, Logging, Safe Process Execution, Shell Opening, and Trash

**Files:**
- Create: `src/CopyFinder.App/CopyFinder.App.csproj`
- Create: `src/CopyFinder.App/Services/XdgPaths.cs`
- Create: `src/CopyFinder.App/Services/AppLogger.cs`
- Create: `src/CopyFinder.App/Services/SettingsService.cs`
- Create: `src/CopyFinder.App/Services/IProcessRunner.cs`
- Create: `src/CopyFinder.App/Services/SystemProcessRunner.cs`
- Create: `src/CopyFinder.App/Services/LinuxTrashService.cs`
- Create: `src/CopyFinder.App/Services/LinuxShellService.cs`
- Create: `tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj`
- Create: `tests/CopyFinder.App.Tests/XdgPathsTests.cs`
- Create: `tests/CopyFinder.App.Tests/LinuxTrashServiceTests.cs`
- Create: `tests/CopyFinder.App.Tests/LinuxShellServiceTests.cs`
- Create: `tests/CopyFinder.App.Tests/Fakes/RecordingProcessRunner.cs`
- Modify: `CopyFinder.sln`

**Interfaces:**
- Produces: `IXdgPaths`, `ISettingsService`, `IAppLogger`, `IProcessRunner`, `ITrashService`, and `IShellService`.
- Consumes: `AppSettings` from Core.

- [ ] **Step 1: Create the App test project before writing service tests**

```xml
<!-- src/CopyFinder.App/CopyFinder.App.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="../CopyFinder.Core/CopyFinder.Core.csproj" />
  </ItemGroup>
</Project>
```

```xml
<!-- tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><IsPackable>false</IsPackable></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.9.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <ProjectReference Include="../../src/CopyFinder.Core/CopyFinder.Core.csproj" />
    <ProjectReference Include="../../src/CopyFinder.App/CopyFinder.App.csproj" />
  </ItemGroup>
</Project>
```

Add both App projects to `CopyFinder.sln` before running the service tests.

- [ ] **Step 2: Write failing XDG and safe-argument tests**

```csharp
[Fact]
public void XdgPaths_UsesEnvironmentOverrides()
{
    var paths = new XdgPaths(
        name => name switch
        {
            "XDG_CONFIG_HOME" => "/tmp/config",
            "XDG_STATE_HOME" => "/tmp/state",
            "XDG_CACHE_HOME" => "/tmp/cache",
            _ => null
        },
        homeDirectory: "/home/tester");

    Assert.Equal("/tmp/config/CopyFinder/settings.json", paths.SettingsFile);
    Assert.Equal("/tmp/state/CopyFinder/logs/copyfinder.log", paths.LogFile);
    Assert.Equal("/tmp/cache/CopyFinder", paths.CacheDirectory);
}

[Fact]
public async Task TrashAsync_PassesDangerousLookingPathAsOneArgument()
{
    var runner = new RecordingProcessRunner(new ProcessResult(0, "", ""));
    var service = new LinuxTrashService(runner);
    var path = "/tmp/name; touch SHOULD_NOT_RUN.txt";

    var result = await service.TrashAsync(path, CancellationToken.None);

    Assert.True(result.Succeeded);
    Assert.Equal("/usr/bin/gio", runner.FileName);
    Assert.Equal(new[] { "trash", "--", Path.GetFullPath(path) }, runner.Arguments);
}

public sealed class RecordingProcessRunner(ProcessResult result) : IProcessRunner
{
    public string? FileName { get; private set; }
    public IReadOnlyList<string> Arguments { get; private set; } = [];
    public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken token)
    {
        FileName = fileName;
        Arguments = arguments.ToArray();
        return Task.FromResult(result);
    }
}
```

- [ ] **Step 3: Run service tests and verify they fail because services are absent**

Run: `dotnet test tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj --filter "XdgPathsTests|LinuxTrashServiceTests"`

Expected: FAIL with missing types.

- [ ] **Step 4: Implement exact XDG fallback rules**

```csharp
public interface IXdgPaths
{
    string SettingsFile { get; }
    string LogFile { get; }
    string CacheDirectory { get; }
}

public sealed class XdgPaths : IXdgPaths
{
    public XdgPaths(Func<string, string?>? getEnvironment = null, string? homeDirectory = null)
    {
        getEnvironment ??= Environment.GetEnvironmentVariable;
        homeDirectory ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var config = getEnvironment("XDG_CONFIG_HOME") ?? Path.Combine(homeDirectory, ".config");
        var state = getEnvironment("XDG_STATE_HOME") ?? Path.Combine(homeDirectory, ".local", "state");
        var cache = getEnvironment("XDG_CACHE_HOME") ?? Path.Combine(homeDirectory, ".cache");
        SettingsFile = Path.Combine(config, "CopyFinder", "settings.json");
        LogFile = Path.Combine(state, "CopyFinder", "logs", "copyfinder.log");
        CacheDirectory = Path.Combine(cache, "CopyFinder");
    }

    public string SettingsFile { get; }
    public string LogFile { get; }
    public string CacheDirectory { get; }
}
```

- [ ] **Step 5: Implement a process runner without shell parsing**

```csharp
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken token);
}

public sealed class SystemProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo(fileName) {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(token);
        var standardError = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token);
        return new ProcessResult(process.ExitCode,
            await standardOutput,
            await standardError);
    }
}
```

- [ ] **Step 6: Implement Trash and shell adapters with no permanent-delete fallback**

```csharp
public interface ITrashService
{
    Task<TrashResult> TrashAsync(string path, CancellationToken token);
}

public sealed record TrashResult(bool Succeeded, string Path, string Message);

public sealed class LinuxTrashService(IProcessRunner runner) : ITrashService
{
    public async Task<TrashResult> TrashAsync(string path, CancellationToken token)
    {
        var fullPath = Path.GetFullPath(path);
        var result = await runner.RunAsync("/usr/bin/gio", ["trash", "--", fullPath], token);
        return result.ExitCode == 0
            ? new TrashResult(true, fullPath, "Moved to Trash.")
            : new TrashResult(false, fullPath,
                string.IsNullOrWhiteSpace(result.StandardError) ? "Trash operation failed." : result.StandardError.Trim());
    }
}
```

Implement `LinuxShellService.OpenContainingFolderAsync(path, token)` by resolving the existing file's parent (or the path itself when it is a directory) and invoking `/usr/bin/xdg-open` with that directory as one argument.

```csharp
public interface IShellService
{
    Task<ProcessResult> OpenContainingFolderAsync(string path, CancellationToken token);
}

public sealed class LinuxShellService(IProcessRunner runner) : IShellService
{
    public Task<ProcessResult> OpenContainingFolderAsync(string path, CancellationToken token)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Directory.Exists(fullPath) ? fullPath : Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException("Could not determine the containing folder.", nameof(path));
        return runner.RunAsync("/usr/bin/xdg-open", [directory], token);
    }
}
```

- [ ] **Step 7: Implement JSON settings and append-only logging**

Ensure the destination directory is created immediately before reading/writing. Save settings atomically by writing `settings.json.tmp`, flushing it, and using `File.Move(temp, target, overwrite: true)`. Log ISO-8601 timestamps to `IXdgPaths.LogFile`; if logging fails, do not crash the application.

```csharp
public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken token);
    Task SaveAsync(AppSettings settings, CancellationToken token);
}

public interface IAppLogger
{
    void Log(string category, string message, Exception? exception = null);
}

public sealed class SettingsService(IXdgPaths paths) : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public async Task<AppSettings> LoadAsync(CancellationToken token)
    {
        if (!File.Exists(paths.SettingsFile)) return new AppSettings();
        await using var stream = File.OpenRead(paths.SettingsFile);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, token) ?? new AppSettings();
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken token)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(paths.SettingsFile)!);
        var temp = paths.SettingsFile + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None,
            4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, token);
        File.Move(temp, paths.SettingsFile, overwrite: true);
    }
}

public sealed class AppLogger(IXdgPaths paths) : IAppLogger
{
    public void Log(string category, string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.LogFile)!);
            File.AppendAllText(paths.LogFile,
                $"{DateTimeOffset.Now:O} [{category}] {message}{(exception is null ? "" : $" {exception.GetType().Name}: {exception.Message}")}{Environment.NewLine}");
        }
        catch { }
    }
}
```

- [ ] **Step 8: Run all platform-service tests**

Run: `dotnet test tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj --filter "XdgPathsTests|LinuxTrashServiceTests|LinuxShellServiceTests"`

Expected: PASS, including nonzero GIO exit behavior leaving deletion decisions to the caller.

- [ ] **Step 9: Commit Linux platform services**

```bash
git add src/CopyFinder.App/Services tests/CopyFinder.App.Tests
git commit -m "feat: add Linux Mint platform services"
```

### Task 4: Port Duplicate Review State Without UI Toolkit Types

**Files:**
- Create: `src/CopyFinder.App/ViewModels/ObservableObject.cs`
- Create: `src/CopyFinder.App/ViewModels/DuplicateFileViewModel.cs`
- Create: `src/CopyFinder.App/ViewModels/DuplicateGroupViewModel.cs`
- Create: `src/CopyFinder.App/Services/FileIconResolver.cs`
- Create: `tests/CopyFinder.App.Tests/DuplicateGroupViewModelTests.cs`
- Create: `tests/CopyFinder.App.Tests/FileIconResolverTests.cs`

**Interfaces:**
- Consumes: `DuplicateFile` from Core.
- Produces: UI-neutral file/group view models with property-change notifications.
- Produces: `DuplicateFileViewModel(DuplicateFile file, int rowIndex)` and `DuplicateGroupViewModel(int groupId, IEnumerable<DuplicateFile> files)` constructors.

- [ ] **Step 1: Write failing keep/selection tests**

```csharp
[Fact]
public void SetOriginal_MakesExactlyOneFileTheKeptFile()
{
    var first = Duplicate(1, "/tmp/a.txt", isOriginal: true);
    var second = Duplicate(1, "/tmp/b.txt", isOriginal: false);
    var group = new DuplicateGroupViewModel(1, [first, second]);

    group.SetOriginal(group.Files[1]);

    Assert.False(group.Files[0].IsOriginal);
    Assert.True(group.Files[1].IsOriginal);
    Assert.False(group.Files[1].IsSelected);
    Assert.Single(group.Files.Where(file => file.IsOriginal));
}

[Fact]
public void SelectDuplicates_NeverSelectsKeptFile()
{
    var group = CreateTwoFileGroup();
    group.SetDuplicateSelection(true);
    Assert.False(group.Files.Single(file => file.IsOriginal).IsSelected);
    Assert.True(group.Files.Single(file => file.IsDuplicate).IsSelected);
}

private static DuplicateFile Duplicate(int groupId, string path, bool isOriginal) =>
    new(groupId, path, 4, "AA", DateTime.UnixEpoch, null, null, isOriginal);

private static DuplicateGroupViewModel CreateTwoFileGroup() =>
    new(1,
    [
        Duplicate(1, "/tmp/a.txt", isOriginal: true),
        Duplicate(1, "/tmp/b.txt", isOriginal: false)
    ]);
```

- [ ] **Step 2: Run view-model tests and verify missing-type failure**

Run: `dotnet test tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj --filter DuplicateGroupViewModelTests`

Expected: FAIL because the Avalonia-neutral view models do not exist.

- [ ] **Step 3: Implement observable file state**

`DuplicateFileViewModel` exposes `GroupId`, `Path`, `FileName`, `Size`, `SizeText`, `Hash`, `LastWriteTime`, `LastWriteText`, `ImageWidth`, `ImageHeight`, `IsOriginal`, `IsDuplicate`, `IsSelected`, `Role`, `RowIndex`, `IsOddRow`, and `IconAsset`. Changing `IsOriginal` clears `IsSelected`; changing either flag raises notifications for dependent properties.

```csharp
public bool IsSelected
{
    get => _isSelected;
    set => SetProperty(ref _isSelected, IsDuplicate && value);
}

internal void SetOriginal(bool value)
{
    if (SetProperty(ref _isOriginal, value, nameof(IsOriginal)))
    {
        if (value) IsSelected = false;
        OnPropertyChanged(nameof(IsDuplicate));
        OnPropertyChanged(nameof(Role));
    }
}
```

- [ ] **Step 4: Implement group invariants and asset resolution**

`DuplicateGroupViewModel.SetOriginal(file)` rejects files from another group, updates every file, and raises `SelectedCount`. `SetDuplicateSelection(bool)` affects duplicates only. Resolve supported images to their local file URI and all other extensions to `avares://CopyFinder/Assets/FileIcons/<icon>.png`, preserving the original extension categories.

```csharp
public void SetOriginal(DuplicateFileViewModel file)
{
    if (!Files.Contains(file)) throw new ArgumentException("File does not belong to this group.", nameof(file));
    foreach (var candidate in Files) candidate.SetOriginal(ReferenceEquals(candidate, file));
    OnPropertyChanged(nameof(SelectedCount));
}

public void SetDuplicateSelection(bool selected)
{
    foreach (var file in Files.Where(file => file.IsDuplicate)) file.IsSelected = selected;
    OnPropertyChanged(nameof(SelectedCount));
}

public static string Resolve(string path) => Path.GetExtension(path).ToLowerInvariant() switch
{
    ".jpg" or ".jpeg" or ".png" or ".bmp" or ".gif" or ".tif" or ".tiff" or ".webp"
        => new Uri(Path.GetFullPath(path)).AbsoluteUri,
    ".mp3" or ".flac" or ".wav" or ".m4a" or ".aac" or ".ogg"
        => "avares://CopyFinder/Assets/FileIcons/file-audio.png",
    ".pdf" => "avares://CopyFinder/Assets/FileIcons/file-pdf.png",
    ".cs" or ".js" or ".ts" or ".json" or ".xml" or ".html" or ".css" or ".py" or ".sql"
        => "avares://CopyFinder/Assets/FileIcons/file-code.png",
    _ => "avares://CopyFinder/Assets/FileIcons/file-generic.png"
};
```

- [ ] **Step 5: Run review-state tests**

Run: `dotnet test tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj --filter "DuplicateGroupViewModelTests|FileIconResolverTests"`

Expected: PASS for one-kept-file, duplicate-only selection, row alternation, and icon categories.

- [ ] **Step 6: Commit review state**

```bash
git add src/CopyFinder.App/ViewModels src/CopyFinder.App/Services/FileIconResolver.cs tests/CopyFinder.App.Tests
git commit -m "feat: port duplicate review state"
```

### Task 5: Implement the Full Scan, Export, Open, and Validated Trash Workflow

**Files:**
- Create: `src/CopyFinder.App/ViewModels/MainWindowViewModel.cs`
- Create: `src/CopyFinder.App/Services/IUserInteractionService.cs`
- Create: `tests/CopyFinder.App.Tests/MainWindowViewModelTests.cs`
- Create: `tests/CopyFinder.App.Tests/Fakes/FakeDuplicateScanner.cs`
- Create: `tests/CopyFinder.App.Tests/Fakes/FakeUserInteractionService.cs`
- Create: `tests/CopyFinder.App.Tests/Fakes/RecordingTrashService.cs`
- Create: `tests/CopyFinder.App.Tests/Fakes/InMemorySettingsService.cs`
- Create: `tests/CopyFinder.App.Tests/TemporaryDirectory.cs`
- Modify: `src/CopyFinder.Core/Services/DuplicateReportFormatter.cs`

**Interfaces:**
- Consumes: `IDuplicateScanner`, `DuplicateDeleteValidator`, `ITrashService`, `IShellService`, `ISettingsService`, `IFileAccess`, `IUserInteractionService`.
- Produces: `MainWindowViewModel` methods `ScanAsync`, `CancelScan`, `ExportAsync`, `OpenFileLocationAsync`, `DeleteSelectedAsync`, `SelectAllDuplicates`, `DeselectAll`, `SelectGroup`, `DeselectGroup`, and `KeepFile`.

```csharp
public interface IUserInteractionService
{
    Task<bool> ConfirmDeleteAsync(int selectedFileCount, CancellationToken token);
    Task ShowMessageAsync(string title, string message, CancellationToken token);
}

public sealed class FakeDuplicateScanner(DuplicateScanResult result) : IDuplicateScanner
{
    public static FakeDuplicateScanner Returning(DuplicateScanResult result) => new(result);
    public Task<DuplicateScanResult> FindDuplicatesAsync(
        string rootDirectory,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken token) => Task.FromResult(result);
}

public sealed class FakeUserInteractionService(bool confirmDelete = true) : IUserInteractionService
{
    public List<string> Messages { get; } = [];
    public Task<bool> ConfirmDeleteAsync(int selectedFileCount, CancellationToken token) => Task.FromResult(confirmDelete);
    public Task ShowMessageAsync(string title, string message, CancellationToken token)
    {
        Messages.Add($"{title}: {message}");
        return Task.CompletedTask;
    }
}

public sealed class RecordingTrashService : ITrashService
{
    public List<string> Paths { get; } = [];
    public Task<TrashResult> TrashAsync(string path, CancellationToken token)
    {
        Paths.Add(path);
        return Task.FromResult(new TrashResult(true, path, "Moved to Trash."));
    }
}

public sealed class InMemorySettingsService : ISettingsService
{
    public AppSettings Settings { get; private set; } = new();
    public Task<AppSettings> LoadAsync(CancellationToken token) => Task.FromResult(Settings);
    public Task SaveAsync(AppSettings settings, CancellationToken token)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 1: Write failing workflow tests**

```csharp
[Fact]
public async Task ScanAsync_PopulatesGroupsAndEnablesReviewActions()
{
    using var fixture = new TemporaryDirectory();
    var scanner = FakeDuplicateScanner.Returning(TwoGroups(fixture.Path));
    var vm = CreateViewModel(scanner: scanner);
    vm.FolderPath = fixture.Path;

    await vm.ScanAsync();

    Assert.Equal(2, vm.DuplicateGroups.Count);
    Assert.True(vm.CanExport);
    Assert.False(vm.IsScanning);
    Assert.Contains("2 duplicate groups", vm.SummaryText);
}

[Fact]
public async Task DeleteSelectedAsync_ValidatesBeforeCallingTrash()
{
    using var fixture = new TemporaryDirectory();
    await File.WriteAllTextAsync(fixture.PathFor("kept.txt"), "same");
    await File.WriteAllTextAsync(fixture.PathFor("duplicate.txt"), "same");
    var scanResult = await new DuplicateScanner().FindDuplicatesAsync(
        fixture.Path, new ScanOptions(), null, CancellationToken.None);
    var trash = new RecordingTrashService();
    var vm = CreateViewModel(trash: trash, scanResult: scanResult);
    vm.FolderPath = fixture.Path;
    await vm.ScanAsync();
    vm.SelectAllDuplicates();

    await vm.DeleteSelectedAsync();

    Assert.Equal(scanResult.DuplicateFileCount, trash.Paths.Count);
    Assert.All(trash.Paths, path => Assert.False(vm.AllFiles.Any(file => file.Path == path)));
}

[Fact]
public async Task DeleteSelectedAsync_DoesNotTrashAChangedFile()
{
    using var fixture = new TemporaryDirectory();
    await File.WriteAllTextAsync(fixture.PathFor("kept.txt"), "same");
    await File.WriteAllTextAsync(fixture.PathFor("duplicate.txt"), "same");
    var scanResult = await new DuplicateScanner().FindDuplicatesAsync(
        fixture.Path, new ScanOptions(), null, CancellationToken.None);
    var trash = new RecordingTrashService();
    var vm = CreateViewModel(trash: trash, scanResult: scanResult);
    vm.FolderPath = fixture.Path;
    await vm.ScanAsync();
    vm.SelectAllDuplicates();
    await File.WriteAllTextAsync(vm.AllFiles.Single(file => file.IsSelected).Path, "changed");

    await vm.DeleteSelectedAsync();

    Assert.Empty(trash.Paths);
    Assert.Contains("could not be moved", vm.StatusText);
}

private static DuplicateScanResult TwoGroups(string root) => new(
[
    new(1, Path.Combine(root, "a.txt"), 4, "AA", DateTime.Now, null, null, true),
    new(1, Path.Combine(root, "b.txt"), 4, "AA", DateTime.Now, null, null, false),
    new(2, Path.Combine(root, "c.txt"), 5, "BB", DateTime.Now, null, null, true),
    new(2, Path.Combine(root, "d.txt"), 5, "BB", DateTime.Now, null, null, false)
], false, 2);

private static MainWindowViewModel CreateViewModel(
    IDuplicateScanner? scanner = null,
    ITrashService? trash = null,
    DuplicateScanResult? scanResult = null)
{
    scanner ??= FakeDuplicateScanner.Returning(scanResult ?? new([], false, 0));
    var files = new SystemFileAccess();
    return new MainWindowViewModel(
        scanner,
        files,
        new DuplicateDeleteValidator(files),
        trash ?? new RecordingTrashService(),
        new RecordingShellService(),
        new InMemorySettingsService(),
        new FakeUserInteractionService());
}

private sealed class RecordingShellService : IShellService
{
    public Task<ProcessResult> OpenContainingFolderAsync(string path, CancellationToken token) =>
        Task.FromResult(new ProcessResult(0, "", ""));
}
```

- [ ] **Step 2: Run workflow tests and verify missing MainWindowViewModel failure**

Run: `dotnet test tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj --filter MainWindowViewModelTests`

Expected: FAIL because the orchestration view model does not exist.

- [ ] **Step 3: Implement scan lifecycle and settings projection**

`ScanAsync` validates `FolderPath`, saves settings, creates a new `CancellationTokenSource`, clears old groups, reports progress through `Progress<ScanProgress>`, calls `IDuplicateScanner`, maps results into group view models, and restores action state in `finally`. `CancelScan` only cancels the active token source.

Use this constructor so tests and the production composition root share one dependency contract:

```csharp
public MainWindowViewModel(
    IDuplicateScanner scanner,
    IFileAccess fileAccess,
    DuplicateDeleteValidator deleteValidator,
    ITrashService trashService,
    IShellService shell,
    ISettingsService settings,
    IUserInteractionService interaction)
```

Expose these stable properties for XAML binding: `FolderPath`, `KeepRule`, `PreferredFolder`, `HashWorkers`, `ScanLimit`, `MinimumSizeKb`, `SkipHiddenFiles`, `ExcludedExtensionsText`, `IsScanning`, `SummaryText`, `StatusText`, `DuplicateGroups`, `AllFiles`, `CanDelete`, `CanExport`, `CanSelectAll`, and `CanDeselectAll`.

```csharp
public async Task ScanAsync()
{
    if (!Directory.Exists(FolderPath))
    {
        await interaction.ShowMessageAsync("CopyFinder", "Choose an existing folder before scanning.", CancellationToken.None);
        return;
    }

    _scanCancellation = new CancellationTokenSource();
    IsScanning = true;
    DuplicateGroups.Clear();
    try
    {
        var options = BuildScanOptions();
        await settings.SaveAsync(BuildSettings(options), _scanCancellation.Token);
        var progress = new Progress<ScanProgress>(value => StatusText = value.Message);
        var result = await scanner.FindDuplicatesAsync(FolderPath, options, progress, _scanCancellation.Token);
        foreach (var group in result.Files.GroupBy(file => file.GroupId))
            DuplicateGroups.Add(new DuplicateGroupViewModel(group.Key, group));
        RefreshSummary(result.LimitReached ? "Scan stopped for review." : "Scan complete.");
    }
    catch (OperationCanceledException) { StatusText = "Scan canceled."; }
    finally
    {
        _scanCancellation.Dispose();
        _scanCancellation = null;
        IsScanning = false;
        RefreshActionState();
    }
}
```

- [ ] **Step 4: Implement report export and shell opening**

`ExportAsync(path)` chooses JSON when the extension is `.json`, otherwise CSV, and calls `IFileAccess.WriteAllTextAsync`. `OpenFileLocationAsync(path)` calls `IShellService` and reports nonzero process results through `IUserInteractionService`.

```csharp
public async Task ExportAsync(string path, CancellationToken token)
{
    var report = CreateReportFiles();
    var content = string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase)
        ? DuplicateReportFormatter.BuildJsonReport(report)
        : DuplicateReportFormatter.BuildCsvReport(report);
    await fileAccess.WriteAllTextAsync(path, content, Encoding.UTF8, token);
    StatusText = $"Report exported: {path}";
}

public async Task OpenFileLocationAsync(string path, CancellationToken token)
{
    var result = await shell.OpenContainingFolderAsync(path, token);
    if (result.ExitCode != 0)
        await interaction.ShowMessageAsync("CopyFinder", result.StandardError.Trim(), token);
}
```

- [ ] **Step 5: Implement confirm-validate-trash-remove order**

```csharp
foreach (var selected in selectedFiles)
{
    var kept = DuplicateGroups.Single(group => group.GroupId == selected.GroupId)
        .Files.Single(file => file.IsOriginal);
    var candidate = new DuplicateDeleteCandidate(
        selected.GroupId, selected.Path, kept.Path, selected.Size, selected.Hash);
    var validation = await deleteValidator.ValidateAsync(candidate, cancellationToken);
    if (!validation.CanDelete)
    {
        failures.Add(validation.FailureMessage!);
        continue;
    }

    var trashResult = await trashService.TrashAsync(selected.Path, cancellationToken);
    if (!trashResult.Succeeded)
    {
        failures.Add($"{selected.Path}: {trashResult.Message}");
        continue;
    }

    RemoveFileFromGroups(selected);
}
```

Ask for confirmation once before the loop. Remove a row only after GIO reports success. Remove a group only when it no longer contains a duplicate.

- [ ] **Step 6: Run workflow tests and all test projects**

Run: `dotnet test tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj --filter MainWindowViewModelTests`

Expected: PASS.

Run: `dotnet test CopyFinder.sln`

Expected: PASS for Core and App tests.

- [ ] **Step 7: Commit workflow orchestration**

```bash
git add src/CopyFinder.App/ViewModels/MainWindowViewModel.cs src/CopyFinder.App/Services tests/CopyFinder.App.Tests src/CopyFinder.Core/Services/DuplicateReportFormatter.cs
git commit -m "feat: implement Linux duplicate review workflow"
```

### Task 6: Recreate the Original Interface in Avalonia

**Files:**
- Modify: `src/CopyFinder.App/CopyFinder.App.csproj`
- Create: `src/CopyFinder.App/Program.cs`
- Create: `src/CopyFinder.App/App.axaml`
- Create: `src/CopyFinder.App/App.axaml.cs`
- Create: `src/CopyFinder.App/Views/MainWindow.axaml`
- Create: `src/CopyFinder.App/Views/MainWindow.axaml.cs`
- Copy: `Technification/Logo/CopyFinder-Banner-08.png` to `src/CopyFinder.App/Assets/CopyFinder-Banner-08.png`
- Copy: `Technification/FileIcons/*.png` to `src/CopyFinder.App/Assets/FileIcons/`
- Create: `src/CopyFinder.App/Assets/CopyFinder-icon.png` from the existing multi-size favicon
- Create: `tests/CopyFinder.App.Tests/AppBuilderFactory.cs`
- Create: `tests/CopyFinder.App.Tests/MainWindowLayoutTests.cs`
- Modify: `tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj`
- Modify: `CopyFinder.sln`

**Interfaces:**
- Consumes: every `MainWindowViewModel` property and action from Task 5.
- Produces: runnable `CopyFinder.App` and a headless-testable `MainWindow`.

- [ ] **Step 1: Write a failing headless layout test**

```csharp
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
```

- [ ] **Step 2: Run the layout test and verify it fails because the Avalonia window is absent**

Run: `dotnet test tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj --filter MainWindow_ContainsTheOriginalPrimaryWorkflowControls`

Expected: FAIL with missing `MainWindow`.

- [ ] **Step 3: Configure the Avalonia application**

Extract the 48px favicon frame to a Linux desktop icon before configuring resources:

Run: `ffmpeg -y -i Technification/Logo/favicon/favicon.ico -vf scale=48:48 -frames:v 1 src/CopyFinder.App/Assets/CopyFinder-icon.png`

Expected: a 48x48 PNG with the existing CopyFinder cube mark.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <AssemblyName>CopyFinder</AssemblyName>
    <Version>3.0.0</Version>
    <InformationalVersion>3.0.0-linux</InformationalVersion>
    <RuntimeIdentifiers>linux-x64</RuntimeIdentifiers>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="12.1.1" />
    <PackageReference Include="Avalonia.Desktop" Version="12.1.1" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="12.1.1" />
    <ProjectReference Include="../CopyFinder.Core/CopyFinder.Core.csproj" />
    <AvaloniaResource Include="Assets/**" Exclude="Assets/CopyFinder-icon.png" />
    <Content Include="Assets/CopyFinder-icon.png"
             CopyToOutputDirectory="PreserveNewest"
             CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>
</Project>
```

Add `<PackageReference Include="Avalonia.Headless.XUnit" Version="12.1.1" />` to the App test project. Use `AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace()` for production and this factory for tests:

```csharp
[assembly: AvaloniaTestApplication(typeof(AppBuilderFactory))]

public static class AppBuilderFactory
{
    public static AppBuilder BuildAvaloniaApp() =>
        App.BuildAvaloniaApp().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
```

Instantiate `XdgPaths`, `AppLogger`, `SettingsService`, `SystemProcessRunner`, `LinuxTrashService`, `LinuxShellService`, `SystemFileAccess`, `DuplicateScanner`, and `DuplicateDeleteValidator` once in `App.OnFrameworkInitializationCompleted`, then construct the main view model and window.

- [ ] **Step 4: Define the original palette and control styles**

Define application resources with these base colours: background `#08090B`, surface `#111214`, panel `#1B1E22`, group `#1E2A3A`, border `#303944`, primary blue `#0797F2`, secondary blue `#55C7FF`, text `#F2F2F2`, muted text `#B8BEC7`, and destructive red `#FF3B30`. Add styles for top buttons, review buttons, compact row buttons, table headers, alternating rows, disabled controls, and the destructive action.

```xml
<Application.Resources>
  <SolidColorBrush x:Key="AppBackgroundBrush" Color="#08090B" />
  <SolidColorBrush x:Key="AppSurfaceBrush" Color="#111214" />
  <SolidColorBrush x:Key="PanelBackgroundBrush" Color="#1B1E22" />
  <SolidColorBrush x:Key="GroupHeaderBrush" Color="#1E2A3A" />
  <SolidColorBrush x:Key="AppBorderBrush" Color="#303944" />
  <SolidColorBrush x:Key="PrimaryBrush" Color="#0797F2" />
  <SolidColorBrush x:Key="SecondaryBrush" Color="#55C7FF" />
  <SolidColorBrush x:Key="TextBrush" Color="#F2F2F2" />
  <SolidColorBrush x:Key="MutedTextBrush" Color="#B8BEC7" />
  <SolidColorBrush x:Key="DangerBrush" Color="#FF3B30" />
</Application.Resources>
```

- [ ] **Step 5: Build the complete bound main-window hierarchy**

The top-level grid rows are banner, folder toolbar, settings expander, progress panel, result list, and bottom action bar. Use the same result columns as the reference image: checkbox, 52px icon, Actions, File Name, Size, Modified, and Path. Bind group headers to `HeaderText` and `ConfidenceText`; bind rows to selection, icon, keep/open actions, and formatted values. Use wrapping toolbars below 900px and a horizontal result scroller rather than clipping controls. Give icon-only/ambiguous controls explicit `AutomationProperties.Name` values, preserve tab order from top to bottom, and expose progress/status text through an accessibility live region.

```xml
<ItemsControl x:Name="DuplicateGroupList" Grid.Row="4" ItemsSource="{Binding DuplicateGroups}">
  <ItemsControl.ItemTemplate>
    <DataTemplate DataType="vm:DuplicateGroupViewModel">
      <Expander IsExpanded="True">
        <Expander.Header>
          <Grid Classes="group-header" ColumnDefinitions="92,154,*">
            <TextBlock Text="{Binding HeaderText}" />
            <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="6">
              <Button Content="Select" Click="SelectGroupButton_Click" />
              <Button Content="Deselect" Click="DeselectGroupButton_Click" />
            </StackPanel>
            <TextBlock Grid.Column="2" Text="{Binding ConfidenceText}" />
          </Grid>
        </Expander.Header>
        <ItemsControl ItemsSource="{Binding Files}" />
      </Expander>
    </DataTemplate>
  </ItemsControl.ItemTemplate>
</ItemsControl>
```

- [ ] **Step 6: Implement native folder/save pickers and dialogs in code-behind adapters**

Use Avalonia `StorageProvider.OpenFolderPickerAsync` for scan/preferred folders and `SaveFilePickerAsync` with CSV/JSON file type choices. Confirmation and message dialogs use styled modal Avalonia windows owned by `MainWindow`; they implement `IUserInteractionService` and return typed results to the view model.

```csharp
private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
{
    var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
    {
        Title = "Choose a folder to scan",
        AllowMultiple = false
    });
    if (result.Count == 1 && result[0].TryGetLocalPath() is { } path && DataContext is MainWindowViewModel vm)
        vm.FolderPath = path;
}

private async void ExportButton_Click(object? sender, RoutedEventArgs e)
{
    var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
    {
        Title = "Export duplicate report",
        SuggestedFileName = $"CopyFinder-report-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        FileTypeChoices =
        [
            new("CSV report") { Patterns = ["*.csv"] },
            new("JSON report") { Patterns = ["*.json"] }
        ]
    });
    if (file?.TryGetLocalPath() is { } path && DataContext is MainWindowViewModel vm)
        await vm.ExportAsync(path, CancellationToken.None);
}
```

- [ ] **Step 7: Run headless layout and binding tests**

Run: `dotnet test tests/CopyFinder.App.Tests/CopyFinder.App.Tests.csproj --filter MainWindowLayoutTests`

Expected: PASS, including named controls, DataContext bindings, default dark theme, and disabled Delete button with no selection.

- [ ] **Step 8: Launch the application against the current Mint desktop**

Run: `dotnet run --project src/CopyFinder.App/CopyFinder.App.csproj`

Expected: a centered CopyFinder window with the original banner, dark palette, complete toolbar/settings/results/action layout, and no startup exception.

- [ ] **Step 9: Commit the Avalonia interface**

```bash
git add src/CopyFinder.App CopyFinder.sln tests/CopyFinder.App.Tests Technification
git commit -m "feat: recreate CopyFinder interface with Avalonia"
```

### Task 7: Remove Windows Code Only After Linux Feature Parity

**Files:**
- Delete: `App.xaml`, `App.xaml.cs`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `MainWindow.cs`, `app.manifest`, `publish.ps1`, `CopyFinder.csproj`
- Delete: `Services/ControlledFolderAccessService.cs`, `Services/DeploymentCompatibilityChecker.cs`, `Services/DeploymentLogger.cs`, `Services/NtfsPermissionService.cs`, `Services/OneDriveFileHandler.cs`, `Services/SafeFile.cs`, `Services/SettingsService.cs`, `Services/ShellFileSavePicker.cs`, `Services/ShellFolderPicker.cs`, `Services/FilePathClassifier.cs`
- Delete: root `ViewModels/`
- Delete: root `Tests/`
- Create: `tests/CopyFinder.Core.Tests/RepositoryRoot.cs`
- Create: `tests/CopyFinder.Core.Tests/SourcePolicyTests.cs`
- Modify: `.gitignore`
- Modify: `CopyFinder.sln`

**Interfaces:**
- Consumes: completed Core, platform, workflow, and Avalonia replacements.
- Produces: a Linux-only repository with no compiled Windows APIs.

- [ ] **Step 1: Add a source-policy test before deleting old files**

```csharp
public static class RepositoryRoot
{
    public static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "CopyFinder.sln")))
                return directory.FullName;
        throw new DirectoryNotFoundException("Could not locate CopyFinder.sln.");
    }
}

[Fact]
public void MaintainedSource_DoesNotReferenceWindowsDesktopApis()
{
    var root = RepositoryRoot.Find();
    var source = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.*", SearchOption.AllDirectories)
        .Where(path => path.EndsWith(".cs") || path.EndsWith(".axaml"))
        .Select(File.ReadAllText);
    var combined = string.Join('\n', source);
    Assert.DoesNotContain("Microsoft.UI", combined);
    Assert.DoesNotContain("Windows.Storage", combined);
    Assert.DoesNotContain("explorer.exe", combined);
    Assert.DoesNotContain("powershell.exe", combined);
}
```

- [ ] **Step 2: Run the policy test against `src/`**

Run: `dotnet test tests/CopyFinder.Core.Tests/CopyFinder.Core.Tests.csproj --filter MaintainedSource_DoesNotReferenceWindowsDesktopApis`

Expected: PASS before deletion because maintained sources are already Linux-only.

- [ ] **Step 3: Delete superseded root sources and update ignore rules**

Remove only the listed files. Preserve `Technification`, documentation, licence, changelog, and Git history. Add `.idea/`, `.vscode/`, `artifacts/`, `publish/`, `TestResults/`, and all `bin/obj` directories to `.gitignore`.

- [ ] **Step 4: Verify the repository no longer compiles or documents Windows runtime assets**

Run: `rg -n "Microsoft\.UI|Windows\.Storage|explorer\.exe|powershell\.exe|win-x64|UseWinUI" src tests CopyFinder.sln`

Expected: no matches.

Run: `dotnet build CopyFinder.sln -warnaserror && dotnet test CopyFinder.sln --no-build`

Expected: PASS.

- [ ] **Step 5: Commit removal of superseded Windows sources**

```bash
git add -A App.xaml App.xaml.cs MainWindow.xaml MainWindow.xaml.cs MainWindow.cs app.manifest publish.ps1 CopyFinder.csproj Services ViewModels Tests .gitignore CopyFinder.sln
git commit -m "chore: remove superseded Windows implementation"
```

### Task 8: Add Linux Mint Publishing, Installation, CI, Documentation, and Credit

**Files:**
- Create: `publish.sh`
- Create: `packaging/install.sh`
- Create: `packaging/io.github.DJW1080.CopyFinder.desktop`
- Create: `.github/workflows/copyfinder-linux-desktop.yml`
- Delete: `.github/workflows/copyfinder-windows-desktop.yml`
- Create: `tests/CopyFinder.Core.Tests/ReleaseDocumentationTests.cs`
- Modify: `README.md`
- Modify: `INSTALL.md`
- Modify: `REPO_LAYOUT.md`
- Modify: `CHANGELOG.md`

**Interfaces:**
- Consumes: `src/CopyFinder.App/CopyFinder.App.csproj` and its assets.
- Produces: `publish/CopyFinder-v3.0.0-linux-x64.tar.gz`, checksum sidecar, user-local installer, and GitHub-ready documentation.

- [ ] **Step 1: Write a failing release-policy check**

Add a test that loads README, INSTALL, the desktop entry, and `publish.sh`, then asserts:

```csharp
Assert.Contains("Linux Mint 22.3", readme);
Assert.Contains("Human-AI collaboration", readme);
Assert.Contains("Dean John Weiniger", readme);
Assert.Contains("ChatGPT by OpenAI", readme);
Assert.DoesNotContain("Windows 11", readme);
Assert.Contains("Exec=", desktopEntry);
Assert.Contains("dotnet publish", publishScript);
Assert.Contains("linux-x64", publishScript);
Assert.Contains("sha256sum", publishScript);
```

- [ ] **Step 2: Run the release-policy test and verify it fails on Windows documentation**

Run: `dotnet test tests/CopyFinder.Core.Tests/CopyFinder.Core.Tests.csproj --filter ReleaseDocumentation`

Expected: FAIL because current docs and publishing scripts describe Windows.

- [ ] **Step 3: Implement deterministic self-contained publishing**

```bash
#!/usr/bin/env bash
set -euo pipefail

repo_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
publish_dir="$repo_dir/publish"
output_dir="$publish_dir/CopyFinder-linux-x64"
archive_name="CopyFinder-v3.0.0-linux-x64.tar.gz"

rm -rf -- "$output_dir"
mkdir -p -- "$output_dir"
dotnet publish "$repo_dir/src/CopyFinder.App/CopyFinder.App.csproj" \
  -c Release -r linux-x64 --self-contained true -o "$output_dir"
cp -- "$repo_dir/packaging/install.sh" "$output_dir/install.sh"
cp -- "$repo_dir/packaging/io.github.DJW1080.CopyFinder.desktop" "$output_dir/io.github.DJW1080.CopyFinder.desktop"
cp -- "$repo_dir/README.md" "$repo_dir/INSTALL.md" "$repo_dir/LICENSE" "$output_dir/"
chmod +x -- "$output_dir/CopyFinder" "$output_dir/install.sh"
tar -C "$output_dir" -czf "$publish_dir/$archive_name" .
(cd "$publish_dir" && sha256sum "$archive_name" > "$archive_name.sha256")
```

- [ ] **Step 4: Add the launcher and user-local installer**

```ini
[Desktop Entry]
Type=Application
Name=CopyFinder
Comment=Find and safely review duplicate files
Exec=REPLACE_INSTALL_DIR/CopyFinder
Icon=io.github.DJW1080.CopyFinder
Terminal=false
Categories=Utility;FileTools;
StartupNotify=true
```

`packaging/install.sh` installs beneath `${XDG_DATA_HOME:-$HOME/.local/share}/CopyFinder`, substitutes that absolute path into the desktop file, installs the icon under `~/.local/share/icons/hicolor/256x256/apps/`, marks the executable and launcher executable, and runs `update-desktop-database` only when available. It must never invoke `sudo`.

```bash
#!/usr/bin/env bash
set -euo pipefail

source_dir=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
data_home=${XDG_DATA_HOME:-"$HOME/.local/share"}
install_dir="$data_home/CopyFinder"
applications_dir="$data_home/applications"
icons_dir="$data_home/icons/hicolor/48x48/apps"

mkdir -p -- "$install_dir" "$applications_dir" "$icons_dir"
cp -a -- "$source_dir/." "$install_dir/"
chmod +x -- "$install_dir/CopyFinder"
sed "s|REPLACE_INSTALL_DIR|$install_dir|g" \
  "$source_dir/io.github.DJW1080.CopyFinder.desktop" \
  > "$applications_dir/io.github.DJW1080.CopyFinder.desktop"
cp -- "$source_dir/Assets/CopyFinder-icon.png" \
  "$icons_dir/io.github.DJW1080.CopyFinder.png"
if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "$applications_dir"
fi
```

- [ ] **Step 5: Rewrite repository documentation for Linux Mint and add approved credit verbatim**

Use this exact visible README/release wording:

> **Human-AI collaboration**
>
> CopyFinder was conceived and directed by Dean John Weiniger, who designed and refined the interface and product experience. The application code was developed with assistance from ChatGPT by OpenAI. The project is presented as a demonstration of practical human-AI collaboration in desktop application development.

Document .NET 10 development requirements, self-contained end-user installation, checksum verification with `sha256sum -c`, XDG data paths, GIO Trash guarantees, and uninstall steps.

- [ ] **Step 6: Replace Windows CI with Ubuntu 24.04 CI**

```yaml
name: CopyFinder Linux Desktop
on:
  push:
  pull_request:
jobs:
  build-test-publish:
    runs-on: ubuntu-24.04
    steps:
      - uses: actions/checkout@v5
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x
      - run: dotnet restore CopyFinder.sln
      - run: dotnet build CopyFinder.sln -c Release --no-restore -warnaserror
      - run: dotnet test CopyFinder.sln -c Release --no-build
      - run: bash -n publish.sh packaging/install.sh
      - run: ./publish.sh
      - run: cd publish && sha256sum -c CopyFinder-v3.0.0-linux-x64.tar.gz.sha256
      - uses: actions/upload-artifact@v4
        with:
          name: CopyFinder-v3.0.0-linux-x64
          path: |
            publish/CopyFinder-v3.0.0-linux-x64.tar.gz
            publish/CopyFinder-v3.0.0-linux-x64.tar.gz.sha256
```

- [ ] **Step 7: Run release policy and shell validation**

Run: `dotnet test tests/CopyFinder.Core.Tests/CopyFinder.Core.Tests.csproj --filter ReleaseDocumentation`

Expected: PASS.

Run: `bash -n publish.sh packaging/install.sh`

Expected: exit 0 with no output.

Run: `chmod +x publish.sh packaging/install.sh`

Expected: Git records both scripts as executable.

- [ ] **Step 8: Commit release and credit updates**

```bash
git add publish.sh packaging .github README.md INSTALL.md REPO_LAYOUT.md CHANGELOG.md tests/CopyFinder.Core.Tests
git commit -m "docs: publish and credit Linux Mint edition"
```

### Task 9: Complete End-to-End Verification and Prepare the GitHub Update

**Files:**
- Create: `scripts/create-smoke-fixture.sh`
- Create: `docs/verification/linux-mint-22.3.md`
- Modify: `CHANGELOG.md`

**Interfaces:**
- Consumes: the finished application, tests, publish script, and installer.
- Produces: reproducible verification evidence and a GitHub-ready commit series.

- [ ] **Step 1: Create a bounded manual fixture script**

```bash
#!/usr/bin/env bash
set -euo pipefail

fixture_dir=$(mktemp -d -t copyfinder-smoke-XXXXXX)
mkdir -p -- "$fixture_dir/first" "$fixture_dir/second"
printf '%s' 'alpha duplicate' > "$fixture_dir/first/alpha.txt"
cp -- "$fixture_dir/first/alpha.txt" "$fixture_dir/second/alpha-copy.txt"
printf '%s' 'beta duplicate' > "$fixture_dir/first/beta.txt"
cp -- "$fixture_dir/first/beta.txt" "$fixture_dir/second/beta-copy.txt"
printf '%s' 'unique' > "$fixture_dir/unique.txt"
printf '%s' 'excluded duplicate' > "$fixture_dir/first/ignored.tmp"
cp -- "$fixture_dir/first/ignored.tmp" "$fixture_dir/second/ignored.tmp"
cp -- "$fixture_dir/first/alpha.txt" "$fixture_dir/.hidden-alpha.txt"
printf 'Fixture: %s\nExpected with hidden files and tmp excluded: 2 groups, 2 duplicate files\n' "$fixture_dir"
```

Run: `chmod +x scripts/create-smoke-fixture.sh`

Expected: the fixture script is directly executable.

- [ ] **Step 2: Run clean restore, build, and all tests**

Run: `dotnet nuget locals all --clear`

Run: `dotnet restore CopyFinder.sln --force --no-cache`

Run: `dotnet build CopyFinder.sln -c Release --no-restore -warnaserror`

Run: `dotnet test CopyFinder.sln -c Release --no-build --logger "console;verbosity=normal"`

Expected: all commands exit 0; record exact test totals in `docs/verification/linux-mint-22.3.md`.

- [ ] **Step 3: Publish and verify the release archive**

Run: `./publish.sh`

Run: `cd publish && sha256sum -c CopyFinder-v3.0.0-linux-x64.tar.gz.sha256`

Expected: `CopyFinder-v3.0.0-linux-x64.tar.gz: OK`.

- [ ] **Step 4: Exercise the complete UI workflow on Mint 22.3**

Launch the published `CopyFinder` binary, scan the bounded fixture, verify the original visual hierarchy, cancel and restart a scan, change the kept file, export CSV and JSON, open a file location, and send one validated duplicate to Trash. Confirm the kept file remains, the trashed file is recoverable in Nemo's Trash view, and no permanent-delete path was offered.

- [ ] **Step 5: Record verification evidence and final release notes**

Record OS (`Linux Mint 22.3`), architecture (`x86_64`), .NET SDK version, test counts, published archive name, checksum, UI workflow outcomes, and any skipped checks. Add a 3.0.0 Linux entry to CHANGELOG with the same facts.

- [ ] **Step 6: Run final repository checks**

Run: `git diff --check`

Run: `git status --short`

Run: `rg -n "Windows 11|win-x64|Recycle Bin|Controlled Folder Access|NTFS" README.md INSTALL.md REPO_LAYOUT.md src tests packaging publish.sh`

Expected: `git diff --check` exits 0; status contains only intentional changes; the terminology scan has no user-facing Windows deployment instructions.

- [ ] **Step 7: Commit verification evidence**

```bash
git add scripts/create-smoke-fixture.sh docs/verification/linux-mint-22.3.md CHANGELOG.md
git commit -m "test: verify CopyFinder on Linux Mint 22.3"
```

- [ ] **Step 8: Review and push the GitHub update**

Run: `git log --oneline --decorate -12`

Run: `git diff origin/main...HEAD --stat`

After confirming the commit list, credit wording, and release artifacts, push `main` to the configured `origin`. Verify that the GitHub README displays the human-AI collaboration credit and that the Linux workflow begins successfully.
