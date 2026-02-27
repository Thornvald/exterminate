using Exterminate.Models;

namespace Exterminate.Services;

internal static class DeleteEngine
{
    public static DeleteResult Delete(string targetPath, AppConfig config)
    {
        if (!PathService.TargetExists(targetPath))
        {
            return new DeleteResult(Success: true, AlreadyGone: true, Message: $"Already gone: {targetPath}");
        }

        var retries = Math.Max(0, config.Retries);
        var retryDelayMs = Math.Max(0, config.RetryDelayMs);

        for (var attempt = 0; attempt <= retries; attempt++)
        {
            if (!PathService.TargetExists(targetPath))
            {
                break;
            }

            TryDeleteOnce(targetPath, config);

            if (attempt < retries && PathService.TargetExists(targetPath))
            {
                Thread.Sleep(retryDelayMs);
            }
        }

        if (PathService.TargetExists(targetPath))
        {
            return new DeleteResult(Success: false, AlreadyGone: false, Message: $"Failed to delete: {targetPath}");
        }

        return new DeleteResult(Success: true, AlreadyGone: false, Message: $"Deleted: {targetPath}");
    }

    private static void TryDeleteOnce(string targetPath, AppConfig config)
    {
        var isDirectory = PathService.IsDirectory(targetPath);

        ClearAttributes(targetPath, isDirectory);

        if (config.ForceTakeOwnership)
        {
            TakeOwnership(targetPath, isDirectory);
        }

        if (config.GrantAdministratorsFullControl)
        {
            GrantAdministrators(targetPath, isDirectory);
        }

        if (config.GrantCurrentUserFullControl)
        {
            GrantCurrentUser(targetPath, isDirectory);
        }

        TryDeleteWithDotNet(targetPath, isDirectory);
        TryDeleteWithCmd(targetPath, isDirectory);
        TryDeleteWithDotNetVerbatim(targetPath, isDirectory);

        if (config.UseRobocopyMirrorFallback && isDirectory && PathService.TargetExists(targetPath))
        {
            TryDeleteWithRobocopyMirror(targetPath);
        }

        if (config.UseWslFallbackIfAvailable && PathService.TargetExists(targetPath))
        {
            TryDeleteWithWsl(targetPath);
        }
    }

    private static void ClearAttributes(string targetPath, bool isDirectory)
    {
        if (isDirectory)
        {
            ProcessRunner.TryRun("attrib.exe", "-R", "-S", "-H", targetPath, "/S", "/D");
        }
        else
        {
            ProcessRunner.TryRun("attrib.exe", "-R", "-S", "-H", targetPath);
        }
    }

    private static void TakeOwnership(string targetPath, bool isDirectory)
    {
        if (isDirectory)
        {
            ProcessRunner.TryRun("takeown.exe", "/F", targetPath, "/A", "/R", "/D", "Y");
        }
        else
        {
            ProcessRunner.TryRun("takeown.exe", "/F", targetPath, "/A", "/D", "Y");
        }
    }

    private static void GrantAdministrators(string targetPath, bool isDirectory)
    {
        if (isDirectory)
        {
            ProcessRunner.TryRun("icacls.exe", targetPath, "/grant", "*S-1-5-32-544:(OI)(CI)F", "/T", "/C");
        }
        else
        {
            ProcessRunner.TryRun("icacls.exe", targetPath, "/grant", "*S-1-5-32-544:F", "/C");
        }
    }

    private static void GrantCurrentUser(string targetPath, bool isDirectory)
    {
        var userName = $"{Environment.UserDomainName}\\{Environment.UserName}";
        if (isDirectory)
        {
            ProcessRunner.TryRun("icacls.exe", targetPath, "/grant", $"{userName}:(OI)(CI)F", "/T", "/C");
        }
        else
        {
            ProcessRunner.TryRun("icacls.exe", targetPath, "/grant", $"{userName}:F", "/C");
        }
    }

    private static void TryDeleteWithDotNet(string targetPath, bool isDirectory)
    {
        try
        {
            if (isDirectory)
            {
                Directory.Delete(targetPath, recursive: true);
            }
            else
            {
                File.Delete(targetPath);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteWithCmd(string targetPath, bool isDirectory)
    {
        var verbatimPath = PathService.ToVerbatimPath(targetPath);
        if (isDirectory)
        {
            ProcessRunner.TryRun("cmd.exe", "/d", "/c", $"rd /s /q \"{verbatimPath}\"");
        }
        else
        {
            ProcessRunner.TryRun("cmd.exe", "/d", "/c", $"del /f /q \"{verbatimPath}\"");
        }
    }

    private static void TryDeleteWithDotNetVerbatim(string targetPath, bool isDirectory)
    {
        var verbatimPath = PathService.ToVerbatimPath(targetPath);
        try
        {
            if (isDirectory)
            {
                Directory.Delete(verbatimPath, recursive: true);
            }
            else
            {
                File.Delete(verbatimPath);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteWithRobocopyMirror(string targetPath)
    {
        var emptyDirectory = Path.Combine(Path.GetTempPath(), "exterminate-empty-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(emptyDirectory);
            ProcessRunner.TryRun("robocopy.exe", emptyDirectory, targetPath, "/MIR", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/R:0", "/W:0");
            TryDeleteWithCmd(targetPath, isDirectory: true);
            TryDeleteWithDotNet(targetPath, isDirectory: true);
        }
        finally
        {
            try
            {
                Directory.Delete(emptyDirectory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void TryDeleteWithWsl(string targetPath)
    {
        if (!ProcessRunner.IsAvailable("wsl.exe"))
        {
            return;
        }

        if (!PathService.TryConvertToWslPath(targetPath, out var wslPath) || string.IsNullOrWhiteSpace(wslPath))
        {
            return;
        }

        ProcessRunner.TryRun("wsl.exe", "--exec", "rm", "-rf", "--", wslPath);
    }
}
