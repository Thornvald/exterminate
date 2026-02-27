using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;

namespace Exterminate.Services;

internal static class PrivilegeService
{
    [SupportedOSPlatform("windows")]
    public static bool IsAdministrator()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static int RelaunchAsAdministrator(string[] originalArgs)
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Console.Error.WriteLine("Unable to relaunch with elevation.");
            return 1;
        }

        var arguments = new List<string>(originalArgs);
        if (!arguments.Any(static item => string.Equals(item, "--elevated-run", StringComparison.OrdinalIgnoreCase)))
        {
            arguments.Add("--elevated-run");
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = string.Join(' ', arguments.Select(QuoteArgument)),
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.CurrentDirectory
        };

        try
        {
            using var process = Process.Start(processStartInfo);
            if (process is null)
            {
                Console.Error.WriteLine("Failed to start elevated process.");
                return 1;
            }

            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            Console.Error.WriteLine("Elevation canceled.");
            return 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Failed to relaunch as administrator: {exception.Message}");
            return 1;
        }
    }

    private static string QuoteArgument(string value)
    {
        if (value.Length == 0)
        {
            return "\"\"";
        }

        var needsQuotes = value.Any(static character => char.IsWhiteSpace(character) || character == '"');
        if (!needsQuotes)
        {
            return value;
        }

        var builder = new StringBuilder();
        builder.Append('"');
        var backslashCount = 0;

        foreach (var character in value)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                builder.Append('\\', backslashCount * 2 + 1);
                builder.Append(character);
                backslashCount = 0;
                continue;
            }

            builder.Append('\\', backslashCount);
            builder.Append(character);
            backslashCount = 0;
        }

        builder.Append('\\', backslashCount * 2);
        builder.Append('"');
        return builder.ToString();
    }
}
