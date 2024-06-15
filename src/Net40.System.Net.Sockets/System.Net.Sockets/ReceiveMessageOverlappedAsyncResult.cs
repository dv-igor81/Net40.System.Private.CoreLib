using System.Net.Sockets.Net40;
using System.Runtime.InteropServices;

namespace System.Net.Sockets;

internal sealed class ReceiveMessageOverlappedAsyncResult : BaseOverlappedAsyncResult
{
    private Internals.SocketAddress _socketAddressOriginal;

    private Internals.SocketAddress _socketAddress;

    private Net40.SocketFlags _socketFlags;

    private IPPacketInformation _ipPacketInformation;

    private unsafe Interop.Winsock.WSAMsg* _message;

    private unsafe WSABuffer* _wsaBuffer;

    private byte[] _wsaBufferArray;

    private byte[] _controlBuffer;

    internal byte[] _messageBuffer;

    internal Internals.SocketAddress SocketAddress => _socketAddress;

    internal Internals.SocketAddress SocketAddressOriginal
    {
        get { return _socketAddressOriginal; }
        set { _socketAddressOriginal = value; }
    }

    internal Net40.SocketFlags SocketFlags => _socketFlags;

    internal IPPacketInformation IPPacketInformation => _ipPacketInformation;

    internal ReceiveMessageOverlappedAsyncResult(Net40.Socket socket, object asyncState, AsyncCallback asyncCallback)
        : base(socket, asyncState, asyncCallback)
    {
    }

    private IntPtr GetSocketAddressSizePtr()
    {
        return Marshal.UnsafeAddrOfPinnedArrayElement(_socketAddress.Buffer, _socketAddress.GetAddressSizeOffset());
    }

    internal unsafe int GetSocketAddressSize()
    {
        return *(int*)(void*)GetSocketAddressSizePtr();
    }

    internal unsafe void SetUnmanagedStructures(byte[] buffer, int offset, int size,
        Internals.SocketAddress socketAddress, Net40.SocketFlags socketFlags)
    {
        _messageBuffer = new byte[sizeof(Interop.Winsock.WSAMsg)];
        _wsaBufferArray = new byte[sizeof(WSABuffer)];
        Net40.Socket.GetIPProtocolInformation(((Net40.Socket)AsyncObject).AddressFamily, socketAddress,
            out var isIPv, out var isIPv2);
        if (isIPv)
        {
            _controlBuffer = new byte[sizeof(Interop.Winsock.ControlData)];
        }
        else if (isIPv2)
        {
            _controlBuffer = new byte[sizeof(Interop.Winsock.ControlDataIPv6)];
        }

        object[] array = new object[(_controlBuffer != null) ? 5 : 4];
        array[0] = buffer;
        array[1] = _messageBuffer;
        array[2] = _wsaBufferArray;
        _socketAddress = socketAddress;
        _socketAddress.CopyAddressSizeIntoBuffer();
        array[3] = _socketAddress.Buffer;
        if (_controlBuffer != null)
        {
            array[4] = _controlBuffer;
        }

        SetUnmanagedStructures(array);
        _wsaBuffer = (WSABuffer*)(void*)Marshal.UnsafeAddrOfPinnedArrayElement(_wsaBufferArray, 0);
        _wsaBuffer->Length = size;
        _wsaBuffer->Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, offset);
        _message = (Interop.Winsock.WSAMsg*)(void*)Marshal.UnsafeAddrOfPinnedArrayElement(_messageBuffer, 0);
        _message->socketAddress = Marshal.UnsafeAddrOfPinnedArrayElement(_socketAddress.Buffer, 0);
        _message->addressLength = (uint)_socketAddress.Size;
        _message->buffers = Marshal.UnsafeAddrOfPinnedArrayElement(_wsaBufferArray, 0);
        _message->count = 1u;
        if (_controlBuffer != null)
        {
            _message->controlBuffer.Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(_controlBuffer, 0);
            _message->controlBuffer.Length = _controlBuffer.Length;
        }

        _message->flags = socketFlags;
    }

    private unsafe void InitIPPacketInformation()
    {
        if (_controlBuffer.Length == sizeof(Interop.Winsock.ControlData))
        {
            _ipPacketInformation =
                SocketPal.GetIPPacketInformation((Interop.Winsock.ControlData*)(void*)_message->controlBuffer.Pointer);
        }
        else if (_controlBuffer.Length == sizeof(Interop.Winsock.ControlDataIPv6))
        {
            _ipPacketInformation =
                SocketPal.GetIPPacketInformation(
                    (Interop.Winsock.ControlDataIPv6*)(void*)_message->controlBuffer.Pointer);
        }
        else
        {
            _ipPacketInformation = default(IPPacketInformation);
        }
    }

    protected override unsafe void ForceReleaseUnmanagedStructures()
    {
        _socketFlags = _message->flags;
        base.ForceReleaseUnmanagedStructures();
    }

    internal override object PostCompletion(int numBytes)
    {
        InitIPPacketInformation();
        if (ErrorCode == 0 && NetEventSource.IsEnabled)
        {
            LogBuffer(numBytes);
        }

        return base.PostCompletion(numBytes);
    }

    private unsafe void LogBuffer(int size)
    {
        NetEventSource.DumpBuffer(this, _wsaBuffer->Pointer, Math.Min(_wsaBuffer->Length, size));
    }
}