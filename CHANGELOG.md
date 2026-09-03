# CopyFinder Changelog

## V3.0.0 - 2026-09-03

### Linux Mint edition

- Rebuilt CopyFinder exclusively for Linux Mint 22.3 x86-64 with .NET 10 and Avalonia 12.1.1.
- Preserved the original Technification banner, dark visual hierarchy, blue result groups, file-type artwork, and review workflow.
- Added Linux case-sensitive paths, dotfile filtering, and symbolic-link directory protection.
- Added XDG settings/log paths, safe argument-list process launching, Nemo-compatible folder opening, and GIO Trash.
- Revalidates every kept and selected file immediately before moving a duplicate to Trash; no permanent-delete fallback exists.
- Added CSV/JSON exports, self-contained publishing, a user-local installer, SHA-256 release checksums, and Ubuntu CI.
- Added real Core, workflow, platform safety, and headless UI regression tests.
- Verified a clean Linux Mint 22.3 Release build with 0 warnings and 29 passing tests; the self-contained archive and temporary user-local install rehearsal both passed.

**Human-AI collaboration**

CopyFinder was conceived and directed by Dean John Weiniger, who designed and refined the interface and product experience. The application code was developed with assistance from ChatGPT by OpenAI. The project is presented as a demonstration of practical human-AI collaboration in desktop application development.

The original interface styling concept was developed with assistance from GitHub Copilot.

## V2.1.8 - 2026-06-30

### Layout Refinement

- Changed the explicit startup window size from `830 x 680` to `830 x 800`.
- Updated project, assembly, manifest, install, and README version markers to `2.1.8`.

## V2.1.5 - 2026-06-25

### Layout Refinement

- Increased Scan Settings expanded content padding to `6,4,6,4`.
- Set Scan Settings row spacing to `4` for a less cramped expanded layout.
- Kept the banner, path bar, scan settings, and table on a consistent 6-pixel vertical rhythm.
- Updated project, assembly, manifest, install, and README version markers to `2.1.5`.

## V2.1.4 - 2026-06-25

### Layout Refinement

- Changed the explicit startup window size from `830 x 680` to `830 x 600`.
- Updated project, assembly, manifest, install, and README version markers to `2.1.4`.

## V2.1.3 - 2026-06-25

### Layout Refinement

- Removed the remaining explicit padding and row spacing from the Scan Settings expanded content.
- Updated project, assembly, manifest, install, and README version markers to `2.1.3`.

## V2.1.2 - 2026-06-25

### Layout Refinement

- Removed page and header padding around the banner so it sits flush with the window edge.
- Kept the banner image centered when the window is widened.
- Updated project, assembly, manifest, install, and README version markers to `2.1.2`.

## V2.1.1 - 2026-06-25

### Window And Branding Update

- Updated the in-app banner asset to `CopyFinder-Banner-07.png`.
- Added the current app version to the native window title bar.
- Updated project, assembly, manifest, install, and README version markers to `2.1.1`.

## V2.1.0 - 2026-06-20

### Manual Repo Update

- Normalized project, assembly, file, manifest, and visible UI version markers to `2.1.0`.
- Updated install instructions to use the `CopyFinder-v2.1.0-win-x64-Standalone.zip` release asset name.
- Added this changelog and linked it from the README and repository layout.
- Included `CHANGELOG.md` in the standalone publish output.
- Kept application behavior unchanged for this manual update.

### Current Validation Notes

- The GitHub Actions workflow creates a bounded scan fixture before running the test project.
- The current test project is a smoke-start harness. It does not yet assert duplicate-scan results from that fixture.
