namespace System.Net.Net40;

internal static class SocketAddressPal
{
	public static uint GetIPv4Address(ReadOnlySpan<byte> buffer)
	{
		return (buffer[4] & 0xFFu) | ((uint)(buffer[5] << 8) & 0xFF00u) | ((uint)(buffer[6] << 16) & 0xFF0000u) | (uint)(buffer[7] << 24);
	}

	public static void GetIPv6Address(ReadOnlySpan<byte> buffer, Span<byte> address, out uint scope)
	{
		for (int i = 0; i < address.Length; i++)
		{
			address[i] = buffer[8 + i];
		}
		scope = (uint)((buffer[27] << 24) + (buffer[26] << 16) + (buffer[25] << 8) + buffer[24]);
	}
}
