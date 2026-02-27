using System.Diagnostics;
using System.Security.Cryptography;
using Exterminate.Models;

namespace Exterminate.Services;

internal static class InstallerService
{
    private const string UninstallMarkerFileName = ".uninstalling";

    private enum InstallState
    {
        Installed,
        Updated,
        AlreadyInstalled
    }

    public static int Install(AppConfig config, string baseDirectory, string? configSourcePath = null)
    {
        var sourceExecutable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(sourceExecutable) || !File.Exists(sourceExecutable))
        {
            Console.Error.WriteLine("Unable to resolve current executable path for install.");
            return 1;
        }

        var installDirectory = ResolveInstallDirectory(config);
        Directory.CreateDirectory(installDirectory);
        TryDeleteUninstallMarker(installDirectory);

        var destinationExecutable = Path.Combine(installDirectory, "exterminate.exe");
        var installState = InstallState.Installed;
        if (!PathEquals(sourceExecutable, destinationExecutable))
        {
            if (File.Exists(destinationExecutable))
            {
                if (FilesAreIdentical(sourceExecutable, destinationExecutable))
                {
                    installState = InstallState.AlreadyInstalled;
                }
                else
                {
                    File.Copy(sourceExecutable, destinationExecutable, overwrite: true);
                    installState = InstallState.Updated;
                }
            }
            else
            {
                File.Copy(sourceExecutable, destinationExecutable, overwrite: true);
                installState = InstallState.Installed;
            }
        }
        else
        {
            installState = InstallState.AlreadyInstalled;
        }

        WriteAliasCommand(installDirectory);
        WriteContextScript(installDirectory);
        RemoveLegacyContextExecutable(installDirectory);

        if (config.CopyDefaultConfigOnInstall)
        {
            CopyBundledConfig(baseDirectory, installDirectory, configSourcePath);
        }

        if (config.SelfInstallToUserPath)
        {
            EnsurePathEntry(installDirectory);
        }
        Console.WriteLine($"Installed path: {destinationExecutable}");
        Console.WriteLine($"Status: {GetInstallStateText(installState)}");
        Console.WriteLine($"Alias available: {Path.Combine(installDirectory, "ex.cmd")}");
        Console.WriteLine($"Context script: {Path.Combine(installDirectory, "exterminate-context.vbs")}");
        Console.WriteLine("Open a new terminal and run: exterminate \"C:\\path\\to\\target\"");
        Console.WriteLine("Short alias also works: ex \"C:\\path\\to\\target\"");
        return 0;
    }

    public static int Uninstall(AppConfig config)
    {
        var installDirectory = ResolveInstallDirectory(config);
        RemovePathEntry(installDirectory);

        if (IsUninstallPending(installDirectory))
        {
            Console.WriteLine("Uninstall already in progress.");
            return 0;
        }

        if (!Directory.Exists(installDirectory))
        {
            Console.WriteLine("Already uninstalled.");
            return 0;
        }

        TryCreateUninstallMarker(installDirectory);

        var currentExecutable = Environment.ProcessPath ?? string.Empty;
        if (currentExecutable.StartsWith(installDirectory, StringComparison.OrdinalIgnoreCase))
        {
            if (TryScheduleSelfRemoval(installDirectory))
            {
                Console.WriteLine("Uninstall started.");
                Console.WriteLine("Status: uninstall in progress.");
                return 0;
            }

            Console.Error.WriteLine("Failed to schedule self-removal.");
            Console.WriteLine($"You can remove it manually: {installDirectory}");
            return 1;
        }

        try
        {
            Directory.Delete(installDirectory, recursive: true);
            Console.WriteLine("Uninstalled exterminate.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to remove install directory: {exception.Message}");
            Console.WriteLine($"You can remove it manually: {installDirectory}");
            return 1;
        }
    }

    public static bool IsUninstallPending(string baseDirectory)
    {
        return File.Exists(Path.Combine(baseDirectory, UninstallMarkerFileName));
    }

    public static void EnsurePathEntry(string directoryPath)
    {
        var normalizedEntry = NormalizePathEntry(directoryPath);
        if (string.IsNullOrWhiteSpace(normalizedEntry))
        {
            return;
        }

        var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;
        if (ContainsPathEntry(userPath, normalizedEntry))
        {
            EnsureCurrentProcessPath(normalizedEntry);
            return;
        }

        var newUserPath = string.IsNullOrWhiteSpace(userPath)
            ? normalizedEntry
            : $"{userPath};{normalizedEntry}";

        Environment.SetEnvironmentVariable("Path", newUserPath, EnvironmentVariableTarget.User);
        EnsureCurrentProcessPath(normalizedEntry);
        Console.WriteLine($"Added to PATH: {normalizedEntry}");
    }

    private static void RemovePathEntry(string directoryPath)
    {
        var normalizedEntry = NormalizePathEntry(directoryPath);
        if (string.IsNullOrWhiteSpace(normalizedEntry))
        {
            return;
        }

        var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;
        var currentEntries = SplitPathEntries(userPath).ToList();
        var filteredEntries = currentEntries
            .Where(item => !string.Equals(NormalizePathEntry(item), normalizedEntry, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var updatedUserPath = string.Join(';', filteredEntries);
        Environment.SetEnvironmentVariable("Path", updatedUserPath, EnvironmentVariableTarget.User);

        var processPath = Environment.GetEnvironmentVariable("Path") ?? string.Empty;
        var processEntries = SplitPathEntries(processPath)
            .Where(item => !string.Equals(NormalizePathEntry(item), normalizedEntry, StringComparison.OrdinalIgnoreCase));
        Environment.SetEnvironmentVariable("Path", string.Join(';', processEntries));
    }

    private static void EnsureCurrentProcessPath(string normalizedEntry)
    {
        var processPath = Environment.GetEnvironmentVariable("Path") ?? string.Empty;
        if (ContainsPathEntry(processPath, normalizedEntry))
        {
            return;
        }

        var updated = string.IsNullOrWhiteSpace(processPath)
            ? normalizedEntry
            : $"{processPath};{normalizedEntry}";
        Environment.SetEnvironmentVariable("Path", updated);
    }

    private static bool ContainsPathEntry(string pathValue, string targetEntry)
    {
        return SplitPathEntries(pathValue)
            .Select(NormalizePathEntry)
            .Any(item => string.Equals(item, targetEntry, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> SplitPathEntries(string pathValue)
    {
        return pathValue.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static string NormalizePathEntry(string pathEntry)
    {
        return pathEntry.Trim().TrimEnd('\\');
    }

    private static string ResolveInstallDirectory(AppConfig config)
    {
        var configured = string.IsNullOrWhiteSpace(config.InstallDirectory)
            ? "%LOCALAPPDATA%\\Exterminate"
            : config.InstallDirectory;

        var expanded = Environment.ExpandEnvironmentVariables(configured);
        return Path.GetFullPath(expanded);
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left).TrimEnd('\\'),
            Path.GetFullPath(right).TrimEnd('\\'),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyBundledConfig(string baseDirectory, string installDirectory, string? configSourcePath)
    {
        var sourceConfigPath = string.IsNullOrWhiteSpace(configSourcePath)
            ? ConfigService.GetBundledConfigPath(baseDirectory)
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(configSourcePath));

        if (!File.Exists(sourceConfigPath))
        {
            sourceConfigPath = ConfigService.GetBundledConfigPath(baseDirectory);
        }

        if (!File.Exists(sourceConfigPath))
        {
            return;
        }

        var targetConfigDirectory = Path.Combine(installDirectory, "config");
        Directory.CreateDirectory(targetConfigDirectory);
        var targetConfigPath = Path.Combine(targetConfigDirectory, "exterminate.config.json");
        File.Copy(sourceConfigPath, targetConfigPath, overwrite: true);
    }

    private static void WriteAliasCommand(string installDirectory)
    {
        var aliasPath = Path.Combine(installDirectory, "ex.cmd");
        const string aliasScript = "@echo off\r\nsetlocal\r\n\"%~dp0exterminate.exe\" %*\r\nexit /b %errorlevel%\r\n";
        File.WriteAllText(aliasPath, aliasScript);
    }

    private static void WriteContextScript(string installDirectory)
    {
        var scriptPath = Path.Combine(installDirectory, "exterminate-context.vbs");
        const string script = "Set shell = CreateObject(\"WScript.Shell\")\r\n"
            + "Set fso = CreateObject(\"Scripting.FileSystemObject\")\r\n"
            + "basePath = fso.GetParentFolderName(WScript.ScriptFullName)\r\n"
            + "exePath = fso.BuildPath(basePath, \"exterminate.exe\")\r\n"
            + "If WScript.Arguments.Count = 0 Then WScript.Quit 1\r\n"
            + "targetPath = WScript.Arguments.Item(0)\r\n"
            + "command = Chr(34) & exePath & Chr(34) & \" --headless --elevated-run \" & Chr(34) & targetPath & Chr(34)\r\n"
            + "exitCode = shell.Run(command, 0, True)\r\n"
            + "WScript.Quit exitCode\r\n";
        File.WriteAllText(scriptPath, script);
    }

    private static void RemoveLegacyContextExecutable(string installDirectory)
    {
        var legacyPath = Path.Combine(installDirectory, "exterminate-context.exe");
        if (!File.Exists(legacyPath))
        {
            return;
        }

        try
        {
            File.Delete(legacyPath);
        }
        catch
        {
        }
    }

    private static string GetInstallStateText(InstallState installState)
    {
        return installState switch
        {
            InstallState.Installed => "installed",
            InstallState.Updated => "updated",
            InstallState.AlreadyInstalled => "already installed",
            _ => "installed"
        };
    }

    private static bool FilesAreIdentical(string leftPath, string rightPath)
    {
        try
        {
            var leftInfo = new FileInfo(leftPath);
            var rightInfo = new FileInfo(rightPath);
            if (!leftInfo.Exists || !rightInfo.Exists)
            {
                return false;
            }

            if (leftInfo.Length != rightInfo.Length)
            {
                return false;
            }

            using var hash = SHA256.Create();
            using var leftStream = File.OpenRead(leftPath);
            using var rightStream = File.OpenRead(rightPath);
            var leftBytes = hash.ComputeHash(leftStream);
            var rightBytes = hash.ComputeHash(rightStream);
            return leftBytes.SequenceEqual(rightBytes);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryScheduleSelfRemoval(string installDirectory)
    {
        try
        {
            var tempScriptPath = Path.Combine(Path.GetTempPath(), "exterminate-uninstall-" + Guid.NewGuid().ToString("N") + ".ps1");
            var scriptLines = new[]
            {
                "param([string]$Target)",
                "$ErrorActionPreference = 'SilentlyContinue'",
                "for ($attempt = 0; $attempt -lt 120; $attempt++) {",
                "    Remove-Item -LiteralPath $Target -Recurse -Force -ErrorAction SilentlyContinue",
                "    if (-not (Test-Path -LiteralPath $Target)) { break }",
                "    Start-Sleep -Milliseconds 100",
                "}"
            };
            File.WriteAllLines(tempScriptPath, scriptLines);

            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\" -Target \"{installDirectory}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            _ = Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryCreateUninstallMarker(string installDirectory)
    {
        try
        {
            Directory.CreateDirectory(installDirectory);
            File.WriteAllText(Path.Combine(installDirectory, UninstallMarkerFileName), DateTime.UtcNow.ToString("O"));
        }
        catch
        {
        }
    }

    private static void TryDeleteUninstallMarker(string installDirectory)
    {
        try
        {
            var markerPath = Path.Combine(installDirectory, UninstallMarkerFileName);
            if (File.Exists(markerPath))
            {
                File.Delete(markerPath);
            }
        }
        catch
        {
        }
    }
}
