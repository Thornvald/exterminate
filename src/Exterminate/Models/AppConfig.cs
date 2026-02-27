namespace Exterminate.Models;

internal sealed class AppConfig
{
    public int Retries { get; init; } = 6;
    public int RetryDelayMs { get; init; } = 350;
    public bool AutoElevate { get; init; } = true;
    public bool SelfInstallToUserPath { get; init; } = true;
    public bool ForceTakeOwnership { get; init; } = true;
    public bool GrantAdministratorsFullControl { get; init; } = true;
    public bool GrantCurrentUserFullControl { get; init; } = true;
    public bool UseRobocopyMirrorFallback { get; init; } = true;
    public bool UseWslFallbackIfAvailable { get; init; } = true;
    public string InstallDirectory { get; init; } = "%LOCALAPPDATA%\\Exterminate";
    public bool CopyDefaultConfigOnInstall { get; init; } = true;
}
