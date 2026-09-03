# CopyFinder 3.0.0 verification — Linux Mint 22.3

Verification date: 2026-09-03
Branch: `codex/linux-mint-port`

## Environment

- Operating system: Linux Mint 22.3
- Architecture: x86_64
- .NET SDK: 10.0.400
- Application runtime: self-contained `linux-x64`
- Desktop integration: Avalonia 12.1.1, XDG user directories, `/usr/bin/xdg-open`, and `/usr/bin/gio`

## Clean build and automated tests

The task-local NuGet caches were cleared before the final restore.

| Check | Result |
| --- | --- |
| `dotnet restore CopyFinder.sln --force --no-cache` | Passed |
| Release build with `-warnaserror` | Passed — 0 warnings, 0 errors |
| Core tests | Passed — 9/9 |
| App/platform/headless UI tests | Passed — 20/20 |
| Total | Passed — 29/29 |
| Shell syntax for publish/install/fixture scripts | Passed |
| Linux-only maintained-source policy | Passed |
| Release documentation and collaboration-credit policy | Passed |

The regression suite covers SHA-256 duplicate grouping, cancellation, minimum size, extension exclusions, Linux case-sensitive paths, dotfile filtering, symlink-directory avoidance, mutation-before-delete rejection, XDG fallbacks, safe process argument handling, failed GIO behavior, review selection/keep invariants, CSV/JSON workflow behavior, and the primary Avalonia control hierarchy.

## Published artifact

- Archive: `CopyFinder-v3.0.0-linux-x64.tar.gz`
- Size: 44,957,054 bytes
- SHA-256: `7cc6c98a5744bb92245217dc5c0f725aaf0d4752d8df38ae12d569cd0bd3f217`
- `sha256sum -c`: Passed
- Temporary user-local installation rehearsal: Passed
- Installed executable, desktop entry, and 48px menu icon: Present
- Unsubstituted launcher placeholder: Absent

## Desktop verification

Both the development build and the published self-contained executable launched successfully on Linux Mint. The published executable remained alive during the bounded startup check and wrote its XDG startup log without an exception.

Visual inspection confirmed the original hierarchy and character: Technification banner, near-black application surface, blue scan and table accents, settings expander, results area, status/action bar, and clearly separated destructive Trash action. A headless test independently confirms the folder, Browse, Scan, Cancel, Settings, duplicate group list, Export, and Trash controls are present.

Use `scripts/create-smoke-fixture.sh` for a bounded acceptance scan. With hidden files skipped and `tmp` excluded, the expected result is two duplicate groups and two duplicate files.

An actual GIO move was intentionally left to the human acceptance test so automated verification would not add anything to the user's real Trash. The code path has unit and orchestration coverage proving that validation happens first, arguments are never shell-parsed, failures retain the file/row, and no permanent-delete fallback exists.
