using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace System.Net.Sockets.Net40;

using Dns = System.Net.Net40.Dns;
using IPEndPoint = System.Net.Net40.IPEndPoint;
using IPAddress = Net.Net40.IPAddress;

public class TcpClient : IDisposable
{
    private Net40.AddressFamily _family;

    private Net40.Socket _clientSocket;

    private NetworkStream _dataStream;

    private bool _cleanedUp;

    private bool _active;

    protected bool Active
    {
        get { return _active; }
        set { _active = value; }
    }

    public int Available => _clientSocket?.Available ?? 0;

    public Net40.Socket Client
    {
        get { return _clientSocket; }
        set
        {
            _clientSocket = value;
            _family = _clientSocket?.AddressFamily ?? Net40.AddressFamily.Unknown;
        }
    }

    public bool Connected => _clientSocket?.Connected ?? false;

    public bool ExclusiveAddressUse
    {
        get { return _clientSocket?.ExclusiveAddressUse ?? false; }
        set
        {
            if (_clientSocket != null)
            {
                _clientSocket.ExclusiveAddressUse = value;
            }
        }
    }

    public int ReceiveBufferSize
    {
        get { return (int)Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer); }
        set { Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, value); }
    }

    public int SendBufferSize
    {
        get { return (int)Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer); }
        set { Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, value); }
    }

    public int ReceiveTimeout
    {
        get { return (int)Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout); }
        set { Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, value); }
    }

    public int SendTimeout
    {
        get { return (int)Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout); }
        set { Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, value); }
    }

    public LingerOption LingerState
    {
        get { return Client.LingerState; }
        set { Client.LingerState = value; }
    }

    public bool NoDelay
    {
        get { return Client.NoDelay; }
        set { Client.NoDelay = value; }
    }

    public TcpClient()
        : this(Net40.AddressFamily.Unknown)
    {
    }

    public TcpClient(Net40.AddressFamily family)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, family, ".ctor");
        }

        if (family != Net40.AddressFamily.InterNetwork && family != Net40.AddressFamily.InterNetworkV6 &&
            family != Net40.AddressFamily.Unknown)
        {
            throw new ArgumentException(SR.Format(SR.net_protocol_invalid_family, "TCP"), "family");
        }

        _family = family;
        InitializeClientSocket();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, null, ".ctor");
        }
    }

    public TcpClient(IPEndPoint localEP)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, localEP, ".ctor");
        }

        if (localEP == null)
        {
            throw new ArgumentNullException("localEP");
        }

        _family = localEP.AddressFamily;
        InitializeClientSocket();
        _clientSocket.Bind(localEP);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, null, ".ctor");
        }
    }

    public TcpClient(string hostname, int port)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, hostname, ".ctor");
        }

        if (hostname == null)
        {
            throw new ArgumentNullException("hostname");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        try
        {
            Connect(hostname, port);
        }
        catch
        {
            _clientSocket?.Close();
            throw;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, null, ".ctor");
        }
    }

    internal TcpClient(Net40.Socket acceptedSocket)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, acceptedSocket, ".ctor");
        }

        _clientSocket = acceptedSocket;
        _active = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, null, ".ctor");
        }
    }

    public void Connect(string hostname, int port)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, hostname);
        }

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

        if (_active)
        {
            throw new Net40.SocketException(10056);
        }

        IPAddress[] hostAddresses = Dns.GetHostAddresses(hostname);
        ExceptionDispatchInfo exceptionDispatchInfo = null;
        try
        {
            IPAddress[] array = hostAddresses;
            foreach (IPAddress iPAddress in array)
            {
                Net40.Socket socket = null;
                try
                {
                    if (_clientSocket == null)
                    {
                        if ((iPAddress.AddressFamily == Net40.AddressFamily.InterNetwork && Net40.Socket.OSSupportsIPv4) ||
                            Net40.Socket.OSSupportsIPv6)
                        {
                            socket = new Net40.Socket(iPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                            socket.Connect(iPAddress, port);
                            _clientSocket = socket;
                            socket = null;
                        }

                        _family = iPAddress.AddressFamily;
                        _active = true;
                        break;
                    }

                    if (iPAddress.AddressFamily == _family || _family == Net40.AddressFamily.Unknown)
                    {
                        Connect(new IPEndPoint(iPAddress, port));
                        _active = true;
                        break;
                    }
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    if (socket != null)
                    {
                        socket.Dispose();
                        socket = null;
                    }

                    exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
                }
            }
        }
        finally
        {
            if (!_active)
            {
                exceptionDispatchInfo?.Throw();
                throw new Net40.SocketException(10057);
            }
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void Connect(IPAddress address, int port)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, address);
        }

        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (address == null)
        {
            throw new ArgumentNullException("address");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        IPEndPoint remoteEP = new IPEndPoint(address, port);
        Connect(remoteEP);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void Connect(IPEndPoint remoteEP)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, remoteEP);
        }

        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        Client.Connect(remoteEP);
        _family = Client.AddressFamily;
        _active = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void Connect(IPAddress[] ipAddresses, int port)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, ipAddresses);
        }

        Client.Connect(ipAddresses, port);
        _family = Client.AddressFamily;
        _active = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public Task ConnectAsync(IPAddress address, int port)
    {
        return Task.Factory.FromAsync(
            (targetAddess, targetPort, callback, state) =>
                ((TcpClient)state).BeginConnect(targetAddess, targetPort, callback, state),
            delegate(IAsyncResult asyncResult) { ((TcpClient)asyncResult.AsyncState).EndConnect(asyncResult); },
            address, port, this);
    }

    public Task ConnectAsync(string host, int port)
    {
        return Task.Factory.FromAsync(
            (targetHost, targetPort, callback, state) =>
                ((TcpClient)state).BeginConnect(targetHost, targetPort, callback, state),
            delegate(IAsyncResult asyncResult) { ((TcpClient)asyncResult.AsyncState).EndConnect(asyncResult); }, host,
            port, this);
    }

    public Task ConnectAsync(IPAddress[] addresses, int port)
    {
        return Task.Factory.FromAsync(
            (targetAddresses, targetPort, callback, state) =>
                ((TcpClient)state).BeginConnect(targetAddresses, targetPort, callback, state),
            delegate(IAsyncResult asyncResult) { ((TcpClient)asyncResult.AsyncState).EndConnect(asyncResult); },
            addresses, port, this);
    }

    public IAsyncResult BeginConnect(IPAddress address, int port, AsyncCallback requestCallback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, address);
        }

        IAsyncResult result = Client.BeginConnect(address, port, requestCallback, state);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }

        return result;
    }

    public IAsyncResult BeginConnect(string host, int port, AsyncCallback requestCallback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, host);
        }

        IAsyncResult result = Client.BeginConnect(host, port, requestCallback, state);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }

        return result;
    }

    public IAsyncResult BeginConnect(IPAddress[] addresses, int port, AsyncCallback requestCallback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, addresses);
        }

        IAsyncResult result = Client.BeginConnect(addresses, port, requestCallback, state);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }

        return result;
    }

    public void EndConnect(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        Net40.Socket client = Client;
        if (client == null)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        client.EndConnect(asyncResult);
        _active = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public NetworkStream GetStream()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!Connected)
        {
            throw new InvalidOperationException(SR.net_notconnected);
        }

        if (_dataStream == null)
        {
            _dataStream = new NetworkStream(Client, ownsSocket: true);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, _dataStream);
        }

        return _dataStream;
    }

    public void Close()
    {
        Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (_cleanedUp)
        {
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Exit(this);
            }

            return;
        }

        if (disposing)
        {
            IDisposable dataStream = _dataStream;
            if (dataStream != null)
            {
                dataStream.Dispose();
            }
            else
            {
                Net40.Socket clientSocket = _clientSocket;
                if (clientSocket != null)
                {
                    try
                    {
                        clientSocket.InternalShutdown(SocketShutdown.Both);
                    }
                    finally
                    {
                        clientSocket.Close();
                        _clientSocket = null;
                    }
                }
            }

            GC.SuppressFinalize(this);
        }

        _cleanedUp = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
    }

    ~TcpClient()
    {
        Dispose(disposing: false);
    }

    private void InitializeClientSocket()
    {
        if (_family == Net40.AddressFamily.Unknown)
        {
            _clientSocket = new Net40.Socket(SocketType.Stream, ProtocolType.Tcp);
            if (_clientSocket.AddressFamily == Net40.AddressFamily.InterNetwork)
            {
                _family = Net40.AddressFamily.InterNetwork;
            }
        }
        else
        {
            _clientSocket = new Net40.Socket(_family, SocketType.Stream, ProtocolType.Tcp);
        }
    }
}