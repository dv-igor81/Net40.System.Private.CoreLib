namespace System.Net.Sockets.Net40;

using EndPoint = System.Net.Net40.EndPoint;

public struct SocketReceiveMessageFromResult
{
	public int ReceivedBytes;

	public SocketFlags SocketFlags;

	public EndPoint RemoteEndPoint;

	public IPPacketInformation PacketInformation;
}