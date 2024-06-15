namespace System.Net.Sockets.Net40;

using EndPoint = System.Net.Net40.EndPoint;

public struct SocketReceiveFromResult
{
	public int ReceivedBytes;

	public EndPoint RemoteEndPoint;
}