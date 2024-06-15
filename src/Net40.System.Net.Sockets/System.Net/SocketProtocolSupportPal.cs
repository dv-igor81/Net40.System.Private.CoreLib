using System.Runtime.InteropServices;
using System.Threading;

namespace System.Net.Net40;

using AddressFamily = System.Net.Sockets.Net40.AddressFamily;
using SocketError = System.Net.Sockets.Net40.SocketError;
using SocketType =  System.Net.Sockets.Net40.SocketType;

internal class SocketProtocolSupportPal
{
    private static bool s_ipv4 = true;

    private static bool s_ipv6 = true;

    private static bool s_initialized;

    private static readonly object s_initializedLock = new object();

    public static bool OSSupportsIPv6
    {
        get
        {
            EnsureInitialized();
            return s_ipv6;
        }
    }

    public static bool OSSupportsIPv4
    {
        get
        {
            EnsureInitialized();
            return s_ipv4;
        }
    }

    private static void EnsureInitialized()
    {
        if (Volatile.Read(ref s_initialized))
        {
            return;
        }

        lock (s_initializedLock)
        {
            if (!s_initialized)
            {
                s_ipv4 = IsProtocolSupported(AddressFamily.InterNetwork);
                s_ipv6 = IsProtocolSupported(AddressFamily.InterNetworkV6);
                Volatile.Write(ref s_initialized, value: true);
            }
        }
    }

    private static bool IsProtocolSupported(AddressFamily af)
    {
        IntPtr intPtr = IntPtr.Zero;
        bool result = true;
        try
        {
            intPtr = /*global::*/Interop.Winsock.WSASocketW(af, SocketType.Dgram, 0, IntPtr.Zero, 0, 128);
            if (intPtr == IntPtr.Zero)
            {
                SocketError lastWin32Error = (SocketError)Marshal.GetLastWin32Error();
                if (lastWin32Error == SocketError.AddressFamilyNotSupported)
                {
                    result = false;
                }
            }
        }
        finally
        {
            if (intPtr != IntPtr.Zero)
            {
                SocketError socketError = Interop.Winsock.closesocket(intPtr);
            }
        }

        return result;
    }
}