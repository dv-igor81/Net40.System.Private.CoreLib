using System.Net.Net40;
using System.Net.Sockets.Net40;

namespace System.Net.Internals;

using IPAddress = System.Net.Net40.IPAddress; 
using IPEndPoint = System.Net.Net40.IPEndPoint;

internal class SocketAddress
{
	internal static readonly int IPv6AddressSize = 28;

	internal static readonly int IPv4AddressSize = 16;

	internal int InternalSize;

	internal byte[] Buffer;

	private bool _changed = true;

	private int _hash;

	public AddressFamily Family => SocketAddressPal.GetAddressFamily(Buffer);

	public int Size => InternalSize;

	public byte this[int offset]
	{
		get
		{
			if (offset < 0 || offset >= Size)
			{
				throw new IndexOutOfRangeException();
			}
			return Buffer[offset];
		}
		set
		{
			if (offset < 0 || offset >= Size)
			{
				throw new IndexOutOfRangeException();
			}
			if (Buffer[offset] != value)
			{
				_changed = true;
			}
			Buffer[offset] = value;
		}
	}

	public SocketAddress(AddressFamily family, int size)
	{
		if (size < 2)
		{
			throw new ArgumentOutOfRangeException("size");
		}
		InternalSize = size;
		Buffer = new byte[(size / IntPtr.Size + 2) * IntPtr.Size];
		SocketAddressPal.SetAddressFamily(Buffer, family);
	}

	internal SocketAddress(IPAddress ipAddress)
		: this(ipAddress.AddressFamily, (ipAddress.AddressFamily == AddressFamily.InterNetwork) ? IPv4AddressSize : IPv6AddressSize)
	{
		SocketAddressPal.SetPort(Buffer, 0);
		if (ipAddress.AddressFamily == AddressFamily.InterNetworkV6)
		{
			Span<byte> span = stackalloc byte[16];
			ipAddress.TryWriteBytes(span, out var _);
			SocketAddressPal.SetIPv6Address(Buffer, span, (uint)ipAddress.ScopeId);
		}
		else
		{
			uint address = (uint)ipAddress.Address;
			SocketAddressPal.SetIPv4Address(Buffer, address);
		}
	}

	internal SocketAddress(IPAddress ipaddress, int port)
		: this(ipaddress)
	{
		SocketAddressPal.SetPort(Buffer, (ushort)port);
	}

	internal IPAddress GetIPAddress()
	{
		if (Family == AddressFamily.InterNetworkV6)
		{
			Span<byte> span = stackalloc byte[16];
			SocketAddressPal.GetIPv6Address(Buffer, span, out var scope);
			return new IPAddress(span, scope);
		}
		if (Family == AddressFamily.InterNetwork)
		{
			long newAddress = (long)SocketAddressPal.GetIPv4Address(Buffer) & 0xFFFFFFFFL;
			return new IPAddress(newAddress);
		}
		throw new SocketException(10047);
	}

	internal IPEndPoint GetIPEndPoint()
	{
		IPAddress iPAddress = GetIPAddress();
		int port = SocketAddressPal.GetPort(Buffer);
		return new IPEndPoint(iPAddress, port);
	}

	internal void CopyAddressSizeIntoBuffer()
	{
		Buffer[Buffer.Length - IntPtr.Size] = (byte)InternalSize;
		Buffer[Buffer.Length - IntPtr.Size + 1] = (byte)(InternalSize >> 8);
		Buffer[Buffer.Length - IntPtr.Size + 2] = (byte)(InternalSize >> 16);
		Buffer[Buffer.Length - IntPtr.Size + 3] = (byte)(InternalSize >> 24);
	}

	internal int GetAddressSizeOffset()
	{
		return Buffer.Length - IntPtr.Size;
	}

	public override bool Equals(object comparand)
	{
		if (!(comparand is SocketAddress socketAddress) || Size != socketAddress.Size)
		{
			return false;
		}
		for (int i = 0; i < Size; i++)
		{
			if (this[i] != socketAddress[i])
			{
				return false;
			}
		}
		return true;
	}

	public override int GetHashCode()
	{
		if (_changed)
		{
			_changed = false;
			_hash = 0;
			int num = Size & -4;
			int i;
			for (i = 0; i < num; i += 4)
			{
				_hash ^= Buffer[i] | (Buffer[i + 1] << 8) | (Buffer[i + 2] << 16) | (Buffer[i + 3] << 24);
			}
			if (((uint)Size & 3u) != 0)
			{
				int num2 = 0;
				int num3 = 0;
				for (; i < Size; i++)
				{
					num2 |= Buffer[i] << num3;
					num3 += 8;
				}
				_hash ^= num2;
			}
		}
		return _hash;
	}

	public override string ToString()
	{
		string text = Family.ToString();
		int num = text.Length + 1 + 10 + 2 + (Size - 2) * 4 + 1;
		Span<char> span = ((num > 256) ? ((Span<char>)new char[num]) : stackalloc char[256]);
		Span<char> destination = span;
		text.AsSpan().CopyTo(destination);
		int length = text.Length;
		destination[length++] = ':';
		bool flag = Size.TryFormat(destination.Slice(length), out var charsWritten);
		length += charsWritten;
		destination[length++] = ':';
		destination[length++] = '{';
		byte[] buffer = Buffer;
		for (int i = 2; i < Size; i++)
		{
			if (i > 2)
			{
				destination[length++] = ',';
			}
			flag = buffer[i].TryFormat(destination.Slice(length), out charsWritten);
			length += charsWritten;
		}
		destination[length++] = '}';
		return destination.Slice(0, length).ToString();
	}
}