using System.Text;

namespace System.Net.Sockets.Net40;

using EndPoint = System.Net.Net40.EndPoint;
using SocketAddress = System.Net.Net40.SocketAddress;

public sealed class UnixDomainSocketEndPoint : EndPoint
{
	private static readonly Encoding s_pathEncoding = Encoding.UTF8;

	private static readonly Lazy<bool> s_udsSupported = new Lazy<bool>(delegate
	{
		try
		{
			new Net40.Socket(Net40.AddressFamily.Unix, SocketType.Stream, ProtocolType.IP).Dispose();
			return true;
		}
		catch
		{
			return false;
		}
	});

	private readonly string _path;

	private readonly byte[] _encodedPath;

	private static readonly int s_nativePathOffset = 2;

	private static readonly int s_nativePathLength = 108;

	private static readonly int s_nativeAddressSize = s_nativePathOffset + s_nativePathLength;

	public override Net40.AddressFamily AddressFamily => Net40.AddressFamily.Unix;

	public UnixDomainSocketEndPoint(string path)
	{
		if (path == null)
		{
			throw new ArgumentNullException("path");
		}
		bool flag = IsAbstract(path);
		int num = s_pathEncoding.GetByteCount(path);
		if (!flag)
		{
			num++;
		}
		if (path.Length == 0 || num > s_nativePathLength)
		{
			throw new ArgumentOutOfRangeException("path", path, SR.Format(SR.ArgumentOutOfRange_PathLengthInvalid, path, s_nativePathLength));
		}
		_path = path;
		_encodedPath = new byte[num];
		int bytes = s_pathEncoding.GetBytes(path, 0, path.Length, _encodedPath, 0);
		if (!s_udsSupported.Value)
		{
			throw new PlatformNotSupportedException();
		}
	}

	internal UnixDomainSocketEndPoint(SocketAddress socketAddress)
	{
		if (socketAddress == null)
		{
			throw new ArgumentNullException("socketAddress");
		}
		if (socketAddress.Family != Net40.AddressFamily.Unix || socketAddress.Size > s_nativeAddressSize)
		{
			throw new ArgumentOutOfRangeException("socketAddress");
		}
		if (socketAddress.Size > s_nativePathOffset)
		{
			_encodedPath = new byte[socketAddress.Size - s_nativePathOffset];
			for (int i = 0; i < _encodedPath.Length; i++)
			{
				_encodedPath[i] = socketAddress[s_nativePathOffset + i];
			}
			int num = _encodedPath.Length;
			if (!IsAbstract(_encodedPath))
			{
				while (_encodedPath[num - 1] == 0)
				{
					num--;
				}
			}
			_path = s_pathEncoding.GetString(_encodedPath, 0, num);
		}
		else
		{
			_encodedPath = ArrayEx.Empty<byte>();
			_path = string.Empty;
		}
	}

	public override SocketAddress Serialize()
	{
		SocketAddress socketAddress = CreateSocketAddressForSerialize();
		for (int i = 0; i < _encodedPath.Length; i++)
		{
			socketAddress[s_nativePathOffset + i] = _encodedPath[i];
		}
		return socketAddress;
	}

	public override EndPoint Create(SocketAddress socketAddress)
	{
		return new UnixDomainSocketEndPoint(socketAddress);
	}

	public override string ToString()
	{
		if (IsAbstract(_path))
		{
			return "@" + _path.AsSpan(1);
		}
		return _path;
	}

	private static bool IsAbstract(string path)
	{
		if (path.Length > 0)
		{
			return path[0] == '\0';
		}
		return false;
	}

	private static bool IsAbstract(byte[] encodedPath)
	{
		if (encodedPath.Length != 0)
		{
			return encodedPath[0] == 0;
		}
		return false;
	}

	private SocketAddress CreateSocketAddressForSerialize()
	{
		return new SocketAddress(Net40.AddressFamily.Unix, s_nativeAddressSize);
	}
}