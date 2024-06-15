using System.Runtime.CompilerServices;

namespace System.Net;

public static class IPAddressExtension
{
    public static bool TryWriteBytes(this IPAddress address, Span<byte> destination, out int bytesWritten)
    {
        if (address.IsIPv6())
        {
            if (destination.Length < 16)
            {
                bytesWritten = 0;
                return false;
            }
            address.WriteIPv6Bytes(destination);
            bytesWritten = 16;
        }
        else
        {
            if (destination.Length < 4)
            {
                bytesWritten = 0;
                return false;
            }
            address.WriteIPv4Bytes(destination);
            bytesWritten = 4;
        }
        return true;
    }
    
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static void WriteIPv6Bytes(this IPAddress address, Span<byte> destination)
    {
        int num = 0;
        for (int i = 0; i < 16; i++)
        {
            byte[] numbers = address.GetAddressBytes();
            destination[i] = numbers[i];
        }
    }
    
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static void WriteIPv4Bytes(this IPAddress address, Span<byte> destination)
    {
        byte[] privateAddress =  address.GetAddressBytes();
        destination[0] = privateAddress[0];
        destination[1] = privateAddress[1];
        destination[2] = privateAddress[2];
        destination[3] = privateAddress[3];
    }

    private static bool IsIPv6(this IPAddress address)
    {
        byte[] numbers = address.GetAddressBytes();
        return numbers.Length == 16;
    }
}