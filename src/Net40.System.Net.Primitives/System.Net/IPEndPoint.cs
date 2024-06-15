using System.Globalization;

namespace System.Net.Net40;

using AddressFamily = System.Net.Sockets.Net40.AddressFamily;

public class IPEndPoint : EndPoint
{
    public const int MinPort = 0;

    public const int MaxPort = 65535;

    private Net40.IPAddress _address;

    private int _port;

    internal static IPEndPoint Any = new IPEndPoint(Net40.IPAddress.Any, 0);

    internal static IPEndPoint IPv6Any = new IPEndPoint(Net40.IPAddress.IPv6Any, 0);

    public override AddressFamily AddressFamily => _address.AddressFamily;

    public Net40.IPAddress Address
    {
        get { return _address; }
        set { _address = value; }
    }

    public int Port
    {
        get { return _port; }
        set
        {
            if (!TcpValidationHelpers.ValidatePortNumber(value))
            {
                throw new ArgumentOutOfRangeException("value");
            }

            _port = value;
        }
    }

    public IPEndPoint(long address, int port)
    {
        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        _port = port;
        _address = new Net40.IPAddress(address);
    }

    public IPEndPoint(Net40.IPAddress address, int port)
    {
        if (address == null)
        {
            throw new ArgumentNullException("address");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        _port = port;
        _address = address;
    }

    public static bool TryParse(string s, out IPEndPoint result)
    {
        return TryParse(s.AsSpan(), out result);
    }

    public static bool TryParse(ReadOnlySpan<char> s, out IPEndPoint result)
    {
        int num = s.Length;
        int num2 = s.LastIndexOf(':');
        if (num2 > 0)
        {
            if (s[num2 - 1] == ']')
            {
                num = num2;
            }
            else if (s.Slice(0, num2).LastIndexOf(':') == -1)
            {
                num = num2;
            }
        }

        if (Net40.IPAddress.TryParse(s.Slice(0, num).ToString(), out var address))
        {
            uint result2 = 0u;
            if (num == s.Length || (uint.TryParse(s.Slice(num + 1).ToString(), NumberStyles.None,
                    CultureInfo.InvariantCulture, out result2) && result2 <= 65535))
            {
                result = new IPEndPoint(address, (int)result2);
                return true;
            }
        }

        result = null;
        return false;
    }

    public static IPEndPoint Parse(string s)
    {
        if (s == null)
        {
            throw new ArgumentNullException("s");
        }

        return Parse(s.AsSpan());
    }

    public static IPEndPoint Parse(ReadOnlySpan<char> s)
    {
        if (TryParse(s, out var result))
        {
            return result;
        }

        throw new FormatException(System.SR.bad_endpoint_string);
    }

    public override string ToString()
    {
        string format = ((_address.AddressFamily == AddressFamily.InterNetworkV6) ? "[{0}]:{1}" : "{0}:{1}");
        return string.Format(format, _address.ToString(), Port.ToString(NumberFormatInfo.InvariantInfo));
    }

    public override SocketAddress Serialize()
    {
        return new SocketAddress(Address, Port);
    }

    public override EndPoint Create(SocketAddress socketAddress)
    {
        if (socketAddress.Family != AddressFamily)
        {
            throw new ArgumentException(
                System.SR.Format(System.SR.net_InvalidAddressFamily, socketAddress.Family.ToString(),
                    GetType().FullName, AddressFamily.ToString()), "socketAddress");
        }

        if (socketAddress.Size < 8)
        {
            throw new ArgumentException(
                System.SR.Format(System.SR.net_InvalidSocketAddressSize, socketAddress.GetType().FullName,
                    GetType().FullName), "socketAddress");
        }

        return socketAddress.GetIPEndPoint();
    }

    public override bool Equals(object comparand)
    {
        if (comparand is IPEndPoint iPEndPoint && iPEndPoint._address.Equals(_address))
        {
            return iPEndPoint._port == _port;
        }

        return false;
    }

    public override int GetHashCode()
    {
        return _address.GetHashCode() ^ _port;
    }
}