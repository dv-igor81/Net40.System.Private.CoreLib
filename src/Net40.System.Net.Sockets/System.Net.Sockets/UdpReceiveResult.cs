namespace System.Net.Sockets;


using IPEndPoint = System.Net.Net40.IPEndPoint;

public struct UdpReceiveResult : IEquatable<UdpReceiveResult>
{
    private byte[] _buffer;

    private IPEndPoint _remoteEndPoint;

    public byte[] Buffer => _buffer;

    public IPEndPoint RemoteEndPoint => _remoteEndPoint;

    public UdpReceiveResult(byte[] buffer, IPEndPoint remoteEndPoint)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (remoteEndPoint == null)
        {
            throw new ArgumentNullException("remoteEndPoint");
        }

        _buffer = buffer;
        _remoteEndPoint = remoteEndPoint;
    }

    public override int GetHashCode()
    {
        if (_buffer == null)
        {
            return 0;
        }

        return _buffer.GetHashCode() ^ _remoteEndPoint.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        if (!(obj is UdpReceiveResult))
        {
            return false;
        }

        return Equals((UdpReceiveResult)obj);
    }

    public bool Equals(UdpReceiveResult other)
    {
        if (Equals(_buffer, other._buffer))
        {
            return Equals(_remoteEndPoint, other._remoteEndPoint);
        }

        return false;
    }

    public static bool operator ==(UdpReceiveResult left, UdpReceiveResult right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(UdpReceiveResult left, UdpReceiveResult right)
    {
        return !left.Equals(right);
    }
}