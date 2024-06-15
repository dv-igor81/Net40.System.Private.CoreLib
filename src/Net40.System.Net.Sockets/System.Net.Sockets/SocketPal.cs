using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace System.Net.Sockets.Net40;


using Dns = System.Net.Net40.Dns;
using IPAddress = Net.Net40.IPAddress;

internal static class SocketPal
{
    private static void MicrosecondsToTimeValue(long microseconds, ref Interop.Winsock.TimeValue socketTime)
    {
        socketTime.Seconds = (int)(microseconds / 1000000);
        socketTime.Microseconds = (int)(microseconds % 1000000);
    }

    public static void Initialize()
    {
        Dns.GetHostName();
    }

    public static SocketError GetLastSocketError()
    {
        return (SocketError)Marshal.GetLastWin32Error();
    }

    public static SocketError CreateSocket(AddressFamily addressFamily, SocketType socketType,
        ProtocolType protocolType, out SafeSocketHandle socket)
    {
        socket = SafeSocketHandle.CreateWSASocket(addressFamily, socketType, protocolType);
        if (!socket.IsInvalid)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError SetBlocking(SafeSocketHandle handle, bool shouldBlock, out bool willBlock)
    {
        int argp = ((!shouldBlock) ? (-1) : 0);
        SocketError socketError = Interop.Winsock.ioctlsocket(handle, -2147195266, ref argp);
        if (socketError == SocketError.SocketError)
        {
            socketError = GetLastSocketError();
        }

        willBlock = argp == 0;
        return socketError;
    }

    public static SocketError GetSockName(SafeSocketHandle handle, byte[] buffer, ref int nameLen)
    {
        SocketError socketError = Interop.Winsock.getsockname(handle, buffer, ref nameLen);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError GetAvailable(SafeSocketHandle handle, out int available)
    {
        int argp = 0;
        SocketError socketError = Interop.Winsock.ioctlsocket(handle, 1074030207, ref argp);
        available = argp;
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError GetPeerName(SafeSocketHandle handle, byte[] buffer, ref int nameLen)
    {
        SocketError socketError = Interop.Winsock.getpeername(handle, buffer, ref nameLen);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError Bind(SafeSocketHandle handle, ProtocolType socketProtocolType, byte[] buffer, int nameLen)
    {
        SocketError socketError = Interop.Winsock.bind(handle, buffer, nameLen);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError Listen(SafeSocketHandle handle, int backlog)
    {
        SocketError socketError = Interop.Winsock.listen(handle, backlog);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError Accept(SafeSocketHandle handle, byte[] buffer, ref int nameLen,
        out SafeSocketHandle socket)
    {
        socket = SafeSocketHandle.Accept(handle, buffer, ref nameLen);
        if (!socket.IsInvalid)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError Connect(SafeSocketHandle handle, byte[] peerAddress, int peerAddressLen)
    {
        SocketError socketError = Interop.Winsock.WSAConnect(handle.DangerousGetHandle(), peerAddress, peerAddressLen,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static unsafe SocketError Send(SafeSocketHandle handle, IList<ArraySegment<byte>> buffers,
        SocketFlags socketFlags, out int bytesTransferred)
    {
        int count = buffers.Count;
        bool flag = count <= 16;
        WSABuffer[] array = null;
        GCHandle[] array2 = null;
        Span<WSABuffer> span = default(Span<WSABuffer>);
        Span<GCHandle> span2 = default(Span<GCHandle>);
        if (flag)
        {
            span = stackalloc WSABuffer[16];
            span2 = stackalloc GCHandle[16];
        }
        else
        {
            span = (array = ArrayPool<WSABuffer>.Shared.Rent(count));
            span2 = (array2 = ArrayPool<GCHandle>.Shared.Rent(count));
        }

        span2 = span2.Slice(0, count);
        span2.Clear();
        try
        {
            for (int i = 0; i < count; i++)
            {
                ArraySegment<byte> segment = buffers[i];
                RangeValidationHelpers.ValidateSegment(segment);
                span2[i] = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
                span[i].Length = segment.Count;
                span[i].Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(segment.Array, segment.Offset);
            }

            SocketError socketError = Interop.Winsock.WSASend(handle.DangerousGetHandle(), span, count,
                out bytesTransferred, socketFlags, null, IntPtr.Zero);
            if (socketError == SocketError.SocketError)
            {
                socketError = GetLastSocketError();
            }

            return socketError;
        }
        finally
        {
            for (int j = 0; j < count; j++)
            {
                if (span2[j].IsAllocated)
                {
                    span2[j].Free();
                }
            }

            if (!flag)
            {
                ArrayPool<WSABuffer>.Shared.Return(array);
                ArrayPool<GCHandle>.Shared.Return(array2);
            }
        }
    }

    public static SocketError Send(SafeSocketHandle handle, byte[] buffer, int offset, int size,
        SocketFlags socketFlags, out int bytesTransferred)
    {
        return Send(handle, new ReadOnlySpan<byte>(buffer, offset, size), socketFlags, out bytesTransferred);
    }

    public static unsafe SocketError Send(SafeSocketHandle handle, ReadOnlySpan<byte> buffer, SocketFlags socketFlags,
        out int bytesTransferred)
    {
        int num;
        fixed (byte* pinnedBuffer = &MemoryMarshal.GetReference(buffer))
        {
            num = Interop.Winsock.send(handle.DangerousGetHandle(), pinnedBuffer, buffer.Length, socketFlags);
        }

        if (num == -1)
        {
            bytesTransferred = 0;
            return GetLastSocketError();
        }

        bytesTransferred = num;
        return SocketError.Success;
    }

    public static unsafe SocketError SendFile(SafeSocketHandle handle, SafeFileHandle fileHandle, byte[] preBuffer,
        byte[] postBuffer, TransmitFileOptions flags)
    {
        fixed (byte* ptr2 = preBuffer)
        {
            fixed (byte* ptr = postBuffer)
            {
                if (!TransmitFileHelper(handle, fileHandle, null, preBuffer, postBuffer, flags))
                {
                    return GetLastSocketError();
                }

                return SocketError.Success;
            }
        }
    }

    public static unsafe SocketError SendTo(SafeSocketHandle handle, byte[] buffer, int offset, int size,
        SocketFlags socketFlags, byte[] peerAddress, int peerAddressSize, out int bytesTransferred)
    {
        int num;
        if (buffer.Length == 0)
        {
            num = Interop.Winsock.sendto(handle.DangerousGetHandle(), null, 0, socketFlags, peerAddress,
                peerAddressSize);
        }
        else
        {
            fixed (byte* ptr = &buffer[0])
            {
                num = Interop.Winsock.sendto(handle.DangerousGetHandle(), ptr + offset, size, socketFlags, peerAddress,
                    peerAddressSize);
            }
        }

        if (num == -1)
        {
            bytesTransferred = 0;
            return GetLastSocketError();
        }

        bytesTransferred = num;
        return SocketError.Success;
    }

    public static unsafe SocketError Receive(SafeSocketHandle handle, IList<ArraySegment<byte>> buffers,
        ref SocketFlags socketFlags, out int bytesTransferred)
    {
        int count = buffers.Count;
        bool flag = count <= 16;
        WSABuffer[] array = null;
        GCHandle[] array2 = null;
        Span<WSABuffer> span = default(Span<WSABuffer>);
        Span<GCHandle> span2 = default(Span<GCHandle>);
        if (flag)
        {
            span = stackalloc WSABuffer[16];
            span2 = stackalloc GCHandle[16];
        }
        else
        {
            span = (array = ArrayPool<WSABuffer>.Shared.Rent(count));
            span2 = (array2 = ArrayPool<GCHandle>.Shared.Rent(count));
        }

        span2 = span2.Slice(0, count);
        span2.Clear();
        try
        {
            for (int i = 0; i < count; i++)
            {
                ArraySegment<byte> segment = buffers[i];
                RangeValidationHelpers.ValidateSegment(segment);
                span2[i] = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
                span[i].Length = segment.Count;
                span[i].Pointer = Marshal.UnsafeAddrOfPinnedArrayElement(segment.Array, segment.Offset);
            }

            SocketError socketError = Interop.Winsock.WSARecv(handle.DangerousGetHandle(), span, count,
                out bytesTransferred, ref socketFlags, null, IntPtr.Zero);
            if (socketError == SocketError.SocketError)
            {
                socketError = GetLastSocketError();
            }

            return socketError;
        }
        finally
        {
            for (int j = 0; j < count; j++)
            {
                if (span2[j].IsAllocated)
                {
                    span2[j].Free();
                }
            }

            if (!flag)
            {
                ArrayPool<WSABuffer>.Shared.Return(array);
                ArrayPool<GCHandle>.Shared.Return(array2);
            }
        }
    }

    public static SocketError Receive(SafeSocketHandle handle, byte[] buffer, int offset, int size,
        SocketFlags socketFlags, out int bytesTransferred)
    {
        return Receive(handle, new Span<byte>(buffer, offset, size), socketFlags, out bytesTransferred);
    }

    public static unsafe SocketError Receive(SafeSocketHandle handle, Span<byte> buffer, SocketFlags socketFlags,
        out int bytesTransferred)
    {
        int num;
        fixed (byte* pinnedBuffer = &MemoryMarshal.GetReference(buffer))
        {
            num = Interop.Winsock.recv(handle.DangerousGetHandle(), pinnedBuffer, buffer.Length, socketFlags);
        }

        if (num == -1)
        {
            bytesTransferred = 0;
            return GetLastSocketError();
        }

        bytesTransferred = num;
        return SocketError.Success;
    }

    public static unsafe IPPacketInformation GetIPPacketInformation(Interop.Winsock.ControlData* controlBuffer)
    {
        IPAddress address = ((controlBuffer->length == UIntPtr.Zero)
            ? IPAddress.None
            : new IPAddress(controlBuffer->address));
        return new IPPacketInformation(address, (int)controlBuffer->index);
    }

    public static unsafe IPPacketInformation GetIPPacketInformation(Interop.Winsock.ControlDataIPv6* controlBuffer)
    {
        IPAddress address = ((controlBuffer->length != UIntPtr.Zero)
            ? new IPAddress(new ReadOnlySpan<byte>(controlBuffer->address, 16))
            : IPAddress.IPv6None);
        return new IPPacketInformation(address, (int)controlBuffer->index);
    }

    public static unsafe SocketError ReceiveMessageFrom(Socket socket, SafeSocketHandle handle, byte[] buffer,
        int offset, int size, ref SocketFlags socketFlags, Internals.SocketAddress socketAddress,
        out Internals.SocketAddress receiveAddress, out IPPacketInformation ipPacketInformation,
        out int bytesTransferred)
    {
        Socket.GetIPProtocolInformation(socket.AddressFamily, socketAddress, out var isIPv, out var isIPv2);
        bytesTransferred = 0;
        receiveAddress = socketAddress;
        ipPacketInformation = default(IPPacketInformation);
        fixed (byte* ptr2 = buffer)
        {
            fixed (byte* ptr = socketAddress.Buffer)
            {
                Unsafe.SkipInit(out Interop.Winsock.WSAMsg wSAMsg);
                wSAMsg.socketAddress = (IntPtr)ptr;
                wSAMsg.addressLength = (uint)socketAddress.Size;
                wSAMsg.flags = socketFlags;
                Unsafe.SkipInit(out WSABuffer wSABuffer);
                wSABuffer.Length = size;
                wSABuffer.Pointer = (IntPtr)(ptr2 + offset);
                wSAMsg.buffers = (IntPtr)(&wSABuffer);
                wSAMsg.count = 1u;
                if (isIPv)
                {
                    Unsafe.SkipInit(out Interop.Winsock.ControlData controlData);
                    wSAMsg.controlBuffer.Pointer = (IntPtr)(&controlData);
                    wSAMsg.controlBuffer.Length = sizeof(Interop.Winsock.ControlData);
                    if (socket.WSARecvMsgBlocking(handle.DangerousGetHandle(), (IntPtr)(&wSAMsg), out bytesTransferred,
                            IntPtr.Zero, IntPtr.Zero) == SocketError.SocketError)
                    {
                        return GetLastSocketError();
                    }

                    ipPacketInformation = GetIPPacketInformation(&controlData);
                }
                else if (isIPv2)
                {
                    Unsafe.SkipInit(out Interop.Winsock.ControlDataIPv6 controlDataIPv);
                    wSAMsg.controlBuffer.Pointer = (IntPtr)(&controlDataIPv);
                    wSAMsg.controlBuffer.Length = sizeof(Interop.Winsock.ControlDataIPv6);
                    if (socket.WSARecvMsgBlocking(handle.DangerousGetHandle(), (IntPtr)(&wSAMsg), out bytesTransferred,
                            IntPtr.Zero, IntPtr.Zero) == SocketError.SocketError)
                    {
                        return GetLastSocketError();
                    }

                    ipPacketInformation = GetIPPacketInformation(&controlDataIPv);
                }
                else
                {
                    wSAMsg.controlBuffer.Pointer = IntPtr.Zero;
                    wSAMsg.controlBuffer.Length = 0;
                    if (socket.WSARecvMsgBlocking(handle.DangerousGetHandle(), (IntPtr)(&wSAMsg), out bytesTransferred,
                            IntPtr.Zero, IntPtr.Zero) == SocketError.SocketError)
                    {
                        return GetLastSocketError();
                    }
                }

                socketFlags = wSAMsg.flags;
            }
        }

        return SocketError.Success;
    }

    public static unsafe SocketError ReceiveFrom(SafeSocketHandle handle, byte[] buffer, int offset, int size,
        SocketFlags socketFlags, byte[] socketAddress, ref int addressLength, out int bytesTransferred)
    {
        int num;
        if (buffer.Length == 0)
        {
            num = Interop.Winsock.recvfrom(handle.DangerousGetHandle(), null, 0, socketFlags, socketAddress,
                ref addressLength);
        }
        else
        {
            fixed (byte* ptr = &buffer[0])
            {
                num = Interop.Winsock.recvfrom(handle.DangerousGetHandle(), ptr + offset, size, socketFlags,
                    socketAddress, ref addressLength);
            }
        }

        if (num == -1)
        {
            bytesTransferred = 0;
            return GetLastSocketError();
        }

        bytesTransferred = num;
        return SocketError.Success;
    }

    public static SocketError WindowsIoctl(SafeSocketHandle handle, int ioControlCode, byte[] optionInValue,
        byte[] optionOutValue, out int optionLength)
    {
        if (ioControlCode == -2147195266)
        {
            throw new InvalidOperationException(SR.net_sockets_useblocking);
        }

        SocketError socketError = Interop.Winsock.WSAIoctl_Blocking(handle.DangerousGetHandle(), ioControlCode,
            optionInValue, (optionInValue != null) ? optionInValue.Length : 0, optionOutValue,
            (optionOutValue != null) ? optionOutValue.Length : 0, out optionLength, IntPtr.Zero, IntPtr.Zero);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError SetSockOpt(SafeSocketHandle handle, SocketOptionLevel optionLevel,
        SocketOptionName optionName, int optionValue)
    {
        SocketError socketError =
            ((optionLevel != SocketOptionLevel.Tcp ||
              (optionName != SocketOptionName.TypeOfService && optionName != SocketOptionName.BlockSource) ||
              !IOControlKeepAlive.IsNeeded)
                ? Interop.Winsock.setsockopt(handle, optionLevel, optionName, ref optionValue, 4)
                : IOControlKeepAlive.Set(handle, optionName, optionValue));
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError SetSockOpt(SafeSocketHandle handle, SocketOptionLevel optionLevel,
        SocketOptionName optionName, byte[] optionValue)
    {
        if (optionLevel == SocketOptionLevel.Tcp &&
            (optionName == SocketOptionName.TypeOfService || optionName == SocketOptionName.BlockSource) &&
            IOControlKeepAlive.IsNeeded)
        {
            return IOControlKeepAlive.Set(handle, optionName, optionValue);
        }

        SocketError socketError = Interop.Winsock.setsockopt(handle, optionLevel, optionName, optionValue,
            (optionValue != null) ? optionValue.Length : 0);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static void SetReceivingDualModeIPv4PacketInformation(Socket socket)
    {
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, optionValue: true);
    }

    public static SocketError SetMulticastOption(SafeSocketHandle handle, SocketOptionName optionName,
        MulticastOption optionValue)
    {
        Interop.Winsock.IPMulticastRequest mreq = default(Interop.Winsock.IPMulticastRequest);
        mreq.MulticastAddress = (int)optionValue.Group.Address;
        if (optionValue.LocalAddress != null)
        {
            mreq.InterfaceAddress = (int)optionValue.LocalAddress.Address;
        }
        else
        {
            int interfaceAddress = IPAddress.HostToNetworkOrder(optionValue.InterfaceIndex);
            mreq.InterfaceAddress = interfaceAddress;
        }

        SocketError socketError = Interop.Winsock.setsockopt(handle, SocketOptionLevel.IP, optionName, ref mreq,
            Interop.Winsock.IPMulticastRequest.Size);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError SetIPv6MulticastOption(SafeSocketHandle handle, SocketOptionName optionName,
        IPv6MulticastOption optionValue)
    {
        Interop.Winsock.IPv6MulticastRequest mreq = default(Interop.Winsock.IPv6MulticastRequest);
        mreq.MulticastAddress = optionValue.Group.GetAddressBytes();
        mreq.InterfaceIndex = (int)optionValue.InterfaceIndex;
        SocketError socketError = Interop.Winsock.setsockopt(handle, SocketOptionLevel.IPv6, optionName, ref mreq,
            Interop.Winsock.IPv6MulticastRequest.Size);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError SetLingerOption(SafeSocketHandle handle, LingerOption optionValue)
    {
        Interop.Winsock.Linger linger = default(Interop.Winsock.Linger);
        linger.OnOff = (ushort)(optionValue.Enabled ? 1 : 0);
        linger.Time = (ushort)optionValue.LingerTime;
        SocketError socketError =
            Interop.Winsock.setsockopt(handle, SocketOptionLevel.Socket, SocketOptionName.Linger, ref linger, 4);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static void SetIPProtectionLevel(Socket socket, SocketOptionLevel optionLevel, int protectionLevel)
    {
        socket.SetSocketOption(optionLevel, SocketOptionName.IPProtectionLevel, protectionLevel);
    }

    public static SocketError GetSockOpt(SafeSocketHandle handle, SocketOptionLevel optionLevel,
        SocketOptionName optionName, out int optionValue)
    {
        if (optionLevel == SocketOptionLevel.Tcp &&
            (optionName == SocketOptionName.TypeOfService || optionName == SocketOptionName.BlockSource) &&
            IOControlKeepAlive.IsNeeded)
        {
            optionValue = IOControlKeepAlive.Get(handle, optionName);
            return SocketError.Success;
        }

        int optionLength = 4;
        SocketError socketError =
            Interop.Winsock.getsockopt(handle, optionLevel, optionName, out optionValue, ref optionLength);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError GetSockOpt(SafeSocketHandle handle, SocketOptionLevel optionLevel,
        SocketOptionName optionName, byte[] optionValue, ref int optionLength)
    {
        if (optionLevel == SocketOptionLevel.Tcp &&
            (optionName == SocketOptionName.TypeOfService || optionName == SocketOptionName.BlockSource) &&
            IOControlKeepAlive.IsNeeded)
        {
            return IOControlKeepAlive.Get(handle, optionName, optionValue, ref optionLength);
        }

        SocketError socketError =
            Interop.Winsock.getsockopt(handle, optionLevel, optionName, optionValue, ref optionLength);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static SocketError GetMulticastOption(SafeSocketHandle handle, SocketOptionName optionName,
        out MulticastOption optionValue)
    {
        Interop.Winsock.IPMulticastRequest optionValue2 = default(Interop.Winsock.IPMulticastRequest);
        int optionLength = Interop.Winsock.IPMulticastRequest.Size;
        SocketError socketError = Interop.Winsock.getsockopt(handle, SocketOptionLevel.IP, optionName, out optionValue2,
            ref optionLength);
        if (socketError == SocketError.SocketError)
        {
            optionValue = null;
            return GetLastSocketError();
        }

        IPAddress group = new IPAddress(optionValue2.MulticastAddress);
        IPAddress mcint = new IPAddress(optionValue2.InterfaceAddress);
        optionValue = new MulticastOption(group, mcint);
        return SocketError.Success;
    }

    public static SocketError GetIPv6MulticastOption(SafeSocketHandle handle, SocketOptionName optionName,
        out IPv6MulticastOption optionValue)
    {
        Interop.Winsock.IPv6MulticastRequest optionValue2 = default(Interop.Winsock.IPv6MulticastRequest);
        int optionLength = Interop.Winsock.IPv6MulticastRequest.Size;
        SocketError socketError = Interop.Winsock.getsockopt(handle, SocketOptionLevel.IP, optionName, out optionValue2,
            ref optionLength);
        if (socketError == SocketError.SocketError)
        {
            optionValue = null;
            return GetLastSocketError();
        }

        optionValue =
            new IPv6MulticastOption(new IPAddress(optionValue2.MulticastAddress), optionValue2.InterfaceIndex);
        return SocketError.Success;
    }

    public static SocketError GetLingerOption(SafeSocketHandle handle, out LingerOption optionValue)
    {
        Interop.Winsock.Linger optionValue2 = default(Interop.Winsock.Linger);
        int optionLength = 4;
        SocketError socketError = Interop.Winsock.getsockopt(handle, SocketOptionLevel.Socket, SocketOptionName.Linger,
            out optionValue2, ref optionLength);
        if (socketError == SocketError.SocketError)
        {
            optionValue = null;
            return GetLastSocketError();
        }

        optionValue = new LingerOption(optionValue2.OnOff != 0, optionValue2.Time);
        return SocketError.Success;
    }

    public static unsafe SocketError Poll(SafeSocketHandle handle, int microseconds, SelectMode mode, out bool status)
    {
        IntPtr intPtr = handle.DangerousGetHandle();
        IntPtr* ptr = stackalloc IntPtr[2]
        {
            (IntPtr)1,
            intPtr
        };
        Interop.Winsock.TimeValue socketTime = default(Interop.Winsock.TimeValue);
        int num;
        if (microseconds != -1)
        {
            MicrosecondsToTimeValue((uint)microseconds, ref socketTime);
            num = Interop.Winsock.select(0, (mode == SelectMode.SelectRead) ? ptr : null,
                (mode == SelectMode.SelectWrite) ? ptr : null, (mode == SelectMode.SelectError) ? ptr : null,
                ref socketTime);
        }
        else
        {
            num = Interop.Winsock.select(0, (mode == SelectMode.SelectRead) ? ptr : null,
                (mode == SelectMode.SelectWrite) ? ptr : null, (mode == SelectMode.SelectError) ? ptr : null,
                IntPtr.Zero);
        }

        if (num == -1)
        {
            status = false;
            return GetLastSocketError();
        }

        status = (int)(*ptr) != 0 && ptr[1] == intPtr;
        return SocketError.Success;
    }

    public static unsafe SocketError Select(IList checkRead, IList checkWrite, IList checkError, int microseconds)
    {
        IntPtr[] lease2 = null;
        IntPtr[] lease3 = null;
        IntPtr[] lease4 = null;
        try
        {
            Span<IntPtr> span2;
            Span<IntPtr> span3 =
                ((!ShouldStackAlloc(checkRead, ref lease2, out span2)) ? span2 : stackalloc IntPtr[64]);
            Span<IntPtr> span4 = span3;
            Socket.SocketListToFileDescriptorSet(checkRead, span4);
            Span<IntPtr> span5 =
                ((!ShouldStackAlloc(checkWrite, ref lease3, out span2)) ? span2 : stackalloc IntPtr[64]);
            Span<IntPtr> span6 = span5;
            Socket.SocketListToFileDescriptorSet(checkWrite, span6);
            Span<IntPtr> span7 =
                ((!ShouldStackAlloc(checkError, ref lease4, out span2)) ? span2 : stackalloc IntPtr[64]);
            Span<IntPtr> span8 = span7;
            Socket.SocketListToFileDescriptorSet(checkError, span8);
            int num;
            fixed (IntPtr* readfds = &MemoryMarshal.GetReference(span4))
            {
                fixed (IntPtr* writefds = &MemoryMarshal.GetReference(span6))
                {
                    fixed (IntPtr* exceptfds = &MemoryMarshal.GetReference(span8))
                    {
                        if (microseconds != -1)
                        {
                            Interop.Winsock.TimeValue socketTime = default(Interop.Winsock.TimeValue);
                            MicrosecondsToTimeValue((uint)microseconds, ref socketTime);
                            num = Interop.Winsock.select(0, readfds, writefds, exceptfds, ref socketTime);
                        }
                        else
                        {
                            num = Interop.Winsock.select(0, readfds, writefds, exceptfds, IntPtr.Zero);
                        }
                    }
                }
            }

            if (NetEventSource.IsEnabled)
            {
                NetEventSource.Info(null, $"Interop.Winsock.select returns socketCount:{num}");
            }

            if (num == -1)
            {
                return GetLastSocketError();
            }

            Socket.SelectFileDescriptor(checkRead, span4);
            Socket.SelectFileDescriptor(checkWrite, span6);
            Socket.SelectFileDescriptor(checkError, span8);
            return SocketError.Success;
        }
        finally
        {
            if (lease2 != null)
            {
                ArrayPool<IntPtr>.Shared.Return(lease2);
            }

            if (lease3 != null)
            {
                ArrayPool<IntPtr>.Shared.Return(lease3);
            }

            if (lease4 != null)
            {
                ArrayPool<IntPtr>.Shared.Return(lease4);
            }
        }

        static bool ShouldStackAlloc(IList list, ref IntPtr[] lease, out Span<IntPtr> span)
        {
            int count;
            if (list == null || (count = list.Count) == 0)
            {
                span = default(Span<IntPtr>);
                return false;
            }

            if (count >= 64)
            {
                span = (lease = ArrayPool<IntPtr>.Shared.Rent(count + 1));
                return false;
            }

            span = default(Span<IntPtr>);
            return true;
        }
    }

    public static SocketError Shutdown(SafeSocketHandle handle, bool isConnected, bool isDisconnected,
        SocketShutdown how)
    {
        SocketError socketError = Interop.Winsock.shutdown(handle, (int)how);
        if (socketError != SocketError.SocketError)
        {
            return SocketError.Success;
        }

        return GetLastSocketError();
    }

    public static unsafe SocketError ConnectAsync(Socket socket, SafeSocketHandle handle, byte[] socketAddress,
        int socketAddressLen, ConnectOverlappedAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(socketAddress);
        try
        {
            int bytesSent;
            bool success = socket.ConnectEx(handle, Marshal.UnsafeAddrOfPinnedArrayElement(socketAddress, 0),
                socketAddressLen, IntPtr.Zero, 0, out bytesSent, asyncResult.DangerousOverlappedPointer);
            return asyncResult.ProcessOverlappedResult(success, 0);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    public static unsafe SocketError SendAsync(SafeSocketHandle handle, byte[] buffer, int offset, int count,
        SocketFlags socketFlags, OverlappedAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(buffer, offset, count, null);
        try
        {
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSASend(handle, ref asyncResult._singleBuffer, 1,
                out bytesTransferred, socketFlags, asyncResult.DangerousOverlappedPointer, IntPtr.Zero);
            return asyncResult.ProcessOverlappedResult(socketError == SocketError.Success, bytesTransferred);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    public static unsafe SocketError SendAsync(SafeSocketHandle handle, IList<ArraySegment<byte>> buffers,
        SocketFlags socketFlags, OverlappedAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(buffers);
        try
        {
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSASend(handle, asyncResult._wsaBuffers,
                asyncResult._wsaBuffers.Length, out bytesTransferred, socketFlags,
                asyncResult.DangerousOverlappedPointer, IntPtr.Zero);
            return asyncResult.ProcessOverlappedResult(socketError == SocketError.Success, bytesTransferred);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    private static unsafe bool TransmitFileHelper(SafeHandle socket, SafeHandle fileHandle,
        NativeOverlapped* overlapped, byte[] preBuffer, byte[] postBuffer, TransmitFileOptions flags)
    {
        bool flag = false;
        Interop.Mswsock.TransmitFileBuffers transmitFileBuffers = default(Interop.Mswsock.TransmitFileBuffers);
        if (preBuffer != null && preBuffer.Length != 0)
        {
            flag = true;
            transmitFileBuffers.Head = Marshal.UnsafeAddrOfPinnedArrayElement(preBuffer, 0);
            transmitFileBuffers.HeadLength = preBuffer.Length;
        }

        if (postBuffer != null && postBuffer.Length != 0)
        {
            flag = true;
            transmitFileBuffers.Tail = Marshal.UnsafeAddrOfPinnedArrayElement(postBuffer, 0);
            transmitFileBuffers.TailLength = postBuffer.Length;
        }

        return Interop.Mswsock.TransmitFile(socket, fileHandle, 0, 0, overlapped, flag ? (&transmitFileBuffers) : null,
            flags);
    }

    public static unsafe SocketError SendFileAsync(SafeSocketHandle handle, FileStream fileStream, byte[] preBuffer,
        byte[] postBuffer, TransmitFileOptions flags, TransmitFileAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(fileStream, preBuffer, postBuffer,
            (flags & (TransmitFileOptions.Disconnect | TransmitFileOptions.ReuseSocket)) != 0);
        try
        {
            bool success = TransmitFileHelper(handle, fileStream?.SafeFileHandle,
                asyncResult.DangerousOverlappedPointer, preBuffer, postBuffer, flags);
            return asyncResult.ProcessOverlappedResult(success, 0);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    public static unsafe SocketError SendToAsync(SafeSocketHandle handle, byte[] buffer, int offset, int count,
        SocketFlags socketFlags, Internals.SocketAddress socketAddress, OverlappedAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(buffer, offset, count, socketAddress);
        try
        {
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSASendTo(handle, ref asyncResult._singleBuffer, 1,
                out bytesTransferred, socketFlags, asyncResult.GetSocketAddressPtr(), asyncResult.SocketAddress.Size,
                asyncResult.DangerousOverlappedPointer, IntPtr.Zero);
            return asyncResult.ProcessOverlappedResult(socketError == SocketError.Success, bytesTransferred);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    public static unsafe SocketError ReceiveAsync(SafeSocketHandle handle, byte[] buffer, int offset, int count,
        SocketFlags socketFlags, OverlappedAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(buffer, offset, count, null);
        try
        {
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSARecv(handle, ref asyncResult._singleBuffer, 1,
                out bytesTransferred, ref socketFlags, asyncResult.DangerousOverlappedPointer, IntPtr.Zero);
            return asyncResult.ProcessOverlappedResult(socketError == SocketError.Success, bytesTransferred);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    public static unsafe SocketError ReceiveAsync(SafeSocketHandle handle, IList<ArraySegment<byte>> buffers,
        SocketFlags socketFlags, OverlappedAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(buffers);
        try
        {
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSARecv(handle, asyncResult._wsaBuffers,
                asyncResult._wsaBuffers.Length, out bytesTransferred, ref socketFlags,
                asyncResult.DangerousOverlappedPointer, IntPtr.Zero);
            return asyncResult.ProcessOverlappedResult(socketError == SocketError.Success, bytesTransferred);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    public static unsafe SocketError ReceiveFromAsync(SafeSocketHandle handle, byte[] buffer, int offset, int count,
        SocketFlags socketFlags, Internals.SocketAddress socketAddress, OverlappedAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(buffer, offset, count, socketAddress);
        try
        {
            int bytesTransferred;
            SocketError socketError = Interop.Winsock.WSARecvFrom(handle, ref asyncResult._singleBuffer, 1,
                out bytesTransferred, ref socketFlags, asyncResult.GetSocketAddressPtr(),
                asyncResult.GetSocketAddressSizePtr(), asyncResult.DangerousOverlappedPointer, IntPtr.Zero);
            return asyncResult.ProcessOverlappedResult(socketError == SocketError.Success, bytesTransferred);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    public static unsafe SocketError ReceiveMessageFromAsync(Socket socket, SafeSocketHandle handle, byte[] buffer,
        int offset, int count, SocketFlags socketFlags, Internals.SocketAddress socketAddress,
        ReceiveMessageOverlappedAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(buffer, offset, count, socketAddress, socketFlags);
        try
        {
            int bytesTransferred;
            SocketError socketError = socket.WSARecvMsg(handle,
                Marshal.UnsafeAddrOfPinnedArrayElement(asyncResult._messageBuffer, 0), out bytesTransferred,
                asyncResult.DangerousOverlappedPointer, IntPtr.Zero);
            return asyncResult.ProcessOverlappedResult(socketError == SocketError.Success, bytesTransferred);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    public static unsafe SocketError AcceptAsync(Socket socket, SafeSocketHandle handle, SafeSocketHandle acceptHandle,
        int receiveSize, int socketAddressSize, AcceptOverlappedAsyncResult asyncResult)
    {
        int num = socketAddressSize + 16;
        byte[] buffer = new byte[receiveSize + num * 2];
        asyncResult.SetUnmanagedStructures(buffer, num);
        try
        {
            int bytesReceived;
            bool success = socket.AcceptEx(handle, acceptHandle,
                Marshal.UnsafeAddrOfPinnedArrayElement(asyncResult.Buffer, 0), receiveSize, num, num, out bytesReceived,
                asyncResult.DangerousOverlappedPointer);
            return asyncResult.ProcessOverlappedResult(success, 0);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    public static void CheckDualModeReceiveSupport(Socket socket)
    {
    }

    internal static unsafe SocketError DisconnectAsync(Socket socket, SafeSocketHandle handle, bool reuseSocket,
        DisconnectOverlappedAsyncResult asyncResult)
    {
        asyncResult.SetUnmanagedStructures(null);
        try
        {
            bool success = socket.DisconnectEx(handle, asyncResult.DangerousOverlappedPointer, reuseSocket ? 2 : 0, 0);
            return asyncResult.ProcessOverlappedResult(success, 0);
        }
        catch
        {
            asyncResult.ReleaseUnmanagedStructures();
            throw;
        }
    }

    internal static SocketError Disconnect(Socket socket, SafeSocketHandle handle, bool reuseSocket)
    {
        SocketError result = SocketError.Success;
        if (!socket.DisconnectExBlocking(handle, IntPtr.Zero, reuseSocket ? 2 : 0, 0))
        {
            result = GetLastSocketError();
        }

        return result;
    }
}