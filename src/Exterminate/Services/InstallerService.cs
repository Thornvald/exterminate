using Exterminate.Models;

namespace Exterminate.Services;

internal static class InstallerService
{
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

        var destinationExecutable = Path.Combine(installDirectory, "exterminate.exe");
        if (!PathEquals(sourceExecutable, destinationExecutable))
        {
            File.Copy(sourceExecutable, destinationExecutable, overwrite: true);
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
        Console.WriteLine($"Installed: {destinationExecutable}");
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

        var currentExecutable = Environment.ProcessPath ?? string.Empty;
        if (currentExecutable.StartsWith(installDirectory, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("PATH entry removed.");
            Console.WriteLine($"Delete this folder after closing terminal: {installDirectory}");
            return 0;
        }

        if (!Directory.Exists(installDirectory))
        {
            Console.WriteLine("Already uninstalled.");
            return 0;
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
}
