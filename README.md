# exterminate

Terminal tool to force-delete files and folders on Windows.

## Usage

```powershell
exterminate "C:\path\to\target"
```

If the command is not yet in PATH, run once:

```powershell
.\install.ps1
```

## Config

Edit `config/exterminate.config.json` to tune retries, elevation behavior, and fallback methods.

## Notes

- Uses ownership/ACL reset, PowerShell deletion, cmd deletion, .NET deletion, robocopy mirror fallback, and optional WSL fallback.
- Optional WSL fallback only runs when `wsl.exe` exists on the machine.
