# CopyFinder repository layout

CopyFinder is a Linux Mint 22.3 desktop application targeting .NET 10 and Avalonia 12.1.1. The repository contains no maintained non-Linux application target.

```text
CopyFinder/
├── .github/workflows/copyfinder-linux-desktop.yml
├── Directory.Build.props
├── CopyFinder.sln
├── src/
│   ├── CopyFinder.Core/
│   │   ├── Models/
│   │   └── Services/
│   └── CopyFinder.App/
│       ├── Assets/
│       ├── Services/
│       ├── ViewModels/
│       └── Views/
├── tests/
│   ├── CopyFinder.Core.Tests/
│   └── CopyFinder.App.Tests/
├── packaging/
│   ├── install.sh
│   └── io.github.DJW1080.CopyFinder.desktop
├── docs/
├── Technification/
├── publish.sh
├── README.md
├── INSTALL.md
└── CHANGELOG.md
```

## Maintained projects

- `CopyFinder.Core` contains filesystem scanning, SHA-256 grouping, keep rules, report formatting, and pre-Trash validation. It has no UI or process-launching dependency.
- `CopyFinder.App` contains the Avalonia interface, review state, XDG settings/logging, safe process execution, GIO Trash, and file-manager integration.
- `CopyFinder.Core.Tests` covers scanning, Linux path semantics, deletion validation, and repository/release policies.
- `CopyFinder.App.Tests` covers platform adapters, review workflow, and headless Avalonia layout.

## Generated files

`bin/`, `obj/`, `TestResults/`, `artifacts/`, and `publish/` are generated and ignored. `publish.sh` creates:

```text
publish/CopyFinder-v3.0.0-linux-x64.tar.gz
publish/CopyFinder-v3.0.0-linux-x64.tar.gz.sha256
```

## Common commands

```bash
dotnet restore CopyFinder.sln
dotnet build CopyFinder.sln -warnaserror
dotnet test CopyFinder.sln
dotnet run --project src/CopyFinder.App/CopyFinder.App.csproj
./publish.sh
```
