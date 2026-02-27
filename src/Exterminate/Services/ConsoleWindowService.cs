using System.Runtime.InteropServices;

namespace Exterminate.Services;

internal static class ConsoleWindowService
{
    private const int SwHide = 0;

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr handle, int command);

    public static bool HasHeadlessFlag(string[] args)
    {
        return args.Any(static argument => string.Equals(argument, "--headless", StringComparison.OrdinalIgnoreCase));
    }

    public static void HideCurrentConsoleWindow()
    {
        var handle = GetConsoleWindow();
        if (handle != IntPtr.Zero)
        {
            _ = ShowWindow(handle, SwHide);
        }
    }
}
