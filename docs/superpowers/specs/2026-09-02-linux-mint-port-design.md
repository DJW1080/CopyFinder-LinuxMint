# CopyFinder Linux Mint Port Design

Date: 2026-09-02

## Purpose

Port CopyFinder from its Windows-only WinUI implementation to a Linux Mint 22.3 x86-64 desktop application while preserving the original product identity, visual design, and complete duplicate-file review workflow.

The Linux edition will be a dedicated Linux Mint application. It does not need to retain a Windows build or Windows-specific services.

## Goals

- Preserve the original dark CopyFinder interface, banner, blue accents, grouped duplicate cards, file icons, controls, and overall interaction flow.
- Preserve scanning, cancellation, scan settings, keep rules, manual keep selection, duplicate selection, CSV/JSON export, opening file locations, and deletion safety validation.
- Target .NET 10 and use Avalonia for the desktop interface.
- Replace Windows-only filesystem, permissions, shell, settings, logging, and Recycle Bin behavior with Linux Mint equivalents.
- Publish a self-contained `linux-x64` release that can be installed and launched from the Mint application menu.
- Credit the project accurately as a human-AI collaboration.

## Non-goals

- Retaining WinUI, Windows builds, Controlled Folder Access checks, NTFS ownership repair, Windows Defender guidance, or OneDrive placeholder handling.
- Adding cloud storage, background scheduling, or automatic duplicate deletion.
- Permanently deleting a file when the desktop Trash operation is unavailable.
- Redesigning the interface beyond changes required for Linux controls, accessibility, and layout fidelity.

## Technology Choice

Use C# on .NET 10 with Avalonia.

This keeps the existing scanner, models, reporting logic, and pre-delete hash validation while replacing only the Windows-bound application shell and services. Avalonia supports a close recreation of the current WinUI layout and provides Linux folder and save pickers without requiring a full engine rewrite.

The incomplete GTK 3 draft in the working tree is reference material only. Correct cross-platform scanner changes may be retained, but the reduced scan-only interface will be replaced.

## Architecture

The solution will contain three clear areas:

1. **Core**: models, duplicate scanning, keep-rule selection, report formatting, delete validation, and filesystem-safe operations that do not depend on a UI toolkit.
2. **Linux platform services**: XDG paths, Trash integration, folder opening, permissions diagnostics, logging, and process execution.
3. **Avalonia application**: views, UI-facing view models, commands, dialogs, progress reporting, and application lifecycle.

UI code will depend on Core through explicit services. Core will not reference Avalonia.

## User Interface

The main window will reproduce the existing hierarchy:

- CopyFinder/Technification banner and versioned title.
- Folder path field with Browse, Scan, and Cancel actions.
- Collapsible scan settings containing keep rule, preferred folder, worker count, result limit, minimum size, hidden-file handling, and excluded extensions.
- Progress and summary panel.
- Scrollable grouped duplicate results with Select, Deselect, Keep, Open, size, modified date, and path information.
- Bottom action bar with Export report, Select duplicates, Deselect all, and Delete Duplicates.

The existing dark palette and bright blue/red action colours will be recreated as Avalonia resources. Controls will remain keyboard accessible, status updates will be readable by assistive technology, and the layout will adapt when the window is narrower than the reference screenshot.

## Scan and Review Flow

1. The user chooses or types a directory.
2. CopyFinder validates settings and starts a cancellable recursive scan on background workers.
3. Files are grouped by size before SHA-256 hashing to avoid unnecessary I/O.
4. Duplicate groups are created and a kept file is chosen using the selected rule.
5. The user reviews groups, changes kept files, selects duplicates, opens their locations, or exports the report.
6. Before trashing a selected duplicate, CopyFinder verifies that both the kept file and duplicate still exist, have the expected size, and hash to the expected value.
7. Only validated duplicates are sent to Trash. Results and failures are reported per file.

## Linux Filesystem Behavior

- Path identity is case-sensitive on Linux. File paths that differ only by case remain distinct.
- Hidden files are detected by a leading `.` in the filename; Windows-only hidden/system attribute assumptions are removed.
- Symbolic-link directories are not recursively followed, preventing loops and accidental scans outside the chosen tree.
- Permission errors are logged and reported without attempting ownership changes or elevation.
- Settings are stored beneath `${XDG_CONFIG_HOME:-~/.config}/CopyFinder`.
- Logs are stored beneath `${XDG_STATE_HOME:-~/.local/state}/CopyFinder`.
- Temporary working data is stored beneath `${XDG_CACHE_HOME:-~/.cache}/CopyFinder`.
- File locations are opened through `xdg-open`.
- Deletion uses GIO's Trash behavior through a safely argument-escaped process invocation. If Trash is unsupported for a mount, the file is left untouched and the UI reports the failure. There is no automatic permanent-delete fallback.

## Error Handling

- Invalid folders or settings are rejected before a scan starts.
- Inaccessible folders and files are skipped with bounded diagnostics while the rest of the scan continues.
- Cancellation returns the UI to an idle state without showing a generic failure.
- Export, open-location, and Trash failures identify the affected path and preserve application state.
- A failed delete validation always leaves the file untouched.
- Unexpected errors are logged to the XDG state directory and summarized in a user-facing dialog.

## Packaging and Installation

- `publish.sh` produces a Release, self-contained `linux-x64` application directory and compressed archive.
- The release includes the banner, icons, documentation, a `.desktop` launcher, and an install script.
- The installer places application files under the current user's local application directory, the launcher under `~/.local/share/applications`, and icons under the matching XDG icon directory. It does not require root access.
- A SHA-256 checksum is generated for the release archive.
- README, INSTALL, repository layout, changelog, and GitHub Actions documentation are updated for Linux Mint and Bash commands.

## Testing and Verification

Automated tests will cover:

- Size-first grouping and SHA-256 duplicate detection.
- Keep rules and manual keep changes.
- Duplicate limits, cancellation, exclusions, and minimum size.
- Linux case-sensitive paths and dotfile handling.
- Symbolic-link loop avoidance.
- Report formatting and settings persistence.
- Pre-delete size/hash validation.
- Trash service success, unsupported-mount, cancellation, and failure behavior through a fake process runner.
- View-model selection and command state.

Verification will include a clean .NET 10 restore/build/test, a self-contained publish, archive checksum verification, and an application launch smoke test on Linux Mint 22.3. The main workflow will also be exercised manually against temporary fixtures before release artifacts are reported complete.

## GitHub Credit

README and release-facing documentation will include a visible section with this wording:

> **Human-AI collaboration**
>
> CopyFinder was conceived and directed by Dean John Weiniger, who designed and refined the interface and product experience. The application code was developed with assistance from ChatGPT by OpenAI. The project is presented as a demonstration of practical human-AI collaboration in desktop application development.

This wording attributes the human product and interface work, accurately describes ChatGPT as development assistance, and does not imply endorsement or sponsorship by OpenAI.

## Migration

The Windows-only WinUI files, manifest, shell COM pickers, PowerShell publisher, and Windows deployment services will be removed after their required behavior has been represented in the Avalonia application or Linux services. Existing user work will not be reset wholesale; reusable changes will be incorporated deliberately. Repository history remains the record of the previous Windows edition.
