# CopyFinder installation — Linux Mint

Version 3.0.0 targets Linux Mint 22.3 on x86-64. The release contains the runtime it needs and installs entirely within your user account.

## Download and verify

Download both release files:

```text
CopyFinder-v3.0.0-linux-x64.tar.gz
CopyFinder-v3.0.0-linux-x64.tar.gz.sha256
```

From the download folder, verify the archive before running it:

```bash
sha256sum -c CopyFinder-v3.0.0-linux-x64.tar.gz.sha256
```

The result must say `CopyFinder-v3.0.0-linux-x64.tar.gz: OK`.

## Install

```bash
mkdir -p CopyFinder-v3.0.0
tar -xzf CopyFinder-v3.0.0-linux-x64.tar.gz -C CopyFinder-v3.0.0
cd CopyFinder-v3.0.0
./install.sh
```

No administrator password or `sudo` is required. The installer places the app at `${XDG_DATA_HOME:-$HOME/.local/share}/CopyFinder`, adds a desktop entry, and installs the CopyFinder icon. Open the Mint menu and search for **CopyFinder**.

You may also run the extracted `CopyFinder` executable directly without installing it.

## Updates

Close CopyFinder, verify and extract the new release, then run its `install.sh`. It replaces the application files without deleting your saved settings.

## Data locations

CopyFinder follows the XDG Base Directory convention:

- Settings: `${XDG_CONFIG_HOME:-$HOME/.config}/CopyFinder/settings.json`
- Logs: `${XDG_STATE_HOME:-$HOME/.local/state}/CopyFinder/logs/copyfinder.log`
- Cache: `${XDG_CACHE_HOME:-$HOME/.cache}/CopyFinder`

Removing a selected duplicate uses `/usr/bin/gio trash -- <path>`. CopyFinder validates the selected and kept files first and never offers an automatic permanent-delete fallback.

## Uninstall

Close CopyFinder, then remove the user-local application and launcher:

```bash
data_home=${XDG_DATA_HOME:-"$HOME/.local/share"}
rm -rf -- "$data_home/CopyFinder"
rm -f -- "$data_home/applications/io.github.DJW1080.CopyFinder.desktop"
rm -f -- "$data_home/icons/hicolor/48x48/apps/io.github.DJW1080.CopyFinder.png"
```

Optionally remove settings, logs, and cache:

```bash
rm -rf -- "${XDG_CONFIG_HOME:-$HOME/.config}/CopyFinder"
rm -rf -- "${XDG_STATE_HOME:-$HOME/.local/state}/CopyFinder"
rm -rf -- "${XDG_CACHE_HOME:-$HOME/.cache}/CopyFinder"
```

These commands affect only CopyFinder's user-local files.

## Build from source

Install the .NET 10 SDK, then run:

```bash
dotnet restore CopyFinder.sln
dotnet build CopyFinder.sln -c Release --no-restore -warnaserror
dotnet test CopyFinder.sln -c Release --no-build
dotnet run --project src/CopyFinder.App/CopyFinder.App.csproj
```
