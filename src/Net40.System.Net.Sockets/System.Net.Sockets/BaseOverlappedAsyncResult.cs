using System.Threading;

namespace System.Net.Sockets.Net40;

internal class BaseOverlappedAsyncResult : ContextAwareResult
{
    private static readonly object s_resultObjectSentinel = new object();

    internal int _numBytes;

    private int _cleanupCount;

    private SafeNativeOverlapped _nativeOverlapped;

    private static readonly unsafe IOCompletionCallback s_ioCallback = CompletionPortCallback;

    internal unsafe NativeOverlapped* DangerousOverlappedPointer =>
        (NativeOverlapped*)(void*)_nativeOverlapped.DangerousGetHandle();

    internal virtual object PostCompletion(int numBytes)
    {
        _numBytes = numBytes;
        return s_resultObjectSentinel;
    }

    internal int InternalWaitForCompletionInt32Result()
    {
        InternalWaitForCompletion();
        return _numBytes;
    }

    internal BaseOverlappedAsyncResult(Socket socket, object asyncState, AsyncCallback asyncCallback)
        : base(socket, asyncState, asyncCallback)
    {
        _cleanupCount = 1;
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this, socket, ".ctor");
        }
    }

    internal unsafe void SetUnmanagedStructures(object objectsToPin)
    {
        Socket socket = (Socket)AsyncObject;
        if (socket.SafeHandle.IsInvalid)
        {
            throw new ObjectDisposedException(socket.GetType().FullName);
        }

        ThreadPoolBoundHandle orAllocateThreadPoolBoundHandle = socket.GetOrAllocateThreadPoolBoundHandle();
        NativeOverlapped* handle =
            orAllocateThreadPoolBoundHandle.AllocateNativeOverlapped(s_ioCallback, this, objectsToPin);
        _nativeOverlapped = new SafeNativeOverlapped(socket.SafeHandle, handle);
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(this,
                $"{orAllocateThreadPoolBoundHandle}::AllocateNativeOverlapped. return={_nativeOverlapped}");
        }
    }

    private static unsafe void CompletionPortCallback(uint errorCode, uint numBytes, NativeOverlapped* nativeOverlapped)
    {
        BaseOverlappedAsyncResult baseOverlappedAsyncResult =
            (BaseOverlappedAsyncResult)ThreadPoolBoundHandle.GetNativeOverlappedState(nativeOverlapped);
        if (baseOverlappedAsyncResult.InternalPeekCompleted)
        {
            NetEventSource.Fail(null, $"asyncResult.IsCompleted: {baseOverlappedAsyncResult}");
        }

        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Info(null,
                $"errorCode:{errorCode} numBytes:{numBytes} nativeOverlapped:{(IntPtr)nativeOverlapped}");
        }

        SocketError socketError = (SocketError)errorCode;
        if (socketError != 0 && socketError != SocketError.OperationAborted)
        {
            if (!(baseOverlappedAsyncResult.AsyncObject is Socket socket))
            {
                socketError = SocketError.NotSocket;
            }
            else if (socket.CleanedUp)
            {
                socketError = SocketError.OperationAborted;
            }
            else
            {
                try
                {
                    SocketFlags socketFlags;
                    bool flag = Interop.Winsock.WSAGetOverlappedResult(socket.SafeHandle, nativeOverlapped,
                        out numBytes, wait: false, out socketFlags);
                    if (!flag)
                    {
                        socketError = SocketPal.GetLastSocketError();
                    }

                    if (flag)
                    {
                        NetEventSource.Fail(baseOverlappedAsyncResult,
                            $"Unexpectedly succeeded. errorCode:{errorCode} numBytes:{numBytes}");
                    }
                }
                catch (ObjectDisposedException)
                {
                    socketError = SocketError.OperationAborted;
                }
            }
        }

        baseOverlappedAsyncResult.CompletionCallback((int)numBytes, socketError);
    }

    private void CompletionCallback(int numBytes, SocketError socketError)
    {
        ErrorCode = (int)socketError;
        object result = PostCompletion(numBytes);
        ReleaseUnmanagedStructures();
        InvokeCallback(result);
    }

    internal SocketError ProcessOverlappedResult(bool success, int bytesTransferred)
    {
        if (success)
        {
            Socket socket = (Socket)AsyncObject;
            if (socket.SafeHandle.SkipCompletionPortOnSuccess)
            {
                CompletionCallback(bytesTransferred, SocketError.Success);
                return SocketError.Success;
            }

            return SocketError.IOPending;
        }

        SocketError lastSocketError = SocketPal.GetLastSocketError();
        if (lastSocketError == SocketError.IOPending)
        {
            return SocketError.IOPending;
        }

        ReleaseUnmanagedStructures();
        return lastSocketError;
    }

    internal void ReleaseUnmanagedStructures()
    {
        if (Interlocked.Decrement(ref _cleanupCount) == 0)
        {
            ForceReleaseUnmanagedStructures();
        }
    }

    protected override void Cleanup()
    {
        base.Cleanup();
        if (_cleanupCount > 0 && Interlocked.Exchange(ref _cleanupCount, 0) > 0)
        {
            ForceReleaseUnmanagedStructures();
        }
    }

    protected virtual void ForceReleaseUnmanagedStructures()
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, null);
        }

        _nativeOverlapped.Dispose();
        _nativeOverlapped = null;
        GC.SuppressFinalize(this);
    }
}