# exterminate

`exterminate` is a Windows CLI tool for force-deleting files and folders.

Warning: Exterminate permanently deletes targets and does not send them to Recycle Bin.

Every delete action asks for confirmation first.

## Quick install

From repo root:

```powershell
.\install.cmd
```

This builds the C++ binary with CMake, installs to `%LOCALAPPDATA%\Exterminate`, and adds that install directory to your user PATH.

## Commands

```powershell
exterminate "C:\path\to\target"
exterminate --install
exterminate -install
exterminate --uninstall
exterminate -uninstall
exterminate --config "C:\path\to\config.json" "C:\path\to\target"
```

Terminal delete prompt requires confirmation (`YES`, `yes`, or `Y`).

Uninstall confirms simply as:

```text
Uninstalled exterminate.
```

## `--config`

Use a custom config file for one run instead of default installed config.

Example:

```powershell
exterminate --config "C:\path\to\config.json" "C:\path\to\target"
```

## Context menu (.reg)

Install right-click entries:

```powershell
reg import .\registry\context-menu-install.reg
```

Remove right-click entries:

```powershell
reg import .\registry\context-menu-uninstall.reg
```

Context menu uses hidden `wscript.exe` launcher (`%LOCALAPPDATA%\Exterminate\exterminate-context.vbs`) to avoid cmd window popups.
Context menu also asks for confirmation before delete.

## Build manually

```powershell
cmake -S . -B build -DCMAKE_BUILD_TYPE=Release
cmake --build build --config Release
cmake --install build --config Release --prefix .\dist\win-x64
```

## Config keys

Default file: `config/exterminate.config.json`

- `retries`
- `retryDelayMs`
- `autoElevate`
- `selfInstallToUserPath`
- `installDirectory`
- `copyDefaultConfigOnInstall`
- `forceTakeOwnership`
- `grantAdministratorsFullControl`
- `grantCurrentUserFullControl`
- `useRobocopyMirrorFallback`
- `useWslFallbackIfAvailable`
