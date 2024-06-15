using System.Net.Sockets;

namespace System.Net.Internals.Net40;

internal static class SocketExceptionFactory
{
	public static SocketException CreateSocketException(SocketError errorCode, int platformError)
	{
		return new SocketException((int)errorCode);
	}
}
