using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Internals.Net40;
using System.Net.Net40;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Microsoft.Win32.SafeHandles;

namespace System.Net.Sockets.Net40;

using DnsEndPoint = System.Net.Net40.DnsEndPoint;
using EndPoint = System.Net.Net40.EndPoint;
using Dns = System.Net.Net40.Dns;
using IPEndPoint = System.Net.Net40.IPEndPoint;
using IPAddress = Net.Net40.IPAddress;

public class Socket : IDisposable
{
    private class CacheSet
    {
        internal CallbackClosure ConnectClosureCache;

        internal CallbackClosure AcceptClosureCache;

        internal CallbackClosure SendClosureCache;

        internal CallbackClosure ReceiveClosureCache;
    }

    private sealed class ConnectAsyncResult : ContextAwareResult
    {
        private EndPoint _endPoint;

        internal override EndPoint RemoteEndPoint => _endPoint;

        internal ConnectAsyncResult(object myObject, EndPoint endPoint, object myState, AsyncCallback myCallBack)
            : base(myObject, myState, myCallBack)
        {
            _endPoint = endPoint;
        }
    }

    private sealed class MultipleAddressConnectAsyncResult : ContextAwareResult
    {
        internal Socket _socket;

        internal IPAddress[] _addresses;

        internal int _index;

        internal int _port;

        internal Exception _lastException;

        internal override EndPoint RemoteEndPoint
        {
            get
            {
                if (_addresses != null && _index > 0 && _index < _addresses.Length)
                {
                    return new IPEndPoint(_addresses[_index], _port);
                }

                return null;
            }
        }

        internal MultipleAddressConnectAsyncResult(IPAddress[] addresses, int port, Socket socket, object myState,
            AsyncCallback myCallBack)
            : base(socket, myState, myCallBack)
        {
            _addresses = addresses;
            _port = port;
            _socket = socket;
        }
    }

    private class StateTaskCompletionSource<TField1, TResult> : TaskCompletionSource<TResult>
    {
        internal TField1 _field1;

        public StateTaskCompletionSource(object baseState)
            : base(baseState)
        {
        }
    }

    private class StateTaskCompletionSource<TField1, TField2, TResult> : StateTaskCompletionSource<TField1, TResult>
    {
        internal TField2 _field2;

        public StateTaskCompletionSource(object baseState)
            : base(baseState)
        {
        }
    }

    private sealed class CachedEventArgs
    {
        public TaskSocketAsyncEventArgs<Socket> TaskAccept;

        public Int32TaskSocketAsyncEventArgs TaskReceive;

        public Int32TaskSocketAsyncEventArgs TaskSend;

        public AwaitableSocketAsyncEventArgs ValueTaskReceive;

        public AwaitableSocketAsyncEventArgs ValueTaskSend;
    }

    private class TaskSocketAsyncEventArgs<TResult> : SocketAsyncEventArgs
    {
        internal AsyncTaskMethodBuilder<TResult> _builder;

        internal bool _accessed;

        internal TaskSocketAsyncEventArgs()
            : base(flowExecutionContext: false)
        {
        }

        internal AsyncTaskMethodBuilder<TResult> GetCompletionResponsibility(out bool responsibleForReturningToPool)
        {
            lock (this)
            {
                responsibleForReturningToPool = _accessed;
                _accessed = true;
                Task<TResult> task = _builder.Task;
                return _builder;
            }
        }
    }

    private sealed class Int32TaskSocketAsyncEventArgs : TaskSocketAsyncEventArgs<int>
    {
        internal bool _wrapExceptionsInIOExceptions;
    }

    internal sealed class AwaitableSocketAsyncEventArgs : SocketAsyncEventArgs, IValueTaskSource,
        IValueTaskSource<int>
    {
        internal static readonly AwaitableSocketAsyncEventArgs Reserved = new AwaitableSocketAsyncEventArgs
        {
            _continuation = null
        };

        private static readonly Action<object> s_completedSentinel = delegate
        {
            throw new Exception("s_completedSentinel");
        };

        private static readonly Action<object> s_availableSentinel = delegate
        {
            throw new Exception("s_availableSentinel");
        };

        private Action<object> _continuation = s_availableSentinel;

        private ExecutionContext _executionContext;

        private object _scheduler;

        private short _token;

        private CancellationToken _cancellationToken;

        public bool WrapExceptionsInIOExceptions { get; set; }

        public AwaitableSocketAsyncEventArgs()
            : base(flowExecutionContext: false)
        {
        }

        public bool Reserve()
        {
            return (object)Interlocked.CompareExchange(ref _continuation, null, s_availableSentinel) ==
                   s_availableSentinel;
        }

        private void Release()
        {
            _cancellationToken = default(CancellationToken);
            _token++;
            Volatile.Write(ref _continuation, s_availableSentinel);
        }

        protected override void OnCompleted(SocketAsyncEventArgs _)
        {
            Action<object> action = _continuation;
            if (action == null &&
                (action = Interlocked.CompareExchange(ref _continuation, s_completedSentinel, null)) == null)
            {
                return;
            }

            object userToken = UserToken;
            UserToken = null;
            _continuation = s_completedSentinel;
            ExecutionContext executionContext = _executionContext;
            if (executionContext == null)
            {
                InvokeContinuation(action, userToken, forceAsync: false, requiresExecutionContextFlow: false);
                return;
            }

            _executionContext = null;
            ExecutionContext.Run(executionContext, delegate(object runState)
            {
                Tuple<AwaitableSocketAsyncEventArgs, Action<object>, object> tuple =
                    (Tuple<AwaitableSocketAsyncEventArgs, Action<object>, object>)runState;
                tuple.Item1.InvokeContinuation(tuple.Item2, tuple.Item3, forceAsync: false,
                    requiresExecutionContextFlow: false);
            }, Tuple.Create(this, action, userToken));
        }

        public ValueTask<int> ReceiveAsync(Socket socket, CancellationToken cancellationToken)
        {
            if (socket.ReceiveAsync(this, cancellationToken))
            {
                _cancellationToken = cancellationToken;
                return new ValueTask<int>(this, _token);
            }

            int bytesTransferred = BytesTransferred;
            SocketError socketError = SocketError;
            Release();
            if (socketError != 0)
            {
                return new ValueTask<int>(TaskExEx.FromException<int>(CreateException(socketError)));
            }

            return new ValueTask<int>(bytesTransferred);
        }

        public ValueTask<int> SendAsync(Socket socket, CancellationToken cancellationToken)
        {
            if (socket.SendAsync(this, cancellationToken))
            {
                _cancellationToken = cancellationToken;
                return new ValueTask<int>(this, _token);
            }

            int bytesTransferred = BytesTransferred;
            SocketError socketError = SocketError;
            Release();
            if (socketError != 0)
            {
                return new ValueTask<int>(TaskExEx.FromException<int>(CreateException(socketError)));
            }

            return new ValueTask<int>(bytesTransferred);
        }

        public ValueTask SendAsyncForNetworkStream(Socket socket, CancellationToken cancellationToken)
        {
            if (socket.SendAsync(this, cancellationToken))
            {
                _cancellationToken = cancellationToken;
                return new ValueTask(this, _token);
            }

            SocketError socketError = SocketError;
            Release();
            if (socketError != 0)
            {
                return new ValueTask(TaskExEx.FromException(CreateException(socketError)));
            }

            return default(ValueTask);
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            if (token != _token)
            {
                ThrowIncorrectTokenException();
            }

            if ((object)_continuation == s_completedSentinel)
            {
                if (SocketError != 0)
                {
                    return ValueTaskSourceStatus.Faulted;
                }

                return ValueTaskSourceStatus.Succeeded;
            }

            return ValueTaskSourceStatus.Pending;
        }

        public void OnCompleted(Action<object> continuation, object state, short token,
            ValueTaskSourceOnCompletedFlags flags)
        {
            if (token != _token)
            {
                ThrowIncorrectTokenException();
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                _executionContext = ExecutionContext.Capture();
            }

            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                SynchronizationContext current = SynchronizationContext.Current;
                if (current != null && current.GetType() != typeof(SynchronizationContext))
                {
                    _scheduler = current;
                }
                else
                {
                    TaskScheduler current2 = TaskScheduler.Current;
                    if (current2 != TaskScheduler.Default)
                    {
                        _scheduler = current2;
                    }
                }
            }

            UserToken = state;
            Action<object> action = Interlocked.CompareExchange(ref _continuation, continuation, null);
            if ((object)action == s_completedSentinel)
            {
                bool requiresExecutionContextFlow = _executionContext != null;
                _executionContext = null;
                UserToken = null;
                InvokeContinuation(continuation, state, forceAsync: true, requiresExecutionContextFlow);
            }
            else if (action != null)
            {
                ThrowMultipleContinuationsException();
            }
        }

        private void InvokeContinuation(Action<object> continuation, object state, bool forceAsync,
            bool requiresExecutionContextFlow)
        {
            object scheduler = _scheduler;
            _scheduler = null;
            if (scheduler != null)
            {
                if (scheduler is SynchronizationContext synchronizationContext)
                {
                    synchronizationContext.Post(delegate(object s)
                    {
                        Tuple<Action<object>, object> tuple = (Tuple<Action<object>, object>)s;
                        tuple.Item1(tuple.Item2);
                    }, Tuple.Create(continuation, state));
                }
                else
                {
                    Task.Factory.StartNew(continuation, state, CancellationToken.None,
                        //TaskCreationOptions.DenyChildAttach,
                        TaskCreationOptions.None,
                        (TaskScheduler)scheduler);
                }
            }
            else if (forceAsync)
            {
                if (requiresExecutionContextFlow)
                {
                    ThreadPoolEx.QueueUserWorkItem(continuation, state, preferLocal: true);
                }
                else
                {
                    ThreadPoolEx.UnsafeQueueUserWorkItem(continuation, state, preferLocal: true);
                }
            }
            else
            {
                continuation(state);
            }
        }

        public int GetResult(short token)
        {
            if (token != _token)
            {
                ThrowIncorrectTokenException();
            }

            SocketError socketError = SocketError;
            int bytesTransferred = BytesTransferred;
            CancellationToken cancellationToken = _cancellationToken;
            Release();
            if (socketError != 0)
            {
                ThrowException(socketError, cancellationToken);
            }

            return bytesTransferred;
        }

        void IValueTaskSource.GetResult(short token)
        {
            if (token != _token)
            {
                ThrowIncorrectTokenException();
            }

            SocketError socketError = SocketError;
            CancellationToken cancellationToken = _cancellationToken;
            Release();
            if (socketError != 0)
            {
                ThrowException(socketError, cancellationToken);
            }
        }

        private void ThrowIncorrectTokenException()
        {
            throw new InvalidOperationException(SR.InvalidOperation_IncorrectToken);
        }

        private void ThrowMultipleContinuationsException()
        {
            throw new InvalidOperationException(SR.InvalidOperation_MultipleContinuations);
        }

        private void ThrowException(SocketError error, CancellationToken cancellationToken)
        {
            if (error == SocketError.OperationAborted)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            throw CreateException(error);
        }

        private Exception CreateException(SocketError error)
        {
            SocketException ex = new SocketException((int)error);
            if (!WrapExceptionsInIOExceptions)
            {
                return ex;
            }

            return new IOException(SR.Format(SR.net_io_readfailure, ex.Message),
                (Exception?)ex);
        }
    }

    private SafeSocketHandle _handle;

    internal EndPoint _rightEndPoint;

    internal EndPoint _remoteEndPoint;

    private bool _isConnected;

    private bool _isDisconnected;

    private bool _willBlock = true;

    private bool _willBlockInternal = true;

    private bool _isListening;

    private bool _nonBlockingConnectInProgress;

    private EndPoint _nonBlockingConnectRightEndPoint;

    private AddressFamily _addressFamily;

    private SocketType _socketType;

    private ProtocolType _protocolType;

    private CacheSet _caches;

    private bool _receivingPacketInformation;

    private static object s_internalSyncObject;

    private int _closeTimeout = -1;

    private int _intCleanedUp;

    internal static volatile bool s_initialized;

    private static AsyncCallback s_multipleAddressConnectCallback;

    private static readonly EventHandler<SocketAsyncEventArgs> AcceptCompletedHandler =
        delegate(object s, SocketAsyncEventArgs e)
        {
            CompleteAccept((Socket)s, (TaskSocketAsyncEventArgs<Socket>)e);
        };

    private static readonly EventHandler<SocketAsyncEventArgs> ReceiveCompletedHandler =
        delegate(object s, SocketAsyncEventArgs e)
        {
            CompleteSendReceive((Socket)s, (Int32TaskSocketAsyncEventArgs)e, isReceive: true);
        };

    private static readonly EventHandler<SocketAsyncEventArgs> SendCompletedHandler =
        delegate(object s, SocketAsyncEventArgs e)
        {
            CompleteSendReceive((Socket)s, (Int32TaskSocketAsyncEventArgs)e, isReceive: false);
        };

    private static readonly TaskSocketAsyncEventArgs<Socket> s_rentedSocketSentinel =
        new TaskSocketAsyncEventArgs<Socket>();

    private static readonly Int32TaskSocketAsyncEventArgs s_rentedInt32Sentinel = new Int32TaskSocketAsyncEventArgs();

    private static readonly Task<int> s_zeroTask = TaskExEx.FromResult(0);

    private CachedEventArgs _cachedTaskEventArgs;

    private DynamicWinsockMethods _dynamicWinsockMethods;

    [Obsolete(
        "SupportsIPv4 is obsoleted for this type, please use OSSupportsIPv4 instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static bool SupportsIPv4 => OSSupportsIPv4;

    [Obsolete(
        "SupportsIPv6 is obsoleted for this type, please use OSSupportsIPv6 instead. https://go.microsoft.com/fwlink/?linkid=14202")]
    public static bool SupportsIPv6 => OSSupportsIPv6;

    public static bool OSSupportsIPv4
    {
        get
        {
            InitializeSockets();
            return SocketProtocolSupportPal.OSSupportsIPv4;
        }
    }

    public static bool OSSupportsIPv6
    {
        get
        {
            InitializeSockets();
            return SocketProtocolSupportPal.OSSupportsIPv6;
        }
    }

    public int Available
    {
        get
        {
            if (CleanedUp)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            int available;
            SocketError available2 = SocketPal.GetAvailable(_handle, out available);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, $"Interop.Winsock.ioctlsocket returns errorCode:{available2}");
            }

            if (available2 != 0)
            {
                UpdateStatusAfterSocketErrorAndThrowException(available2);
            }

            return available;
        }
    }

    public EndPoint LocalEndPoint
    {
        get
        {
            if (CleanedUp)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (_nonBlockingConnectInProgress && Poll(0, SelectMode.SelectWrite))
            {
                _isConnected = true;
                _rightEndPoint = _nonBlockingConnectRightEndPoint;
                _nonBlockingConnectInProgress = false;
            }

            if (_rightEndPoint == null)
            {
                return null;
            }

            Internals.SocketAddress socketAddress = IPEndPointExtensions.Serialize(_rightEndPoint);
            SocketError sockName =
                SocketPal.GetSockName(_handle, socketAddress.Buffer, ref socketAddress.InternalSize);
            if (sockName != 0)
            {
                UpdateStatusAfterSocketErrorAndThrowException(sockName);
            }

            return _rightEndPoint.Create(socketAddress);
        }
    }

    public EndPoint RemoteEndPoint
    {
        get
        {
            if (CleanedUp)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (_remoteEndPoint == null)
            {
                if (_nonBlockingConnectInProgress && Poll(0, SelectMode.SelectWrite))
                {
                    _isConnected = true;
                    _rightEndPoint = _nonBlockingConnectRightEndPoint;
                    _nonBlockingConnectInProgress = false;
                }

                if (_rightEndPoint == null)
                {
                    return null;
                }

                Internals.SocketAddress socketAddress = IPEndPointExtensions.Serialize(_rightEndPoint);
                SocketError peerName =
                    SocketPal.GetPeerName(_handle, socketAddress.Buffer, ref socketAddress.InternalSize);
                if (peerName != 0)
                {
                    UpdateStatusAfterSocketErrorAndThrowException(peerName);
                }

                try
                {
                    _remoteEndPoint = _rightEndPoint.Create(socketAddress);
                }
                catch
                {
                }
            }

            return _remoteEndPoint;
        }
    }

    public IntPtr Handle
    {
        get
        {
            _handle.SetExposed();
            return _handle.DangerousGetHandle();
        }
    }

    public SafeSocketHandle SafeHandle => _handle;

    public bool Blocking
    {
        get { return _willBlock; }
        set
        {
            if (CleanedUp)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this,
                    $"value:{value} willBlock:{_willBlock} willBlockInternal:{_willBlockInternal}");
            }

            bool current;
            SocketError socketError = InternalSetBlocking(value, out current);
            if (socketError != 0)
            {
                UpdateStatusAfterSocketErrorAndThrowException(socketError);
            }

            _willBlock = current;
        }
    }

    public bool UseOnlyOverlappedIO
    {
        get { return false; }
        set { }
    }

    public bool Connected
    {
        get
        {
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, $"_isConnected:{_isConnected}");
            }

            if (_nonBlockingConnectInProgress && Poll(0, SelectMode.SelectWrite))
            {
                _isConnected = true;
                _rightEndPoint = _nonBlockingConnectRightEndPoint;
                _nonBlockingConnectInProgress = false;
            }

            return _isConnected;
        }
    }

    public AddressFamily AddressFamily => _addressFamily;

    public SocketType SocketType => _socketType;

    public ProtocolType ProtocolType => _protocolType;

    public bool IsBound => _rightEndPoint != null;

    public bool ExclusiveAddressUse
    {
        get
        {
            if ((int)GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse) == 0)
            {
                return false;
            }

            return true;
        }
        set
        {
            if (IsBound)
            {
                throw new InvalidOperationException(SR.net_sockets_mustnotbebound);
            }

            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, value ? 1 : 0);
        }
    }

    public int ReceiveBufferSize
    {
        get { return (int)GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer); }
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException("value");
            }

            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveBuffer, value);
        }
    }

    public int SendBufferSize
    {
        get { return (int)GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer); }
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException("value");
            }

            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, value);
        }
    }

    public int ReceiveTimeout
    {
        get { return (int)GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout); }
        set
        {
            if (value < -1)
            {
                throw new ArgumentOutOfRangeException("value");
            }

            if (value == -1)
            {
                value = 0;
            }

            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, value);
        }
    }

    public int SendTimeout
    {
        get { return (int)GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout); }
        set
        {
            if (value < -1)
            {
                throw new ArgumentOutOfRangeException("value");
            }

            if (value == -1)
            {
                value = 0;
            }

            SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, value);
        }
    }

    public LingerOption LingerState
    {
        get { return (LingerOption)GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger); }
        set { SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, value); }
    }

    public bool NoDelay
    {
        get
        {
            if ((int)GetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.Debug) == 0)
            {
                return false;
            }

            return true;
        }
        set { SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.Debug, value ? 1 : 0); }
    }

    public short Ttl
    {
        get
        {
            if (_addressFamily == AddressFamily.InterNetwork)
            {
                return (short)(int)GetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress);
            }

            if (_addressFamily == AddressFamily.InterNetworkV6)
            {
                return (short)(int)GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.ReuseAddress);
            }

            throw new NotSupportedException(SR.net_invalidversion);
        }
        set
        {
            if (value < 0 || value > 255)
            {
                throw new ArgumentOutOfRangeException("value");
            }

            if (_addressFamily == AddressFamily.InterNetwork)
            {
                SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, value);
                return;
            }

            if (_addressFamily == AddressFamily.InterNetworkV6)
            {
                SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.ReuseAddress, value);
                return;
            }

            throw new NotSupportedException(SR.net_invalidversion);
        }
    }

    public bool DontFragment
    {
        get
        {
            if (_addressFamily == AddressFamily.InterNetwork)
            {
                if ((int)GetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment) == 0)
                {
                    return false;
                }

                return true;
            }

            throw new NotSupportedException(SR.net_invalidversion);
        }
        set
        {
            if (_addressFamily == AddressFamily.InterNetwork)
            {
                SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DontFragment, value ? 1 : 0);
                return;
            }

            throw new NotSupportedException(SR.net_invalidversion);
        }
    }

    public bool MulticastLoopback
    {
        get
        {
            if (_addressFamily == AddressFamily.InterNetwork)
            {
                if ((int)GetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback) == 0)
                {
                    return false;
                }

                return true;
            }

            if (_addressFamily == AddressFamily.InterNetworkV6)
            {
                if ((int)GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback) == 0)
                {
                    return false;
                }

                return true;
            }

            throw new NotSupportedException(SR.net_invalidversion);
        }
        set
        {
            if (_addressFamily == AddressFamily.InterNetwork)
            {
                SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, value ? 1 : 0);
                return;
            }

            if (_addressFamily == AddressFamily.InterNetworkV6)
            {
                SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.MulticastLoopback, value ? 1 : 0);
                return;
            }

            throw new NotSupportedException(SR.net_invalidversion);
        }
    }

    public bool EnableBroadcast
    {
        get
        {
            if ((int)GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast) == 0)
            {
                return false;
            }

            return true;
        }
        set { SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, value ? 1 : 0); }
    }

    public bool DualMode
    {
        get
        {
            if (AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new NotSupportedException(SR.net_invalidversion);
            }

            return (int)GetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only) == 0;
        }
        set
        {
            if (AddressFamily != AddressFamily.InterNetworkV6)
            {
                throw new NotSupportedException(SR.net_invalidversion);
            }

            SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.IPv6Only, (!value) ? 1 : 0);
        }
    }

    private bool IsDualMode
    {
        get
        {
            if (AddressFamily == AddressFamily.InterNetworkV6)
            {
                return DualMode;
            }

            return false;
        }
    }

    private static object InternalSyncObject
    {
        get
        {
            if (s_internalSyncObject == null)
            {
                object value = new object();
                Interlocked.CompareExchange(ref s_internalSyncObject, value, null);
            }

            return s_internalSyncObject;
        }
    }

    private CacheSet Caches
    {
        get
        {
            if (_caches == null)
            {
                _caches = new CacheSet();
            }

            return _caches;
        }
    }

    internal bool CleanedUp => _intCleanedUp == 1;

    private static AsyncCallback CachedMultipleAddressConnectCallback
    {
        get
        {
            if (s_multipleAddressConnectCallback == null)
            {
                s_multipleAddressConnectCallback = MultipleAddressConnectCallback;
            }

            return s_multipleAddressConnectCallback;
        }
    }

    private CachedEventArgs EventArgs =>
        LazyInitializer.EnsureInitialized(ref _cachedTaskEventArgs, () => new CachedEventArgs());

    public Socket(SocketType socketType, ProtocolType protocolType)
        : this(OSSupportsIPv6 ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork, socketType,
            protocolType)
    {
        if (OSSupportsIPv6)
        {
            DualMode = true;
        }
    }

    public Socket(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, addressFamily, ".ctor");
        }

        InitializeSockets();
        SocketError socketError = SocketPal.CreateSocket(addressFamily, socketType, protocolType, out _handle);
        if (socketError != 0)
        {
            throw new SocketException((int)socketError);
        }

        _addressFamily = addressFamily;
        _socketType = socketType;
        _protocolType = protocolType;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, null, ".ctor");
        }
    }

    public Socket(SocketInformation socketInformation)
    {
        throw new PlatformNotSupportedException(SR.net_sockets_duplicateandclose_notsupported);
    }

    private Socket(SafeSocketHandle fd)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, null, ".ctor");
        }

        InitializeSockets();
        _handle = fd;
        _addressFamily = AddressFamily.Unknown;
        _socketType = SocketType.Unknown;
        _protocolType = ProtocolType.Unknown;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, null, ".ctor");
        }
    }

    internal bool CanTryAddressFamily(AddressFamily family)
    {
        if (family != _addressFamily)
        {
            if (family == AddressFamily.InterNetwork)
            {
                return IsDualMode;
            }

            return false;
        }

        return true;
    }

    public void Bind(EndPoint localEP)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, localEP);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (localEP == null)
        {
            throw new ArgumentNullException("localEP");
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"localEP:{localEP}");
        }

        EndPoint remoteEP = localEP;
        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref remoteEP);
        DoBind(remoteEP, socketAddress);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    private void DoBind(EndPoint endPointSnapshot, Internals.SocketAddress socketAddress)
    {
        IPEndPoint iPEndPoint = endPointSnapshot as IPEndPoint;
        if (!OSSupportsIPv4 && iPEndPoint != null && iPEndPoint.Address.IsIPv4MappedToIPv6)
        {
            UpdateStatusAfterSocketErrorAndThrowException(SocketError.InvalidArgument);
        }

        SocketError socketError =
            SocketPal.Bind(_handle, _protocolType, socketAddress.Buffer, socketAddress.Size);
        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        if (_rightEndPoint == null)
        {
            _rightEndPoint = endPointSnapshot;
        }
    }

    public void Connect(EndPoint remoteEP)
    {
        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        if (_isDisconnected)
        {
            throw new InvalidOperationException(SR.net_sockets_disconnectedConnect);
        }

        if (_isListening)
        {
            throw new InvalidOperationException(SR.net_sockets_mustnotlisten);
        }

        if (_isConnected)
        {
            throw new SocketException(10056);
        }

        ValidateBlockingMode();
        if (NetEventSource.IsEnabled && NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"DST:{remoteEP}");
        }

        if (remoteEP is DnsEndPoint dnsEndPoint)
        {
            if (dnsEndPoint.AddressFamily != 0 && !CanTryAddressFamily(dnsEndPoint.AddressFamily))
            {
                throw new NotSupportedException(SR.net_invalidversion);
            }

            Connect(dnsEndPoint.Host, dnsEndPoint.Port);
            return;
        }

        EndPoint remoteEP2 = remoteEP;
        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref remoteEP2);
        if (!Blocking)
        {
            _nonBlockingConnectRightEndPoint = remoteEP2;
            _nonBlockingConnectInProgress = true;
        }

        DoConnect(remoteEP2, socketAddress);
    }

    public void Connect(IPAddress address, int port)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, address);
        }

        if (CleanedUp)
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

        if (_isConnected)
        {
            throw new SocketException(10056);
        }

        if (!CanTryAddressFamily(address.AddressFamily))
        {
            throw new NotSupportedException(SR.net_invalidversion);
        }

        IPEndPoint remoteEP = new IPEndPoint(address, port);
        Connect(remoteEP);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void Connect(string host, int port)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, host);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (host == null)
        {
            throw new ArgumentNullException("host");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        if (_addressFamily != AddressFamily.InterNetwork && _addressFamily != AddressFamily.InterNetworkV6)
        {
            throw new NotSupportedException(SR.net_invalidversion);
        }

        if (IPAddress.TryParse(host, out var address))
        {
            Connect(address, port);
        }
        else
        {
            IPAddress[] hostAddresses = Dns.GetHostAddresses(host);
            Connect(hostAddresses, port);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void Connect(IPAddress[] addresses, int port)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, addresses);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (addresses == null)
        {
            throw new ArgumentNullException("addresses");
        }

        if (addresses.Length == 0)
        {
            throw new ArgumentException(SR.net_sockets_invalid_ipaddress_length, "addresses");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        if (_addressFamily != AddressFamily.InterNetwork && _addressFamily != AddressFamily.InterNetworkV6)
        {
            throw new NotSupportedException(SR.net_invalidversion);
        }

        if (_isConnected)
        {
            throw new SocketException(10056);
        }

        ExceptionDispatchInfo exceptionDispatchInfo = null;
        foreach (IPAddress iPAddress in addresses)
        {
            if (CanTryAddressFamily(iPAddress.AddressFamily))
            {
                try
                {
                    Connect(new IPEndPoint(iPAddress, port));
                    exceptionDispatchInfo = null;
                }
                catch (Exception ex) when (!ExceptionCheck.IsFatal(ex))
                {
                    exceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
                    continue;
                }

                break;
            }
        }

        exceptionDispatchInfo?.Throw();
        if (!Connected)
        {
            throw new ArgumentException(SR.net_invalidAddressList, "addresses");
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void Close()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
            NetEventSource.Info(this, $"timeout = {_closeTimeout}");
        }

        Dispose();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void Close(int timeout)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, timeout);
        }

        if (timeout < -1)
        {
            throw new ArgumentOutOfRangeException("timeout");
        }

        _closeTimeout = timeout;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"timeout = {_closeTimeout}");
        }

        Dispose();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, timeout);
        }
    }

    public void Listen(int backlog)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, backlog);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"backlog:{backlog}");
        }

        SocketError socketError = SocketPal.Listen(_handle, backlog);
        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        _isListening = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public Socket Accept()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (_rightEndPoint == null)
        {
            throw new InvalidOperationException(SR.net_sockets_mustbind);
        }

        if (!_isListening)
        {
            throw new InvalidOperationException(SR.net_sockets_mustlisten);
        }

        if (_isDisconnected)
        {
            throw new InvalidOperationException(SR.net_sockets_disconnectedAccept);
        }

        ValidateBlockingMode();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"SRC:{LocalEndPoint}");
        }

        Internals.SocketAddress socketAddress = IPEndPointExtensions.Serialize(_rightEndPoint);
        SafeSocketHandle socket;
        SocketError socketError =
            SocketPal.Accept(_handle, socketAddress.Buffer, ref socketAddress.InternalSize, out socket);
        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        Socket socket2 = CreateAcceptSocket(socket, _rightEndPoint.Create(socketAddress));
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Accepted(socket2, socket2.RemoteEndPoint, socket2.LocalEndPoint);
            NetEventSource.Exit(this, socket2);
        }

        return socket2;
    }

    public int Send(byte[] buffer, int size, SocketFlags socketFlags)
    {
        return Send(buffer, 0, size, socketFlags);
    }

    public int Send(byte[] buffer, SocketFlags socketFlags)
    {
        return Send(buffer, 0, (buffer != null) ? buffer.Length : 0, socketFlags);
    }

    public int Send(byte[] buffer)
    {
        return Send(buffer, 0, (buffer != null) ? buffer.Length : 0, SocketFlags.None);
    }

    public int Send(IList<ArraySegment<byte>> buffers)
    {
        return Send(buffers, SocketFlags.None);
    }

    public int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags)
    {
        SocketError errorCode;
        int result = Send(buffers, socketFlags, out errorCode);
        if (errorCode != 0)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public int Send(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags, out SocketError errorCode)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffers == null)
        {
            throw new ArgumentNullException("buffers");
        }

        if (buffers.Count == 0)
        {
            throw new ArgumentException(SR.Format(SR.net_sockets_zerolist, "buffers"), "buffers");
        }

        ValidateBlockingMode();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"SRC:{LocalEndPoint} DST:{RemoteEndPoint}");
        }

        errorCode = SocketPal.Send(_handle, buffers, socketFlags, out var bytesTransferred);
        if (errorCode != 0)
        {
            UpdateStatusAfterSocketError(errorCode);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, new SocketException((int)errorCode));
                NetEventSource.Exit(this, 0);
            }

            return 0;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, bytesTransferred);
        }

        return bytesTransferred;
    }

    public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        SocketError errorCode;
        int result = Send(buffer, offset, size, socketFlags, out errorCode);
        if (errorCode != 0)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public int Send(byte[] buffer, int offset, int size, SocketFlags socketFlags, out SocketError errorCode)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        errorCode = SocketError.Success;
        ValidateBlockingMode();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"SRC:{LocalEndPoint} DST:{RemoteEndPoint} size:{size}");
        }

        errorCode = SocketPal.Send(_handle, buffer, offset, size, socketFlags, out var bytesTransferred);
        if (errorCode != 0)
        {
            UpdateStatusAfterSocketError(errorCode);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, new SocketException((int)errorCode));
                NetEventSource.Exit(this, 0);
            }

            return 0;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.send returns:{bytesTransferred}");
            NetEventSource.DumpBuffer(this, buffer, offset, bytesTransferred);
            NetEventSource.Exit(this, bytesTransferred);
        }

        return bytesTransferred;
    }

    public int Send(ReadOnlySpan<byte> buffer)
    {
        return Send(buffer, SocketFlags.None);
    }

    public int Send(ReadOnlySpan<byte> buffer, SocketFlags socketFlags)
    {
        SocketError errorCode;
        int result = Send(buffer, socketFlags, out errorCode);
        if (errorCode != 0)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public int Send(ReadOnlySpan<byte> buffer, SocketFlags socketFlags, out SocketError errorCode)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        ValidateBlockingMode();
        errorCode = SocketPal.Send(_handle, buffer, socketFlags, out var bytesTransferred);
        if (errorCode != 0)
        {
            UpdateStatusAfterSocketError(errorCode);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, new SocketException((int)errorCode));
            }

            bytesTransferred = 0;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, bytesTransferred);
        }

        return bytesTransferred;
    }

    public void SendFile(string fileName)
    {
        SendFile(fileName, null, null, TransmitFileOptions.UseDefaultWorkerThread);
    }

    public void SendFile(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!Connected)
        {
            throw new NotSupportedException(SR.net_notconnected);
        }

        ValidateBlockingMode();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"::SendFile() SRC:{LocalEndPoint} DST:{RemoteEndPoint} fileName:{fileName}");
        }

        SendFileInternal(fileName, preBuffer, postBuffer, flags);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public int SendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags, EndPoint remoteEP)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        ValidateBlockingMode();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"SRC:{LocalEndPoint} size:{size} remoteEP:{remoteEP}");
        }

        EndPoint remoteEP2 = remoteEP;
        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref remoteEP2);
        int bytesTransferred;
        SocketError socketError = SocketPal.SendTo(_handle, buffer, offset, size, socketFlags,
            socketAddress.Buffer,
            socketAddress.Size, out bytesTransferred);
        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        if (_rightEndPoint == null)
        {
            _rightEndPoint = remoteEP2;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.DumpBuffer(this, buffer, offset, size);
            NetEventSource.Exit(this, bytesTransferred);
        }

        return bytesTransferred;
    }

    public int SendTo(byte[] buffer, int size, SocketFlags socketFlags, EndPoint remoteEP)
    {
        return SendTo(buffer, 0, size, socketFlags, remoteEP);
    }

    public int SendTo(byte[] buffer, SocketFlags socketFlags, EndPoint remoteEP)
    {
        return SendTo(buffer, 0, (buffer != null) ? buffer.Length : 0, socketFlags, remoteEP);
    }

    public int SendTo(byte[] buffer, EndPoint remoteEP)
    {
        return SendTo(buffer, 0, (buffer != null) ? buffer.Length : 0, SocketFlags.None, remoteEP);
    }

    public int Receive(byte[] buffer, int size, SocketFlags socketFlags)
    {
        return Receive(buffer, 0, size, socketFlags);
    }

    public int Receive(byte[] buffer, SocketFlags socketFlags)
    {
        return Receive(buffer, 0, (buffer != null) ? buffer.Length : 0, socketFlags);
    }

    public int Receive(byte[] buffer)
    {
        return Receive(buffer, 0, (buffer != null) ? buffer.Length : 0, SocketFlags.None);
    }

    public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags)
    {
        SocketError errorCode;
        int result = Receive(buffer, offset, size, socketFlags, out errorCode);
        if (errorCode != 0)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public int Receive(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        out SocketError errorCode)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        ValidateBlockingMode();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"SRC:{LocalEndPoint} DST:{RemoteEndPoint} size:{size}");
        }

        errorCode = SocketPal.Receive(_handle, buffer, offset, size, socketFlags, out var bytesTransferred);
        if (errorCode != 0)
        {
            UpdateStatusAfterSocketError(errorCode);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, new SocketException((int)errorCode));
                NetEventSource.Exit(this, 0);
            }

            return 0;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.DumpBuffer(this, buffer, offset, bytesTransferred);
            NetEventSource.Exit(this, bytesTransferred);
        }

        return bytesTransferred;
    }

    public int Receive(Span<byte> buffer)
    {
        return Receive(buffer, SocketFlags.None);
    }

    public int Receive(Span<byte> buffer, SocketFlags socketFlags)
    {
        SocketError errorCode;
        int result = Receive(buffer, socketFlags, out errorCode);
        if (errorCode != 0)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public int Receive(Span<byte> buffer, SocketFlags socketFlags, out SocketError errorCode)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        ValidateBlockingMode();
        errorCode = SocketPal.Receive(_handle, buffer, socketFlags, out var bytesTransferred);
        if (errorCode != 0)
        {
            UpdateStatusAfterSocketError(errorCode);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, new SocketException((int)errorCode));
            }

            bytesTransferred = 0;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, bytesTransferred);
        }

        return bytesTransferred;
    }

    public int Receive(IList<ArraySegment<byte>> buffers)
    {
        return Receive(buffers, SocketFlags.None);
    }

    public int Receive(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags)
    {
        SocketError errorCode;
        int result = Receive(buffers, socketFlags, out errorCode);
        if (errorCode != 0)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public int Receive(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags,
        out SocketError errorCode)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffers == null)
        {
            throw new ArgumentNullException("buffers");
        }

        if (buffers.Count == 0)
        {
            throw new ArgumentException(SR.Format(SR.net_sockets_zerolist, "buffers"), "buffers");
        }

        ValidateBlockingMode();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"SRC:{LocalEndPoint} DST:{RemoteEndPoint}");
        }

        errorCode = SocketPal.Receive(_handle, buffers, ref socketFlags, out var bytesTransferred);
        if (errorCode != 0)
        {
            UpdateStatusAfterSocketError(errorCode);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, new SocketException((int)errorCode));
                NetEventSource.Exit(this, 0);
            }

            return 0;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, bytesTransferred);
        }

        return bytesTransferred;
    }

    public int ReceiveMessageFrom(byte[] buffer, int offset, int size, ref SocketFlags socketFlags,
        ref EndPoint remoteEP, out IPPacketInformation ipPacketInformation)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        if (!CanTryAddressFamily(remoteEP.AddressFamily))
        {
            throw new ArgumentException(
                SR.Format(SR.net_InvalidEndPointAddressFamily, remoteEP.AddressFamily, _addressFamily),
                "remoteEP");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        if (_rightEndPoint == null)
        {
            throw new InvalidOperationException(SR.net_sockets_mustbind);
        }

        SocketPal.CheckDualModeReceiveSupport(this);
        ValidateBlockingMode();
        EndPoint remoteEP2 = remoteEP;
        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref remoteEP2);
        Internals.SocketAddress socketAddress2 = IPEndPointExtensions.Serialize(remoteEP2);
        SetReceivingPacketInformation();
        Internals.SocketAddress receiveAddress;
        int bytesTransferred;
        SocketError socketError = SocketPal.ReceiveMessageFrom(this, _handle, buffer, offset, size,
            ref socketFlags,
            socketAddress, out receiveAddress, out ipPacketInformation, out bytesTransferred);
        if (socketError != 0 && socketError != SocketError.MessageSize)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        if (!socketAddress2.Equals(receiveAddress))
        {
            try
            {
                remoteEP = remoteEP2.Create(receiveAddress);
            }
            catch
            {
            }

            if (_rightEndPoint == null)
            {
                _rightEndPoint = remoteEP2;
            }
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Error(this, socketError);
        }

        return bytesTransferred;
    }

    public int ReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags, ref EndPoint remoteEP)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        if (!CanTryAddressFamily(remoteEP.AddressFamily))
        {
            throw new ArgumentException(
                SR.Format(SR.net_InvalidEndPointAddressFamily, remoteEP.AddressFamily, _addressFamily),
                "remoteEP");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        if (_rightEndPoint == null)
        {
            throw new InvalidOperationException(SR.net_sockets_mustbind);
        }

        SocketPal.CheckDualModeReceiveSupport(this);
        ValidateBlockingMode();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"SRC{LocalEndPoint} size:{size} remoteEP:{remoteEP}");
        }

        EndPoint remoteEP2 = remoteEP;
        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref remoteEP2);
        Internals.SocketAddress socketAddress2 = IPEndPointExtensions.Serialize(remoteEP2);
        int bytesTransferred;
        SocketError socketError = SocketPal.ReceiveFrom(_handle, buffer, offset, size, socketFlags,
            socketAddress.Buffer, ref socketAddress.InternalSize, out bytesTransferred);
        SocketException ex = null;
        if (socketError != 0)
        {
            ex = new SocketException((int)socketError);
            UpdateStatusAfterSocketError(ex);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, ex);
            }

            if (ex.SocketErrorCode != SocketError.MessageSize)
            {
                throw ex;
            }
        }

        if (!socketAddress2.Equals(socketAddress))
        {
            try
            {
                remoteEP = remoteEP2.Create(socketAddress);
            }
            catch
            {
            }

            if (_rightEndPoint == null)
            {
                _rightEndPoint = remoteEP2;
            }
        }

        if (ex != null)
        {
            throw ex;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.DumpBuffer(this, buffer, offset, size);
            NetEventSource.Exit(this, bytesTransferred);
        }

        return bytesTransferred;
    }

    public int ReceiveFrom(byte[] buffer, int size, SocketFlags socketFlags, ref EndPoint remoteEP)
    {
        return ReceiveFrom(buffer, 0, size, socketFlags, ref remoteEP);
    }

    public int ReceiveFrom(byte[] buffer, SocketFlags socketFlags, ref EndPoint remoteEP)
    {
        return ReceiveFrom(buffer, 0, (buffer != null) ? buffer.Length : 0, socketFlags, ref remoteEP);
    }

    public int ReceiveFrom(byte[] buffer, ref EndPoint remoteEP)
    {
        return ReceiveFrom(buffer, 0, (buffer != null) ? buffer.Length : 0, SocketFlags.None, ref remoteEP);
    }

    public int IOControl(int ioControlCode, byte[] optionInValue, byte[] optionOutValue)
    {
        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        int optionLength = 0;
        SocketError socketError =
            SocketPal.WindowsIoctl(_handle, ioControlCode, optionInValue, optionOutValue, out optionLength);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.WSAIoctl returns errorCode:{socketError}");
        }

        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        return optionLength;
    }

    public int IOControl(IOControlCode ioControlCode, byte[] optionInValue, byte[] optionOutValue)
    {
        return IOControl((int)ioControlCode, optionInValue, optionOutValue);
    }

    public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue)
    {
        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"optionLevel:{optionLevel} optionName:{optionName} optionValue:{optionValue}");
        }

        SetSocketOption(optionLevel, optionName, optionValue, silent: false);
    }

    public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
    {
        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"optionLevel:{optionLevel} optionName:{optionName} optionValue:{optionValue}");
        }

        SocketError socketError = SocketPal.SetSockOpt(_handle, optionLevel, optionName, optionValue);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.setsockopt returns errorCode:{socketError}");
        }

        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }
    }

    public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, bool optionValue)
    {
        SetSocketOption(optionLevel, optionName, optionValue ? 1 : 0);
    }

    public void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, object optionValue)
    {
        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (optionValue == null)
        {
            throw new ArgumentNullException("optionValue");
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"optionLevel:{optionLevel} optionName:{optionName} optionValue:{optionValue}");
        }

        if (optionLevel == SocketOptionLevel.Socket && optionName == SocketOptionName.Linger)
        {
            if (!(optionValue is LingerOption lingerOption))
            {
                throw new ArgumentException(SR.Format(SR.net_sockets_invalid_optionValue, "LingerOption"),
                    "optionValue");
            }

            if (lingerOption.LingerTime < 0 || lingerOption.LingerTime > 65535)
            {
                throw new ArgumentException(SR.Format(SR.ArgumentOutOfRange_Bounds_Lower_Upper, 0, 65535),
                    "optionValue.LingerTime");
            }

            SetLingerOption(lingerOption);
        }
        else if (optionLevel == SocketOptionLevel.IP && (optionName == SocketOptionName.AddMembership ||
                                                         optionName == SocketOptionName.DropMembership))
        {
            if (!(optionValue is MulticastOption mR))
            {
                throw new ArgumentException(
                    SR.Format(SR.net_sockets_invalid_optionValue, "MulticastOption"), "optionValue");
            }

            SetMulticastOption(optionName, mR);
        }
        else
        {
            if (optionLevel != SocketOptionLevel.IPv6 || (optionName != SocketOptionName.AddMembership &&
                                                          optionName != SocketOptionName.DropMembership))
            {
                throw new ArgumentException(SR.net_sockets_invalid_optionValue_all, "optionValue");
            }

            if (!(optionValue is IPv6MulticastOption mR2))
            {
                throw new ArgumentException(
                    SR.Format(SR.net_sockets_invalid_optionValue, "IPv6MulticastOption"), "optionValue");
            }

            SetIPv6MulticastOption(optionName, mR2);
        }
    }

    public object GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName)
    {
        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (optionLevel == SocketOptionLevel.Socket && optionName == SocketOptionName.Linger)
        {
            return GetLingerOpt();
        }

        if (optionLevel == SocketOptionLevel.IP && (optionName == SocketOptionName.AddMembership ||
                                                    optionName == SocketOptionName.DropMembership))
        {
            return GetMulticastOpt(optionName);
        }

        if (optionLevel == SocketOptionLevel.IPv6 && (optionName == SocketOptionName.AddMembership ||
                                                      optionName == SocketOptionName.DropMembership))
        {
            return GetIPv6MulticastOpt(optionName);
        }

        int optionValue = 0;
        SocketError sockOpt = SocketPal.GetSockOpt(_handle, optionLevel, optionName, out optionValue);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.getsockopt returns errorCode:{sockOpt}");
        }

        if (sockOpt != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(sockOpt);
        }

        return optionValue;
    }

    public void GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, byte[] optionValue)
    {
        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        int optionLength = ((optionValue != null) ? optionValue.Length : 0);
        SocketError sockOpt =
            SocketPal.GetSockOpt(_handle, optionLevel, optionName, optionValue, ref optionLength);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.getsockopt returns errorCode:{sockOpt}");
        }

        if (sockOpt != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(sockOpt);
        }
    }

    public byte[] GetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionLength)
    {
        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        byte[] array = new byte[optionLength];
        int optionLength2 = optionLength;
        SocketError sockOpt = SocketPal.GetSockOpt(_handle, optionLevel, optionName, array, ref optionLength2);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.getsockopt returns errorCode:{sockOpt}");
        }

        if (sockOpt != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(sockOpt);
        }

        if (optionLength != optionLength2)
        {
            byte[] array2 = new byte[optionLength2];
            Buffer.BlockCopy(array, 0, array2, 0, optionLength2);
            array = array2;
        }

        return array;
    }

    public void SetIPProtectionLevel(IPProtectionLevel level)
    {
        if (level == IPProtectionLevel.Unspecified)
        {
            throw new ArgumentException(SR.net_sockets_invalid_optionValue_all, "level");
        }

        if (_addressFamily == AddressFamily.InterNetworkV6)
        {
            SocketPal.SetIPProtectionLevel(this, SocketOptionLevel.IPv6, (int)level);
            return;
        }

        if (_addressFamily == AddressFamily.InterNetwork)
        {
            SocketPal.SetIPProtectionLevel(this, SocketOptionLevel.IP, (int)level);
            return;
        }

        throw new NotSupportedException(SR.net_invalidversion);
    }

    public bool Poll(int microSeconds, SelectMode mode)
    {
        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        bool status;
        SocketError socketError = SocketPal.Poll(_handle, microSeconds, mode, out status);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.select returns socketCount:{(int)socketError}");
        }

        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        return status;
    }

    public static void Select(IList checkRead, IList checkWrite, IList checkError, int microSeconds)
    {
        if ((checkRead == null || checkRead.Count == 0) && (checkWrite == null || checkWrite.Count == 0) &&
            (checkError == null || checkError.Count == 0))
        {
            throw new ArgumentNullException(null, SR.net_sockets_empty_select);
        }

        if (checkRead != null && checkRead.Count > 65536)
        {
            throw new ArgumentOutOfRangeException("checkRead",
                SR.Format(SR.net_sockets_toolarge_select, "checkRead", 65536.ToString()));
        }

        if (checkWrite != null && checkWrite.Count > 65536)
        {
            throw new ArgumentOutOfRangeException("checkWrite",
                SR.Format(SR.net_sockets_toolarge_select, "checkWrite", 65536.ToString()));
        }

        if (checkError != null && checkError.Count > 65536)
        {
            throw new ArgumentOutOfRangeException("checkError",
                SR.Format(SR.net_sockets_toolarge_select, "checkError", 65536.ToString()));
        }

        SocketError socketError = SocketPal.Select(checkRead, checkWrite, checkError, microSeconds);
        if (socketError != 0)
        {
            throw new SocketException((int)socketError);
        }
    }

    public IAsyncResult BeginConnect(EndPoint remoteEP, AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, remoteEP);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        if (_isListening)
        {
            throw new InvalidOperationException(SR.net_sockets_mustnotlisten);
        }

        if (_isConnected)
        {
            throw new SocketException(10056);
        }

        if (remoteEP is DnsEndPoint dnsEndPoint)
        {
            if (dnsEndPoint.AddressFamily != 0 && !CanTryAddressFamily(dnsEndPoint.AddressFamily))
            {
                throw new NotSupportedException(SR.net_invalidversion);
            }

            return BeginConnect(dnsEndPoint.Host, dnsEndPoint.Port, callback, state);
        }

        return UnsafeBeginConnect(remoteEP, callback, state, flowContext: true);
    }

    private bool CanUseConnectEx(EndPoint remoteEP)
    {
        if (_socketType == SocketType.Stream)
        {
            if (_rightEndPoint == null)
            {
                return remoteEP.GetType() == typeof(IPEndPoint);
            }

            return true;
        }

        return false;
    }

    public SocketInformation DuplicateAndClose(int targetProcessId)
    {
        throw new PlatformNotSupportedException(SR.net_sockets_duplicateandclose_notsupported);
    }

    internal IAsyncResult UnsafeBeginConnect(EndPoint remoteEP, AsyncCallback callback, object state,
        bool flowContext = false)
    {
        if (CanUseConnectEx(remoteEP))
        {
            return BeginConnectEx(remoteEP, flowContext, callback, state);
        }

        ConnectAsyncResult connectAsyncResult = new ConnectAsyncResult(this, remoteEP, state, callback);
        Connect(remoteEP);
        connectAsyncResult.FinishPostingAsyncOp();
        connectAsyncResult.InvokeCallback();
        return connectAsyncResult;
    }

    public IAsyncResult BeginConnect(string host, int port, AsyncCallback requestCallback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, host);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (host == null)
        {
            throw new ArgumentNullException("host");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        if (_addressFamily != AddressFamily.InterNetwork && _addressFamily != AddressFamily.InterNetworkV6)
        {
            throw new NotSupportedException(SR.net_invalidversion);
        }

        if (_isListening)
        {
            throw new InvalidOperationException(SR.net_sockets_mustnotlisten);
        }

        if (_isConnected)
        {
            throw new SocketException(10056);
        }

        if (IPAddress.TryParse(host, out var address))
        {
            IAsyncResult asyncResult = BeginConnect(address, port, requestCallback, state);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Exit(this, asyncResult);
            }

            return asyncResult;
        }

        MultipleAddressConnectAsyncResult multipleAddressConnectAsyncResult =
            new MultipleAddressConnectAsyncResult(null, port, this, state, requestCallback);
        multipleAddressConnectAsyncResult.StartPostingAsyncOp(lockCapture: false);
        IAsyncResult asyncResult2 = Dns.BeginGetHostAddresses(host, DnsCallback, multipleAddressConnectAsyncResult);
        if (asyncResult2.CompletedSynchronously && DoDnsCallback(asyncResult2, multipleAddressConnectAsyncResult))
        {
            multipleAddressConnectAsyncResult.InvokeCallback();
        }

        multipleAddressConnectAsyncResult.FinishPostingAsyncOp(ref Caches.ConnectClosureCache);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, multipleAddressConnectAsyncResult);
        }

        return multipleAddressConnectAsyncResult;
    }

    public IAsyncResult BeginConnect(IPAddress address, int port, AsyncCallback requestCallback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, address);
        }

        if (CleanedUp)
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

        if (_isConnected)
        {
            throw new SocketException(10056);
        }

        if (!CanTryAddressFamily(address.AddressFamily))
        {
            throw new NotSupportedException(SR.net_invalidversion);
        }

        IAsyncResult asyncResult = BeginConnect(new IPEndPoint(address, port), requestCallback, state);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, asyncResult);
        }

        return asyncResult;
    }

    public IAsyncResult BeginConnect(IPAddress[] addresses, int port, AsyncCallback requestCallback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, addresses);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (addresses == null)
        {
            throw new ArgumentNullException("addresses");
        }

        if (addresses.Length == 0)
        {
            throw new ArgumentException(SR.net_invalidAddressList, "addresses");
        }

        if (!TcpValidationHelpers.ValidatePortNumber(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }

        if (_addressFamily != AddressFamily.InterNetwork && _addressFamily != AddressFamily.InterNetworkV6)
        {
            throw new NotSupportedException(SR.net_invalidversion);
        }

        if (_isListening)
        {
            throw new InvalidOperationException(SR.net_sockets_mustnotlisten);
        }

        if (_isConnected)
        {
            throw new SocketException(10056);
        }

        MultipleAddressConnectAsyncResult multipleAddressConnectAsyncResult =
            new MultipleAddressConnectAsyncResult(addresses, port, this, state, requestCallback);
        multipleAddressConnectAsyncResult.StartPostingAsyncOp(lockCapture: false);
        if (DoMultipleAddressConnectCallback(PostOneBeginConnect(multipleAddressConnectAsyncResult),
                multipleAddressConnectAsyncResult))
        {
            multipleAddressConnectAsyncResult.InvokeCallback();
        }

        multipleAddressConnectAsyncResult.FinishPostingAsyncOp(ref Caches.ConnectClosureCache);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, multipleAddressConnectAsyncResult);
        }

        return multipleAddressConnectAsyncResult;
    }

    public IAsyncResult BeginDisconnect(bool reuseSocket, AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        DisconnectOverlappedAsyncResult disconnectOverlappedAsyncResult =
            new DisconnectOverlappedAsyncResult(this, state, callback);
        disconnectOverlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        DoBeginDisconnect(reuseSocket, disconnectOverlappedAsyncResult);
        disconnectOverlappedAsyncResult.FinishPostingAsyncOp();
        return disconnectOverlappedAsyncResult;
    }

    private void DoBeginDisconnect(bool reuseSocket, DisconnectOverlappedAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        SocketError socketError = SocketError.Success;
        socketError = SocketPal.DisconnectAsync(this, _handle, reuseSocket, asyncResult);
        if (socketError == SocketError.Success)
        {
            SetToDisconnected();
            _remoteEndPoint = null;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"UnsafeNclNativeMethods.OSSOCK.DisConnectEx returns:{socketError}");
        }

        if (!CheckErrorAndUpdateStatus(socketError))
        {
            throw new SocketException((int)socketError);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, asyncResult);
        }
    }

    public void Disconnect(bool reuseSocket)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        SocketError socketError = SocketError.Success;
        socketError = SocketPal.Disconnect(this, _handle, reuseSocket);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"UnsafeNclNativeMethods.OSSOCK.DisConnectEx returns:{socketError}");
        }

        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        SetToDisconnected();
        _remoteEndPoint = null;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public void EndConnect(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        ContextAwareResult contextAwareResult =
            (ContextAwareResult)((asyncResult as ConnectOverlappedAsyncResult) ??
                                 ((object)(asyncResult as MultipleAddressConnectAsyncResult)) ??
                                 asyncResult as ConnectAsyncResult);
        if (contextAwareResult == null || contextAwareResult.AsyncObject != this)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (contextAwareResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndConnect"));
        }

        contextAwareResult.InternalWaitForCompletion();
        contextAwareResult.EndCalled = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"asyncResult:{asyncResult}");
        }

        Exception ex = contextAwareResult.Result as Exception;
        if (ex != null || contextAwareResult.ErrorCode != 0)
        {
            if (ex == null)
            {
                SocketException ex2 =
                    SocketExceptionFactory.CreateSocketException(contextAwareResult.ErrorCode,
                        contextAwareResult.RemoteEndPoint);
                UpdateStatusAfterSocketError(ex2);
                ex = ex2;
            }

            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, ex);
            }

            ExceptionDispatchInfo.Throw(ex);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Connected(this, LocalEndPoint, RemoteEndPoint);
            NetEventSource.Exit(this, "");
        }
    }

    public void EndDisconnect(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        if (!(asyncResult is LazyAsyncResult lazyAsyncResult) || lazyAsyncResult.AsyncObject != this)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (lazyAsyncResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndDisconnect"));
        }

        lazyAsyncResult.InternalWaitForCompletion();
        lazyAsyncResult.EndCalled = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this);
        }

        if (lazyAsyncResult.ErrorCode != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException((SocketError)lazyAsyncResult.ErrorCode);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        AsyncCallback callback,
        object state)
    {
        SocketError errorCode;
        IAsyncResult result = BeginSend(buffer, offset, size, socketFlags, out errorCode, callback, state);
        if (errorCode != 0 && errorCode != SocketError.IOPending)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public IAsyncResult BeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        out SocketError errorCode, AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        OverlappedAsyncResult overlappedAsyncResult = new OverlappedAsyncResult(this, state, callback);
        overlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        errorCode = DoBeginSend(buffer, offset, size, socketFlags, overlappedAsyncResult);
        if (errorCode != 0 && errorCode != SocketError.IOPending)
        {
            overlappedAsyncResult = null;
        }
        else
        {
            overlappedAsyncResult.FinishPostingAsyncOp(ref Caches.SendClosureCache);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, overlappedAsyncResult);
        }

        return overlappedAsyncResult;
    }

    private SocketError DoBeginSend(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        OverlappedAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"SRC:{LocalEndPoint} DST:{RemoteEndPoint} size:{size}");
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"asyncResult:{asyncResult} size:{size}");
        }

        SocketError socketError = SocketPal.SendAsync(_handle, buffer, offset, size, socketFlags, asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"Interop.Winsock.WSASend returns:{socketError} size:{size} returning AsyncResult:{asyncResult}");
        }

        CheckErrorAndUpdateStatus(socketError);
        return socketError;
    }

    public IAsyncResult BeginSend(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags,
        AsyncCallback callback,
        object state)
    {
        SocketError errorCode;
        IAsyncResult result = BeginSend(buffers, socketFlags, out errorCode, callback, state);
        if (errorCode != 0 && errorCode != SocketError.IOPending)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public IAsyncResult BeginSend(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags,
        out SocketError errorCode,
        AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffers == null)
        {
            throw new ArgumentNullException("buffers");
        }

        if (buffers.Count == 0)
        {
            throw new ArgumentException(SR.Format(SR.net_sockets_zerolist, "buffers"), "buffers");
        }

        OverlappedAsyncResult overlappedAsyncResult = new OverlappedAsyncResult(this, state, callback);
        overlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        errorCode = DoBeginSend(buffers, socketFlags, overlappedAsyncResult);
        overlappedAsyncResult.FinishPostingAsyncOp(ref Caches.SendClosureCache);
        if (errorCode != 0 && errorCode != SocketError.IOPending)
        {
            overlappedAsyncResult = null;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, overlappedAsyncResult);
        }

        return overlappedAsyncResult;
    }

    private SocketError DoBeginSend(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags,
        OverlappedAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"SRC:{LocalEndPoint} DST:{RemoteEndPoint} buffers:{buffers}");
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"asyncResult:{asyncResult}");
        }

        SocketError socketError = SocketPal.SendAsync(_handle, buffers, socketFlags, asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"Interop.Winsock.WSASend returns:{socketError} returning AsyncResult:{asyncResult}");
        }

        CheckErrorAndUpdateStatus(socketError);
        return socketError;
    }

    public int EndSend(IAsyncResult asyncResult)
    {
        SocketError errorCode;
        int result = EndSend(asyncResult, out errorCode);
        if (errorCode != 0)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public int EndSend(IAsyncResult asyncResult, out SocketError errorCode)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        if (!(asyncResult is OverlappedAsyncResult overlappedAsyncResult) || overlappedAsyncResult.AsyncObject != this)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (overlappedAsyncResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndSend"));
        }

        int num = overlappedAsyncResult.InternalWaitForCompletionInt32Result();
        overlappedAsyncResult.EndCalled = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"bytesTransffered:{num}");
        }

        errorCode = (SocketError)overlappedAsyncResult.ErrorCode;
        if (errorCode != 0)
        {
            UpdateStatusAfterSocketError(errorCode);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, new SocketException((int)errorCode));
                NetEventSource.Exit(this, 0);
            }

            return 0;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, num);
        }

        return num;
    }

    public IAsyncResult BeginSendFile(string fileName, AsyncCallback callback, object state)
    {
        return BeginSendFile(fileName, null, null, TransmitFileOptions.UseDefaultWorkerThread, callback, state);
    }

    public IAsyncResult BeginSendFile(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags,
        AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!Connected)
        {
            throw new NotSupportedException(SR.net_notconnected);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"::DoBeginSendFile() SRC:{LocalEndPoint} DST:{RemoteEndPoint} fileName:{fileName}");
        }

        IAsyncResult asyncResult = BeginSendFileInternal(fileName, preBuffer, postBuffer, flags, callback, state);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, asyncResult);
        }

        return asyncResult;
    }

    public void EndSendFile(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        EndSendFileInternal(asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public IAsyncResult BeginSendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        EndPoint remoteEP,
        AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        EndPoint remoteEP2 = remoteEP;
        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref remoteEP2);
        OverlappedAsyncResult overlappedAsyncResult = new OverlappedAsyncResult(this, state, callback);
        overlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        DoBeginSendTo(buffer, offset, size, socketFlags, remoteEP2, socketAddress, overlappedAsyncResult);
        overlappedAsyncResult.FinishPostingAsyncOp(ref Caches.SendClosureCache);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, overlappedAsyncResult);
        }

        return overlappedAsyncResult;
    }

    private void DoBeginSendTo(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        EndPoint endPointSnapshot,
        Internals.SocketAddress socketAddress, OverlappedAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"size:{size}");
        }

        EndPoint rightEndPoint = _rightEndPoint;
        SocketError socketError = SocketError.SocketError;
        try
        {
            if (_rightEndPoint == null)
            {
                _rightEndPoint = endPointSnapshot;
            }

            socketError = SocketPal.SendToAsync(_handle, buffer, offset, size, socketFlags, socketAddress, asyncResult);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this,
                    $"Interop.Winsock.WSASend returns:{socketError} size:{size} returning AsyncResult:{asyncResult}");
            }
        }
        catch (ObjectDisposedException)
        {
            _rightEndPoint = rightEndPoint;
            throw;
        }

        if (!CheckErrorAndUpdateStatus(socketError))
        {
            _rightEndPoint = rightEndPoint;
            throw new SocketException((int)socketError);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"size:{size} returning AsyncResult:{asyncResult}");
        }
    }

    public int EndSendTo(IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        if (!(asyncResult is OverlappedAsyncResult overlappedAsyncResult) || overlappedAsyncResult.AsyncObject != this)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (overlappedAsyncResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndSendTo"));
        }

        int num = overlappedAsyncResult.InternalWaitForCompletionInt32Result();
        overlappedAsyncResult.EndCalled = true;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"bytesTransferred:{num}");
        }

        if (overlappedAsyncResult.ErrorCode != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException((SocketError)overlappedAsyncResult.ErrorCode);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, num);
        }

        return num;
    }

    public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        AsyncCallback callback, object state)
    {
        SocketError errorCode;
        IAsyncResult result = BeginReceive(buffer, offset, size, socketFlags, out errorCode, callback, state);
        if (errorCode != 0 && errorCode != SocketError.IOPending)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public IAsyncResult BeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        out SocketError errorCode, AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        OverlappedAsyncResult overlappedAsyncResult = new OverlappedAsyncResult(this, state, callback);
        overlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        errorCode = DoBeginReceive(buffer, offset, size, socketFlags, overlappedAsyncResult);
        if (errorCode != 0 && errorCode != SocketError.IOPending)
        {
            overlappedAsyncResult = null;
        }
        else
        {
            overlappedAsyncResult.FinishPostingAsyncOp(ref Caches.ReceiveClosureCache);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, overlappedAsyncResult);
        }

        return overlappedAsyncResult;
    }

    private SocketError DoBeginReceive(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        OverlappedAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"size:{size}");
        }

        SocketError socketError = SocketPal.ReceiveAsync(_handle, buffer, offset, size, socketFlags, asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"Interop.Winsock.WSARecv returns:{socketError} returning AsyncResult:{asyncResult}");
        }

        CheckErrorAndUpdateStatus(socketError);
        return socketError;
    }

    public IAsyncResult BeginReceive(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags,
        AsyncCallback callback,
        object state)
    {
        SocketError errorCode;
        IAsyncResult result = BeginReceive(buffers, socketFlags, out errorCode, callback, state);
        if (errorCode != 0 && errorCode != SocketError.IOPending)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public IAsyncResult BeginReceive(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags,
        out SocketError errorCode, AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffers == null)
        {
            throw new ArgumentNullException("buffers");
        }

        if (buffers.Count == 0)
        {
            throw new ArgumentException(SR.Format(SR.net_sockets_zerolist, "buffers"), "buffers");
        }

        OverlappedAsyncResult overlappedAsyncResult = new OverlappedAsyncResult(this, state, callback);
        overlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        errorCode = DoBeginReceive(buffers, socketFlags, overlappedAsyncResult);
        if (errorCode != 0 && errorCode != SocketError.IOPending)
        {
            overlappedAsyncResult = null;
        }
        else
        {
            overlappedAsyncResult.FinishPostingAsyncOp(ref Caches.ReceiveClosureCache);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, overlappedAsyncResult);
        }

        return overlappedAsyncResult;
    }

    private SocketError DoBeginReceive(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags,
        OverlappedAsyncResult asyncResult)
    {
        SocketError socketError = SocketPal.ReceiveAsync(_handle, buffers, socketFlags, asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"Interop.Winsock.WSARecv returns:{socketError} returning AsyncResult:{asyncResult}");
        }

        CheckErrorAndUpdateStatus(socketError);
        return socketError;
    }

    public int EndReceive(IAsyncResult asyncResult)
    {
        SocketError errorCode;
        int result = EndReceive(asyncResult, out errorCode);
        if (errorCode != 0)
        {
            throw new SocketException((int)errorCode);
        }

        return result;
    }

    public int EndReceive(IAsyncResult asyncResult, out SocketError errorCode)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        if (!(asyncResult is OverlappedAsyncResult overlappedAsyncResult) || overlappedAsyncResult.AsyncObject != this)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (overlappedAsyncResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndReceive"));
        }

        int num = overlappedAsyncResult.InternalWaitForCompletionInt32Result();
        overlappedAsyncResult.EndCalled = true;
        errorCode = (SocketError)overlappedAsyncResult.ErrorCode;
        if (errorCode != 0)
        {
            UpdateStatusAfterSocketError(errorCode);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, new SocketException((int)errorCode));
                NetEventSource.Exit(this, 0);
            }

            return 0;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, num);
        }

        return num;
    }

    public IAsyncResult BeginReceiveMessageFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        ref EndPoint remoteEP, AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
            NetEventSource.Info(this, $"size:{size}");
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        if (!CanTryAddressFamily(remoteEP.AddressFamily))
        {
            throw new ArgumentException(
                SR.Format(SR.net_InvalidEndPointAddressFamily, remoteEP.AddressFamily, _addressFamily),
                "remoteEP");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        if (_rightEndPoint == null)
        {
            throw new InvalidOperationException(SR.net_sockets_mustbind);
        }

        SocketPal.CheckDualModeReceiveSupport(this);
        ReceiveMessageOverlappedAsyncResult receiveMessageOverlappedAsyncResult =
            new ReceiveMessageOverlappedAsyncResult(this, state, callback);
        receiveMessageOverlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        EndPoint rightEndPoint = _rightEndPoint;
        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref remoteEP);
        SocketError socketError = SocketError.SocketError;
        try
        {
            receiveMessageOverlappedAsyncResult.SocketAddressOriginal = IPEndPointExtensions.Serialize(remoteEP);
            SetReceivingPacketInformation();
            if (_rightEndPoint == null)
            {
                _rightEndPoint = remoteEP;
            }

            socketError = SocketPal.ReceiveMessageFromAsync(this, _handle, buffer, offset, size, socketFlags,
                socketAddress, receiveMessageOverlappedAsyncResult);
            if (socketError != 0 && socketError == SocketError.MessageSize)
            {
                NetEventSource.Fail(this, "Returned WSAEMSGSIZE!");
                socketError = SocketError.IOPending;
            }

            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this,
                    $"Interop.Winsock.WSARecvMsg returns:{socketError} size:{size} returning AsyncResult:{receiveMessageOverlappedAsyncResult}");
            }
        }
        catch (ObjectDisposedException)
        {
            _rightEndPoint = rightEndPoint;
            throw;
        }

        if (!CheckErrorAndUpdateStatus(socketError))
        {
            _rightEndPoint = rightEndPoint;
            throw new SocketException((int)socketError);
        }

        receiveMessageOverlappedAsyncResult.FinishPostingAsyncOp(ref Caches.ReceiveClosureCache);
        if (receiveMessageOverlappedAsyncResult.CompletedSynchronously &&
            !receiveMessageOverlappedAsyncResult.SocketAddressOriginal.Equals(receiveMessageOverlappedAsyncResult
                .SocketAddress))
        {
            try
            {
                remoteEP = remoteEP.Create(receiveMessageOverlappedAsyncResult.SocketAddress);
            }
            catch
            {
            }
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"size:{size} returning AsyncResult:{receiveMessageOverlappedAsyncResult}");
            NetEventSource.Exit(this, receiveMessageOverlappedAsyncResult);
        }

        return receiveMessageOverlappedAsyncResult;
    }

    public int EndReceiveMessageFrom(IAsyncResult asyncResult, ref SocketFlags socketFlags, ref EndPoint endPoint,
        out IPPacketInformation ipPacketInformation)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (endPoint == null)
        {
            throw new ArgumentNullException("endPoint");
        }

        if (!CanTryAddressFamily(endPoint.AddressFamily))
        {
            throw new ArgumentException(
                SR.Format(SR.net_InvalidEndPointAddressFamily, endPoint.AddressFamily, _addressFamily),
                "endPoint");
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        if (!(asyncResult is ReceiveMessageOverlappedAsyncResult receiveMessageOverlappedAsyncResult) ||
            receiveMessageOverlappedAsyncResult.AsyncObject != this)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (receiveMessageOverlappedAsyncResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall,
                "EndReceiveMessageFrom"));
        }

        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref endPoint);
        int num = receiveMessageOverlappedAsyncResult.InternalWaitForCompletionInt32Result();
        receiveMessageOverlappedAsyncResult.EndCalled = true;
        receiveMessageOverlappedAsyncResult.SocketAddress.InternalSize =
            receiveMessageOverlappedAsyncResult.GetSocketAddressSize();
        if (!socketAddress.Equals(receiveMessageOverlappedAsyncResult.SocketAddress))
        {
            try
            {
                endPoint = endPoint.Create(receiveMessageOverlappedAsyncResult.SocketAddress);
            }
            catch
            {
            }
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"bytesTransferred:{num}");
        }

        if (receiveMessageOverlappedAsyncResult.ErrorCode != 0 &&
            receiveMessageOverlappedAsyncResult.ErrorCode != 10040)
        {
            UpdateStatusAfterSocketErrorAndThrowException(
                (SocketError)receiveMessageOverlappedAsyncResult.ErrorCode);
        }

        socketFlags = receiveMessageOverlappedAsyncResult.SocketFlags;
        ipPacketInformation = receiveMessageOverlappedAsyncResult.IPPacketInformation;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, num);
        }

        return num;
    }

    public IAsyncResult BeginReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        ref EndPoint remoteEP, AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        if (!CanTryAddressFamily(remoteEP.AddressFamily))
        {
            throw new ArgumentException(
                SR.Format(SR.net_InvalidEndPointAddressFamily, remoteEP.AddressFamily, _addressFamily),
                "remoteEP");
        }

        if (offset < 0 || offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if (size < 0 || size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        if (_rightEndPoint == null)
        {
            throw new InvalidOperationException(SR.net_sockets_mustbind);
        }

        SocketPal.CheckDualModeReceiveSupport(this);
        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref remoteEP);
        OriginalAddressOverlappedAsyncResult originalAddressOverlappedAsyncResult =
            new OriginalAddressOverlappedAsyncResult(this, state, callback);
        originalAddressOverlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        DoBeginReceiveFrom(buffer, offset, size, socketFlags, remoteEP, socketAddress,
            originalAddressOverlappedAsyncResult);
        originalAddressOverlappedAsyncResult.FinishPostingAsyncOp(ref Caches.ReceiveClosureCache);
        if (originalAddressOverlappedAsyncResult.CompletedSynchronously &&
            !originalAddressOverlappedAsyncResult.SocketAddressOriginal.Equals(originalAddressOverlappedAsyncResult
                .SocketAddress))
        {
            try
            {
                remoteEP = remoteEP.Create(originalAddressOverlappedAsyncResult.SocketAddress);
            }
            catch
            {
            }
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, originalAddressOverlappedAsyncResult);
        }

        return originalAddressOverlappedAsyncResult;
    }

    private void DoBeginReceiveFrom(byte[] buffer, int offset, int size, SocketFlags socketFlags,
        EndPoint endPointSnapshot, Internals.SocketAddress socketAddress,
        OriginalAddressOverlappedAsyncResult asyncResult)
    {
        EndPoint rightEndPoint = _rightEndPoint;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"size:{size}");
        }

        SocketError socketError = SocketError.SocketError;
        try
        {
            asyncResult.SocketAddressOriginal = IPEndPointExtensions.Serialize(endPointSnapshot);
            if (_rightEndPoint == null)
            {
                _rightEndPoint = endPointSnapshot;
            }

            socketError =
                SocketPal.ReceiveFromAsync(_handle, buffer, offset, size, socketFlags, socketAddress, asyncResult);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this,
                    $"Interop.Winsock.WSARecvFrom returns:{socketError} size:{size} returning AsyncResult:{asyncResult}");
            }
        }
        catch (ObjectDisposedException)
        {
            _rightEndPoint = rightEndPoint;
            throw;
        }

        if (!CheckErrorAndUpdateStatus(socketError))
        {
            _rightEndPoint = rightEndPoint;
            throw new SocketException((int)socketError);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"size:{size} return AsyncResult:{asyncResult}");
        }
    }

    public int EndReceiveFrom(IAsyncResult asyncResult, ref EndPoint endPoint)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (endPoint == null)
        {
            throw new ArgumentNullException("endPoint");
        }

        if (!CanTryAddressFamily(endPoint.AddressFamily))
        {
            throw new ArgumentException(
                SR.Format(SR.net_InvalidEndPointAddressFamily, endPoint.AddressFamily, _addressFamily),
                "endPoint");
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        if (!(asyncResult is OverlappedAsyncResult overlappedAsyncResult) || overlappedAsyncResult.AsyncObject != this)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (overlappedAsyncResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndReceiveFrom"));
        }

        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref endPoint);
        int num = overlappedAsyncResult.InternalWaitForCompletionInt32Result();
        overlappedAsyncResult.EndCalled = true;
        overlappedAsyncResult.SocketAddress.InternalSize = overlappedAsyncResult.GetSocketAddressSize();
        if (!socketAddress.Equals(overlappedAsyncResult.SocketAddress))
        {
            try
            {
                endPoint = endPoint.Create(overlappedAsyncResult.SocketAddress);
            }
            catch
            {
            }
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"bytesTransferred:{num}");
        }

        if (overlappedAsyncResult.ErrorCode != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException((SocketError)overlappedAsyncResult.ErrorCode);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, num);
        }

        return num;
    }

    public IAsyncResult BeginAccept(AsyncCallback callback, object state)
    {
        if (!_isDisconnected)
        {
            return BeginAccept(0, callback, state);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        throw new ObjectDisposedException(GetType().FullName);
    }

    public IAsyncResult BeginAccept(int receiveSize, AsyncCallback callback, object state)
    {
        return BeginAccept(null, receiveSize, callback, state);
    }

    public IAsyncResult BeginAccept(Socket acceptSocket, int receiveSize, AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (receiveSize < 0)
        {
            throw new ArgumentOutOfRangeException("receiveSize");
        }

        AcceptOverlappedAsyncResult acceptOverlappedAsyncResult =
            new AcceptOverlappedAsyncResult(this, state, callback);
        acceptOverlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        DoBeginAccept(acceptSocket, receiveSize, acceptOverlappedAsyncResult);
        acceptOverlappedAsyncResult.FinishPostingAsyncOp(ref Caches.AcceptClosureCache);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, acceptOverlappedAsyncResult);
        }

        return acceptOverlappedAsyncResult;
    }

    private void DoBeginAccept(Socket acceptSocket, int receiveSize, AcceptOverlappedAsyncResult asyncResult)
    {
        if (_rightEndPoint == null)
        {
            throw new InvalidOperationException(SR.net_sockets_mustbind);
        }

        if (!_isListening)
        {
            throw new InvalidOperationException(SR.net_sockets_mustlisten);
        }

        asyncResult.AcceptSocket =
            GetOrCreateAcceptSocket(acceptSocket, checkDisconnected: false, "acceptSocket", out var handle);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"AcceptSocket:{acceptSocket}");
        }

        int addressSize = GetAddressSize(_rightEndPoint);
        SocketError socketError =
            SocketPal.AcceptAsync(this, _handle, handle, receiveSize, addressSize, asyncResult);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.AcceptEx returns:{socketError} {asyncResult}");
        }

        if (!CheckErrorAndUpdateStatus(socketError))
        {
            throw new SocketException((int)socketError);
        }
    }

    public Socket EndAccept(IAsyncResult asyncResult)
    {
        byte[] buffer;
        int bytesTransferred;
        return EndAccept(out buffer, out bytesTransferred, asyncResult);
    }

    public Socket EndAccept(out byte[] buffer, IAsyncResult asyncResult)
    {
        byte[] buffer2;
        int bytesTransferred;
        Socket result = EndAccept(out buffer2, out bytesTransferred, asyncResult);
        buffer = new byte[bytesTransferred];
        Buffer.BlockCopy(buffer2, 0, buffer, 0, bytesTransferred);
        return result;
    }

    public Socket EndAccept(out byte[] buffer, out int bytesTransferred, IAsyncResult asyncResult)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, asyncResult);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        if (!(asyncResult is AcceptOverlappedAsyncResult acceptOverlappedAsyncResult) ||
            acceptOverlappedAsyncResult.AsyncObject != this)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (acceptOverlappedAsyncResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndAccept"));
        }

        Socket socket = (Socket)acceptOverlappedAsyncResult.InternalWaitForCompletion();
        bytesTransferred = acceptOverlappedAsyncResult.BytesTransferred;
        buffer = acceptOverlappedAsyncResult.Buffer;
        acceptOverlappedAsyncResult.EndCalled = true;
        if (acceptOverlappedAsyncResult.ErrorCode != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException((SocketError)acceptOverlappedAsyncResult.ErrorCode);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Accepted(socket, socket.RemoteEndPoint, socket.LocalEndPoint);
            NetEventSource.Exit(this, socket);
        }

        return socket;
    }

    public void Shutdown(SocketShutdown how)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, how);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"how:{how}");
        }

        SocketError socketError = SocketPal.Shutdown(_handle, _isConnected, _isDisconnected, how);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.shutdown returns errorCode:{socketError}");
        }

        if (socketError != 0 && socketError != SocketError.NotSocket)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        SetToDisconnected();
        InternalSetBlocking(_willBlockInternal);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    public bool AcceptAsync(SocketAsyncEventArgs e)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, e);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        if (e.HasMultipleBuffers)
        {
            throw new ArgumentException(SR.net_multibuffernotsupported, "BufferList");
        }

        if (_rightEndPoint == null)
        {
            throw new InvalidOperationException(SR.net_sockets_mustbind);
        }

        if (!_isListening)
        {
            throw new InvalidOperationException(SR.net_sockets_mustlisten);
        }

        e.AcceptSocket =
            GetOrCreateAcceptSocket(e.AcceptSocket, checkDisconnected: true, "AcceptSocket", out var handle);
        e.StartOperationCommon(this, SocketAsyncOperation.Accept);
        e.StartOperationAccept();
        SocketError socketError = SocketError.Success;
        try
        {
            socketError = e.DoOperationAccept(this, _handle, handle);
        }
        catch
        {
            e.Complete();
            throw;
        }

        bool flag = socketError == SocketError.IOPending;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, flag);
        }

        return flag;
    }

    public bool ConnectAsync(SocketAsyncEventArgs e)
    {
        return ConnectAsync(e, userSocket: true);
    }

    private bool ConnectAsync(SocketAsyncEventArgs e, bool userSocket)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, e);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        if (e.HasMultipleBuffers)
        {
            throw new ArgumentException(SR.net_multibuffernotsupported, "BufferList");
        }

        if (e.RemoteEndPoint == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        if (_isListening)
        {
            throw new InvalidOperationException(SR.net_sockets_mustnotlisten);
        }

        if (_isConnected)
        {
            throw new SocketException(10056);
        }

        EndPoint remoteEP = e.RemoteEndPoint;
        bool flag;
        if (remoteEP is DnsEndPoint dnsEndPoint)
        {
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.ConnectedAsyncDns(this);
            }

            if (dnsEndPoint.AddressFamily != 0 && !CanTryAddressFamily(dnsEndPoint.AddressFamily))
            {
                throw new NotSupportedException(SR.net_invalidversion);
            }

            MultipleConnectAsync multipleConnectAsync = new SingleSocketMultipleConnectAsync(this, userSocket: true);
            e.StartOperationCommon(this, SocketAsyncOperation.Connect);
            e.StartOperationConnect(multipleConnectAsync, userSocket: true);
            try
            {
                flag = multipleConnectAsync.StartConnectAsync(e, dnsEndPoint);
            }
            catch
            {
                e.Complete();
                throw;
            }
        }
        else
        {
            if (!CanTryAddressFamily(e.RemoteEndPoint.AddressFamily))
            {
                throw new NotSupportedException(SR.net_invalidversion);
            }

            e._socketAddress = SnapshotAndSerialize(ref remoteEP);
            WildcardBindForConnectIfNecessary(remoteEP.AddressFamily);
            EndPoint rightEndPoint = _rightEndPoint;
            if (_rightEndPoint == null)
            {
                _rightEndPoint = remoteEP;
            }

            e.StartOperationCommon(this, SocketAsyncOperation.Connect);
            e.StartOperationConnect(null, userSocket);
            SocketError socketError = SocketError.Success;
            try
            {
                socketError = e.DoOperationConnect(this, _handle);
            }
            catch
            {
                _rightEndPoint = rightEndPoint;
                e.Complete();
                throw;
            }

            flag = socketError == SocketError.IOPending;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, flag);
        }

        return flag;
    }

    public static bool ConnectAsync(SocketType socketType, ProtocolType protocolType, SocketAsyncEventArgs e)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(null);
        }

        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        if (e.HasMultipleBuffers)
        {
            throw new ArgumentException(SR.net_multibuffernotsupported, "BufferList");
        }

        if (e.RemoteEndPoint == null)
        {
            throw new ArgumentNullException("remoteEP");
        }

        EndPoint remoteEndPoint = e.RemoteEndPoint;
        bool flag;
        if (remoteEndPoint is DnsEndPoint dnsEndPoint)
        {
            Socket socket = null;
            MultipleConnectAsync multipleConnectAsync = null;
            if (dnsEndPoint.AddressFamily == AddressFamily.Unspecified)
            {
                multipleConnectAsync = new DualSocketMultipleConnectAsync(socketType, protocolType);
            }
            else
            {
                socket = new Socket(dnsEndPoint.AddressFamily, socketType, protocolType);
                multipleConnectAsync = new SingleSocketMultipleConnectAsync(socket, userSocket: false);
            }

            e.StartOperationCommon(socket, SocketAsyncOperation.Connect);
            e.StartOperationConnect(multipleConnectAsync, userSocket: false);
            try
            {
                flag = multipleConnectAsync.StartConnectAsync(e, dnsEndPoint);
            }
            catch
            {
                e.Complete();
                throw;
            }
        }
        else
        {
            Socket socket2 = new Socket(remoteEndPoint.AddressFamily, socketType, protocolType);
            flag = socket2.ConnectAsync(e, userSocket: false);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(null, flag);
        }

        return flag;
    }

    private void WildcardBindForConnectIfNecessary(AddressFamily addressFamily)
    {
        if (_rightEndPoint == null)
        {
            IPAddress iPAddress;
            switch (addressFamily)
            {
                default:
                    return;
                case AddressFamily.InterNetwork:
                    iPAddress = (IsDualMode ? IPAddress.Any.MapToIPv6() : IPAddress.Any);
                    break;
                case AddressFamily.InterNetworkV6:
                    iPAddress = IPAddress.IPv6Any;
                    break;
            }

            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, iPAddress);
            }

            IPEndPoint iPEndPoint = new IPEndPoint(iPAddress, 0);
            DoBind(iPEndPoint, IPEndPointExtensions.Serialize(iPEndPoint));
        }
    }

    public static void CancelConnectAsync(SocketAsyncEventArgs e)
    {
        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        e.CancelConnectAsync();
    }

    public bool DisconnectAsync(SocketAsyncEventArgs e)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        e.StartOperationCommon(this, SocketAsyncOperation.Disconnect);
        SocketError socketError = SocketError.Success;
        try
        {
            socketError = e.DoOperationDisconnect(this, _handle);
        }
        catch
        {
            e.Complete();
            throw;
        }

        bool flag = socketError == SocketError.IOPending;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, flag);
        }

        return flag;
    }

    public bool ReceiveAsync(SocketAsyncEventArgs e)
    {
        return ReceiveAsync(e, default(CancellationToken));
    }

    private bool ReceiveAsync(SocketAsyncEventArgs e, CancellationToken cancellationToken)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, e);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        e.StartOperationCommon(this, SocketAsyncOperation.Receive);
        SocketError socketError;
        try
        {
            socketError = e.DoOperationReceive(_handle, cancellationToken);
        }
        catch
        {
            e.Complete();
            throw;
        }

        bool flag = socketError == SocketError.IOPending;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, flag);
        }

        return flag;
    }

    public bool ReceiveFromAsync(SocketAsyncEventArgs e)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, e);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        if (e.RemoteEndPoint == null)
        {
            throw new ArgumentNullException("RemoteEndPoint");
        }

        if (!CanTryAddressFamily(e.RemoteEndPoint.AddressFamily))
        {
            throw new ArgumentException(
                SR.Format(SR.net_InvalidEndPointAddressFamily, e.RemoteEndPoint.AddressFamily,
                    _addressFamily), "RemoteEndPoint");
        }

        SocketPal.CheckDualModeReceiveSupport(this);
        EndPoint remoteEP = e.RemoteEndPoint;
        e._socketAddress = SnapshotAndSerialize(ref remoteEP);
        e.RemoteEndPoint = remoteEP;
        e.StartOperationCommon(this, SocketAsyncOperation.ReceiveFrom);
        SocketError socketError;
        try
        {
            socketError = e.DoOperationReceiveFrom(_handle);
        }
        catch
        {
            e.Complete();
            throw;
        }

        bool flag = socketError == SocketError.IOPending;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, flag);
        }

        return flag;
    }

    public bool ReceiveMessageFromAsync(SocketAsyncEventArgs e)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, e);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        if (e.RemoteEndPoint == null)
        {
            throw new ArgumentNullException("RemoteEndPoint");
        }

        if (!CanTryAddressFamily(e.RemoteEndPoint.AddressFamily))
        {
            throw new ArgumentException(
                SR.Format(SR.net_InvalidEndPointAddressFamily, e.RemoteEndPoint.AddressFamily,
                    _addressFamily), "RemoteEndPoint");
        }

        SocketPal.CheckDualModeReceiveSupport(this);
        EndPoint remoteEP = e.RemoteEndPoint;
        e._socketAddress = SnapshotAndSerialize(ref remoteEP);
        e.RemoteEndPoint = remoteEP;
        SetReceivingPacketInformation();
        e.StartOperationCommon(this, SocketAsyncOperation.ReceiveMessageFrom);
        SocketError socketError;
        try
        {
            socketError = e.DoOperationReceiveMessageFrom(this, _handle);
        }
        catch
        {
            e.Complete();
            throw;
        }

        bool flag = socketError == SocketError.IOPending;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, flag);
        }

        return flag;
    }

    public bool SendAsync(SocketAsyncEventArgs e)
    {
        return SendAsync(e, default(CancellationToken));
    }

    private bool SendAsync(SocketAsyncEventArgs e, CancellationToken cancellationToken)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, e);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        e.StartOperationCommon(this, SocketAsyncOperation.Send);
        SocketError socketError;
        try
        {
            socketError = e.DoOperationSend(_handle, cancellationToken);
        }
        catch
        {
            e.Complete();
            throw;
        }

        bool flag = socketError == SocketError.IOPending;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, flag);
        }

        return flag;
    }

    public bool SendPacketsAsync(SocketAsyncEventArgs e)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, e);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        if (e.SendPacketsElements == null)
        {
            throw new ArgumentNullException("e.SendPacketsElements");
        }

        if (!Connected)
        {
            throw new NotSupportedException(SR.net_notconnected);
        }

        e.StartOperationCommon(this, SocketAsyncOperation.SendPackets);
        SocketError socketError;
        try
        {
            socketError = e.DoOperationSendPackets(this, _handle);
        }
        catch (Exception)
        {
            e.Complete();
            throw;
        }

        bool flag = socketError == SocketError.IOPending;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, flag);
        }

        return flag;
    }

    public bool SendToAsync(SocketAsyncEventArgs e)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, e);
        }

        if (CleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (e == null)
        {
            throw new ArgumentNullException("e");
        }

        if (e.RemoteEndPoint == null)
        {
            throw new ArgumentNullException("RemoteEndPoint");
        }

        EndPoint remoteEP = e.RemoteEndPoint;
        e._socketAddress = SnapshotAndSerialize(ref remoteEP);
        e.StartOperationCommon(this, SocketAsyncOperation.SendTo);
        SocketError socketError;
        try
        {
            socketError = e.DoOperationSendTo(_handle);
        }
        catch
        {
            e.Complete();
            throw;
        }

        bool flag = socketError == SocketError.IOPending;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this, flag);
        }

        return flag;
    }

    internal static void GetIPProtocolInformation(AddressFamily addressFamily,
        Internals.SocketAddress socketAddress, out bool isIPv4, out bool isIPv6)
    {
        bool flag = socketAddress.Family == AddressFamily.InterNetworkV6 &&
                    socketAddress.GetIPAddress().IsIPv4MappedToIPv6;
        isIPv4 = addressFamily == AddressFamily.InterNetwork || flag;
        isIPv6 = addressFamily == AddressFamily.InterNetworkV6;
    }

    internal static int GetAddressSize(EndPoint endPoint)
    {
        return endPoint.AddressFamily switch
        {
            AddressFamily.InterNetworkV6 => 28,
            AddressFamily.InterNetwork => 16,
            _ => endPoint.Serialize().Size,
        };
    }

    private Internals.SocketAddress SnapshotAndSerialize(ref EndPoint remoteEP)
    {
        if (remoteEP is IPEndPoint thisObj)
        {
            IPEndPoint input = thisObj.Snapshot();
            remoteEP = RemapIPEndPoint(input);
        }
        else if (remoteEP is DnsEndPoint)
        {
            throw new ArgumentException(SR.Format(SR.net_sockets_invalid_dnsendpoint, "remoteEP"),
                "remoteEP");
        }

        return IPEndPointExtensions.Serialize(remoteEP);
    }

    private IPEndPoint RemapIPEndPoint(IPEndPoint input)
    {
        if (input.AddressFamily == AddressFamily.InterNetwork && IsDualMode)
        {
            return new IPEndPoint(input.Address.MapToIPv6(), input.Port);
        }

        return input;
    }

    internal static void InitializeSockets()
    {
        if (!s_initialized)
        {
            InitializeSocketsCore();
        }

        static void InitializeSocketsCore()
        {
            lock (InternalSyncObject)
            {
                if (!s_initialized)
                {
                    SocketPal.Initialize();
                    s_initialized = true;
                }
            }
        }
    }

    private void DoConnect(EndPoint endPointSnapshot, Internals.SocketAddress socketAddress)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, endPointSnapshot);
        }

        SocketError socketError = SocketPal.Connect(_handle, socketAddress.Buffer, socketAddress.Size);
        if (socketError != 0)
        {
            SocketException ex =
                SocketExceptionFactory.CreateSocketException((int)socketError, endPointSnapshot);
            UpdateStatusAfterSocketError(ex);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Error(this, ex);
            }

            throw ex;
        }

        if (_rightEndPoint == null)
        {
            _rightEndPoint = endPointSnapshot;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"connection to:{endPointSnapshot}");
        }

        SetToConnected();
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Connected(this, LocalEndPoint, RemoteEndPoint);
            NetEventSource.Exit(this);
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (NetEventSource.IsEnabled)
        {
            try
            {
                NetEventSource.Info(this, $"disposing:{disposing} CleanedUp:{CleanedUp}");
                NetEventSource.Enter(this);
            }
            catch (Exception exception) when (!ExceptionCheck.IsFatal(exception))
            {
            }
        }

        if (Interlocked.CompareExchange(ref _intCleanedUp, 1, 0) == 1)
        {
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Exit(this);
            }

            return;
        }

        SetToDisconnected();
        try
        {
            int closeTimeout = _closeTimeout;
            if (closeTimeout == 0 || !disposing)
            {
                if (NetEventSource.IsEnabled)
                {
                    NetEventSource.Info(this, "Calling _handle.Dispose()");
                }

                _handle?.Dispose();
            }
            else
            {
                if (!_willBlock || !_willBlockInternal)
                {
                    bool willBlock;
                    SocketError socketError = SocketPal.SetBlocking(_handle, shouldBlock: false, out willBlock);
                    if (NetEventSource.IsEnabled)
                    {
                        NetEventSource.Info(this, $"handle:{_handle} ioctlsocket(FIONBIO):{socketError}");
                    }
                }

                if (closeTimeout < 0)
                {
                    if (NetEventSource.IsEnabled)
                    {
                        NetEventSource.Info(this, "Calling _handle.CloseAsIs()");
                    }

                    _handle.CloseAsIs();
                }
                else
                {
                    SocketError socketError =
                        SocketPal.Shutdown(_handle, _isConnected, _isDisconnected, SocketShutdown.Send);
                    if (NetEventSource.IsEnabled)
                    {
                        NetEventSource.Info(this, $"handle:{_handle} shutdown():{socketError}");
                    }

                    socketError = SocketPal.SetSockOpt(_handle, SocketOptionLevel.Socket,
                        SocketOptionName.ReceiveTimeout, closeTimeout);
                    if (NetEventSource.IsEnabled)
                    {
                        NetEventSource.Info(this, $"handle:{_handle} setsockopt():{socketError}");
                    }

                    if (socketError != 0)
                    {
                        _handle.Dispose();
                    }
                    else
                    {
                        socketError = SocketPal.Receive(_handle, ArrayEx.Empty<byte>(), 0, 0, SocketFlags.None,
                            out var _);
                        if (NetEventSource.IsEnabled)
                        {
                            NetEventSource.Info(this, $"handle:{_handle} recv():{socketError}");
                        }

                        if (socketError != 0)
                        {
                            _handle.Dispose();
                        }
                        else
                        {
                            int available = 0;
                            socketError = SocketPal.GetAvailable(_handle, out available);
                            if (NetEventSource.IsEnabled)
                            {
                                NetEventSource.Info(this,
                                    $"handle:{_handle} ioctlsocket(FIONREAD):{socketError}");
                            }

                            if (socketError != 0 || available != 0)
                            {
                                _handle.Dispose();
                            }
                            else
                            {
                                _handle.CloseAsIs();
                            }
                        }
                    }
                }
            }
        }
        catch (ObjectDisposedException)
        {
            NetEventSource.Fail(this, $"handle:{_handle}, Closing the handle threw ObjectDisposedException.");
        }

        DisposeCachedTaskSocketAsyncEventArgs();
    }

    public void Dispose()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"timeout = {_closeTimeout}");
            NetEventSource.Enter(this);
        }

        Dispose(disposing: true);
        GC.SuppressFinalize(this);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Exit(this);
        }
    }

    ~Socket()
    {
        Dispose(disposing: false);
    }

    internal void InternalShutdown(SocketShutdown how)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, $"how:{how}");
        }

        if (CleanedUp || _handle.IsInvalid)
        {
            return;
        }

        try
        {
            SocketPal.Shutdown(_handle, _isConnected, _isDisconnected, how);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    internal void SetReceivingPacketInformation()
    {
        if (!_receivingPacketInformation)
        {
            IPAddress iPAddress = ((_rightEndPoint is IPEndPoint iPEndPoint) ? iPEndPoint.Address : null);
            if (_addressFamily == AddressFamily.InterNetwork)
            {
                SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, optionValue: true);
            }

            if (iPAddress != null && IsDualMode &&
                (iPAddress.IsIPv4MappedToIPv6 || iPAddress.Equals(IPAddress.IPv6Any)))
            {
                SocketPal.SetReceivingDualModeIPv4PacketInformation(this);
            }

            if (_addressFamily == AddressFamily.InterNetworkV6 &&
                (iPAddress == null || !iPAddress.IsIPv4MappedToIPv6))
            {
                SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.PacketInformation, optionValue: true);
            }

            _receivingPacketInformation = true;
        }
    }

    internal void SetSocketOption(SocketOptionLevel optionLevel, SocketOptionName optionName, int optionValue,
        bool silent)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this,
                $"optionLevel:{optionLevel} optionName:{optionName} optionValue:{optionValue} silent:{silent}");
        }

        if (silent && (CleanedUp || _handle.IsInvalid))
        {
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, "skipping the call");
            }

            return;
        }

        SocketError socketError = SocketError.Success;
        try
        {
            socketError = SocketPal.SetSockOpt(_handle, optionLevel, optionName, optionValue);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, $"Interop.Winsock.setsockopt returns errorCode:{socketError}");
            }
        }
        catch
        {
            if (silent && _handle.IsInvalid)
            {
                return;
            }

            throw;
        }

        if (optionName == SocketOptionName.PacketInformation && optionValue == 0 &&
            socketError == SocketError.Success)
        {
            _receivingPacketInformation = false;
        }

        if (!silent && socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }
    }

    private void SetMulticastOption(SocketOptionName optionName, MulticastOption MR)
    {
        SocketError socketError = SocketPal.SetMulticastOption(_handle, optionName, MR);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.setsockopt returns errorCode:{socketError}");
        }

        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }
    }

    private void SetIPv6MulticastOption(SocketOptionName optionName, IPv6MulticastOption MR)
    {
        SocketError socketError = SocketPal.SetIPv6MulticastOption(_handle, optionName, MR);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.setsockopt returns errorCode:{socketError}");
        }

        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }
    }

    private void SetLingerOption(LingerOption lref)
    {
        SocketError socketError = SocketPal.SetLingerOption(_handle, lref);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.setsockopt returns errorCode:{socketError}");
        }

        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }
    }

    private LingerOption GetLingerOpt()
    {
        LingerOption optionValue;
        SocketError lingerOption = SocketPal.GetLingerOption(_handle, out optionValue);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.getsockopt returns errorCode:{lingerOption}");
        }

        if (lingerOption != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(lingerOption);
        }

        return optionValue;
    }

    private MulticastOption GetMulticastOpt(SocketOptionName optionName)
    {
        MulticastOption optionValue;
        SocketError multicastOption = SocketPal.GetMulticastOption(_handle, optionName, out optionValue);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.getsockopt returns errorCode:{multicastOption}");
        }

        if (multicastOption != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(multicastOption);
        }

        return optionValue;
    }

    private IPv6MulticastOption GetIPv6MulticastOpt(SocketOptionName optionName)
    {
        IPv6MulticastOption optionValue;
        SocketError iPv6MulticastOption = SocketPal.GetIPv6MulticastOption(_handle, optionName, out optionValue);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.getsockopt returns errorCode:{iPv6MulticastOption}");
        }

        if (iPv6MulticastOption != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(iPv6MulticastOption);
        }

        return optionValue;
    }

    private SocketError InternalSetBlocking(bool desired, out bool current)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this,
                $"desired:{desired} willBlock:{_willBlock} willBlockInternal:{_willBlockInternal}");
        }

        if (CleanedUp)
        {
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Exit(this, "ObjectDisposed");
            }

            current = _willBlock;
            return SocketError.Success;
        }

        bool willBlock = false;
        SocketError socketError;
        try
        {
            socketError = SocketPal.SetBlocking(_handle, desired, out willBlock);
        }
        catch (ObjectDisposedException)
        {
            socketError = SocketError.NotSocket;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.ioctlsocket returns errorCode:{socketError}");
        }

        if (socketError == SocketError.Success)
        {
            _willBlockInternal = willBlock;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"errorCode:{socketError} willBlock:{_willBlock} willBlockInternal:{_willBlockInternal}");
        }

        current = _willBlockInternal;
        return socketError;
    }

    internal void InternalSetBlocking(bool desired)
    {
        InternalSetBlocking(desired, out var _);
    }

    private IAsyncResult BeginConnectEx(EndPoint remoteEP, bool flowContext, AsyncCallback callback, object state)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        EndPoint remoteEP2 = remoteEP;
        Internals.SocketAddress socketAddress = SnapshotAndSerialize(ref remoteEP2);
        WildcardBindForConnectIfNecessary(remoteEP2.AddressFamily);
        ConnectOverlappedAsyncResult connectOverlappedAsyncResult =
            new ConnectOverlappedAsyncResult(this, remoteEP2, state, callback);
        if (flowContext)
        {
            connectOverlappedAsyncResult.StartPostingAsyncOp(lockCapture: false);
        }

        EndPoint rightEndPoint = _rightEndPoint;
        if (_rightEndPoint == null)
        {
            _rightEndPoint = remoteEP2;
        }

        SocketError socketError;
        try
        {
            socketError = SocketPal.ConnectAsync(this, _handle, socketAddress.Buffer, socketAddress.Size,
                connectOverlappedAsyncResult);
        }
        catch
        {
            _rightEndPoint = rightEndPoint;
            throw;
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"Interop.Winsock.connect returns:{socketError}");
        }

        if (socketError == SocketError.Success)
        {
            SetToConnected();
        }

        if (!CheckErrorAndUpdateStatus(socketError))
        {
            _rightEndPoint = rightEndPoint;
            throw new SocketException((int)socketError);
        }

        connectOverlappedAsyncResult.FinishPostingAsyncOp(ref Caches.ConnectClosureCache);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"{remoteEP2} returning AsyncResult:{connectOverlappedAsyncResult}");
            NetEventSource.Exit(this, connectOverlappedAsyncResult);
        }

        return connectOverlappedAsyncResult;
    }

    private static void DnsCallback(IAsyncResult result)
    {
        if (!result.CompletedSynchronously)
        {
            bool flag = false;
            MultipleAddressConnectAsyncResult multipleAddressConnectAsyncResult =
                (MultipleAddressConnectAsyncResult)result.AsyncState;
            try
            {
                flag = DoDnsCallback(result, multipleAddressConnectAsyncResult);
            }
            catch (Exception result2)
            {
                multipleAddressConnectAsyncResult.InvokeCallback(result2);
            }

            if (flag)
            {
                multipleAddressConnectAsyncResult.InvokeCallback();
            }
        }
    }

    private static bool DoDnsCallback(IAsyncResult result, MultipleAddressConnectAsyncResult context)
    {
        IPAddress[] addresses = Dns.EndGetHostAddresses(result);
        context._addresses = addresses;
        return DoMultipleAddressConnectCallback(PostOneBeginConnect(context), context);
    }

    private static object PostOneBeginConnect(MultipleAddressConnectAsyncResult context)
    {
        IPAddress iPAddress = context._addresses[context._index];
        context._socket.ReplaceHandleIfNecessaryAfterFailedConnect();
        if (!context._socket.CanTryAddressFamily(iPAddress.AddressFamily))
        {
            if (context._lastException == null)
            {
                return new ArgumentException(SR.net_invalidAddressList, "context");
            }

            return context._lastException;
        }

        try
        {
            EndPoint remoteEP = new IPEndPoint(iPAddress, context._port);
            context._socket.SnapshotAndSerialize(ref remoteEP);
            IAsyncResult asyncResult =
                context._socket.UnsafeBeginConnect(remoteEP, CachedMultipleAddressConnectCallback, context);
            if (asyncResult.CompletedSynchronously)
            {
                return asyncResult;
            }
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            object obj = ex;
            return ex;
        }

        return null;
    }

    private static void MultipleAddressConnectCallback(IAsyncResult result)
    {
        if (!result.CompletedSynchronously)
        {
            bool flag = false;
            MultipleAddressConnectAsyncResult multipleAddressConnectAsyncResult =
                (MultipleAddressConnectAsyncResult)result.AsyncState;
            try
            {
                flag = DoMultipleAddressConnectCallback(result, multipleAddressConnectAsyncResult);
            }
            catch (Exception result2)
            {
                multipleAddressConnectAsyncResult.InvokeCallback(result2);
            }

            if (flag)
            {
                multipleAddressConnectAsyncResult.InvokeCallback();
            }
        }
    }

    private static bool DoMultipleAddressConnectCallback(object result, MultipleAddressConnectAsyncResult context)
    {
        while (result != null)
        {
            Exception ex = result as Exception;
            if (ex == null)
            {
                try
                {
                    context._socket.EndConnect((IAsyncResult)result);
                }
                catch (Exception ex2)
                {
                    ex = ex2;
                }
            }

            if (ex == null)
            {
                return true;
            }

            if (++context._index >= context._addresses.Length)
            {
                ExceptionDispatchInfo.Throw(ex);
            }

            context._lastException = ex;
            result = PostOneBeginConnect(context);
        }

        return false;
    }

    internal Socket CreateAcceptSocket(SafeSocketHandle fd, EndPoint remoteEP)
    {
        Socket socket = new Socket(fd);
        return UpdateAcceptSocket(socket, remoteEP);
    }

    internal Socket UpdateAcceptSocket(Socket socket, EndPoint remoteEP)
    {
        socket._addressFamily = _addressFamily;
        socket._socketType = _socketType;
        socket._protocolType = _protocolType;
        socket._rightEndPoint = _rightEndPoint;
        socket._remoteEndPoint = remoteEP;
        socket.SetToConnected();
        socket._willBlock = _willBlock;
        socket.InternalSetBlocking(_willBlock);
        return socket;
    }

    internal void SetToConnected()
    {
        if (!_isConnected)
        {
            _isConnected = true;
            _isDisconnected = false;
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, "now connected");
            }
        }
    }

    internal void SetToDisconnected()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (_isConnected)
        {
            _isConnected = false;
            _isDisconnected = true;
            if (!CleanedUp && NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, "!CleanedUp");
            }
        }
    }

    private void UpdateStatusAfterSocketErrorAndThrowException(SocketError error,
        [CallerMemberName] string callerName = null)
    {
        SocketException ex = new SocketException((int)error);
        UpdateStatusAfterSocketError(ex);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Error(this, ex, callerName);
        }

        throw ex;
    }

    internal void UpdateStatusAfterSocketError(SocketException socketException)
    {
        UpdateStatusAfterSocketError(socketException.SocketErrorCode);
    }

    internal void UpdateStatusAfterSocketError(SocketError errorCode)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this);
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Error(this, $"errorCode:{errorCode}");
        }

        if (_isConnected && (_handle.IsInvalid || (errorCode != SocketError.WouldBlock &&
                                                   errorCode != SocketError.IOPending &&
                                                   errorCode != SocketError.NoBufferSpaceAvailable &&
                                                   errorCode != SocketError.TimedOut)))
        {
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, "Invalidating socket.");
            }

            SetToDisconnected();
        }
    }

    private bool CheckErrorAndUpdateStatus(SocketError errorCode)
    {
        if (errorCode == SocketError.Success || errorCode == SocketError.IOPending)
        {
            return true;
        }

        UpdateStatusAfterSocketError(errorCode);
        return false;
    }

    private void ValidateBlockingMode()
    {
        if (_willBlock && !_willBlockInternal)
        {
            throw new InvalidOperationException(SR.net_invasync);
        }
    }

    private static FileStream OpenFile(string name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            return File.OpenRead(name);
        }

        return null;
    }

    internal Task<Socket> AcceptAsync(Socket acceptSocket)
    {
        TaskSocketAsyncEventArgs<Socket> taskSocketAsyncEventArgs =
            Interlocked.Exchange(ref EventArgs.TaskAccept, s_rentedSocketSentinel);
        if (taskSocketAsyncEventArgs == s_rentedSocketSentinel)
        {
            return AcceptAsyncApm(acceptSocket);
        }

        if (taskSocketAsyncEventArgs == null)
        {
            taskSocketAsyncEventArgs = new TaskSocketAsyncEventArgs<Socket>();
            taskSocketAsyncEventArgs.Completed += AcceptCompletedHandler;
        }

        taskSocketAsyncEventArgs.AcceptSocket = acceptSocket;
        Task<Socket> result;
        if (AcceptAsync(taskSocketAsyncEventArgs))
        {
            result = taskSocketAsyncEventArgs.GetCompletionResponsibility(out var responsibleForReturningToPool).Task;
            if (responsibleForReturningToPool)
            {
                ReturnSocketAsyncEventArgs(taskSocketAsyncEventArgs);
            }
        }
        else
        {
            result = ((taskSocketAsyncEventArgs.SocketError == SocketError.Success)
                ? TaskExEx.FromResult(taskSocketAsyncEventArgs.AcceptSocket)
                : TaskExEx.FromException<Socket>(GetException(taskSocketAsyncEventArgs.SocketError)));
            ReturnSocketAsyncEventArgs(taskSocketAsyncEventArgs);
        }

        return result;
    }

    private Task<Socket> AcceptAsyncApm(Socket acceptSocket)
    {
        TaskCompletionSource<Socket> taskCompletionSource = new TaskCompletionSource<Socket>(this);
        BeginAccept(acceptSocket, 0, delegate(IAsyncResult iar)
        {
            TaskCompletionSource<Socket> taskCompletionSource2 =
                (TaskCompletionSource<Socket>)iar.AsyncState;
            try
            {
                taskCompletionSource2.TrySetResult(((Socket)taskCompletionSource2.Task.AsyncState)
                    .EndAccept(iar));
            }
            catch (Exception exception)
            {
                taskCompletionSource2.TrySetException(exception);
            }
        }, taskCompletionSource);
        return taskCompletionSource.Task;
    }

    internal Task ConnectAsync(EndPoint remoteEP)
    {
        TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>(this);
        BeginConnect(remoteEP, delegate(IAsyncResult iar)
        {
            TaskCompletionSource<bool> taskCompletionSource2 = (TaskCompletionSource<bool>)iar.AsyncState;
            try
            {
                ((Socket)taskCompletionSource2.Task.AsyncState).EndConnect(iar);
                taskCompletionSource2.TrySetResult(result: true);
            }
            catch (Exception exception)
            {
                taskCompletionSource2.TrySetException(exception);
            }
        }, taskCompletionSource);
        return taskCompletionSource.Task;
    }

    internal Task ConnectAsync(IPAddress address, int port)
    {
        TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>(this);
        BeginConnect(address, port, delegate(IAsyncResult iar)
        {
            TaskCompletionSource<bool> taskCompletionSource2 = (TaskCompletionSource<bool>)iar.AsyncState;
            try
            {
                ((Socket)taskCompletionSource2.Task.AsyncState).EndConnect(iar);
                taskCompletionSource2.TrySetResult(result: true);
            }
            catch (Exception exception)
            {
                taskCompletionSource2.TrySetException(exception);
            }
        }, taskCompletionSource);
        return taskCompletionSource.Task;
    }

    internal Task ConnectAsync(IPAddress[] addresses, int port)
    {
        TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>(this);
        BeginConnect(addresses, port, delegate(IAsyncResult iar)
        {
            TaskCompletionSource<bool> taskCompletionSource2 = (TaskCompletionSource<bool>)iar.AsyncState;
            try
            {
                ((Socket)taskCompletionSource2.Task.AsyncState).EndConnect(iar);
                taskCompletionSource2.TrySetResult(result: true);
            }
            catch (Exception exception)
            {
                taskCompletionSource2.TrySetException(exception);
            }
        }, taskCompletionSource);
        return taskCompletionSource.Task;
    }

    internal Task ConnectAsync(string host, int port)
    {
        TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>(this);
        BeginConnect(host, port, delegate(IAsyncResult iar)
        {
            TaskCompletionSource<bool> taskCompletionSource2 = (TaskCompletionSource<bool>)iar.AsyncState;
            try
            {
                ((Socket)taskCompletionSource2.Task.AsyncState).EndConnect(iar);
                taskCompletionSource2.TrySetResult(result: true);
            }
            catch (Exception exception)
            {
                taskCompletionSource2.TrySetException(exception);
            }
        }, taskCompletionSource);
        return taskCompletionSource.Task;
    }

    internal Task<int> ReceiveAsync(ArraySegment<byte> buffer, SocketFlags socketFlags, bool fromNetworkStream)
    {
        ValidateBuffer(buffer);
        return ReceiveAsync(buffer, socketFlags, fromNetworkStream, default(CancellationToken)).AsTask();
    }

    internal ValueTask<int> ReceiveAsync(Memory<byte> buffer, SocketFlags socketFlags, bool fromNetworkStream,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<int>(TaskExEx.FromCanceled<int>(cancellationToken));
        }

        AwaitableSocketAsyncEventArgs awaitableSocketAsyncEventArgs =
            LazyInitializer.EnsureInitialized(ref EventArgs.ValueTaskReceive,
                () => new AwaitableSocketAsyncEventArgs());
        if (awaitableSocketAsyncEventArgs.Reserve())
        {
            awaitableSocketAsyncEventArgs.SetBuffer(buffer);
            awaitableSocketAsyncEventArgs.SocketFlags = socketFlags;
            awaitableSocketAsyncEventArgs.WrapExceptionsInIOExceptions = fromNetworkStream;
            return awaitableSocketAsyncEventArgs.ReceiveAsync(this, cancellationToken);
        }

        return new ValueTask<int>(ReceiveAsyncApm(buffer, socketFlags));
    }

    private Task<int> ReceiveAsyncApm(Memory<byte> buffer, SocketFlags socketFlags)
    {
        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> segment))
        {
            TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>(this);
            BeginReceive(segment.Array, segment.Offset, segment.Count, socketFlags, delegate(IAsyncResult iar)
            {
                TaskCompletionSource<int> taskCompletionSource3 = (TaskCompletionSource<int>)iar.AsyncState;
                try
                {
                    taskCompletionSource3.TrySetResult(
                        ((Socket)taskCompletionSource3.Task.AsyncState).EndReceive(iar));
                }
                catch (Exception exception2)
                {
                    taskCompletionSource3.TrySetException(exception2);
                }
            }, taskCompletionSource);
            return taskCompletionSource.Task;
        }

        byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
        TaskCompletionSource<int> taskCompletionSource2 = new TaskCompletionSource<int>(this);
        BeginReceive(array, 0, buffer.Length, socketFlags, delegate(IAsyncResult iar)
        {
            Tuple<TaskCompletionSource<int>, Memory<byte>, byte[]> tuple =
                (Tuple<TaskCompletionSource<int>, Memory<byte>, byte[]>)iar.AsyncState;
            try
            {
                int num = ((Socket)tuple.Item1.Task.AsyncState).EndReceive(iar);
                new ReadOnlyMemory<byte>(tuple.Item3, 0, num).Span.CopyTo(tuple.Item2.Span);
                tuple.Item1.TrySetResult(num);
            }
            catch (Exception exception)
            {
                tuple.Item1.TrySetException(exception);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tuple.Item3);
            }
        }, Tuple.Create(taskCompletionSource2, buffer, array));
        return taskCompletionSource2.Task;
    }

    internal Task<int> ReceiveAsync(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags)
    {
        ValidateBuffersList(buffers);
        Int32TaskSocketAsyncEventArgs int32TaskSocketAsyncEventArgs = RentSocketAsyncEventArgs(isReceive: true);
        if (int32TaskSocketAsyncEventArgs != null)
        {
            ConfigureBufferList(int32TaskSocketAsyncEventArgs, buffers, socketFlags);
            return GetTaskForSendReceive(ReceiveAsync(int32TaskSocketAsyncEventArgs), int32TaskSocketAsyncEventArgs,
                fromNetworkStream: false, isReceive: true);
        }

        return ReceiveAsyncApm(buffers, socketFlags);
    }

    private Task<int> ReceiveAsyncApm(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags)
    {
        TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>(this);
        BeginReceive(buffers, socketFlags, delegate(IAsyncResult iar)
        {
            TaskCompletionSource<int> taskCompletionSource2 = (TaskCompletionSource<int>)iar.AsyncState;
            try
            {
                taskCompletionSource2.TrySetResult(
                    ((Socket)taskCompletionSource2.Task.AsyncState).EndReceive(iar));
            }
            catch (Exception exception)
            {
                taskCompletionSource2.TrySetException(exception);
            }
        }, taskCompletionSource);
        return taskCompletionSource.Task;
    }

    internal Task<SocketReceiveFromResult> ReceiveFromAsync(ArraySegment<byte> buffer, SocketFlags socketFlags,
        EndPoint remoteEndPoint)
    {
        StateTaskCompletionSource<EndPoint, SocketReceiveFromResult> stateTaskCompletionSource =
            new StateTaskCompletionSource<EndPoint, SocketReceiveFromResult>(this)
            {
                _field1 = remoteEndPoint
            };
        BeginReceiveFrom(buffer.Array, buffer.Offset, buffer.Count, socketFlags, ref stateTaskCompletionSource._field1,
            delegate(IAsyncResult iar)
            {
                StateTaskCompletionSource<EndPoint, SocketReceiveFromResult> stateTaskCompletionSource2 =
                    (StateTaskCompletionSource<EndPoint, SocketReceiveFromResult>)iar.AsyncState;
                try
                {
                    int receivedBytes =
                        ((Socket)stateTaskCompletionSource2.Task.AsyncState).EndReceiveFrom(iar,
                            ref stateTaskCompletionSource2._field1);
                    stateTaskCompletionSource2.TrySetResult(new SocketReceiveFromResult
                    {
                        ReceivedBytes = receivedBytes,
                        RemoteEndPoint = stateTaskCompletionSource2._field1
                    });
                }
                catch (Exception exception)
                {
                    stateTaskCompletionSource2.TrySetException(exception);
                }
            }, stateTaskCompletionSource);
        return stateTaskCompletionSource.Task;
    }

    internal Task<SocketReceiveMessageFromResult> ReceiveMessageFromAsync(ArraySegment<byte> buffer,
        SocketFlags socketFlags, EndPoint remoteEndPoint)
    {
        StateTaskCompletionSource<SocketFlags, EndPoint, SocketReceiveMessageFromResult>
            stateTaskCompletionSource =
                new StateTaskCompletionSource<SocketFlags, EndPoint, SocketReceiveMessageFromResult>(this)
                {
                    _field1 = socketFlags,
                    _field2 = remoteEndPoint
                };
        BeginReceiveMessageFrom(buffer.Array, buffer.Offset, buffer.Count, socketFlags,
            ref stateTaskCompletionSource._field2, delegate(IAsyncResult iar)
            {
                StateTaskCompletionSource<SocketFlags, EndPoint, SocketReceiveMessageFromResult>
                    stateTaskCompletionSource2 =
                        (StateTaskCompletionSource<SocketFlags, EndPoint, SocketReceiveMessageFromResult>)iar
                            .AsyncState;
                try
                {
                    IPPacketInformation ipPacketInformation;
                    int receivedBytes =
                        ((Socket)stateTaskCompletionSource2.Task.AsyncState).EndReceiveMessageFrom(iar,
                            ref stateTaskCompletionSource2._field1, ref stateTaskCompletionSource2._field2,
                            out ipPacketInformation);
                    stateTaskCompletionSource2.TrySetResult(new SocketReceiveMessageFromResult
                    {
                        ReceivedBytes = receivedBytes,
                        RemoteEndPoint = stateTaskCompletionSource2._field2,
                        SocketFlags = stateTaskCompletionSource2._field1,
                        PacketInformation = ipPacketInformation
                    });
                }
                catch (Exception exception)
                {
                    stateTaskCompletionSource2.TrySetException(exception);
                }
            }, stateTaskCompletionSource);
        return stateTaskCompletionSource.Task;
    }

    internal Task<int> SendAsync(ArraySegment<byte> buffer, SocketFlags socketFlags)
    {
        ValidateBuffer(buffer);
        return SendAsync(buffer, socketFlags, default(CancellationToken)).AsTask();
    }

    internal ValueTask<int> SendAsync(ReadOnlyMemory<byte> buffer, SocketFlags socketFlags,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask<int>(TaskExEx.FromCanceled<int>(cancellationToken));
        }

        AwaitableSocketAsyncEventArgs awaitableSocketAsyncEventArgs =
            LazyInitializer.EnsureInitialized(ref EventArgs.ValueTaskSend, () => new AwaitableSocketAsyncEventArgs());
        if (awaitableSocketAsyncEventArgs.Reserve())
        {
            awaitableSocketAsyncEventArgs.SetBuffer(MemoryMarshal.AsMemory(buffer));
            awaitableSocketAsyncEventArgs.SocketFlags = socketFlags;
            awaitableSocketAsyncEventArgs.WrapExceptionsInIOExceptions = false;
            return awaitableSocketAsyncEventArgs.SendAsync(this, cancellationToken);
        }

        return new ValueTask<int>(SendAsyncApm(buffer, socketFlags));
    }

    internal ValueTask SendAsyncForNetworkStream(ReadOnlyMemory<byte> buffer, SocketFlags socketFlags,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return new ValueTask(TaskExEx.FromCanceled(cancellationToken));
        }

        AwaitableSocketAsyncEventArgs awaitableSocketAsyncEventArgs =
            LazyInitializer.EnsureInitialized(ref EventArgs.ValueTaskSend, () => new AwaitableSocketAsyncEventArgs());
        if (awaitableSocketAsyncEventArgs.Reserve())
        {
            awaitableSocketAsyncEventArgs.SetBuffer(MemoryMarshal.AsMemory(buffer));
            awaitableSocketAsyncEventArgs.SocketFlags = socketFlags;
            awaitableSocketAsyncEventArgs.WrapExceptionsInIOExceptions = true;
            return awaitableSocketAsyncEventArgs.SendAsyncForNetworkStream(this, cancellationToken);
        }

        return new ValueTask(SendAsyncApm(buffer, socketFlags));
    }

    private Task<int> SendAsyncApm(ReadOnlyMemory<byte> buffer, SocketFlags socketFlags)
    {
        if (MemoryMarshal.TryGetArray(buffer, out var segment))
        {
            TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>(this);
            BeginSend(segment.Array, segment.Offset, segment.Count, socketFlags, delegate(IAsyncResult iar)
            {
                TaskCompletionSource<int> taskCompletionSource3 = (TaskCompletionSource<int>)iar.AsyncState;
                try
                {
                    taskCompletionSource3.TrySetResult(
                        ((Socket)taskCompletionSource3.Task.AsyncState).EndSend(iar));
                }
                catch (Exception exception2)
                {
                    taskCompletionSource3.TrySetException(exception2);
                }
            }, taskCompletionSource);
            return taskCompletionSource.Task;
        }

        byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
        buffer.Span.CopyTo(array);
        TaskCompletionSource<int> taskCompletionSource2 = new TaskCompletionSource<int>(this);
        BeginSend(array, 0, buffer.Length, socketFlags, delegate(IAsyncResult iar)
        {
            Tuple<TaskCompletionSource<int>, byte[]> tuple = (Tuple<TaskCompletionSource<int>, byte[]>)iar.AsyncState;
            try
            {
                tuple.Item1.TrySetResult(((Socket)tuple.Item1.Task.AsyncState).EndSend(iar));
            }
            catch (Exception exception)
            {
                tuple.Item1.TrySetException(exception);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(tuple.Item2);
            }
        }, Tuple.Create(taskCompletionSource2, array));
        return taskCompletionSource2.Task;
    }

    internal Task<int> SendAsync(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags)
    {
        ValidateBuffersList(buffers);
        Int32TaskSocketAsyncEventArgs int32TaskSocketAsyncEventArgs = RentSocketAsyncEventArgs(isReceive: false);
        if (int32TaskSocketAsyncEventArgs != null)
        {
            ConfigureBufferList(int32TaskSocketAsyncEventArgs, buffers, socketFlags);
            return GetTaskForSendReceive(SendAsync(int32TaskSocketAsyncEventArgs), int32TaskSocketAsyncEventArgs,
                fromNetworkStream: false, isReceive: false);
        }

        return SendAsyncApm(buffers, socketFlags);
    }

    private Task<int> SendAsyncApm(IList<ArraySegment<byte>> buffers, SocketFlags socketFlags)
    {
        TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>(this);
        BeginSend(buffers, socketFlags, delegate(IAsyncResult iar)
        {
            TaskCompletionSource<int> taskCompletionSource2 = (TaskCompletionSource<int>)iar.AsyncState;
            try
            {
                taskCompletionSource2.TrySetResult(((Socket)taskCompletionSource2.Task.AsyncState).EndSend(iar));
            }
            catch (Exception exception)
            {
                taskCompletionSource2.TrySetException(exception);
            }
        }, taskCompletionSource);
        return taskCompletionSource.Task;
    }

    internal Task<int> SendToAsync(ArraySegment<byte> buffer, SocketFlags socketFlags, EndPoint remoteEP)
    {
        TaskCompletionSource<int> taskCompletionSource = new TaskCompletionSource<int>(this);
        BeginSendTo(buffer.Array, buffer.Offset, buffer.Count, socketFlags, remoteEP, delegate(IAsyncResult iar)
        {
            TaskCompletionSource<int> taskCompletionSource2 = (TaskCompletionSource<int>)iar.AsyncState;
            try
            {
                taskCompletionSource2.TrySetResult(((Socket)taskCompletionSource2.Task.AsyncState)
                    .EndSendTo(iar));
            }
            catch (Exception exception)
            {
                taskCompletionSource2.TrySetException(exception);
            }
        }, taskCompletionSource);
        return taskCompletionSource.Task;
    }

    private static void ValidateBuffer(ArraySegment<byte> buffer)
    {
        if (buffer.Array == null)
        {
            throw new ArgumentNullException("Array");
        }

        if (buffer.Offset < 0 || buffer.Offset > buffer.Array.Length)
        {
            throw new ArgumentOutOfRangeException("Offset");
        }

        if (buffer.Count < 0 || buffer.Count > buffer.Array.Length - buffer.Offset)
        {
            throw new ArgumentOutOfRangeException("Count");
        }
    }

    private static void ValidateBuffersList(IList<ArraySegment<byte>> buffers)
    {
        if (buffers == null)
        {
            throw new ArgumentNullException("buffers");
        }

        if (buffers.Count == 0)
        {
            throw new ArgumentException(SR.Format(SR.net_sockets_zerolist, "buffers"), "buffers");
        }
    }

    private static void ConfigureBufferList(Int32TaskSocketAsyncEventArgs saea, IList<ArraySegment<byte>> buffers,
        SocketFlags socketFlags)
    {
        if (!saea.MemoryBuffer.Equals(default(Memory<byte>)))
        {
            saea.SetBuffer(default(Memory<byte>));
        }

        saea.BufferList = buffers;
        saea.SocketFlags = socketFlags;
    }

    private Task<int> GetTaskForSendReceive(bool pending, Int32TaskSocketAsyncEventArgs saea, bool fromNetworkStream,
        bool isReceive)
    {
        Task<int> result;
        if (pending)
        {
            result = saea.GetCompletionResponsibility(out var responsibleForReturningToPool).Task;
            if (responsibleForReturningToPool)
            {
                ReturnSocketAsyncEventArgs(saea, isReceive);
            }
        }
        else
        {
            if (saea.SocketError == SocketError.Success)
            {
                int bytesTransferred = saea.BytesTransferred;
                result = ((bytesTransferred != 0 && !(fromNetworkStream && !isReceive))
                    ? TaskExEx.FromResult(bytesTransferred)
                    : s_zeroTask);
            }
            else
            {
                result = TaskExEx.FromException<int>(GetException(saea.SocketError, fromNetworkStream));
            }

            ReturnSocketAsyncEventArgs(saea, isReceive);
        }

        return result;
    }

    private static void CompleteAccept(Socket s, TaskSocketAsyncEventArgs<Socket> saea)
    {
        SocketError socketError = saea.SocketError;
        Socket acceptSocket = saea.AcceptSocket;
        bool responsibleForReturningToPool;
        AsyncTaskMethodBuilder<Socket> completionResponsibility =
            saea.GetCompletionResponsibility(out responsibleForReturningToPool);
        if (responsibleForReturningToPool)
        {
            s.ReturnSocketAsyncEventArgs(saea);
        }

        if (socketError == SocketError.Success)
        {
            completionResponsibility.SetResult(acceptSocket);
        }
        else
        {
            completionResponsibility.SetException(GetException(socketError));
        }
    }

    private static void CompleteSendReceive(Socket s, Int32TaskSocketAsyncEventArgs saea, bool isReceive)
    {
        SocketError socketError = saea.SocketError;
        int bytesTransferred = saea.BytesTransferred;
        bool wrapExceptionsInIOExceptions = saea._wrapExceptionsInIOExceptions;
        bool responsibleForReturningToPool;
        AsyncTaskMethodBuilder<int> completionResponsibility =
            saea.GetCompletionResponsibility(out responsibleForReturningToPool);
        if (responsibleForReturningToPool)
        {
            s.ReturnSocketAsyncEventArgs(saea, isReceive);
        }

        if (socketError == SocketError.Success)
        {
            completionResponsibility.SetResult(bytesTransferred);
        }
        else
        {
            completionResponsibility.SetException(GetException(socketError, wrapExceptionsInIOExceptions));
        }
    }

    private static Exception GetException(SocketError error, bool wrapExceptionsInIOExceptions = false)
    {
        Exception ex = new SocketException((int)error);
        if (!wrapExceptionsInIOExceptions)
        {
            return ex;
        }

        return new IOException(SR.Format(SR.net_io_readwritefailure, ex.Message), ex);
    }

    private Int32TaskSocketAsyncEventArgs RentSocketAsyncEventArgs(bool isReceive)
    {
        CachedEventArgs eventArgs = EventArgs;
        Int32TaskSocketAsyncEventArgs int32TaskSocketAsyncEventArgs = (isReceive
            ? Interlocked.Exchange(ref eventArgs.TaskReceive, s_rentedInt32Sentinel)
            : Interlocked.Exchange(ref eventArgs.TaskSend, s_rentedInt32Sentinel));
        if (int32TaskSocketAsyncEventArgs == s_rentedInt32Sentinel)
        {
            return null;
        }

        if (int32TaskSocketAsyncEventArgs == null)
        {
            int32TaskSocketAsyncEventArgs = new Int32TaskSocketAsyncEventArgs();
            int32TaskSocketAsyncEventArgs.Completed += (isReceive ? ReceiveCompletedHandler : SendCompletedHandler);
        }

        return int32TaskSocketAsyncEventArgs;
    }

    private void ReturnSocketAsyncEventArgs(Int32TaskSocketAsyncEventArgs saea, bool isReceive)
    {
        saea._accessed = false;
        saea._builder = default(AsyncTaskMethodBuilder<int>);
        saea._wrapExceptionsInIOExceptions = false;
        if (isReceive)
        {
            Volatile.Write(ref _cachedTaskEventArgs.TaskReceive, saea);
        }
        else
        {
            Volatile.Write(ref _cachedTaskEventArgs.TaskSend, saea);
        }
    }

    private void ReturnSocketAsyncEventArgs(TaskSocketAsyncEventArgs<Socket> saea)
    {
        saea.AcceptSocket = null;
        saea._accessed = false;
        saea._builder = default(AsyncTaskMethodBuilder<Socket>);
        Volatile.Write(ref _cachedTaskEventArgs.TaskAccept, saea);
    }

    private void DisposeCachedTaskSocketAsyncEventArgs()
    {
        CachedEventArgs cachedTaskEventArgs = _cachedTaskEventArgs;
        if (cachedTaskEventArgs != null)
        {
            Interlocked.Exchange(ref cachedTaskEventArgs.TaskAccept, s_rentedSocketSentinel)?.Dispose();
            Interlocked.Exchange(ref cachedTaskEventArgs.TaskReceive, s_rentedInt32Sentinel)?.Dispose();
            Interlocked.Exchange(ref cachedTaskEventArgs.TaskSend, s_rentedInt32Sentinel)?.Dispose();
            Interlocked.Exchange(ref cachedTaskEventArgs.ValueTaskReceive, AwaitableSocketAsyncEventArgs.Reserved)
                ?.Dispose();
            Interlocked.Exchange(ref cachedTaskEventArgs.ValueTaskSend, AwaitableSocketAsyncEventArgs.Reserved)
                ?.Dispose();
        }
    }

    internal void ReplaceHandleIfNecessaryAfterFailedConnect()
    {
    }

    private void EnsureDynamicWinsockMethods()
    {
        if (_dynamicWinsockMethods == null)
        {
            _dynamicWinsockMethods = DynamicWinsockMethods.GetMethods(_addressFamily, _socketType, _protocolType);
        }
    }

    internal unsafe bool AcceptEx(SafeSocketHandle listenSocketHandle, SafeSocketHandle acceptSocketHandle,
        IntPtr buffer, int len, int localAddressLength, int remoteAddressLength, out int bytesReceived,
        NativeOverlapped* overlapped)
    {
        EnsureDynamicWinsockMethods();
        AcceptExDelegate @delegate = _dynamicWinsockMethods.GetDelegate<AcceptExDelegate>(listenSocketHandle);
        return @delegate(listenSocketHandle, acceptSocketHandle, buffer, len, localAddressLength, remoteAddressLength,
            out bytesReceived, overlapped);
    }

    internal void GetAcceptExSockaddrs(IntPtr buffer, int receiveDataLength, int localAddressLength,
        int remoteAddressLength, out IntPtr localSocketAddress, out int localSocketAddressLength,
        out IntPtr remoteSocketAddress, out int remoteSocketAddressLength)
    {
        EnsureDynamicWinsockMethods();
        GetAcceptExSockaddrsDelegate @delegate =
            _dynamicWinsockMethods.GetDelegate<GetAcceptExSockaddrsDelegate>(_handle);
        @delegate(buffer, receiveDataLength, localAddressLength, remoteAddressLength, out localSocketAddress,
            out localSocketAddressLength, out remoteSocketAddress, out remoteSocketAddressLength);
    }

    internal unsafe bool DisconnectEx(SafeSocketHandle socketHandle, NativeOverlapped* overlapped, int flags,
        int reserved)
    {
        EnsureDynamicWinsockMethods();
        DisconnectExDelegate @delegate = _dynamicWinsockMethods.GetDelegate<DisconnectExDelegate>(socketHandle);
        return @delegate(socketHandle, overlapped, flags, reserved);
    }

    internal bool DisconnectExBlocking(SafeSocketHandle socketHandle, IntPtr overlapped, int flags, int reserved)
    {
        EnsureDynamicWinsockMethods();
        DisconnectExDelegateBlocking @delegate =
            _dynamicWinsockMethods.GetDelegate<DisconnectExDelegateBlocking>(socketHandle);
        return @delegate(socketHandle, overlapped, flags, reserved);
    }

    internal unsafe bool ConnectEx(SafeSocketHandle socketHandle, IntPtr socketAddress, int socketAddressSize,
        IntPtr buffer, int dataLength, out int bytesSent, NativeOverlapped* overlapped)
    {
        EnsureDynamicWinsockMethods();
        ConnectExDelegate @delegate = _dynamicWinsockMethods.GetDelegate<ConnectExDelegate>(socketHandle);
        return @delegate(socketHandle, socketAddress, socketAddressSize, buffer, dataLength, out bytesSent, overlapped);
    }

    internal unsafe SocketError WSARecvMsg(SafeSocketHandle socketHandle, IntPtr msg, out int bytesTransferred,
        NativeOverlapped* overlapped, IntPtr completionRoutine)
    {
        EnsureDynamicWinsockMethods();
        WSARecvMsgDelegate @delegate = _dynamicWinsockMethods.GetDelegate<WSARecvMsgDelegate>(socketHandle);
        return @delegate(socketHandle, msg, out bytesTransferred, overlapped, completionRoutine);
    }

    internal SocketError WSARecvMsgBlocking(IntPtr socketHandle, IntPtr msg, out int bytesTransferred,
        IntPtr overlapped, IntPtr completionRoutine)
    {
        EnsureDynamicWinsockMethods();
        WSARecvMsgDelegateBlocking @delegate = _dynamicWinsockMethods.GetDelegate<WSARecvMsgDelegateBlocking>(_handle);
        return @delegate(socketHandle, msg, out bytesTransferred, overlapped, completionRoutine);
    }

    internal unsafe bool TransmitPackets(SafeSocketHandle socketHandle, IntPtr packetArray, int elementCount,
        int sendSize, NativeOverlapped* overlapped, TransmitFileOptions flags)
    {
        EnsureDynamicWinsockMethods();
        TransmitPacketsDelegate @delegate = _dynamicWinsockMethods.GetDelegate<TransmitPacketsDelegate>(socketHandle);
        return @delegate(socketHandle, packetArray, elementCount, sendSize, overlapped, flags);
    }

    internal static void SocketListToFileDescriptorSet(IList socketList, Span<IntPtr> fileDescriptorSet)
    {
        int count;
        if (socketList == null || (count = socketList.Count) == 0)
        {
            return;
        }

        fileDescriptorSet[0] = (IntPtr)count;
        for (int i = 0; i < count; i++)
        {
            if (!(socketList[i] is Socket))
            {
                throw new ArgumentException(
                    SR.Format(SR.net_sockets_select, socketList[i].GetType().FullName,
                        typeof(Socket).FullName), "socketList");
            }

            fileDescriptorSet[i + 1] = ((Socket)socketList[i])._handle.DangerousGetHandle();
        }
    }

    internal static void SelectFileDescriptor(IList socketList, Span<IntPtr> fileDescriptorSet)
    {
        int num;
        if (socketList == null || (num = socketList.Count) == 0)
        {
            return;
        }

        int num2 = (int)fileDescriptorSet[0];
        if (num2 == 0)
        {
            socketList.Clear();
            return;
        }

        lock (socketList)
        {
            for (int i = 0; i < num; i++)
            {
                Socket socket = socketList[i] as Socket;
                int j;
                for (j = 0; j < num2 && !(fileDescriptorSet[j + 1] == socket._handle.DangerousGetHandle()); j++)
                {
                }

                if (j == num2)
                {
                    socketList.RemoveAt(i--);
                    num--;
                }
            }
        }
    }

    private Socket GetOrCreateAcceptSocket(Socket acceptSocket, bool checkDisconnected, string propertyName,
        out SafeSocketHandle handle)
    {
        if (acceptSocket == null)
        {
            acceptSocket = new Socket(_addressFamily, _socketType, _protocolType);
        }
        else if (acceptSocket._rightEndPoint != null && (!checkDisconnected || !acceptSocket._isDisconnected))
        {
            throw new InvalidOperationException(SR.Format(SR.net_sockets_namedmustnotbebound,
                propertyName));
        }

        handle = acceptSocket._handle;
        return acceptSocket;
    }

    private void SendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
    {
        FileStream fileStream = OpenFile(fileName);
        SocketError socketError;
        using (fileStream)
        {
            SafeFileHandle fileHandle = fileStream?.SafeFileHandle;
            socketError = SocketPal.SendFile(_handle, fileHandle, preBuffer, postBuffer, flags);
        }

        if (socketError != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException(socketError);
        }

        if ((flags & (TransmitFileOptions.Disconnect | TransmitFileOptions.ReuseSocket)) != 0)
        {
            SetToDisconnected();
            _remoteEndPoint = null;
        }
    }

    private IAsyncResult BeginSendFileInternal(string fileName, byte[] preBuffer, byte[] postBuffer,
        TransmitFileOptions flags, AsyncCallback callback, object state)
    {
        FileStream fileStream = OpenFile(fileName);
        TransmitFileAsyncResult transmitFileAsyncResult = new TransmitFileAsyncResult(this, state, callback);
        transmitFileAsyncResult.StartPostingAsyncOp(lockCapture: false);
        SocketError errorCode =
            SocketPal.SendFileAsync(_handle, fileStream, preBuffer, postBuffer, flags, transmitFileAsyncResult);
        if (!CheckErrorAndUpdateStatus(errorCode))
        {
            throw new SocketException((int)errorCode);
        }

        transmitFileAsyncResult.FinishPostingAsyncOp(ref Caches.SendClosureCache);
        return transmitFileAsyncResult;
    }

    private void EndSendFileInternal(IAsyncResult asyncResult)
    {
        if (!(asyncResult is TransmitFileAsyncResult transmitFileAsyncResult) ||
            transmitFileAsyncResult.AsyncObject != this)
        {
            throw new ArgumentException(SR.net_io_invalidasyncresult, "asyncResult");
        }

        if (transmitFileAsyncResult.EndCalled)
        {
            throw new InvalidOperationException(SR.Format(SR.net_io_invalidendcall, "EndSendFile"));
        }

        transmitFileAsyncResult.InternalWaitForCompletion();
        transmitFileAsyncResult.EndCalled = true;
        if (transmitFileAsyncResult.DoDisconnect)
        {
            SetToDisconnected();
            _remoteEndPoint = null;
        }

        if (transmitFileAsyncResult.ErrorCode != 0)
        {
            UpdateStatusAfterSocketErrorAndThrowException((SocketError)transmitFileAsyncResult.ErrorCode);
        }
    }

    internal ThreadPoolBoundHandle GetOrAllocateThreadPoolBoundHandle()
    {
        return _handle.GetThreadPoolBoundHandle() ?? GetOrAllocateThreadPoolBoundHandleSlow();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal ThreadPoolBoundHandle GetOrAllocateThreadPoolBoundHandleSlow()
    {
        bool trySkipCompletionPortOnSuccess =
            !CompletionPortHelper.PlatformHasUdpIssue || _protocolType != ProtocolType.Udp;
        return _handle.GetOrAllocateThreadPoolBoundHandle(trySkipCompletionPortOnSuccess);
    }
}