using System.Text.Json;
using Exterminate.Models;

namespace Exterminate.Services;

internal static class ConfigService
{
    public static AppConfig Load(string? explicitConfigPath, string baseDirectory)
    {
        foreach (var candidatePath in GetCandidatePaths(explicitConfigPath, baseDirectory))
        {
            if (!File.Exists(candidatePath))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(candidatePath);
                var config = JsonSerializer.Deserialize(json, ExterminateJsonContext.Default.AppConfig);
                if (config is not null)
                {
                    return config;
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Failed to read config '{candidatePath}': {exception.Message}");
            }
        }

        return new AppConfig();
    }

    public static string GetBundledConfigPath(string baseDirectory)
    {
        return Path.Combine(baseDirectory, "config", "exterminate.config.json");
    }

    private static IEnumerable<string> GetCandidatePaths(string? explicitConfigPath, string baseDirectory)
    {
        if (!string.IsNullOrWhiteSpace(explicitConfigPath))
        {
            yield return Path.GetFullPath(Environment.ExpandEnvironmentVariables(explicitConfigPath.Trim()));
        }

        yield return Path.Combine(baseDirectory, "config", "exterminate.config.json");
        yield return Path.Combine(baseDirectory, "exterminate.config.json");
    }
}
