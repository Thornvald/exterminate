using Exterminate.Models;
using Exterminate.Services;

if (OperatingSystem.IsWindows() && ConsoleWindowService.HasHeadlessFlag(args))
{
    ConsoleWindowService.HideCurrentConsoleWindow();
}

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("exterminate currently supports Windows only.");
    return 1;
}

if (!CliOptions.TryParse(args, out var options, out var parseError))
{
    Console.Error.WriteLine(parseError);
    PrintUsage();
    return 1;
}

if (options.Help)
{
    PrintUsage();
    return 0;
}

var config = ConfigService.Load(options.ConfigPath, AppContext.BaseDirectory);

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
    return 0;
}

Console.Error.WriteLine(result.Message);
return 1;

static void PrintUsage()
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  exterminate \"C:\\path\\to\\target\"");
    Console.WriteLine("  ex \"C:\\path\\to\\target\"");
    Console.WriteLine("  exterminate --install   (or -install)");
    Console.WriteLine("  exterminate --uninstall (or -uninstall)");
    Console.WriteLine("  exterminate --config \"C:\\path\\to\\config.json\" \"C:\\path\\to\\target\"");
}
