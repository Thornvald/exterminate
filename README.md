# exterminate

Windows terminal tool to aggressively delete files or folders.

## Quick Install

From this repository root:

```powershell
.\install.cmd
```

That command builds a trimmed single-file `exterminate.exe` (~11 MB), installs it to `%LOCALAPPDATA%\Exterminate`, and adds that folder to your user PATH.
`install.cmd` now shows install status and keeps the window open when double-clicked.

If you run from an existing terminal and do not want pause behavior:

```powershell
.\install.cmd --no-pause
```

Open a new terminal, then use:

```powershell
exterminate "C:\path\to\target"
ex "C:\path\to\target"
```

## Commands

```powershell
exterminate "C:\path\to\target"
ex "C:\path\to\target"
exterminate --install
exterminate --uninstall
```

## Right-Click Context Menu (.reg)

After `exterminate` is installed, you can add Explorer right-click entries:

```powershell
reg import .\registry\context-menu-install.reg
```

This adds **Exterminate** to:

- files
- folders
- folder background (current folder)
- drives

The registry commands call `wscript.exe` with `%LOCALAPPDATA%\Exterminate\exterminate-context.vbs` (hidden wrapper, no cmd popup).
For heavily protected files, run `exterminate` from an elevated terminal.

To remove those entries:

```powershell
reg import .\registry\context-menu-uninstall.reg
```

## Build Publish Manually

```powershell
dotnet publish .\src\Exterminate\Exterminate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial -p:InvariantGlobalization=true -p:EnableCompressionInSingleFile=true -o .\dist\win-x64
```

## Config

Default config file: `config/exterminate.config.json`.

- retries and delay
- auto-elevation
- ACL ownership takeover
- robocopy fallback
- optional WSL fallback if `wsl.exe` exists
- install directory and PATH behavior
