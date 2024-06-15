namespace System.Net.Sockets;

using IPAddress = Net.Net40.IPAddress;

internal static class IPAddressExtensions
{
    public static IPAddress Snapshot(this IPAddress original)
    {
        switch (original.AddressFamily)
        {
            case Net40.AddressFamily.InterNetwork:
                return new IPAddress(original.Address);
            case Net40.AddressFamily.InterNetworkV6:
            {
                Span<byte> span = stackalloc byte[16];
                original.TryWriteBytes(span, out var _);
                return new IPAddress(span, (uint)original.ScopeId);
            }
            default:
                throw new InternalException(original.AddressFamily);
        }
    }
}