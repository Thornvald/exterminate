using System.Diagnostics;

if (!OperatingSystem.IsWindows())
{
    return;
}

if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
{
    return;
}

var exterminatePath = Path.Combine(AppContext.BaseDirectory, "exterminate.exe");
if (!File.Exists(exterminatePath))
{
    return;
}

var processStartInfo = new ProcessStartInfo
{
    FileName = exterminatePath,
    UseShellExecute = false,
    CreateNoWindow = true,
    WindowStyle = ProcessWindowStyle.Hidden,
    WorkingDirectory = Environment.CurrentDirectory
};

processStartInfo.ArgumentList.Add(args[0]);

using var process = Process.Start(processStartInfo);
if (process is null)
{
    return;
}

process.WaitForExit();
Environment.ExitCode = process.ExitCode;
