using System.Net.Net40;
using System.Threading.Tasks;

namespace System.Net.Sockets.Net40;

using EndPoint = System.Net.Net40.EndPoint;
using Dns = System.Net.Net40.Dns;
using IPEndPoint = System.Net.Net40.IPEndPoint;
using IPAddress = Net.Net40.IPAddress;

public class UdpClient : IDisposable
{
    private Socket _clientSocket;

    private bool _active;

    private byte[] _buffer = new byte[65536];

    private AddressFamily _family = AddressFamily.InterNetwork;

    private bool _cleanedUp;

    private bool _isBroadcast;

    protected bool Active
    {
        get { return _active; }
        set { _active = value; }
    }

    public int Available => _clientSocket.Available;

    public Socket Client
    {
        get { return _clientSocket; }
        set { _clientSocket = value; }
    }

    public short Ttl
    {
        get { return _clientSocket.Ttl; }
        set { _clientSocket.Ttl = value; }
    }

    public bool DontFragment
    {
        get { return _clientSocket.DontFragment; }
        set { _clientSocket.DontFragment = value; }
    }

    public bool MulticastLoopback
    {
        get { return _clientSocket.MulticastLoopback; }
        set { _clientSocket.MulticastLoopback = value; }
    }

    public bool EnableBroadcast
    {
        get { return _clientSocket.EnableBroadcast; }
        set { _clientSocket.EnableBroadcast = value; }
    }

    public bool ExclusiveAddressUse
    {
        get { return _clientSocket.ExclusiveAddressUse; }
        set { _clientSocket.ExclusiveAddressUse = value; }
    }

    public UdpClient()
        : this(AddressFamily.InterNetwork)
    {
    }

    public UdpClient(AddressFamily family)
    {
        if (family != AddressFamily.InterNetwork && family != AddressFamily.InterNetworkV6)
        {
            throw new ArgumentException(SR.Format(SR.net_protocol_invalid_family, "UDP"), "family");
        }

        _family = family;
        CreateClientSocket();
    }

    public UdpClient(int port)
        : this(port, AddressFamily.InterNetwork)
    {
    }

    public UdpClient(int port, AddressFamily family)
    {
        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        if (family != AddressFamily.InterNetwork && family != AddressFamily.InterNetworkV6)
        {
            throw new ArgumentException(SR.Format(SR.net_protocol_invalid_family, "UDP"), "family");
        }

        _family = family;
        IPEndPoint localEP = ((_family != AddressFamily.InterNetwork)
            ? new IPEndPoint(IPAddress.IPv6Any, port)
            : new IPEndPoint(IPAddress.Any, port));
        CreateClientSocket();
        _clientSocket.Bind(localEP);
    }

    public UdpClient(IPEndPoint localEP)
    {
        if (localEP == null)
        {
            throw new ArgumentNullException("localEP");
        }

        _family = localEP.AddressFamily;
        CreateClientSocket();
        _clientSocket.Bind(localEP);
    }

    public void AllowNatTraversal(bool allowed)
    {
        _clientSocket.SetIPProtectionLevel(allowed ? IPProtectionLevel.Unrestricted : IPProtectionLevel.EdgeRestricted);
    }

    private void FreeResources()
    {
        if (!_cleanedUp)
        {
            Socket clientSocket = _clientSocket;
            if (clientSocket != null)
            {
                clientSocket.InternalShutdown(SocketShutdown.Both);
                clientSocket.Dispose();
                _clientSocket = null;
            }

            _cleanedUp = true;
        }
    }

    private bool IsAddressFamilyCompatible(AddressFamily family)
    {
        if (family == _family)
        {
            return true;
        }

        if (family == AddressFamily.InterNetwork)
        {
            if (_family == AddressFamily.InterNetworkV6)
            {
                return _clientSocket.DualMode;
            }

            return false;
        }

        return false;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this);
            }

            FreeResources();
            GC.SuppressFinalize(this);
        }
    }

    private void CheckForBroadcast(IPAddress ipAddress)
    {
        if (_clientSocket != null && !_isBroadcast && IsBroadcast(ipAddress))
        {
            _isBroadcast = true;
            _clientSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
        }
    }

    private bool IsBroadcast(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return false;
        }

        return address.Equals(IPAddress.Broadcast);
    }

    public IAsyncResult BeginSend(byte[] datagram, int bytes, IPEndPoint endPoint, AsyncCallback requestCallback,
        object state)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (datagram == null)
        {
            throw new ArgumentNullException("datagram");
        }

        if (bytes > datagram.Length || bytes < 0)
        {
            throw new ArgumentOutOfRangeException("bytes");
        }

        if (_active && endPoint != null)
        {
            throw new InvalidOperationException(SR.net_udpconnected);
        }

        if (endPoint == null)
        {
            return _clientSocket.BeginSend(datagram, 0, bytes, SocketFlags.None, requestCallback, state);
        }

        CheckForBroadcast(endPoint.Address);
        return _clientSocket.BeginSendTo(datagram, 0, bytes, SocketFlags.None, endPoint, requestCallback, state);
    }

    public IAsyncResult BeginSend(byte[] datagram, int bytes, string hostname, int port, AsyncCallback requestCallback,
        object state)
    {
        if (_active && (hostname != null || port != 0))
        {
            throw new InvalidOperationException(SR.net_udpconnected);
        }

        IPEndPoint endPoint = null;
        if (hostname != null && port != 0)
        {
            IPAddress[] hostAddresses = Dns.GetHostAddresses(hostname);
            int i;
            for (i = 0; i < hostAddresses.Length && !IsAddressFamilyCompatible(hostAddresses[i].AddressFamily); i++)
            {
            }

            if (hostAddresses.Length == 0 || i == hostAddresses.Length)
            {
                throw new ArgumentException(SR.net_invalidAddressList, "hostname");
            }

            CheckForBroadcast(hostAddresses[i]);
            endPoint = new IPEndPoint(hostAddresses[i], port);
        }

        return BeginSend(datagram, bytes, endPoint, requestCallback, state);
    }

    public IAsyncResult BeginSend(byte[] datagram, int bytes, AsyncCallback requestCallback, object state)
    {
        return BeginSend(datagram, bytes, null, requestCallback, state);
    }

    public int EndSend(IAsyncResult asyncResult)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (_active)
        {
            return _clientSocket.EndSend(asyncResult);
        }

        return _clientSocket.EndSendTo(asyncResult);
    }

    public IAsyncResult BeginReceive(AsyncCallback requestCallback, object state)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        EndPoint remoteEP =
            ((_family != AddressFamily.InterNetwork) ? IPEndPointStatics.IPv6Any : IPEndPointStatics.Any);
        return _clientSocket.BeginReceiveFrom(_buffer, 0, 65536, SocketFlags.None, ref remoteEP, requestCallback,
            state);
    }

    public byte[] EndReceive(IAsyncResult asyncResult, ref IPEndPoint remoteEP)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        EndPoint endPoint =
            ((_family != AddressFamily.InterNetwork) ? IPEndPointStatics.IPv6Any : IPEndPointStatics.Any);
        int num = _clientSocket.EndReceiveFrom(asyncResult, ref endPoint);
        remoteEP = (IPEndPoint)endPoint;
        if (num < 65536)
        {
            byte[] array = new byte[num];
            Buffer.BlockCopy(_buffer, 0, array, 0, num);
            return array;
        }

        return _buffer;
    }

    public void JoinMulticastGroup(IPAddress multicastAddr)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (multicastAddr == null)
        {
            throw new ArgumentNullException("multicastAddr");
        }

        if (multicastAddr.AddressFamily != _family)
        {
            throw new ArgumentException(SR.Format(SR.net_protocol_invalid_multicast_family, "UDP"), "multicastAddr");
        }

        if (_family == AddressFamily.InterNetwork)
        {
            MulticastOption optionValue = new MulticastOption(multicastAddr);
            _clientSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, optionValue);
        }
        else
        {
            IPv6MulticastOption optionValue2 = new IPv6MulticastOption(multicastAddr);
            _clientSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, optionValue2);
        }
    }

    public void JoinMulticastGroup(IPAddress multicastAddr, IPAddress localAddress)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (_family != AddressFamily.InterNetwork)
        {
            throw new SocketException(10045);
        }

        MulticastOption optionValue = new MulticastOption(multicastAddr, localAddress);
        _clientSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, optionValue);
    }

    public void JoinMulticastGroup(int ifindex, IPAddress multicastAddr)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (multicastAddr == null)
        {
            throw new ArgumentNullException("multicastAddr");
        }

        if (ifindex < 0)
        {
            throw new ArgumentException(SR.net_value_cannot_be_negative, "ifindex");
        }

        if (_family != AddressFamily.InterNetworkV6)
        {
            throw new SocketException(10045);
        }

        IPv6MulticastOption optionValue = new IPv6MulticastOption(multicastAddr, ifindex);
        _clientSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership, optionValue);
    }

    public void JoinMulticastGroup(IPAddress multicastAddr, int timeToLive)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (multicastAddr == null)
        {
            throw new ArgumentNullException("multicastAddr");
        }

        if (!RangeValidationHelpers.ValidateRange(timeToLive, 0, 255))
        {
            throw new ArgumentOutOfRangeException("timeToLive");
        }

        JoinMulticastGroup(multicastAddr);
        _clientSocket.SetSocketOption(
            (_family != AddressFamily.InterNetwork) ? SocketOptionLevel.IPv6 : SocketOptionLevel.IP,
            SocketOptionName.MulticastTimeToLive, timeToLive);
    }

    public void DropMulticastGroup(IPAddress multicastAddr)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (multicastAddr == null)
        {
            throw new ArgumentNullException("multicastAddr");
        }

        if (multicastAddr.AddressFamily != _family)
        {
            throw new ArgumentException(SR.Format(SR.net_protocol_invalid_multicast_family, "UDP"), "multicastAddr");
        }

        if (_family == AddressFamily.InterNetwork)
        {
            MulticastOption optionValue = new MulticastOption(multicastAddr);
            _clientSocket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership, optionValue);
        }
        else
        {
            IPv6MulticastOption optionValue2 = new IPv6MulticastOption(multicastAddr);
            _clientSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.DropMembership, optionValue2);
        }
    }

    public void DropMulticastGroup(IPAddress multicastAddr, int ifindex)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (multicastAddr == null)
        {
            throw new ArgumentNullException("multicastAddr");
        }

        if (ifindex < 0)
        {
            throw new ArgumentException(SR.net_value_cannot_be_negative, "ifindex");
        }

        if (_family != AddressFamily.InterNetworkV6)
        {
            throw new SocketException(10045);
        }

        IPv6MulticastOption optionValue = new IPv6MulticastOption(multicastAddr, ifindex);
        _clientSocket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.DropMembership, optionValue);
    }

    public Task<int> SendAsync(byte[] datagram, int bytes)
    {
        return Task<int>.Factory.FromAsync(
            (targetDatagram, targetBytes, callback, state) =>
                ((UdpClient)state).BeginSend(targetDatagram, targetBytes, callback, state),
            asyncResult => ((UdpClient)asyncResult.AsyncState).EndSend(asyncResult), datagram, bytes, this);
    }

    public Task<int> SendAsync(byte[] datagram, int bytes, IPEndPoint endPoint)
    {
        return Task<int>.Factory.FromAsync(
            (targetDatagram, targetBytes, targetEndpoint, callback, state) =>
                ((UdpClient)state).BeginSend(targetDatagram, targetBytes, targetEndpoint, callback, state),
            asyncResult => ((UdpClient)asyncResult.AsyncState).EndSend(asyncResult), datagram, bytes, endPoint, this);
    }

    public Task<int> SendAsync(byte[] datagram, int bytes, string hostname, int port)
    {
        Tuple<byte[], string> arg = Tuple.Create(datagram, hostname);
        return Task<int>.Factory.FromAsync(delegate(Tuple<byte[], string> targetPackedArguments, int targetBytes,
            int targetPort, AsyncCallback callback, object state)
        {
            byte[] item = targetPackedArguments.Item1;
            string item2 = targetPackedArguments.Item2;
            UdpClient udpClient = (UdpClient)state;
            return udpClient.BeginSend(item, targetBytes, item2, targetPort, callback, state);
        }, asyncResult => ((UdpClient)asyncResult.AsyncState).EndSend(asyncResult), arg, bytes, port, this);
    }

    public Task<UdpReceiveResult> ReceiveAsync()
    {
        return Task<UdpReceiveResult>.Factory.FromAsync(
            (callback, state) => ((UdpClient)state).BeginReceive(callback, state), delegate(IAsyncResult asyncResult)
            {
                UdpClient udpClient = (UdpClient)asyncResult.AsyncState;
                IPEndPoint remoteEP = null;
                byte[] buffer = udpClient.EndReceive(asyncResult, ref remoteEP);
                return new UdpReceiveResult(buffer, remoteEP);
            }, this);
    }

    private void CreateClientSocket()
    {
        _clientSocket = new Socket(_family, SocketType.Dgram, ProtocolType.Udp);
    }

    public UdpClient(string hostname, int port)
    {
        if (hostname == null)
        {
            throw new ArgumentNullException("hostname");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        Connect(hostname, port);
    }

    public void Close()
    {
        Dispose(disposing: true);
    }

    public void Connect(string hostname, int port)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (hostname == null)
        {
            throw new ArgumentNullException("hostname");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        IPAddress[] hostAddresses = Dns.GetHostAddresses(hostname);
        Exception ex = null;
        Socket socket = null;
        Socket socket2 = null;
        try
        {
            if (_clientSocket == null)
            {
                if (Socket.OSSupportsIPv4)
                {
                    socket2 = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                }

                if (Socket.OSSupportsIPv6)
                {
                    socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
                }
            }

            IPAddress[] array = hostAddresses;
            foreach (IPAddress iPAddress in array)
            {
                try
                {
                    if (_clientSocket == null)
                    {
                        if (iPAddress.AddressFamily == AddressFamily.InterNetwork && socket2 != null)
                        {
                            socket2.Connect(iPAddress, port);
                            _clientSocket = socket2;
                            socket?.Close();
                        }
                        else if (socket != null)
                        {
                            socket.Connect(iPAddress, port);
                            _clientSocket = socket;
                            socket2?.Close();
                        }

                        _family = iPAddress.AddressFamily;
                        _active = true;
                        break;
                    }

                    if (IsAddressFamilyCompatible(iPAddress.AddressFamily))
                    {
                        Connect(new IPEndPoint(iPAddress, port));
                        _active = true;
                        break;
                    }
                }
                catch (Exception ex2)
                {
                    if (ExceptionCheck.IsFatal(ex2))
                    {
                        throw;
                    }

                    ex = ex2;
                }
            }
        }
        catch (Exception ex3)
        {
            if (ExceptionCheck.IsFatal(ex3))
            {
                throw;
            }

            ex = ex3;
        }
        finally
        {
            if (!_active)
            {
                socket?.Close();
                socket2?.Close();
                if (ex != null)
                {
                    throw ex;
                }

                throw new SocketException(10057);
            }
        }
    }

    public void Connect(IPAddress addr, int port)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (addr == null)
        {
            throw new ArgumentNullException("addr");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        IPEndPoint endPoint = new IPEndPoint(addr, port);
        Connect(endPoint);
    }

    public void Connect(IPEndPoint endPoint)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (endPoint == null)
        {
            throw new ArgumentNullException("endPoint");
        }

        CheckForBroadcast(endPoint.Address);
        Client.Connect(endPoint);
        _active = true;
    }

    public byte[] Receive(ref IPEndPoint remoteEP)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        EndPoint remoteEP2 =
            ((_family != AddressFamily.InterNetwork) ? IPEndPointStatics.IPv6Any : IPEndPointStatics.Any);
        int num = Client.ReceiveFrom(_buffer, 65536, SocketFlags.None, ref remoteEP2);
        remoteEP = (IPEndPoint)remoteEP2;
        if (num < 65536)
        {
            byte[] array = new byte[num];
            Buffer.BlockCopy(_buffer, 0, array, 0, num);
            return array;
        }

        return _buffer;
    }

    public int Send(byte[] dgram, int bytes, IPEndPoint endPoint)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (dgram == null)
        {
            throw new ArgumentNullException("dgram");
        }

        if (_active && endPoint != null)
        {
            throw new InvalidOperationException(SR.net_udpconnected);
        }

        if (endPoint == null)
        {
            return Client.Send(dgram, 0, bytes, SocketFlags.None);
        }

        CheckForBroadcast(endPoint.Address);
        return Client.SendTo(dgram, 0, bytes, SocketFlags.None, endPoint);
    }

    public int Send(byte[] dgram, int bytes, string hostname, int port)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (dgram == null)
        {
            throw new ArgumentNullException("dgram");
        }

        if (_active && (hostname != null || port != 0))
        {
            throw new InvalidOperationException(SR.net_udpconnected);
        }

        if (hostname == null || port == 0)
        {
            return Client.Send(dgram, 0, bytes, SocketFlags.None);
        }

        IPAddress[] hostAddresses = Dns.GetHostAddresses(hostname);
        int i;
        for (i = 0; i < hostAddresses.Length && !IsAddressFamilyCompatible(hostAddresses[i].AddressFamily); i++)
        {
        }

        if (hostAddresses.Length == 0 || i == hostAddresses.Length)
        {
            throw new ArgumentException(SR.net_invalidAddressList, "hostname");
        }

        CheckForBroadcast(hostAddresses[i]);
        IPEndPoint remoteEP = new IPEndPoint(hostAddresses[i], port);
        return Client.SendTo(dgram, 0, bytes, SocketFlags.None, remoteEP);
    }

    public int Send(byte[] dgram, int bytes)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (dgram == null)
        {
            throw new ArgumentNullException("dgram");
        }

        if (!_active)
        {
            throw new InvalidOperationException(SR.net_notconnected);
        }

        return Client.Send(dgram, 0, bytes, SocketFlags.None);
    }
}