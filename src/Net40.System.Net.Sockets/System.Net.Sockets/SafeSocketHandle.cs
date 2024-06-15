using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace System.Net.Sockets.Net40;

public sealed class SafeSocketHandle : SafeHandleMinusOneIsInvalid
{
    internal sealed class InnerSafeCloseSocket : SafeHandleMinusOneIsInvalid
    {
        private bool _blockable;

        public override bool IsInvalid
        {
            get
            {
                if (!IsClosed)
                {
                    return base.IsInvalid;
                }

                return true;
            }
        }

        private InnerSafeCloseSocket()
            : base(ownsHandle: true)
        {
        }

        protected override bool ReleaseHandle()
        {
            bool flag = false;
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, $"handle:{handle}");
            }

            Net40.SocketError socketError = InnerReleaseHandle();
            return flag = socketError == Net40.SocketError.Success;
        }

        internal void BlockingRelease()
        {
            _blockable = true;
            DangerousRelease();
        }

        private Net40.SocketError InnerReleaseHandle()
        {
            Net40.SocketError socketError;
            if (_blockable)
            {
                if (NetEventSource.IsEnabled)
                {
                    NetEventSource.Info(this, $"handle:{handle}, Following 'blockable' branch");
                }

                socketError = Interop.Winsock.closesocket(handle);
                if (socketError == Net40.SocketError.SocketError)
                {
                    socketError = (Net40.SocketError)Marshal.GetLastWin32Error();
                }

                if (NetEventSource.IsEnabled)
                {
                    NetEventSource.Info(this, $"handle:{handle}, closesocket()#1:{socketError}");
                }

                if (socketError != Net40.SocketError.WouldBlock)
                {
                    return socketError;
                }

                int argp = 0;
                socketError = Interop.Winsock.ioctlsocket(handle, -2147195266, ref argp);
                if (socketError == Net40.SocketError.SocketError)
                {
                    socketError = (Net40.SocketError)Marshal.GetLastWin32Error();
                }

                if (NetEventSource.IsEnabled)
                {
                    NetEventSource.Info(this, $"handle:{handle}, ioctlsocket()#1:{socketError}");
                }

                if (socketError == Net40.SocketError.Success)
                {
                    socketError = Interop.Winsock.closesocket(handle);
                    if (socketError == Net40.SocketError.SocketError)
                    {
                        socketError = (Net40.SocketError)Marshal.GetLastWin32Error();
                    }

                    if (NetEventSource.IsEnabled)
                    {
                        NetEventSource.Info(this, $"handle:{handle}, closesocket#2():{socketError}");
                    }

                    if (socketError != Net40.SocketError.WouldBlock)
                    {
                        return socketError;
                    }
                }
            }

            Interop.Winsock.Linger linger = default(Interop.Winsock.Linger);
            linger.OnOff = 1;
            linger.Time = 0;
            socketError = Interop.Winsock.setsockopt(handle, SocketOptionLevel.Socket, SocketOptionName.Linger,
                ref linger, 4);
            if (socketError == Net40.SocketError.SocketError)
            {
                socketError = (Net40.SocketError)Marshal.GetLastWin32Error();
            }

            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this, $"handle:{handle}, setsockopt():{socketError}");
            }

            if (socketError != 0 && socketError != Net40.SocketError.InvalidArgument &&
                socketError != Net40.SocketError.ProtocolOption)
            {
                return socketError;
            }

            socketError = Interop.Winsock.closesocket(handle);
            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(this,
                    $"handle:{handle}, closesocket#3():{((socketError == Net40.SocketError.SocketError) ? ((Net40.SocketError)Marshal.GetLastWin32Error()) : socketError)}");
            }

            return socketError;
        }

        internal static InnerSafeCloseSocket CreateWSASocket(Net40.AddressFamily addressFamily, SocketType socketType,
            ProtocolType protocolType)
        {
            InnerSafeCloseSocket innerSafeCloseSocket = Interop.Winsock.WSASocketW(addressFamily, socketType,
                protocolType, IntPtr.Zero, 0u,
                
                Interop.Winsock.SocketConstructorFlags.WSA_FLAG_OVERLAPPED 
                //| Interop.Winsock.SocketConstructorFlags.WSA_FLAG_NO_HANDLE_INHERIT
                );
            
            
            if (innerSafeCloseSocket.IsInvalid)
            {
                innerSafeCloseSocket.SetHandleAsInvalid();
            }

            return innerSafeCloseSocket;
        }

        internal static InnerSafeCloseSocket Accept(SafeSocketHandle socketHandle, byte[] socketAddress,
            ref int socketAddressSize)
        {
            InnerSafeCloseSocket innerSafeCloseSocket = Interop.Winsock.accept(socketHandle.DangerousGetHandle(),
                socketAddress, ref socketAddressSize);
            if (innerSafeCloseSocket.IsInvalid)
            {
                innerSafeCloseSocket.SetHandleAsInvalid();
            }

            return innerSafeCloseSocket;
        }
    }

    private InnerSafeCloseSocket _innerSocket;

    private volatile bool _released;

    private ThreadPoolBoundHandle _iocpBoundHandle;

    private bool _skipCompletionPortOnSuccess;

    private object _iocpBindingLock = new object();

    public override bool IsInvalid
    {
        get
        {
            if (!IsClosed)
            {
                return base.IsInvalid;
            }

            return true;
        }
    }

    internal ThreadPoolBoundHandle IOCPBoundHandle => _iocpBoundHandle;

    internal bool SkipCompletionPortOnSuccess => _skipCompletionPortOnSuccess;

    public SafeSocketHandle(IntPtr preexistingHandle, bool ownsHandle)
        : base(ownsHandle)
    {
        handle = preexistingHandle;
    }

    private SafeSocketHandle()
        : base(ownsHandle: true)
    {
    }

    private void SetInnerSocket(InnerSafeCloseSocket socket)
    {
        _innerSocket = socket;
        SetHandle(socket.DangerousGetHandle());
    }

    private static SafeSocketHandle CreateSocket(InnerSafeCloseSocket socket)
    {
        SafeSocketHandle safeSocketHandle = new SafeSocketHandle();
        CreateSocket(socket, safeSocketHandle);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(null, safeSocketHandle);
        }

        return safeSocketHandle;
    }

    private static void CreateSocket(InnerSafeCloseSocket socket, SafeSocketHandle target)
    {
        if (socket != null && socket.IsInvalid)
        {
            target.SetHandleAsInvalid();
            return;
        }

        bool success = false;
        try
        {
            socket.DangerousAddRef(ref success);
        }
        catch
        {
            if (success)
            {
                socket.DangerousRelease();
                success = false;
            }
        }
        finally
        {
            if (success)
            {
                target.SetInnerSocket(socket);
                socket.Dispose();
            }
            else
            {
                target.SetHandleAsInvalid();
            }
        }
    }

    protected override bool ReleaseHandle()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"_innerSocket={_innerSocket}");
        }

        _released = true;
        InnerSafeCloseSocket innerSafeCloseSocket =
            ((_innerSocket == null) ? null : Interlocked.Exchange(ref _innerSocket, null));
        if (innerSafeCloseSocket != null)
        {
            InnerReleaseHandle();
            innerSafeCloseSocket.DangerousRelease();
        }

        return true;
    }

    internal void CloseAsIs()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, $"_innerSocket={_innerSocket}");
        }

        InnerSafeCloseSocket innerSafeCloseSocket =
            ((_innerSocket == null) ? null : Interlocked.Exchange(ref _innerSocket, null));
        Dispose();
        if (innerSafeCloseSocket != null)
        {
            SpinWait spinWait = default(SpinWait);
            while (!_released)
            {
                spinWait.SpinOnce();
            }

            InnerReleaseHandle();
            innerSafeCloseSocket.BlockingRelease();
        }
    }

    internal void SetExposed()
    {
    }

    internal ThreadPoolBoundHandle GetThreadPoolBoundHandle()
    {
        if (_released)
        {
            return null;
        }

        return _iocpBoundHandle;
    }

    internal ThreadPoolBoundHandle GetOrAllocateThreadPoolBoundHandle(bool trySkipCompletionPortOnSuccess)
    {
        if (_released)
        {
            throw new ObjectDisposedException(typeof(Net40.Socket).FullName);
        }

        if (_iocpBoundHandle != null)
        {
            return _iocpBoundHandle;
        }

        lock (_iocpBindingLock)
        {
            ThreadPoolBoundHandle threadPoolBoundHandle = _iocpBoundHandle;
            if (threadPoolBoundHandle == null)
            {
                if (NetEventSource.IsEnabled)
                {
                    NetEventSource.Info(this, "calling ThreadPool.BindHandle()");
                }

                try
                {
                    threadPoolBoundHandle = ThreadPoolBoundHandle.BindHandle(this);
                }
                catch (Exception ex) when (!ExceptionCheck.IsFatal(ex))
                {
                    bool isClosed = IsClosed;
                    CloseAsIs();
                    if (isClosed)
                    {
                        throw new ObjectDisposedException(typeof(Net40.Socket).FullName, ex);
                    }

                    throw;
                }

                if (trySkipCompletionPortOnSuccess &&
                    CompletionPortHelper.SkipCompletionPortOnSuccess(threadPoolBoundHandle.Handle))
                {
                    _skipCompletionPortOnSuccess = true;
                }

                Volatile.Write(ref _iocpBoundHandle, threadPoolBoundHandle);
            }

            return threadPoolBoundHandle;
        }
    }

    internal static SafeSocketHandle CreateWSASocket(Net40.AddressFamily addressFamily, SocketType socketType,
        ProtocolType protocolType)
    {
        return CreateSocket(InnerSafeCloseSocket.CreateWSASocket(addressFamily, socketType, protocolType));
    }

    internal static SafeSocketHandle Accept(SafeSocketHandle socketHandle, byte[] socketAddress,
        ref int socketAddressSize)
    {
        return CreateSocket(InnerSafeCloseSocket.Accept(socketHandle, socketAddress, ref socketAddressSize));
    }

    private void InnerReleaseHandle()
    {
        if (_iocpBoundHandle != null)
        {
            _iocpBoundHandle.Dispose();
        }
    }
}