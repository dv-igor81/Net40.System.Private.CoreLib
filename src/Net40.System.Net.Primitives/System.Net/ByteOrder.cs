namespace System.Net;

internal static class ByteOrder
{
	public static void HostToNetworkBytes(this ushort host, byte[] bytes, int index)
	{
			bytes[index] = (byte)(host >> 8);
			bytes[index + 1] = (byte)host;
		}

	public static ushort NetworkBytesToHostUInt16(this byte[] bytes, int index)
	{
			return (ushort)((bytes[index] << 8) | bytes[index + 1]);
		}
}