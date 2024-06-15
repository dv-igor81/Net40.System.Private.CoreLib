using System.Runtime.InteropServices;

namespace System.Net.Sockets;

internal static class CompletionPortHelper
{
    internal static readonly bool PlatformHasUdpIssue = CheckIfPlatformHasUdpIssue();

    internal static bool SkipCompletionPortOnSuccess(SafeHandle handle)
    {
        // return Interop.Kernel32.SetFileCompletionNotificationModes(handle,
        //     Interop.Kernel32.FileCompletionNotificationModes.SkipCompletionPortOnSuccess |
        //     Interop.Kernel32.FileCompletionNotificationModes.SkipSetEventOnHandle);
        return false; // DIA-Замена
    }

    private static bool CheckIfPlatformHasUdpIssue()
    {
        Version version = Environment.OSVersion.Version;
        if (version.Major >= 6)
        {
            if (version.Major == 6)
            {
                return version.Minor <= 1;
            }

            return false;
        }

        return true;
    }
}