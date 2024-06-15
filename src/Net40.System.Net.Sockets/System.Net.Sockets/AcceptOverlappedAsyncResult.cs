using System.Runtime.InteropServices;

namespace System.Net.Sockets.Net40;

internal sealed class AcceptOverlappedAsyncResult : BaseOverlappedAsyncResult
{
    private Socket _listenSocket;

    private byte[] _buffer;

    private Socket _acceptSocket;

    private int _addressBufferLength;

    internal byte[] Buffer => _buffer;

    internal int BytesTransferred => _numBytes;

    internal Socket AcceptSocket
    {
        set { _acceptSocket = value; }
    }

    internal AcceptOverlappedAsyncResult(Socket listenSocket, object asyncState, AsyncCallback asyncCallback)
        : base(listenSocket, asyncState, asyncCallback)
    {
        _listenSocket = listenSocket;
    }

    internal override object PostCompletion(int numBytes)
    {
        SocketError socketError = (SocketError)ErrorCode;
        Internals.SocketAddress socketAddress = null;
        if (socketError == SocketError.Success)
        {
            _numBytes = numBytes;
            if (NetEventSource.IsEnabled)
            {
                LogBuffer(numBytes);
            }

            socketAddress = IPEndPointExtensions.Serialize(_listenSocket._rightEndPoint);
            try
            {
                _listenSocket.GetAcceptExSockaddrs(Marshal.UnsafeAddrOfPinnedArrayElement(_buffer, 0),
                    _buffer.Length - _addressBufferLength * 2, _addressBufferLength, _addressBufferLength, out var _,
                    out var _, out var remoteSocketAddress, out socketAddress.InternalSize);
                Marshal.Copy(remoteSocketAddress, socketAddress.Buffer, 0, socketAddress.Size);
                IntPtr pointer = _listenSocket.SafeHandle.DangerousGetHandle();
                socketError = Interop.Winsock.setsockopt(_acceptSocket.SafeHandle, SocketOptionLevel.Socket,
                    SocketOptionName.UpdateAcceptContext, ref pointer, IntPtr.Size);
                if (socketError == SocketError.SocketError)
                {
                    socketError = SocketPal.GetLastSocketError();
                }

                if (NetEventSource.IsEnabled)
                {
                    NetEventSource.Info(this,
                        $"setsockopt handle:{pointer}, AcceptSocket:{_acceptSocket}, returns:{socketError}");
                }
            }
            catch (ObjectDisposedException)
            {
                socketError = SocketError.OperationAborted;
            }

            ErrorCode = (int)socketError;
        }

        if (socketError != 0)
        {
            return null;
        }

        return _listenSocket.UpdateAcceptSocket(_acceptSocket, _listenSocket._rightEndPoint.Create(socketAddress));
    }

    internal void SetUnmanagedStructures(byte[] buffer, int addressBufferLength)
    {
        SetUnmanagedStructures(buffer);
        _addressBufferLength = addressBufferLength;
        _buffer = buffer;
    }

    private void LogBuffer(long size)
    {
        if (size > -1)
        {
            NetEventSource.DumpBuffer(this, _buffer, 0, Math.Min((int)size, _buffer.Length));
        }
        else
        {
            NetEventSource.DumpBuffer(this, _buffer);
        }
    }
}