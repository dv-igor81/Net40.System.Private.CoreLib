namespace System.Net.Sockets.Net40;

using IPEndPoint = Net.Net40.IPEndPoint;
using EndPoint = Net.Net40.EndPoint;
using SocketAddress = Net.Net40.SocketAddress;

internal static class IPEndPointExtensions
{
	public static Internals.SocketAddress Serialize(EndPoint endpoint)
	{
		if (endpoint is IPEndPoint iPEndPoint)
		{
			return new Internals.SocketAddress(iPEndPoint.Address, iPEndPoint.Port);
		}
		SocketAddress address = endpoint.Serialize();
		return GetInternalSocketAddress(address);
	}

	public static EndPoint Create(this EndPoint thisObj, Internals.SocketAddress socketAddress)
	{
		AddressFamily family = socketAddress.Family;
		if (family != thisObj.AddressFamily)
		{
			throw new ArgumentException(SR.Format(SR.net_InvalidAddressFamily, family.ToString(), thisObj.GetType().FullName, thisObj.AddressFamily.ToString()), "socketAddress");
		}
		if (family == AddressFamily.InterNetwork || family == AddressFamily.InterNetworkV6)
		{
			if (socketAddress.Size < 8)
			{
				throw new ArgumentException(SR.Format(SR.net_InvalidSocketAddressSize, socketAddress.GetType().FullName, thisObj.GetType().FullName), "socketAddress");
			}
			return socketAddress.GetIPEndPoint();
		}
		SocketAddress netSocketAddress = GetNetSocketAddress(socketAddress);
		return thisObj.Create(netSocketAddress);
	}

	internal static IPEndPoint Snapshot(this IPEndPoint thisObj)
	{
		return new IPEndPoint(thisObj.Address.Snapshot(), thisObj.Port);
	}

	private static Internals.SocketAddress GetInternalSocketAddress(SocketAddress address)
	{
		Internals.SocketAddress socketAddress = new Internals.SocketAddress(address.Family, address.Size);
		for (int i = 0; i < address.Size; i++)
		{
			socketAddress[i] = address[i];
		}
		return socketAddress;
	}

	private static SocketAddress GetNetSocketAddress(Internals.SocketAddress address)
	{
		SocketAddress socketAddress = new SocketAddress(address.Family, address.Size);
		for (int i = 0; i < address.Size; i++)
		{
			socketAddress[i] = address[i];
		}
		return socketAddress;
	}
}