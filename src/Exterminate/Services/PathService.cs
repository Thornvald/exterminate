using System.Runtime.InteropServices;

namespace Exterminate.Services;

internal static class PathService
{
    private const uint InvalidFileAttributes = 0xFFFFFFFF;
    private const FileAttributes DirectoryAttribute = (FileAttributes)0x10;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFileAttributesW(string lpFileName);

    public static string NormalizeTargetPath(string inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("Target path cannot be empty.", nameof(inputPath));
        }

        var trimmed = inputPath.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            trimmed = trimmed[1..^1];
        }

        var expanded = Environment.ExpandEnvironmentVariables(trimmed);
        if (!Path.IsPathRooted(expanded))
        {
            expanded = Path.Combine(Environment.CurrentDirectory, expanded);
        }

        return Path.GetFullPath(expanded);
    }

    public static string ToVerbatimPath(string path)
    {
        if (path.StartsWith("\\\\?\\", StringComparison.Ordinal))
        {
            return path;
        }

        if (path.StartsWith("\\\\", StringComparison.Ordinal))
        {
            return "\\\\?\\UNC\\" + path.TrimStart('\\');
        }

        return "\\\\?\\" + path;
    }

    public static bool TryConvertToWslPath(string windowsPath, out string? wslPath)
    {
        wslPath = null;
        if (windowsPath.Length < 3)
        {
            return false;
        }

        if (!char.IsLetter(windowsPath[0]) || windowsPath[1] != ':' || (windowsPath[2] != '\\' && windowsPath[2] != '/'))
        {
            return false;
        }

        var drive = char.ToLowerInvariant(windowsPath[0]);
        var remainder = windowsPath.Length == 3 ? string.Empty : windowsPath[3..].Replace('\\', '/');
        wslPath = string.IsNullOrEmpty(remainder) ? $"/mnt/{drive}/" : $"/mnt/{drive}/{remainder}";
        return true;
    }

    public static bool TargetExists(string path)
    {
        return TryGetAttributes(path, out _);
    }

    public static bool IsDirectory(string path)
    {
        return TryGetAttributes(path, out var attributes) && attributes.HasFlag(DirectoryAttribute);
    }

    private static bool TryGetAttributes(string path, out FileAttributes attributes)
    {
        var queryPath = ToVerbatimPath(path);
        var result = GetFileAttributesW(queryPath);
        if (result == InvalidFileAttributes)
        {
            attributes = default;
            return false;
        }

        attributes = (FileAttributes)result;
        return true;
    }
}
