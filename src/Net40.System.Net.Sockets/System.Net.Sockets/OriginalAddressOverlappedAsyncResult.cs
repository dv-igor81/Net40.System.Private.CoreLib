namespace System.Net.Sockets;

internal sealed class OriginalAddressOverlappedAsyncResult : OverlappedAsyncResult
{
	internal Internals.SocketAddress SocketAddressOriginal { get; set; }

	internal OriginalAddressOverlappedAsyncResult(Net40.Socket socket, object asyncState, AsyncCallback asyncCallback)
		: base(socket, asyncState, asyncCallback)
	{
	}
}