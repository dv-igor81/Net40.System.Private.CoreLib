namespace System.Net.Sockets.Net40;

using EndPoint = Net.Net40.EndPoint;

internal sealed class ConnectOverlappedAsyncResult : BaseOverlappedAsyncResult
{
    private EndPoint _endPoint;

    internal override EndPoint RemoteEndPoint => _endPoint;

    internal ConnectOverlappedAsyncResult(Socket socket, EndPoint endPoint, object asyncState,
        AsyncCallback asyncCallback)
        : base(socket, asyncState, asyncCallback)
    {
        _endPoint = endPoint;
    }

    internal override object PostCompletion(int numBytes)
    {
        SocketError socketError = (SocketError)ErrorCode;
        Socket socket = (Socket)AsyncObject;
        if (socketError == SocketError.Success)
        {
            try
            {
                socketError = Interop.Winsock.setsockopt(socket.SafeHandle, SocketOptionLevel.Socket,
                    SocketOptionName.UpdateConnectContext, null, 0);
                if (socketError == SocketError.SocketError)
                {
                    socketError = SocketPal.GetLastSocketError();
                }
            }
            catch (ObjectDisposedException)
            {
                socketError = SocketError.OperationAborted;
            }

            ErrorCode = (int)socketError;
        }

        if (socketError == SocketError.Success)
        {
            socket.SetToConnected();
            return socket;
        }

        return null;
    }
}