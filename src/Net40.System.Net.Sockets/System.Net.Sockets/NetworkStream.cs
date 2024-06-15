using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Net.Sockets.Net40;

public class NetworkStream : StreamBase
{
    private readonly Socket _streamSocket;

    private readonly bool _ownsSocket;

    private bool _readable;

    private bool _writeable;

    private int _closeTimeout = -1;

    private volatile bool _cleanedUp;

    private int _currentReadTimeout = -1;

    private int _currentWriteTimeout = -1;

    protected Socket Socket => _streamSocket;

    protected bool Readable
    {
        get { return _readable; }
        set { _readable = value; }
    }

    protected bool Writeable
    {
        get { return _writeable; }
        set { _writeable = value; }
    }

    public override bool CanRead => _readable;

    public override bool CanSeek => false;

    public override bool CanWrite => _writeable;

    public override bool CanTimeout => true;

    public override int ReadTimeout
    {
        get
        {
            int num = (int)_streamSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout);
            if (num == 0)
            {
                return -1;
            }

            return num;
        }
        set
        {
            if (value <= 0 && value != -1)
            {
                throw new ArgumentOutOfRangeException("value", SR.net_io_timeout_use_gt_zero);
            }

            SetSocketTimeoutOption(SocketShutdown.Receive, value, silent: false);
        }
    }

    public override int WriteTimeout
    {
        get
        {
            int num = (int)_streamSocket.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout);
            if (num == 0)
            {
                return -1;
            }

            return num;
        }
        set
        {
            if (value <= 0 && value != -1)
            {
                throw new ArgumentOutOfRangeException("value", SR.net_io_timeout_use_gt_zero);
            }

            SetSocketTimeoutOption(SocketShutdown.Send, value, silent: false);
        }
    }

    public virtual bool DataAvailable
    {
        get
        {
            if (_cleanedUp)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            return _streamSocket.Available != 0;
        }
    }

    public override long Length
    {
        get { throw new NotSupportedException(SR.net_noseek); }
    }

    public override long Position
    {
        get { throw new NotSupportedException(SR.net_noseek); }
        set { throw new NotSupportedException(SR.net_noseek); }
    }

    public NetworkStream(Socket socket)
        : this(socket, FileAccess.ReadWrite, ownsSocket: false)
    {
    }

    public NetworkStream(Socket socket, bool ownsSocket)
        : this(socket, FileAccess.ReadWrite, ownsSocket)
    {
    }

    public NetworkStream(Socket socket, FileAccess access)
        : this(socket, access, ownsSocket: false)
    {
    }

    public NetworkStream(Socket socket, FileAccess access, bool ownsSocket)
    {
        if (socket == null)
        {
            throw new ArgumentNullException("socket");
        }

        if (!socket.Blocking)
        {
            throw new IOException(SR.net_sockets_blocking);
        }

        if (!socket.Connected)
        {
            throw new IOException(SR.net_notconnected);
        }

        if (socket.SocketType != SocketType.Stream)
        {
            throw new IOException(SR.net_notstream);
        }

        _streamSocket = socket;
        _ownsSocket = ownsSocket;
        switch (access)
        {
            case FileAccess.Read:
                _readable = true;
                break;
            case FileAccess.Write:
                _writeable = true;
                break;
            default:
                _readable = true;
                _writeable = true;
                break;
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException(SR.net_noseek);
    }

    public override int Read(byte[] buffer, int offset, int size)
    {
        bool canRead = CanRead;
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!canRead)
        {
            throw new InvalidOperationException(SR.net_writeonlystream);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if ((uint)offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if ((uint)size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        try
        {
            return _streamSocket.Receive(buffer, offset, size, SocketFlags.None);
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_readfailure, ex.Message), ex);
        }
    }

    protected override int Read(Span<byte> buffer)
    {
        if (GetType() != typeof(NetworkStream))
        {
            return base.Read(buffer);
        }

        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!CanRead)
        {
            throw new InvalidOperationException(SR.net_writeonlystream);
        }

        Net40.SocketError errorCode;
        int result = _streamSocket.Receive(buffer, SocketFlags.None, out errorCode);
        if (errorCode != 0)
        {
            Net40.SocketException ex = new Net40.SocketException((int)errorCode);
            throw new IOException(SR.Format(SR.net_io_readfailure, ex.Message),
                (Exception?)ex);
        }

        return result;
    }

    public override unsafe int ReadByte()
    {
        Unsafe.SkipInit(out byte result);
        if (Read(new Span<byte>(&result, 1)) != 0)
        {
            return result;
        }

        return -1;
    }

    public override void Write(byte[] buffer, int offset, int size)
    {
        bool canWrite = CanWrite;
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!canWrite)
        {
            throw new InvalidOperationException(SR.net_readonlystream);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if ((uint)offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if ((uint)size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        try
        {
            _streamSocket.Send(buffer, offset, size, SocketFlags.None);
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_writefailure, ex.Message), ex);
        }
    }


    protected override void Write(ReadOnlySpan<byte> buffer)
    {
        if (GetType() != typeof(NetworkStream))
        {
            base.Write(buffer);
            return;
        }

        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!CanWrite)
        {
            throw new InvalidOperationException(SR.net_readonlystream);
        }

        _streamSocket.Send(buffer, SocketFlags.None, out var errorCode);
        if (errorCode == Net40.SocketError.Success)
        {
            return;
        }

        Net40.SocketException ex = new Net40.SocketException((int)errorCode);
        throw new IOException(SR.Format(SR.net_io_writefailure, ex.Message),
            (Exception?)ex);
    }

    public override unsafe void WriteByte(byte value)
    {
        Write(new ReadOnlySpan<byte>(&value, 1));
    }

    public void Close(int timeout)
    {
        if (timeout < -1)
        {
            throw new ArgumentOutOfRangeException("timeout");
        }

        _closeTimeout = timeout;
        Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        bool cleanedUp = _cleanedUp;
        _cleanedUp = true;
        if (!cleanedUp && disposing)
        {
            _readable = false;
            _writeable = false;
            if (_ownsSocket)
            {
                _streamSocket.InternalShutdown(SocketShutdown.Both);
                _streamSocket.Close(_closeTimeout);
            }
        }

        base.Dispose(disposing);
    }

    ~NetworkStream()
    {
        Dispose(disposing: false);
    }

    public override IAsyncResult BeginRead(byte[] buffer, int offset, int size, AsyncCallback callback, object state)
    {
        bool canRead = CanRead;
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!canRead)
        {
            throw new InvalidOperationException(SR.net_writeonlystream);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if ((uint)offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if ((uint)size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        try
        {
            return _streamSocket.BeginReceive(buffer, offset, size, SocketFlags.None, callback, state);
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_readfailure, ex.Message), ex);
        }
    }

    public override int EndRead(IAsyncResult asyncResult)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        try
        {
            return _streamSocket.EndReceive(asyncResult);
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_readfailure, ex.Message), ex);
        }
    }

    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int size, AsyncCallback callback, object state)
    {
        bool canWrite = CanWrite;
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!canWrite)
        {
            throw new InvalidOperationException(SR.net_readonlystream);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if ((uint)offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if ((uint)size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        try
        {
            return _streamSocket.BeginSend(buffer, offset, size, SocketFlags.None, callback, state);
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_writefailure, ex.Message), ex);
        }
    }

    public override void EndWrite(IAsyncResult asyncResult)
    {
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (asyncResult == null)
        {
            throw new ArgumentNullException("asyncResult");
        }

        try
        {
            _streamSocket.EndSend(asyncResult);
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_writefailure, ex.Message), ex);
        }
    }

    protected override Task<int> ReadAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
    {
        bool canRead = CanRead;
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!canRead)
        {
            throw new InvalidOperationException(SR.net_writeonlystream);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if ((uint)offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if ((uint)size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        try
        {
            return _streamSocket.ReceiveAsync(new Memory<byte>(buffer, offset, size), SocketFlags.None,
                fromNetworkStream: true, cancellationToken).AsTask();
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_readfailure, ex.Message), ex);
        }
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        bool canRead = CanRead;
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!canRead)
        {
            throw new InvalidOperationException(SR.net_writeonlystream);
        }

        try
        {
            return _streamSocket.ReceiveAsync(buffer, SocketFlags.None, fromNetworkStream: true, cancellationToken);
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_readfailure, ex.Message), ex);
        }
    }

    protected override Task WriteAsync(byte[] buffer, int offset, int size, CancellationToken cancellationToken)
    {
        bool canWrite = CanWrite;
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!canWrite)
        {
            throw new InvalidOperationException(SR.net_readonlystream);
        }

        if (buffer == null)
        {
            throw new ArgumentNullException("buffer");
        }

        if ((uint)offset > buffer.Length)
        {
            throw new ArgumentOutOfRangeException("offset");
        }

        if ((uint)size > buffer.Length - offset)
        {
            throw new ArgumentOutOfRangeException("size");
        }

        try
        {
            return _streamSocket.SendAsyncForNetworkStream(new ReadOnlyMemory<byte>(buffer, offset, size),
                SocketFlags.None, cancellationToken).AsTask();
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_writefailure, ex.Message), ex);
        }
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        bool canWrite = CanWrite;
        if (_cleanedUp)
        {
            throw new ObjectDisposedException(GetType().FullName);
        }

        if (!canWrite)
        {
            throw new InvalidOperationException(SR.net_readonlystream);
        }

        try
        {
            return _streamSocket.SendAsyncForNetworkStream(buffer, SocketFlags.None, cancellationToken);
        }
        catch (Exception ex) when (!(ex is OutOfMemoryException))
        {
            throw new IOException(SR.Format(SR.net_io_writefailure, ex.Message), ex);
        }
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return TaskExEx.CompletedTask;
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException(SR.net_noseek);
    }

    internal void SetSocketTimeoutOption(SocketShutdown mode, int timeout, bool silent)
    {
        if (NetEventSource.IsEnabled)
        {
            NetEventSource.Enter(this, mode, timeout, silent);
        }

        if (timeout < 0)
        {
            timeout = 0;
        }

        if ((mode == SocketShutdown.Send || mode == SocketShutdown.Both) && timeout != _currentWriteTimeout)
        {
            _streamSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, timeout, silent);
            _currentWriteTimeout = timeout;
        }

        if ((mode == SocketShutdown.Receive || mode == SocketShutdown.Both) && timeout != _currentReadTimeout)
        {
            _streamSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, timeout, silent);
            _currentReadTimeout = timeout;
        }
    }
}