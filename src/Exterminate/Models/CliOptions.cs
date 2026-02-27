namespace Exterminate.Models;

internal sealed class CliOptions
{
    public string? TargetPath { get; init; }
    public string? ConfigPath { get; init; }
    public bool Install { get; init; }
    public bool Uninstall { get; init; }
    public bool Help { get; init; }
    public bool ElevatedRun { get; init; }
    public bool Headless { get; init; }

    public static bool TryParse(string[] args, out CliOptions options, out string? error)
    {
        options = new CliOptions();
        error = null;

        string? targetPath = null;
        string? configPath = null;
        var install = false;
        var uninstall = false;
        var help = false;
        var elevatedRun = false;
        var headless = false;

        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            var key = argument.Trim();
            var normalized = key.ToLowerInvariant();

            switch (normalized)
            {
                case "--install":
                case "-install":
                case "/install":
                    install = true;
                    continue;
                case "--uninstall":
                case "-uninstall":
                case "/uninstall":
                    uninstall = true;
                    continue;
                case "--elevated-run":
                    elevatedRun = true;
                    continue;
                case "--headless":
                    headless = true;
                    continue;
                case "--help":
                case "-h":
                case "/?":
                    help = true;
                    continue;
                case "--config":
                case "-config":
                case "/config":
                    if (!TryReadValue(args, ref index, out configPath))
                    {
                        error = "Missing value for --config.";
                        return false;
                    }

                    continue;
                case "--target-path":
                case "-targetpath":
                case "/targetpath":
                    if (!TryReadValue(args, ref index, out targetPath))
                    {
                        error = "Missing value for --target-path.";
                        return false;
                    }

                    continue;
                default:
                    if ((key.StartsWith("-", StringComparison.Ordinal) && key.Length > 1)
                        || key.StartsWith("/", StringComparison.Ordinal))
                    {
                        error = $"Unknown option: {argument}";
                        return false;
                    }

                    if (targetPath is null)
                    {
                        targetPath = argument;
                    }
                    else
                    {
                        error = "Only one target path is allowed.";
                        return false;
                    }

                    continue;
            }
        }

        if (install && uninstall)
        {
            error = "Use either --install or --uninstall, not both.";
            return false;
        }

        if ((install || uninstall) && !string.IsNullOrWhiteSpace(targetPath))
        {
            error = "Install and uninstall modes do not accept a target path.";
            return false;
        }

        options = new CliOptions
        {
            TargetPath = targetPath,
            ConfigPath = configPath,
            Install = install,
            Uninstall = uninstall,
            Help = help,
            ElevatedRun = elevatedRun,
            Headless = headless
        };

        return true;
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        value = null;
        var nextIndex = index + 1;
        if (nextIndex >= args.Length)
        {
            return false;
        }

        value = args[nextIndex];
        index = nextIndex;
        return true;
    }
}
