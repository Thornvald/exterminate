# exterminate

Windows terminal tool to aggressively delete files or folders.

## Quick Install

From this repository root:

```powershell
.\install.cmd
```

That command builds a single-file `exterminate.exe`, installs it to `%LOCALAPPDATA%\Exterminate`, and adds that folder to your user PATH.

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

## Build Publish Manually

```powershell
dotnet publish .\src\Exterminate\Exterminate.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\dist\win-x64
```

## Config

Default config file: `config/exterminate.config.json`.

- retries and delay
- auto-elevation
- ACL ownership takeover
- robocopy fallback
- optional WSL fallback if `wsl.exe` exists
- install directory and PATH behavior
