using System.Net.Sockets.Net40;

namespace System.Net.Sockets;

internal sealed class DisconnectOverlappedAsyncResult : BaseOverlappedAsyncResult
{
	internal DisconnectOverlappedAsyncResult(Net40.Socket socket, object asyncState, AsyncCallback asyncCallback)
		: base(socket, asyncState, asyncCallback)
	{
		}

	internal override object PostCompletion(int numBytes)
	{
			if (ErrorCode == 0)
			{
				Net40.Socket socket = (Net40.Socket)AsyncObject;
				socket.SetToDisconnected();
				socket._remoteEndPoint = null;
			}
			return base.PostCompletion(numBytes);
		}
}