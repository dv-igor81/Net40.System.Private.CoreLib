namespace System.Net.Sockets;

public struct SocketInformation
{
	public byte[] ProtocolInformation { get; set; }

	public SocketInformationOptions Options { get; set; }
}