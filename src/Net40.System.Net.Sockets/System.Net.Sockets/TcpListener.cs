using System.Threading.Tasks;

namespace System.Net.Sockets.Net40;

using EndPoint = System.Net.Net40.EndPoint;
using IPEndPoint = System.Net.Net40.IPEndPoint;
using IPAddress = Net.Net40.IPAddress;

public class TcpListener
{
    private IPEndPoint _serverSocketEP;

    private Socket _serverSocket;

    private bool _active;

    private bool _exclusiveAddressUse;

    public Socket Server => _serverSocket;

    protected bool Active => _active;

    public EndPoint LocalEndpoint
    {
        get
        {
            if (!_active)
            {
                return _serverSocketEP;
            }

            return _serverSocket.LocalEndPoint;
        }
    }

    public bool ExclusiveAddressUse
    {
        get { return _serverSocket.ExclusiveAddressUse; }
        set
        {
            if (_active)
            {
                throw new InvalidOperationException(SR.net_tcplistener_mustbestopped);
            }

            _serverSocket.ExclusiveAddressUse = value;
            _exclusiveAddressUse = value;
        }
    }

    public TcpListener(IPEndPoint localEP)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, localEP, ".ctor");
        }

        if (localEP == null)
        {
            throw new ArgumentNullException("localEP");
        }

        _serverSocketEP = localEP;
        _serverSocket = new Socket(_serverSocketEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, null, ".ctor");
        }
    }

    public TcpListener(IPAddress localaddr, int port)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, localaddr, ".ctor");
        }

        if (localaddr == null)
        {
            throw new ArgumentNullException("localaddr");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        _serverSocketEP = new IPEndPoint(localaddr, port);
        _serverSocket = new Socket(_serverSocketEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, null, ".ctor");
        }
    }

    [Obsolete(
        "This method has been deprecated. Please use TcpListener(IPAddress localaddr, int port) instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public TcpListener(int port)
    {
        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        _serverSocketEP = new IPEndPoint(IPAddress.Any, port);
        _serverSocket = new Socket(_serverSocketEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
    }

    public void AllowNatTraversal(bool allowed)
    {
        if (_active)
        {
            throw new InvalidOperationException(SR.net_tcplistener_mustbestopped);
        }

        _serverSocket.SetIPProtectionLevel(allowed ? IPProtectionLevel.Unrestricted : IPProtectionLevel.EdgeRestricted);
    }

    public void Start()
    {
        Start(int.MaxValue);
    }

    public void Start(int backlog)
    {
        if (backlog > int.MaxValue || backlog < 0)
        {
            throw new ArgumentOutOfRangeException("backlog");
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (_active)
        {
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Exit(this);
            }

            return;
        }

        _serverSocket.Bind(_serverSocketEP);
        try
        {
            _serverSocket.Listen(backlog);
        }
        catch (SocketException)
        {
            Stop();
            throw;
        }

        _active = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void Stop()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        _serverSocket.Dispose();
        _active = false;
        _serverSocket = new Socket(_serverSocketEP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        if (_exclusiveAddressUse)
        {
            _serverSocket.ExclusiveAddressUse = true;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public bool Pending()
    {
        if (!_active)
        {
            throw new InvalidOperationException(SR.net_stopped);
        }

        return _serverSocket.Poll(0, SelectMode.SelectRead);
    }

    public Socket AcceptSocket()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (!_active)
        {
            throw new InvalidOperationException(SR.net_stopped);
        }

        Socket socket = _serverSocket.Accept();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, socket);
        }

        return socket;
    }

    public TcpClient AcceptTcpClient()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (!_active)
        {
            throw new InvalidOperationException(SR.net_stopped);
        }

        Socket acceptedSocket = _serverSocket.Accept();
        TcpClient tcpClient = new TcpClient(acceptedSocket);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, tcpClient);
        }

        return tcpClient;
    }

    public IAsyncResult BeginAcceptSocket(AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (!_active)
        {
            throw new InvalidOperationException(SR.net_stopped);
        }

        IAsyncResult result = _serverSocket.BeginAccept(callback, state);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }

        return result;
    }

    public Socket EndAcceptSocket(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        Socket socket = ((!(asyncResult is LazyAsyncResult lazyAsyncResult))
            ? null
            : (lazyAsyncResult.AsyncObject as Socket));
        if (socket == null)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        Socket socket2 = socket.EndAccept(asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, socket2);
        }

        return socket2;
    }

    public IAsyncResult BeginAcceptTcpClient(AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (!_active)
        {
            throw new InvalidOperationException(SR.net_stopped);
        }

        IAsyncResult asyncResult = _serverSocket.BeginAccept(callback, state);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, asyncResult);
        }

        return asyncResult;
    }

    public TcpClient EndAcceptTcpClient(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        Socket socket = ((!(asyncResult is LazyAsyncResult lazyAsyncResult))
            ? null
            : (lazyAsyncResult.AsyncObject as Socket));
        if (socket == null)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        Socket socket2 = socket.EndAccept(asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, socket2);
        }

        return new TcpClient(socket2);
    }

    public Task<Socket> AcceptSocketAsync()
    {
        return Task<Socket>.Factory.FromAsync(
            (callback, state) => ((TcpListener)state).BeginAcceptSocket(callback, state),
            asyncResult => ((TcpListener)asyncResult.AsyncState).EndAcceptSocket(asyncResult), this);
    }

    public Task<TcpClient> AcceptTcpClientAsync()
    {
        return Task<TcpClient>.Factory.FromAsync(
            (callback, state) => ((TcpListener)state).BeginAcceptTcpClient(callback, state),
            asyncResult => ((TcpListener)asyncResult.AsyncState).EndAcceptTcpClient(asyncResult), this);
    }

    public static TcpListener Create(int port)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null, port);
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        TcpListener tcpListener;
        if (Socket.OSSupportsIPv6)
        {
            tcpListener = new TcpListener(IPAddress.IPv6Any, port);
            tcpListener.Server.DualMode = true;
        }
        else
        {
            tcpListener = new TcpListener(IPAddress.Any, port);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, port);
        }

        return tcpListener;
    }
}