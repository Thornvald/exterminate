using System.Diagnostics;

namespace Exterminate.Services;

internal static class ProcessRunner
{
    public static bool TryRun(string fileName, params string[] arguments)
    {
        try
        {
            _ = Run(fileName, arguments);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static int Run(string fileName, params string[] arguments)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        process.WaitForExit();
        return process.ExitCode;
    }

    public static bool IsAvailable(string commandName)
    {
        if (Path.IsPathRooted(commandName))
        {
            return File.Exists(commandName);
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            return false;
        }

        var pathExtValue = Environment.GetEnvironmentVariable("PATHEXT");
        var extensions = string.IsNullOrWhiteSpace(pathExtValue)
            ? new[] { string.Empty, ".exe", ".cmd", ".bat" }
            : pathExtValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var rawDirectory in pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(rawDirectory, commandName.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? commandName : commandName + extension);
                if (File.Exists(candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
