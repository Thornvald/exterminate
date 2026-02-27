using Exterminate.Models;
using Exterminate.Services;

var isHeadless = OperatingSystem.IsWindows() && ConsoleWindowService.HasHeadlessFlag(args);
if (isHeadless)
{
    ConsoleWindowService.HideCurrentConsoleWindow();
}

var isStandaloneLaunch = OperatingSystem.IsWindows() && !isHeadless && ConsoleWindowService.IsStandaloneConsoleSession();

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("exterminate currently supports Windows only.");
    if (isStandaloneLaunch)
    {
        ConsoleWindowService.WaitForUserToClose();
    }

    return 1;
}

if (args.Length == 0 && isStandaloneLaunch)
{
    var autoConfig = ConfigService.Load(null, AppContext.BaseDirectory);
    var autoInstallExitCode = InstallerService.Install(autoConfig, AppContext.BaseDirectory);
    ConsoleWindowService.WaitForUserToClose();
    return autoInstallExitCode;
}

if (!CliOptions.TryParse(args, out var options, out var parseError))
{
    Console.Error.WriteLine(parseError);
    PrintUsage();

    if (isStandaloneLaunch)
    {
        ConsoleWindowService.WaitForUserToClose();
    }

    return 1;
}

if (options.Help)
{
    PrintUsage();
    return 0;
}

var config = ConfigService.Load(options.ConfigPath, AppContext.BaseDirectory);

if (InstallerService.IsUninstallPending(AppContext.BaseDirectory) && !options.Install && !options.Uninstall)
{
    Console.Error.WriteLine("Uninstall in progress. Please wait and run install again if needed.");

    if (isStandaloneLaunch)
    {
        ConsoleWindowService.WaitForUserToClose();
    }

    return 1;
}

if (options.Install)
{
    return InstallerService.Install(config, AppContext.BaseDirectory, options.ConfigPath);
}

if (options.Uninstall)
{
    return InstallerService.Uninstall(config);
}

if (string.IsNullOrWhiteSpace(options.TargetPath))
{
    PrintUsage();

    if (isStandaloneLaunch)
    {
        ConsoleWindowService.WaitForUserToClose();
    }

    return 1;
}

string normalizedTargetPath;
try
{
    normalizedTargetPath = PathService.NormalizeTargetPath(options.TargetPath);
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Invalid target path: {exception.Message}");

    if (isStandaloneLaunch)
    {
        ConsoleWindowService.WaitForUserToClose();
    }

    return 1;
}

if (config.AutoElevate && !options.ElevatedRun && !PrivilegeService.IsAdministrator())
{
    return PrivilegeService.RelaunchAsAdministrator(args);
}

var result = DeleteEngine.Delete(normalizedTargetPath, config);
if (result.Success)
{
    Console.WriteLine(result.Message);

    if (isStandaloneLaunch)
    {
        ConsoleWindowService.WaitForUserToClose();
    }

    return 0;
}

Console.Error.WriteLine(result.Message);

if (isStandaloneLaunch)
{
    ConsoleWindowService.WaitForUserToClose();
}

return 1;

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  exterminate \"C:\\path\\to\\target\"");
    Console.WriteLine("  ex \"C:\\path\\to\\target\"");
    Console.WriteLine("  exterminate --install");
    Console.WriteLine("  exterminate -install");
    Console.WriteLine("  exterminate --uninstall");
    Console.WriteLine("  exterminate -uninstall");
    Console.WriteLine("  exterminate --config \"C:\\path\\to\\config.json\" \"C:\\path\\to\\target\"");
}
