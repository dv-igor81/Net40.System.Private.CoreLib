using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Sockets.Net40;

using EndPoint = System.Net.Net40.EndPoint;
using IPAddress = Net.Net40.IPAddress;

public class SocketAsyncEventArgs : EventArgs, IDisposable
{
    private enum SingleBufferHandleState : byte
    {
        None,
        InProcess,
        Set
    }

    private enum PinState : byte
    {
        None,
        MultipleBuffer,
        SendPackets
    }

    private Socket _acceptSocket;

    private Socket _connectSocket;

    private Memory<byte> _buffer;

    private int _offset;

    private int _count;

    private bool _bufferIsExplicitArray;

    private IList<ArraySegment<byte>> _bufferList;

    private List<ArraySegment<byte>> _bufferListInternal;

    private int _bytesTransferred;

    private bool _disconnectReuseSocket;

    private SocketAsyncOperation _completedOperation;

    private IPPacketInformation _receiveMessageFromPacketInfo;

    private EndPoint _remoteEndPoint;

    private int _sendPacketsSendSize;

    private SendPacketsElement[] _sendPacketsElements;

    private TransmitFileOptions _sendPacketsFlags;

    private SocketError _socketError;

    private Exception _connectByNameError;

    private SocketFlags _socketFlags;

    private object _userToken;

    private byte[] _acceptBuffer;

    private int _acceptAddressBufferCount;

    internal Internals.SocketAddress _socketAddress;

    private readonly bool _flowExecutionContext;

    private ExecutionContext _context;

    private static readonly ContextCallback s_executionCallback = ExecutionCallback;

    private Socket _currentSocket;

    private bool _userSocket;

    private bool _disposeCalled;

    private int _operating;

    private MultipleConnectAsync _multipleConnect;

    private MemoryHandle _singleBufferHandle;

    private volatile SingleBufferHandleState _singleBufferHandleState;

    private WSABuffer[] _wsaBufferArray;

    private GCHandle[] _multipleBufferGCHandles;

    private byte[] _wsaMessageBuffer;

    private GCHandle _wsaMessageBufferGCHandle;

    private byte[] _controlBuffer;

    private GCHandle _controlBufferGCHandle;

    private WSABuffer[] _wsaRecvMsgWSABufferArray;

    private GCHandle _wsaRecvMsgWSABufferArrayGCHandle;

    private GCHandle _socketAddressGCHandle;

    private Internals.SocketAddress _pinnedSocketAddress;

    private FileStream[] _sendPacketsFileStreams;

    private PreAllocatedOverlapped _preAllocatedOverlapped;

    private readonly StrongBox<SocketAsyncEventArgs> _strongThisRef = new StrongBox<SocketAsyncEventArgs>();

    private CancellationTokenRegistration _registrationToCancelPendingIO;

    private unsafe NativeOverlapped* _pendingOverlappedForCancellation;

    private PinState _pinState;

    private static readonly unsafe IOCompletionCallback s_completionPortCallback =
        delegate(uint errorCode, uint numBytes, NativeOverlapped* nativeOverlapped)
        {
            StrongBox<SocketAsyncEventArgs> strongBox =
                (StrongBox<SocketAsyncEventArgs>)ThreadPoolBoundHandle.GetNativeOverlappedState(nativeOverlapped);
            SocketAsyncEventArgs value = strongBox.Value;
            if (errorCode == 0)
            {
                value.FreeNativeOverlapped(nativeOverlapped);
                value.FinishOperationAsyncSuccess((int)numBytes, SocketFlags.None);
            }
            else
            {
                value.HandleCompletionPortCallbackError(errorCode, numBytes, nativeOverlapped);
            }
        };

    public Socket AcceptSocket
    {
        get { return _acceptSocket; }
        set { _acceptSocket = value; }
    }

    public Socket ConnectSocket => _connectSocket;

    public byte[] Buffer
    {
        get
        {
            if (_bufferIsExplicitArray)
            {
                ArraySegment<byte> segment;
                bool flag = MemoryMarshal.TryGetArray(_buffer, out segment);
                return segment.Array;
            }

            return null;
        }
    }

    public Memory<byte> MemoryBuffer => _buffer;

    public int Offset => _offset;

    public int Count => _count;

    public TransmitFileOptions SendPacketsFlags
    {
        get { return _sendPacketsFlags; }
        set { _sendPacketsFlags = value; }
    }

    public IList<ArraySegment<byte>> BufferList
    {
        get { return _bufferList; }
        set
        {
            StartConfiguring();
            try
            {
                if (value != null)
                {
                    if (!_buffer.Equals(default(Memory<byte>)))
                    {
                        throw new ArgumentException(SR.Format(SR.net_ambiguousbuffers, "Buffer"));
                    }

                    int count = value.Count;
                    if (_bufferListInternal == null)
                    {
                        _bufferListInternal = new List<ArraySegment<byte>>(count);
                    }
                    else
                    {
                        _bufferListInternal.Clear();
                    }

                    for (int i = 0; i < count; i++)
                    {
                        ArraySegment<byte> arraySegment = value[i];
                        RangeValidationHelpers.ValidateSegment(arraySegment);
                        _bufferListInternal.Add(arraySegment);
                    }
                }
                else
                {
                    _bufferListInternal?.Clear();
                }

                _bufferList = value;
                SetupMultipleBuffers();
            }
            finally
            {
                Complete();
            }
        }
    }

    public int BytesTransferred => _bytesTransferred;

    public bool DisconnectReuseSocket
    {
        get { return _disconnectReuseSocket; }
        set { _disconnectReuseSocket = value; }
    }

    public SocketAsyncOperation LastOperation => _completedOperation;

    public IPPacketInformation ReceiveMessageFromPacketInfo => _receiveMessageFromPacketInfo;

    public EndPoint RemoteEndPoint
    {
        get { return _remoteEndPoint; }
        set { _remoteEndPoint = value; }
    }

    public SendPacketsElement[] SendPacketsElements
    {
        get { return _sendPacketsElements; }
        set
        {
            StartConfiguring();
            try
            {
                _sendPacketsElements = value;
            }
            finally
            {
                Complete();
            }
        }
    }

    public int SendPacketsSendSize
    {
        get { return _sendPacketsSendSize; }
        set { _sendPacketsSendSize = value; }
    }

    public SocketError SocketError
    {
        get { return _socketError; }
        set { _socketError = value; }
    }

    public Exception ConnectByNameError => _connectByNameError;

    public SocketFlags SocketFlags
    {
        get { return _socketFlags; }
        set { _socketFlags = value; }
    }

    public object UserToken
    {
        get { return _userToken; }
        set { _userToken = value; }
    }

    internal bool HasMultipleBuffers => _bufferList != null;

    private unsafe IntPtr PtrSocketAddressBuffer
    {
        get
        {
            fixed (byte* ptr = &_pinnedSocketAddress.Buffer[0])
            {
                void* ptr2 = ptr;
                return (IntPtr)ptr2;
            }
        }
    }

    private IntPtr PtrSocketAddressBufferSize => PtrSocketAddressBuffer + _socketAddress.GetAddressSizeOffset();

    public event EventHandler<SocketAsyncEventArgs> Completed;

    public SocketAsyncEventArgs()
        : this(flowExecutionContext: true)
    {
    }

    internal SocketAsyncEventArgs(bool flowExecutionContext)
    {
        _flowExecutionContext = flowExecutionContext;
        InitializeInternals();
    }

    protected virtual void OnCompleted(SocketAsyncEventArgs e)
    {
        Completed?.Invoke(e._currentSocket, e);
    }

    public void SetBuffer(int offset, int count)
    {
        StartConfiguring();
        try
        {
            if (!_buffer.Equals(default(Memory<byte>)))
            {
                if ((uint)offset > _buffer.Length)
                {
                    throw new ArgumentOutOfRangeException("offset");
                }

                if ((uint)count > _buffer.Length - offset)
                {
                    throw new ArgumentOutOfRangeException("count");
                }

                if (!_bufferIsExplicitArray)
                {
                    throw new InvalidOperationException(SR.InvalidOperation_BufferNotExplicitArray);
                }

                _offset = offset;
                _count = count;
            }
        }
        finally
        {
            Complete();
        }
    }

    internal void CopyBufferFrom(SocketAsyncEventArgs source)
    {
        StartConfiguring();
        try
        {
            _buffer = source._buffer;
            _offset = source._offset;
            _count = source._count;
            _bufferIsExplicitArray = source._bufferIsExplicitArray;
        }
        finally
        {
            Complete();
        }
    }

    public void SetBuffer(byte[] buffer, int offset, int count)
    {
        StartConfiguring();
        try
        {
            if (buffer == null)
            {
                _buffer = default(Memory<byte>);
                _offset = 0;
                _count = 0;
                _bufferIsExplicitArray = false;
                return;
            }

            if (_bufferList != null)
            {
                throw new ArgumentException(SR.Format(SR.net_ambiguousbuffers, "BufferList"));
            }

            if ((uint)offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            if ((uint)count > buffer.Length - offset)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            _buffer = buffer;
            _offset = offset;
            _count = count;
            _bufferIsExplicitArray = true;
        }
        finally
        {
            Complete();
        }
    }

    public void SetBuffer(Memory<byte> buffer)
    {
        StartConfiguring();
        try
        {
            if (buffer.Length != 0 && _bufferList != null)
            {
                throw new ArgumentException(SR.Format(SR.net_ambiguousbuffers, "BufferList"));
            }

            _buffer = buffer;
            _offset = 0;
            _count = buffer.Length;
            _bufferIsExplicitArray = false;
        }
        finally
        {
            Complete();
        }
    }

    internal void SetResults(SocketError socketError, int bytesTransferred, SocketFlags flags)
    {
        _socketError = socketError;
        _connectByNameError = null;
        _bytesTransferred = bytesTransferred;
        _socketFlags = flags;
    }

    internal void SetResults(Exception exception, int bytesTransferred, SocketFlags flags)
    {
        _connectByNameError = exception;
        _bytesTransferred = bytesTransferred;
        _socketFlags = flags;
        if (exception == null)
        {
            _socketError = SocketError.Success;
        }
        else if (exception is SocketException ex)
        {
            _socketError = ex.SocketErrorCode;
        }
        else
        {
            _socketError = SocketError.SocketError;
        }
    }

    private static void ExecutionCallback(object state)
    {
        SocketAsyncEventArgs socketAsyncEventArgs = (SocketAsyncEventArgs)state;
        socketAsyncEventArgs.OnCompleted(socketAsyncEventArgs);
    }

    internal void Complete()
    {
        CompleteCore();
        _context = null;
        _operating = 0;
        if (_disposeCalled)
        {
            Dispose();
        }
    }

    public void Dispose()
    {
        _disposeCalled = true;
        if (Interlocked.CompareExchange(ref _operating, 2, 0) == 0)
        {
            FreeInternals();
            FinishOperationSendPackets();
            GC.SuppressFinalize(this);
        }
    }

    ~SocketAsyncEventArgs()
    {
        if (!Environment.HasShutdownStarted)
        {
            FreeInternals();
        }
    }

    private void StartConfiguring()
    {
        int num = Interlocked.CompareExchange(ref _operating, -1, 0);
        if (num != 0)
        {
            ThrowForNonFreeStatus(num);
        }
    }

    private void ThrowForNonFreeStatus(int status)
    {
        if (status == 2)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        throw new InvalidOperationException(SR.net_socketopinprogress);
    }

    internal void StartOperationCommon(Socket socket, SocketAsyncOperation operation)
    {
        int num = Interlocked.CompareExchange(ref _operating, 1, 0);
        if (num != 0)
        {
            ThrowForNonFreeStatus(num);
        }

        _completedOperation = operation;
        _currentSocket = socket;
        if (_flowExecutionContext)
        {
            _context = ExecutionContext.Capture();
        }

        StartOperationCommonCore();
    }

    private void StartOperationCommonCore()
    {
        _strongThisRef.Value = this;
    }

    internal void StartOperationAccept()
    {
        _acceptAddressBufferCount = 2 * (Socket.GetAddressSize(_currentSocket._rightEndPoint) + 16);
        if (!_buffer.Equals(default(Memory<byte>)))
        {
            if (_count < _acceptAddressBufferCount)
            {
                throw new ArgumentException(SR.Format(SR.net_buffercounttoosmall, "Count"));
            }
        }
        else if (_acceptBuffer == null || _acceptBuffer.Length < _acceptAddressBufferCount)
        {
            _acceptBuffer = new byte[_acceptAddressBufferCount];
        }
    }

    internal void StartOperationConnect(MultipleConnectAsync multipleConnect, bool userSocket)
    {
        _multipleConnect = multipleConnect;
        _connectSocket = null;
        _userSocket = userSocket;
    }

    internal void CancelConnectAsync()
    {
        if (_operating != 1 || _completedOperation != SocketAsyncOperation.Connect)
        {
            return;
        }

        if (_multipleConnect != null)
        {
            _multipleConnect.Cancel();
            return;
        }

        if (_currentSocket == null)
        {
            NetEventSource.Fail(this, "CurrentSocket and MultipleConnect both null!");
        }

        _currentSocket.Dispose();
    }

    internal void FinishOperationSyncFailure(SocketError socketError, int bytesTransferred, SocketFlags flags)
    {
        SetResults(socketError, bytesTransferred, flags);
        Socket currentSocket = _currentSocket;
        if (currentSocket != null)
        {
            currentSocket.UpdateStatusAfterSocketError(socketError);
            if (_completedOperation == SocketAsyncOperation.Connect && !_userSocket)
            {
                currentSocket.Dispose();
                _currentSocket = null;
            }
        }

        SocketAsyncOperation completedOperation = _completedOperation;
        if (completedOperation == SocketAsyncOperation.SendPackets)
        {
            FinishOperationSendPackets();
        }

        Complete();
    }

    internal void FinishConnectByNameSyncFailure(Exception exception, int bytesTransferred, SocketFlags flags)
    {
        SetResults(exception, bytesTransferred, flags);
        _currentSocket?.UpdateStatusAfterSocketError(_socketError);
        Complete();
    }

    internal void FinishOperationAsyncFailure(SocketError socketError, int bytesTransferred, SocketFlags flags)
    {
        ExecutionContext context = _context;
        FinishOperationSyncFailure(socketError, bytesTransferred, flags);
        if (context == null)
        {
            OnCompleted(this);
        }
        else
        {
            ExecutionContext.Run(context, s_executionCallback, this);
        }
    }

    internal void FinishConnectByNameAsyncFailure(Exception exception, int bytesTransferred, SocketFlags flags)
    {
        ExecutionContext context = _context;
        FinishConnectByNameSyncFailure(exception, bytesTransferred, flags);
        if (context == null)
        {
            OnCompleted(this);
        }
        else
        {
            ExecutionContext.Run(context, s_executionCallback, this);
        }
    }

    internal void FinishWrapperConnectSuccess(Socket connectSocket, int bytesTransferred, SocketFlags flags)
    {
        SetResults(SocketError.Success, bytesTransferred, flags);
        _currentSocket = connectSocket;
        _connectSocket = connectSocket;
        ExecutionContext context = _context;
        Complete();
        if (context == null)
        {
            OnCompleted(this);
        }
        else
        {
            ExecutionContext.Run(context, s_executionCallback, this);
        }
    }

    internal void FinishOperationSyncSuccess(int bytesTransferred, SocketFlags flags)
    {
        SetResults(SocketError.Success, bytesTransferred, flags);
        if (NetEventSource.IsEnabled && bytesTransferred > 0)
        {
            LogBuffer(bytesTransferred);
        }

        SocketError socketError = SocketError.Success;
        switch (_completedOperation)
        {
            case SocketAsyncOperation.Accept:
            {
                Internals.SocketAddress socketAddress2 =
                    IPEndPointExtensions.Serialize(_currentSocket._rightEndPoint);
                socketError = FinishOperationAccept(socketAddress2);
                if (socketError == SocketError.Success)
                {
                    _acceptSocket = _currentSocket.UpdateAcceptSocket(_acceptSocket,
                        _currentSocket._rightEndPoint.Create(socketAddress2));
                    if (NetEventSource.IsEnabled)
                    {
                        NetEventSource.Accepted(_acceptSocket, _acceptSocket.RemoteEndPoint,
                            _acceptSocket.LocalEndPoint);
                    }
                }
                else
                {
                    SetResults(socketError, bytesTransferred, flags);
                    _acceptSocket = null;
                    _currentSocket.UpdateStatusAfterSocketError(socketError);
                }

                break;
            }
            case SocketAsyncOperation.Connect:
                socketError = FinishOperationConnect();
                if (socketError == SocketError.Success)
                {
                    if (NetEventSource.IsEnabled)
                    {
                        NetEventSource.Connected(_currentSocket, _currentSocket.LocalEndPoint,
                            _currentSocket.RemoteEndPoint);
                    }

                    _currentSocket.SetToConnected();
                    _connectSocket = _currentSocket;
                }
                else
                {
                    SetResults(socketError, bytesTransferred, flags);
                    _currentSocket.UpdateStatusAfterSocketError(socketError);
                }

                break;
            case SocketAsyncOperation.Disconnect:
                _currentSocket.SetToDisconnected();
                _currentSocket._remoteEndPoint = null;
                break;
            case SocketAsyncOperation.ReceiveFrom:
            {
                _socketAddress.InternalSize = GetSocketAddressSize();
                Internals.SocketAddress socketAddress = IPEndPointExtensions.Serialize(_remoteEndPoint);
                if (!socketAddress.Equals(_socketAddress))
                {
                    try
                    {
                        _remoteEndPoint = _remoteEndPoint.Create(_socketAddress);
                    }
                    catch
                    {
                    }
                }

                break;
            }
            case SocketAsyncOperation.ReceiveMessageFrom:
            {
                _socketAddress.InternalSize = GetSocketAddressSize();
                Internals.SocketAddress socketAddress = IPEndPointExtensions.Serialize(_remoteEndPoint);
                if (!socketAddress.Equals(_socketAddress))
                {
                    try
                    {
                        _remoteEndPoint = _remoteEndPoint.Create(_socketAddress);
                    }
                    catch
                    {
                    }
                }

                FinishOperationReceiveMessageFrom();
                break;
            }
            case SocketAsyncOperation.SendPackets:
                FinishOperationSendPackets();
                break;
        }

        Complete();
    }

    internal void FinishOperationAsyncSuccess(int bytesTransferred, SocketFlags flags)
    {
        ExecutionContext context = _context;
        FinishOperationSyncSuccess(bytesTransferred, flags);
        if (context == null)
        {
            OnCompleted(this);
        }
        else
        {
            ExecutionContext.Run(context, s_executionCallback, this);
        }
    }

    private void InitializeInternals()
    {
        bool flag = !ExecutionContext.IsFlowSuppressed();
        try
        {
            if (flag)
            {
                ExecutionContext.SuppressFlow();
            }

            _preAllocatedOverlapped = new PreAllocatedOverlapped(s_completionPortCallback, _strongThisRef, null);
        }
        finally
        {
            if (flag)
            {
                ExecutionContext.RestoreFlow();
            }
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"new PreAllocatedOverlapped {_preAllocatedOverlapped}");
        }
    }

    private void FreeInternals()
    {
        FreePinHandles();
        FreeOverlapped();
    }

    private unsafe NativeOverlapped* AllocateNativeOverlapped()
    {
        ThreadPoolBoundHandle orAllocateThreadPoolBoundHandle = _currentSocket.GetOrAllocateThreadPoolBoundHandle();
        return orAllocateThreadPoolBoundHandle.AllocateNativeOverlapped(_preAllocatedOverlapped);
    }

    private unsafe void FreeNativeOverlapped(NativeOverlapped* overlapped)
    {
        _currentSocket.SafeHandle.IOCPBoundHandle.FreeNativeOverlapped(overlapped);
    }

    private unsafe void RegisterToCancelPendingIO(NativeOverlapped* overlapped, CancellationToken cancellationToken)
    {
        _pendingOverlappedForCancellation = overlapped;
        _registrationToCancelPendingIO = cancellationToken. /*Unsafe*/Register(delegate(object s)
        {
            SocketAsyncEventArgs socketAsyncEventArgs = (SocketAsyncEventArgs)s;
            SafeSocketHandle safeHandle = socketAsyncEventArgs._currentSocket.SafeHandle;
            if (!safeHandle.IsClosed)
            {
                try
                {
                    // bool flag = Interop.Kernel32.CancelIoEx(safeHandle,
                    //     socketAsyncEventArgs._pendingOverlappedForCancellation);
                    // DIA-Замена: CancelIoEx нет в Windows XP!!!
                    SocketError socketError = Interop.Winsock.shutdown(safeHandle, Convert.ToInt32(SocketShutdown.Both));
                    if (NetEventSource.IsEnabled)
                    {
                        // NetEventSource.Info(socketAsyncEventArgs,
                        //     socketError == SocketError.Success
                        //         ? "Socket operation canceled."
                        //         : $"CancelIoEx failed with error '{Marshal.GetLastWin32Error()}'.");
                    }
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }, this);
    }

    private unsafe SocketError ProcessIOCPResult(bool success, int bytesTransferred, NativeOverlapped* overlapped)
    {
        if (success)
        {
            if (_currentSocket.SafeHandle.SkipCompletionPortOnSuccess)
            {
                FreeNativeOverlapped(overlapped);
                FinishOperationSyncSuccess(bytesTransferred, SocketFlags.None);
                return SocketError.Success;
            }
        }
        else
        {
            SocketError lastSocketError = SocketPal.GetLastSocketError();
            if (lastSocketError != SocketError.IOPending)
            {
                FreeNativeOverlapped(overlapped);
                FinishOperationSyncFailure(lastSocketError, bytesTransferred, SocketFlags.None);
                return lastSocketError;
            }
        }

        return SocketError.IOPending;
    }

    private unsafe SocketError ProcessIOCPResultWithSingleBufferHandle(SocketError socketError, int bytesTransferred,
        NativeOverlapped* overlapped, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (socketError == SocketError.Success)
        {
            if (_currentSocket.SafeHandle.SkipCompletionPortOnSuccess)
            {
                _singleBufferHandleState = SingleBufferHandleState.None;
                FreeNativeOverlapped(overlapped);
                FinishOperationSyncSuccess(bytesTransferred, SocketFlags.None);
                return SocketError.Success;
            }
        }
        else
        {
            socketError = SocketPal.GetLastSocketError();
            if (socketError != SocketError.IOPending)
            {
                _singleBufferHandleState = SingleBufferHandleState.None;
                FreeNativeOverlapped(overlapped);
                FinishOperationSyncFailure(socketError, bytesTransferred, SocketFlags.None);
                return socketError;
            }
        }

        if (_singleBufferHandleState == SingleBufferHandleState.InProcess)
        {
            RegisterToCancelPendingIO(overlapped, cancellationToken);
            _singleBufferHandle = _buffer.Pin();
            _singleBufferHandleState = SingleBufferHandleState.Set;
        }
        return SocketError.IOPending;
    }

    internal unsafe SocketError DoOperationAccept(Socket socket, SafeSocketHandle handle, SafeSocketHandle acceptHandle)
    {
        bool flag = _count != 0;
        Memory<byte> memory = (flag ? _buffer : ((Memory<byte>)_acceptBuffer));
        NativeOverlapped* overlapped = AllocateNativeOverlapped();
        try
        {
            _singleBufferHandle = memory.Pin();
            _singleBufferHandleState = SingleBufferHandleState.Set;
            int bytesReceived;
            bool success = socket.AcceptEx(handle, acceptHandle,
                flag ? ((IntPtr)((byte*)_singleBufferHandle.Pointer + _offset)) : ((IntPtr)_singleBufferHandle.Pointer),
                flag ? (_count - _acceptAddressBufferCount) : 0, _acceptAddressBufferCount / 2,
                _acceptAddressBufferCount / 2, out bytesReceived, overlapped);
            return ProcessIOCPResult(success, bytesReceived, overlapped);
        }
        catch
        {
            _singleBufferHandleState = SingleBufferHandleState.None;
            FreeNativeOverlapped(overlapped);
            _singleBufferHandle.Dispose();
            throw;
        }
    }

    internal unsafe SocketError DoOperationConnect(Socket socket, SafeSocketHandle handle)
    {
        PinSocketAddressBuffer();
        NativeOverlapped* overlapped = AllocateNativeOverlapped();
        try
        {
            _singleBufferHandle = _buffer.Pin();
            _singleBufferHandleState = SingleBufferHandleState.Set;
            int bytesSent;
            bool success = socket.ConnectEx(handle, PtrSocketAddressBuffer, _socketAddress.Size,
                (IntPtr)((byte*)_singleBufferHandle.Pointer + _offset), _count, out bytesSent, overlapped);
            return ProcessIOCPResult(success, bytesSent, overlapped);
        }
        catch
        {
            _singleBufferHandleState = SingleBufferHandleState.None;
            FreeNativeOverlapped(overlapped);
            _singleBufferHandle.Dispose();
            throw;
        }
    }

    internal unsafe SocketError DoOperationDisconnect(Socket socket, SafeSocketHandle handle)
    {
        NativeOverlapped* overlapped = AllocateNativeOverlapped();
        try
        {
            bool success = socket.DisconnectEx(handle, overlapped, DisconnectReuseSocket ? 2 : 0, 0);
            return ProcessIOCPResult(success, 0, overlapped);
        }
        catch
        {
            FreeNativeOverlapped(overlapped);
            throw;
        }
    }

    internal SocketError DoOperationReceive(SafeSocketHandle handle, CancellationToken cancellationToken)
    {
        if (_bufferList != null)
        {
            return DoOperationReceiveMultiBuffer(handle);
        }

        return DoOperationReceiveSingleBuffer(handle, cancellationToken);
    }

    internal unsafe SocketError DoOperationReceiveSingleBuffer(SafeSocketHandle handle,
        CancellationToken cancellationToken)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(_buffer.Span))
        {
            NativeOverlapped* overlapped = AllocateNativeOverlapped();
            try
            {
                _singleBufferHandleState = SingleBufferHandleState.InProcess;
                WSABuffer wSABuffer = default(WSABuffer);
                wSABuffer.Length = _count;
                wSABuffer.Pointer = (IntPtr)(ptr + _offset);
                WSABuffer buffer = wSABuffer;
                SocketFlags socketFlags = _socketFlags;
                int bytesTransferred;
                SocketError socketError = Interop.Winsock.WSARecv(handle, ref buffer, 1, out bytesTransferred,
                    ref socketFlags, overlapped, IntPtr.Zero);
                
                return ProcessIOCPResultWithSingleBufferHandle(socketError, bytesTransferred, overlapped,
                    cancellationToken);
            }
            catch
            {
                _singleBufferHandleState = SingleBufferHandleState.None;
                FreeNativeOverlapped(overlapped);
                throw;
            }
        }

    }

    internal unsafe SocketError DoOperationReceiveMultiBuffer(SafeSocketHandle handle)
    {
        NativeOverlapped* overlapped = AllocateNativeOverlapped();
        try
        {
            SocketFlags socketFlags = _socketFlags;
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSARecv(handle, _wsaBufferArray, _bufferListInternal.Count,
                out bytesTransferred, ref socketFlags, overlapped, IntPtr.Zero);
            return ProcessIOCPResult(socketError == SocketError.Success, bytesTransferred, overlapped);
        }
        catch
        {
            FreeNativeOverlapped(overlapped);
            throw;
        }
    }

    internal SocketError DoOperationReceiveFrom(SafeSocketHandle handle)
    {
        PinSocketAddressBuffer();
        if (_bufferList != null)
        {
            return DoOperationReceiveFromMultiBuffer(handle);
        }

        return DoOperationReceiveFromSingleBuffer(handle);
    }

    internal unsafe SocketError DoOperationReceiveFromSingleBuffer(SafeSocketHandle handle)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(_buffer.Span))
        {
            NativeOverlapped* overlapped = AllocateNativeOverlapped();
            try
            {
                _singleBufferHandleState = SingleBufferHandleState.InProcess;
                WSABuffer wSABuffer = default(WSABuffer);
                wSABuffer.Length = _count;
                wSABuffer.Pointer = (IntPtr)(ptr + _offset);
                WSABuffer buffer = wSABuffer;
                SocketFlags socketFlags = _socketFlags;
                int bytesTransferred;
                SocketError socketError = Interop.Winsock.WSARecvFrom(handle, ref buffer, 1, out bytesTransferred,
                    ref socketFlags, PtrSocketAddressBuffer, PtrSocketAddressBufferSize, overlapped, IntPtr.Zero);
                return ProcessIOCPResultWithSingleBufferHandle(socketError, bytesTransferred, overlapped);
            }
            catch
            {
                _singleBufferHandleState = SingleBufferHandleState.None;
                FreeNativeOverlapped(overlapped);
                throw;
            }
        }
    }

    internal unsafe SocketError DoOperationReceiveFromMultiBuffer(SafeSocketHandle handle)
    {
        NativeOverlapped* overlapped = AllocateNativeOverlapped();
        try
        {
            SocketFlags socketFlags = _socketFlags;
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSARecvFrom(handle, _wsaBufferArray, _bufferListInternal.Count,
                out bytesTransferred, ref socketFlags, PtrSocketAddressBuffer, PtrSocketAddressBufferSize, overlapped,
                IntPtr.Zero);
            return ProcessIOCPResult(socketError == SocketError.Success, bytesTransferred, overlapped);
        }
        catch
        {
            FreeNativeOverlapped(overlapped);
            throw;
        }
    }

    internal unsafe SocketError DoOperationReceiveMessageFrom(Socket socket, SafeSocketHandle handle)
    {
        PinSocketAddressBuffer();
        if (_wsaMessageBuffer == null)
        {
            _wsaMessageBuffer = new byte[sizeof(Interop.Winsock.WSAMsg)];
        }

        if (!_wsaMessageBufferGCHandle.IsAllocated)
        {
            _wsaMessageBufferGCHandle = GCHandle.Alloc(_wsaMessageBuffer, GCHandleType.Pinned);
        }

        IPAddress iPAddress = ((_socketAddress.Family == AddressFamily.InterNetworkV6)
            ? _socketAddress.GetIPAddress()
            : null);
        bool flag = _currentSocket.AddressFamily == AddressFamily.InterNetwork ||
                    (iPAddress?.IsIPv4MappedToIPv6 ?? false);
        bool flag2 = _currentSocket.AddressFamily == AddressFamily.InterNetworkV6;
        if (flag && (_controlBuffer == null || _controlBuffer.Length != sizeof(Interop.Winsock.ControlData)))
        {
            if (_controlBufferGCHandle.IsAllocated)
            {
                _controlBufferGCHandle.Free();
            }

            _controlBuffer = new byte[sizeof(Interop.Winsock.ControlData)];
        }
        else if (flag2 && (_controlBuffer == null || _controlBuffer.Length != sizeof(Interop.Winsock.ControlDataIPv6)))
        {
            if (_controlBufferGCHandle.IsAllocated)
            {
                _controlBufferGCHandle.Free();
            }

            _controlBuffer = new byte[sizeof(Interop.Winsock.ControlDataIPv6)];
        }

        WSABuffer[] array;
        uint count;
        if (_bufferList == null)
        {
            if (_wsaRecvMsgWSABufferArray == null)
            {
                _wsaRecvMsgWSABufferArray = new WSABuffer[1];
            }

            _singleBufferHandle = _buffer.Pin();
            _singleBufferHandleState = SingleBufferHandleState.Set;
            _wsaRecvMsgWSABufferArray[0].Pointer = (IntPtr)_singleBufferHandle.Pointer;
            _wsaRecvMsgWSABufferArray[0].Length = _count;
            array = _wsaRecvMsgWSABufferArray;
            count = 1u;
        }
        else
        {
            array = _wsaBufferArray;
            count = (uint)_bufferListInternal.Count;
        }

        if (!_wsaRecvMsgWSABufferArrayGCHandle.IsAllocated)
        {
            _wsaRecvMsgWSABufferArrayGCHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
        }

        Interop.Winsock.WSAMsg* ptr =
            (Interop.Winsock.WSAMsg*)(void*)Marshal.UnsafeAddrOfPinnedArrayElement(_wsaMessageBuffer, 0);
        ptr->socketAddress = PtrSocketAddressBuffer;
        ptr->addressLength = (uint)_socketAddress.Size;
        fixed (WSABuffer* ptr2 = &array[0])
        {
            void* ptr3 = ptr2;
            ptr->buffers = (IntPtr)ptr3;
        }

        ptr->count = count;
        if (_controlBuffer != null)
        {
            if (!_controlBufferGCHandle.IsAllocated)
            {
                _controlBufferGCHandle = GCHandle.Alloc(_controlBuffer, GCHandleType.Pinned);
            }

            fixed (byte* ptr4 = &_controlBuffer[0])
            {
                void* ptr5 = ptr4;
                ptr->controlBuffer.Pointer = (IntPtr)ptr5;
            }

            ptr->controlBuffer.Length = _controlBuffer.Length;
        }

        ptr->flags = _socketFlags;
        NativeOverlapped* overlapped = AllocateNativeOverlapped();
        try
        {
            int bytesTransferred;
            SocketError socketError = socket.WSARecvMsg(handle,
                Marshal.UnsafeAddrOfPinnedArrayElement(_wsaMessageBuffer, 0), out bytesTransferred, overlapped,
                IntPtr.Zero);
            return ProcessIOCPResultWithSingleBufferHandle(socketError, bytesTransferred, overlapped);
        }
        catch
        {
            _singleBufferHandleState = SingleBufferHandleState.None;
            FreeNativeOverlapped(overlapped);
            _singleBufferHandle.Dispose();
            throw;
        }
    }

    internal SocketError DoOperationSend(SafeSocketHandle handle, CancellationToken cancellationToken)
    {
        if (_bufferList != null)
        {
            return DoOperationSendMultiBuffer(handle);
        }

        return DoOperationSendSingleBuffer(handle, cancellationToken);
    }

    internal unsafe SocketError DoOperationSendSingleBuffer(SafeSocketHandle handle,
        CancellationToken cancellationToken)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(_buffer.Span))
        {
            NativeOverlapped* overlapped = AllocateNativeOverlapped();
            try
            {
                _singleBufferHandleState = SingleBufferHandleState.InProcess;
                WSABuffer wSABuffer = default(WSABuffer);
                wSABuffer.Length = _count;
                wSABuffer.Pointer = (IntPtr)(ptr + _offset);
                WSABuffer buffer = wSABuffer;
                int bytesTransferred;
                SocketError socketError = Interop.Winsock.WSASend(handle, ref buffer, 1, out bytesTransferred,
                    _socketFlags, overlapped, IntPtr.Zero);
                return ProcessIOCPResultWithSingleBufferHandle(socketError, bytesTransferred, overlapped,
                    cancellationToken);
            }
            catch
            {
                _singleBufferHandleState = SingleBufferHandleState.None;
                FreeNativeOverlapped(overlapped);
                throw;
            }
        }
    }

    internal unsafe SocketError DoOperationSendMultiBuffer(SafeSocketHandle handle)
    {
        NativeOverlapped* overlapped = AllocateNativeOverlapped();
        try
        {
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSASend(handle, _wsaBufferArray, _bufferListInternal.Count,
                out bytesTransferred, _socketFlags, overlapped, IntPtr.Zero);
            return ProcessIOCPResult(socketError == SocketError.Success, bytesTransferred, overlapped);
        }
        catch
        {
            FreeNativeOverlapped(overlapped);
            throw;
        }
    }

    internal unsafe SocketError DoOperationSendPackets(Socket socket, SafeSocketHandle handle)
    {
        SendPacketsElement[] array = (SendPacketsElement[])_sendPacketsElements.Clone();
        int num = 0;
        int num2 = 0;
        int num3 = 0;
        SendPacketsElement[] array2 = array;
        foreach (SendPacketsElement sendPacketsElement in array2)
        {
            if (sendPacketsElement != null)
            {
                if (sendPacketsElement.FilePath != null)
                {
                    num++;
                }
                else if (sendPacketsElement.FileStream != null)
                {
                    num2++;
                }
                else if (sendPacketsElement.Buffer != null && sendPacketsElement.Count > 0)
                {
                    num3++;
                }
            }
        }

        if (num + num2 + num3 == 0)
        {
            FinishOperationSyncSuccess(0, SocketFlags.None);
            return SocketError.Success;
        }

        if (num > 0)
        {
            int num4 = 0;
            _sendPacketsFileStreams = new FileStream[num];
            try
            {
                SendPacketsElement[] array3 = array;
                foreach (SendPacketsElement sendPacketsElement2 in array3)
                {
                    if (sendPacketsElement2 != null && sendPacketsElement2.FilePath != null)
                    {
                        _sendPacketsFileStreams[num4] = new FileStream(sendPacketsElement2.FilePath, FileMode.Open,
                            FileAccess.Read, FileShare.Read);
                        num4++;
                    }
                }
            }
            catch
            {
                for (int num5 = num4 - 1; num5 >= 0; num5--)
                {
                    _sendPacketsFileStreams[num5].Dispose();
                }

                _sendPacketsFileStreams = null;
                throw;
            }
        }

        Interop.Winsock.TransmitPacketsElement[] array4 = SetupPinHandlesSendPackets(array, num, num2, num3);
        NativeOverlapped* overlapped = AllocateNativeOverlapped();
        try
        {
            bool success = socket.TransmitPackets(handle, _multipleBufferGCHandles[0].AddrOfPinnedObject(),
                array4.Length, _sendPacketsSendSize, overlapped, _sendPacketsFlags);
            return ProcessIOCPResult(success, 0, overlapped);
        }
        catch
        {
            FreeNativeOverlapped(overlapped);
            throw;
        }
    }

    internal SocketError DoOperationSendTo(SafeSocketHandle handle)
    {
        PinSocketAddressBuffer();
        if (_bufferList != null)
        {
            return DoOperationSendToMultiBuffer(handle);
        }

        return DoOperationSendToSingleBuffer(handle);
    }

    internal unsafe SocketError DoOperationSendToSingleBuffer(SafeSocketHandle handle)
    {
        fixed (byte* ptr = &MemoryMarshal.GetReference(_buffer.Span))
        {
            NativeOverlapped* overlapped = AllocateNativeOverlapped();
            try
            {
                _singleBufferHandleState = SingleBufferHandleState.InProcess;
                WSABuffer wSABuffer = default(WSABuffer);
                wSABuffer.Length = _count;
                wSABuffer.Pointer = (IntPtr)(ptr + _offset);
                WSABuffer buffer = wSABuffer;
                int bytesTransferred;
                SocketError socketError = Interop.Winsock.WSASendTo(handle, ref buffer, 1, out bytesTransferred,
                    _socketFlags, PtrSocketAddressBuffer, _socketAddress.Size, overlapped, IntPtr.Zero);
                return ProcessIOCPResultWithSingleBufferHandle(socketError, bytesTransferred, overlapped);
            }
            catch
            {
                _singleBufferHandleState = SingleBufferHandleState.None;
                FreeNativeOverlapped(overlapped);
                throw;
            }
        }
    }

    internal unsafe SocketError DoOperationSendToMultiBuffer(SafeSocketHandle handle)
    {
        NativeOverlapped* overlapped = AllocateNativeOverlapped();
        try
        {
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSASendTo(handle, _wsaBufferArray, _bufferListInternal.Count,
                out bytesTransferred, _socketFlags, PtrSocketAddressBuffer, _socketAddress.Size, overlapped,
                IntPtr.Zero);
            return ProcessIOCPResult(socketError == SocketError.Success, bytesTransferred, overlapped);
        }
        catch
        {
            FreeNativeOverlapped(overlapped);
            throw;
        }
    }

    private void SetupMultipleBuffers()
    {
        if (_bufferListInternal == null || _bufferListInternal.Count == 0)
        {
            if (_pinState == PinState.MultipleBuffer)
            {
                FreePinHandles();
            }

            return;
        }

        FreePinHandles();
        try
        {
            int count = _bufferListInternal.Count;
            if (_multipleBufferGCHandles == null || _multipleBufferGCHandles.Length < count)
            {
                _multipleBufferGCHandles = new GCHandle[count];
            }

            for (int i = 0; i < count; i++)
            {
                _multipleBufferGCHandles[i] = GCHandle.Alloc(_bufferListInternal[i].Array, GCHandleType.Pinned);
            }

            if (_wsaBufferArray == null || _wsaBufferArray.Length < count)
            {
                _wsaBufferArray = new WSABuffer[count];
            }

            for (int j = 0; j < count; j++)
            {
                ArraySegment<byte> arraySegment = _bufferListInternal[j];
                _wsaBufferArray[j].Pointer =
                    Marshal.UnsafeAddrOfPinnedArrayElement(arraySegment.Array, arraySegment.Offset);
                _wsaBufferArray[j].Length = arraySegment.Count;
            }

            _pinState = PinState.MultipleBuffer;
        }
        catch (Exception)
        {
            FreePinHandles();
            throw;
        }
    }

    private void PinSocketAddressBuffer()
    {
        if (_pinnedSocketAddress != _socketAddress)
        {
            if (_socketAddressGCHandle.IsAllocated)
            {
                _socketAddressGCHandle.Free();
            }

            _socketAddressGCHandle = GCHandle.Alloc(_socketAddress.Buffer, GCHandleType.Pinned);
            _socketAddress.CopyAddressSizeIntoBuffer();
            _pinnedSocketAddress = _socketAddress;
        }
    }

    private void FreeOverlapped()
    {
        if (_preAllocatedOverlapped != null)
        {
            _preAllocatedOverlapped.Dispose();
            _preAllocatedOverlapped = null;
        }
    }

    private void FreePinHandles()
    {
        _pinState = PinState.None;
        if (_singleBufferHandleState != 0)
        {
            _singleBufferHandleState = SingleBufferHandleState.None;
            _singleBufferHandle.Dispose();
        }

        if (_multipleBufferGCHandles != null)
        {
            for (int i = 0; i < _multipleBufferGCHandles.Length; i++)
            {
                if (_multipleBufferGCHandles[i].IsAllocated)
                {
                    _multipleBufferGCHandles[i].Free();
                }
            }
        }

        if (_socketAddressGCHandle.IsAllocated)
        {
            _socketAddressGCHandle.Free();
            _pinnedSocketAddress = null;
        }

        if (_wsaMessageBufferGCHandle.IsAllocated)
        {
            _wsaMessageBufferGCHandle.Free();
        }

        if (_wsaRecvMsgWSABufferArrayGCHandle.IsAllocated)
        {
            _wsaRecvMsgWSABufferArrayGCHandle.Free();
        }

        if (_controlBufferGCHandle.IsAllocated)
        {
            _controlBufferGCHandle.Free();
        }
    }

    private Interop.Winsock.TransmitPacketsElement[] SetupPinHandlesSendPackets(
        SendPacketsElement[] sendPacketsElementsCopy, int sendPacketsElementsFileCount,
        int sendPacketsElementsFileStreamCount, int sendPacketsElementsBufferCount)
    {
        if (_pinState != 0)
        {
            FreePinHandles();
        }

        Interop.Winsock.TransmitPacketsElement[] array =
            new Interop.Winsock.TransmitPacketsElement[sendPacketsElementsFileCount +
                                                       sendPacketsElementsFileStreamCount +
                                                       sendPacketsElementsBufferCount];
        if (_multipleBufferGCHandles == null || _multipleBufferGCHandles.Length < sendPacketsElementsBufferCount + 1)
        {
            _multipleBufferGCHandles = new GCHandle[sendPacketsElementsBufferCount + 1];
        }

        _multipleBufferGCHandles[0] = GCHandle.Alloc(array, GCHandleType.Pinned);
        int num = 1;
        foreach (SendPacketsElement sendPacketsElement in sendPacketsElementsCopy)
        {
            if (sendPacketsElement?.Buffer != null && sendPacketsElement.Count > 0)
            {
                _multipleBufferGCHandles[num] = GCHandle.Alloc(sendPacketsElement.Buffer, GCHandleType.Pinned);
                num++;
            }
        }

        int num2 = 0;
        int num3 = 0;
        foreach (SendPacketsElement sendPacketsElement2 in sendPacketsElementsCopy)
        {
            if (sendPacketsElement2 != null)
            {
                if (sendPacketsElement2.Buffer != null && sendPacketsElement2.Count > 0)
                {
                    array[num2].buffer =
                        Marshal.UnsafeAddrOfPinnedArrayElement(sendPacketsElement2.Buffer, sendPacketsElement2.Offset);
                    array[num2].length = (uint)sendPacketsElement2.Count;
                    array[num2].flags = Interop.Winsock.TransmitPacketsElementFlags.Memory |
                                        (sendPacketsElement2.EndOfPacket
                                            ? Interop.Winsock.TransmitPacketsElementFlags.EndOfPacket
                                            : Interop.Winsock.TransmitPacketsElementFlags.None);
                    num2++;
                }
                else if (sendPacketsElement2.FilePath != null)
                {
                    array[num2].fileHandle = _sendPacketsFileStreams[num3].SafeFileHandle.DangerousGetHandle();
                    array[num2].fileOffset = sendPacketsElement2.OffsetLong;
                    array[num2].length = (uint)sendPacketsElement2.Count;
                    array[num2].flags = Interop.Winsock.TransmitPacketsElementFlags.File |
                                        (sendPacketsElement2.EndOfPacket
                                            ? Interop.Winsock.TransmitPacketsElementFlags.EndOfPacket
                                            : Interop.Winsock.TransmitPacketsElementFlags.None);
                    num3++;
                    num2++;
                }
                else if (sendPacketsElement2.FileStream != null)
                {
                    array[num2].fileHandle = sendPacketsElement2.FileStream.SafeFileHandle.DangerousGetHandle();
                    array[num2].fileOffset = sendPacketsElement2.OffsetLong;
                    array[num2].length = (uint)sendPacketsElement2.Count;
                    array[num2].flags = Interop.Winsock.TransmitPacketsElementFlags.File |
                                        (sendPacketsElement2.EndOfPacket
                                            ? Interop.Winsock.TransmitPacketsElementFlags.EndOfPacket
                                            : Interop.Winsock.TransmitPacketsElementFlags.None);
                    num2++;
                }
            }
        }

        _pinState = PinState.SendPackets;
        return array;
    }

    internal void LogBuffer(int size)
    {
        if (_bufferList != null)
        {
            for (int i = 0; i < _bufferListInternal.Count; i++)
            {
                WSABuffer wSABuffer = _wsaBufferArray[i];
                NetEventSource.DumpBuffer(this, wSABuffer.Pointer, Math.Min(wSABuffer.Length, size));
                if ((size -= wSABuffer.Length) <= 0)
                {
                    break;
                }
            }
        }
        else if (_buffer.Length != 0)
        {
            NetEventSource.DumpBuffer(this, _buffer, _offset, size);
        }
    }

    private unsafe SocketError FinishOperationAccept(Internals.SocketAddress remoteSocketAddress)
    {
        SocketError socketError;
        try
        {
            bool flag = _count >= _acceptAddressBufferCount;
            _currentSocket.GetAcceptExSockaddrs(
                flag ? ((IntPtr)((byte*)_singleBufferHandle.Pointer + _offset)) : ((IntPtr)_singleBufferHandle.Pointer),
                (_count != 0) ? (_count - _acceptAddressBufferCount) : 0, _acceptAddressBufferCount / 2,
                _acceptAddressBufferCount / 2, out var _, out var _, out var remoteSocketAddress2,
                out remoteSocketAddress.InternalSize);
            Marshal.Copy(remoteSocketAddress2, remoteSocketAddress.Buffer, 0, remoteSocketAddress.Size);
            IntPtr pointer = _currentSocket.SafeHandle.DangerousGetHandle();
            socketError = Interop.Winsock.setsockopt(_acceptSocket.SafeHandle, SocketOptionLevel.Socket,
                SocketOptionName.UpdateAcceptContext, ref pointer, IntPtr.Size);
            if (socketError == SocketError.SocketError)
            {
                socketError = SocketPal.GetLastSocketError();
            }
        }
        catch (ObjectDisposedException)
        {
            socketError = SocketError.OperationAborted;
        }

        return socketError;
    }

    private SocketError FinishOperationConnect()
    {
        try
        {
            SocketError socketError = Interop.Winsock.setsockopt(_currentSocket.SafeHandle, SocketOptionLevel.Socket,
                SocketOptionName.UpdateConnectContext, null, 0);
            return (socketError == SocketError.SocketError) ? SocketPal.GetLastSocketError() : socketError;
        }
        catch (ObjectDisposedException)
        {
            return SocketError.OperationAborted;
        }
    }

    private unsafe int GetSocketAddressSize()
    {
        return *(int*)(void*)PtrSocketAddressBufferSize;
    }

    private void CompleteCore()
    {
        _strongThisRef.Value = null;
        if (_singleBufferHandleState != 0)
        {
            CompleteCoreSpin();
        }

        unsafe void CompleteCoreSpin()
        {
            SpinWait spinWait = default(SpinWait);
            while (_singleBufferHandleState == SingleBufferHandleState.InProcess)
            {
                spinWait.SpinOnce();
            }

            _registrationToCancelPendingIO.Dispose();
            _pendingOverlappedForCancellation = null;
            if (_singleBufferHandleState == SingleBufferHandleState.Set)
            {
                _singleBufferHandleState = SingleBufferHandleState.None;
                _singleBufferHandle.Dispose();
            }
        }
    }

    private unsafe void FinishOperationReceiveMessageFrom()
    {
        Interop.Winsock.WSAMsg* ptr =
            (Interop.Winsock.WSAMsg*)(void*)Marshal.UnsafeAddrOfPinnedArrayElement(_wsaMessageBuffer, 0);
        if (_controlBuffer.Length == sizeof(Interop.Winsock.ControlData))
        {
            _receiveMessageFromPacketInfo =
                SocketPal.GetIPPacketInformation((Interop.Winsock.ControlData*)(void*)ptr->controlBuffer.Pointer);
        }
        else if (_controlBuffer.Length == sizeof(Interop.Winsock.ControlDataIPv6))
        {
            _receiveMessageFromPacketInfo =
                SocketPal.GetIPPacketInformation((Interop.Winsock.ControlDataIPv6*)(void*)ptr->controlBuffer.Pointer);
        }
        else
        {
            _receiveMessageFromPacketInfo = default(IPPacketInformation);
        }
    }

    private void FinishOperationSendPackets()
    {
        if (_sendPacketsFileStreams != null)
        {
            for (int i = 0; i < _sendPacketsFileStreams.Length; i++)
            {
                _sendPacketsFileStreams[i]?.Dispose();
            }

            _sendPacketsFileStreams = null;
        }
    }

    private unsafe void HandleCompletionPortCallbackError(uint errorCode, uint numBytes,
        NativeOverlapped* nativeOverlapped)
    {
        SocketError socketError = (SocketError)errorCode;
        SocketFlags socketFlags = SocketFlags.None;
        if (socketError != SocketError.OperationAborted)
        {
            if (_currentSocket.CleanedUp)
            {
                socketError = SocketError.OperationAborted;
            }
            else
            {
                try
                {
                    bool flag = Interop.Winsock.WSAGetOverlappedResult(_currentSocket.SafeHandle, nativeOverlapped,
                        out numBytes, wait: false, out socketFlags);
                    socketError = SocketPal.GetLastSocketError();
                }
                catch
                {
                    socketError = SocketError.OperationAborted;
                }
            }
        }

        FreeNativeOverlapped(nativeOverlapped);
        FinishOperationAsyncFailure(socketError, (int)numBytes, socketFlags);
    }
}