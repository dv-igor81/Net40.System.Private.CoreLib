namespace System.Net.Net40;

using AddressFamily = System.Net.Sockets.Net40.AddressFamily;

internal static class SocketAddressPal
{
	public static AddressFamily GetAddressFamily(byte[] buffer)
	{
			return (AddressFamily)BitConverter.ToInt16(buffer, 0);
		}

	public static void SetAddressFamily(byte[] buffer, AddressFamily family)
	{
			if (family > (AddressFamily)65535)
			{
				throw new PlatformNotSupportedException();
			}
			buffer[0] = (byte)family;
			buffer[1] = (byte)((int)family >> 8);
		}

	public static ushort GetPort(byte[] buffer)
	{
			return buffer.NetworkBytesToHostUInt16(2);
		}

	public static void SetPort(byte[] buffer, ushort port)
	{
			port.HostToNetworkBytes(buffer, 2);
		}

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

	public static void SetIPv4Address(byte[] buffer, uint address)
	{
			buffer[4] = (byte)address;
			buffer[5] = (byte)(address >> 8);
			buffer[6] = (byte)(address >> 16);
			buffer[7] = (byte)(address >> 24);
		}

	public static void SetIPv6Address(byte[] buffer, Span<byte> address, uint scope)
	{
			buffer[4] = 0;
			buffer[5] = 0;
			buffer[6] = 0;
			buffer[7] = 0;
			buffer[24] = (byte)scope;
			buffer[25] = (byte)(scope >> 8);
			buffer[26] = (byte)(scope >> 16);
			buffer[27] = (byte)(scope >> 24);
			for (int i = 0; i < address.Length; i++)
			{
				buffer[8 + i] = address[i];
			}
		}
}