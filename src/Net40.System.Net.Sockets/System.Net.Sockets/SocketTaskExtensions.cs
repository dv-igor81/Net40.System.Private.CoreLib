using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Sockets.Net40;

using EndPoint = System.Net.Net40.EndPoint;
using IPAddress = Net.Net40.IPAddress;


public static class SocketTaskExtensions
{
    public static Task<Socket> AcceptAsync(this Socket socket)
    {
        return socket.AcceptAsync((Socket)null);
    }

    public static Task<Socket> AcceptAsync(this Socket socket, Socket acceptSocket)
    {
        return socket.AcceptAsync(acceptSocket);
    }

    public static Task ConnectAsync(this Socket socket, EndPoint remoteEP)
    {
        return socket.ConnectAsync(remoteEP);
    }

    public static Task ConnectAsync(this Socket socket, IPAddress address, int port)
    {
        return socket.ConnectAsync(address, port);
    }

    public static Task ConnectAsync(this Socket socket, IPAddress[] addresses, int port)
    {
        return socket.ConnectAsync(addresses, port);
    }

    public static Task ConnectAsync(this Socket socket, string host, int port)
    {
        return socket.ConnectAsync(host, port);
    }

    public static Task<int> ReceiveAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags)
    {
        return socket.ReceiveAsync(buffer, socketFlags, fromNetworkStream: false);
    }

    public static ValueTask<int> ReceiveAsync(this Socket socket, Memory<byte> buffer, SocketFlags socketFlags,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        return socket.ReceiveAsync(buffer, socketFlags, fromNetworkStream: false, cancellationToken);
    }

    public static Task<int> ReceiveAsync(this Socket socket, IList<ArraySegment<byte>> buffers,
        SocketFlags socketFlags)
    {
        return socket.ReceiveAsync(buffers, socketFlags);
    }

    public static Task<SocketReceiveFromResult> ReceiveFromAsync(this Socket socket, ArraySegment<byte> buffer,
        SocketFlags socketFlags, EndPoint remoteEndPoint)
    {
        return socket.ReceiveFromAsync(buffer, socketFlags, remoteEndPoint);
    }

    public static Task<SocketReceiveMessageFromResult> ReceiveMessageFromAsync(this Socket socket,
        ArraySegment<byte> buffer, SocketFlags socketFlags, EndPoint remoteEndPoint)
    {
        return socket.ReceiveMessageFromAsync(buffer, socketFlags, remoteEndPoint);
    }

    public static Task<int> SendAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags)
    {
        return socket.SendAsync(buffer, socketFlags);
    }

    public static ValueTask<int> SendAsync(this Socket socket, ReadOnlyMemory<byte> buffer, SocketFlags socketFlags,
        CancellationToken cancellationToken = default(CancellationToken))
    {
        return socket.SendAsync(buffer, socketFlags, cancellationToken);
    }

    public static Task<int> SendAsync(this Socket socket, IList<ArraySegment<byte>> buffers, SocketFlags socketFlags)
    {
        return socket.SendAsync(buffers, socketFlags);
    }

    public static Task<int> SendToAsync(this Socket socket, ArraySegment<byte> buffer, SocketFlags socketFlags,
        EndPoint remoteEP)
    {
        return socket.SendToAsync(buffer, socketFlags, remoteEP);
    }
}