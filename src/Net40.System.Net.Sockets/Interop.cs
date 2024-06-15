using System;
using System.Net.Sockets.Net40;
using System.Runtime.InteropServices;
using System.Threading;


using SocketError = System.Net.Sockets.Net40.SocketError;
using AddressFamily = System.Net.Sockets.Net40.AddressFamily;


internal static class Interop
{
    internal static class Winsock
    {
        internal struct TimeValue
        {
            public int Seconds;

            public int Microseconds;
        }

        internal struct IPMulticastRequest
        {
            internal int MulticastAddress;

            internal int InterfaceAddress;

            internal static readonly int Size = MarshalEx.SizeOf<IPMulticastRequest>();
        }

        internal struct IPv6MulticastRequest
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            internal byte[] MulticastAddress;

            internal int InterfaceIndex;

            internal static readonly int Size = MarshalEx.SizeOf<IPv6MulticastRequest>();
        }

        internal struct Linger
        {
            internal ushort OnOff;

            internal ushort Time;
        }

        internal struct ControlData
        {
            internal UIntPtr length;

            internal uint level;

            internal uint type;

            internal uint address;

            internal uint index;
        }

        internal struct ControlDataIPv6
        {
            internal UIntPtr length;

            internal uint level;

            internal uint type;

            internal unsafe fixed byte address[16];

            internal uint index;
        }

        [Flags]
        internal enum TransmitPacketsElementFlags : uint
        {
            None = 0u,
            Memory = 1u,
            File = 2u,
            EndOfPacket = 4u
        }

        [StructLayout(LayoutKind.Explicit)]
        internal struct TransmitPacketsElement
        {
            [FieldOffset(0)] internal TransmitPacketsElementFlags flags;

            [FieldOffset(4)] internal uint length;

            [FieldOffset(8)] internal long fileOffset;

            [FieldOffset(8)] internal IntPtr buffer;

            [FieldOffset(16)] internal IntPtr fileHandle;
        }

        internal struct WSAMsg
        {
            internal IntPtr socketAddress;

            internal uint addressLength;

            internal IntPtr buffers;

            internal uint count;

            internal WSABuffer controlBuffer;

            internal SocketFlags flags;
        }

        [Flags]
        internal enum SocketConstructorFlags
        {
            WSA_FLAG_OVERLAPPED = 1,
            WSA_FLAG_MULTIPOINT_C_ROOT = 2,
            WSA_FLAG_MULTIPOINT_C_LEAF = 4,
            WSA_FLAG_MULTIPOINT_D_ROOT = 8,
            WSA_FLAG_MULTIPOINT_D_LEAF = 0x10,
            WSA_FLAG_NO_HANDLE_INHERIT = 0x80
        }

        [DllImport("ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SafeSocketHandle.InnerSafeCloseSocket accept([In] IntPtr socketHandle,
            [Out] byte[] socketAddress, [In] [Out] ref int socketAddressSize);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError bind([In] SafeSocketHandle socketHandle, [In] byte[] socketAddress,
            [In] int socketAddressSize);

        [DllImport("ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SocketError closesocket([In] IntPtr socketHandle);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getpeername([In] SafeSocketHandle socketHandle, [Out] byte[] socketAddress,
            [In] [Out] ref int socketAddressSize);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockname([In] SafeSocketHandle socketHandle, [Out] byte[] socketAddress,
            [In] [Out] ref int socketAddressSize);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, out int optionValue,
            [In] [Out] ref int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [Out] byte[] optionValue,
            [In] [Out] ref int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, out Linger optionValue,
            [In] [Out] ref int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, out IPMulticastRequest optionValue,
            [In] [Out] ref int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError getsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, out IPv6MulticastRequest optionValue,
            [In] [Out] ref int optionLength);

        [DllImport("ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SocketError ioctlsocket([In] IntPtr handle, [In] int cmd, [In] [Out] ref int argp);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError ioctlsocket([In] SafeSocketHandle socketHandle, [In] int cmd,
            [In] [Out] ref int argp);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError listen([In] SafeSocketHandle socketHandle, [In] int backlog);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe int recv([In] IntPtr socketHandle, [In] byte* pinnedBuffer, [In] int len,
            [In] SocketFlags socketFlags);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe int recvfrom([In] IntPtr socketHandle, [In] byte* pinnedBuffer, [In] int len,
            [In] SocketFlags socketFlags, [Out] byte[] socketAddress, [In] [Out] ref int socketAddressSize);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe int select([In] int ignoredParameter, [In] IntPtr* readfds, [In] IntPtr* writefds,
            [In] IntPtr* exceptfds, [In] ref TimeValue timeout);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe int select([In] int ignoredParameter, [In] IntPtr* readfds, [In] IntPtr* writefds,
            [In] IntPtr* exceptfds, [In] IntPtr nullTimeout);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe int send([In] IntPtr socketHandle, [In] byte* pinnedBuffer, [In] int len,
            [In] SocketFlags socketFlags);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe int sendto([In] IntPtr socketHandle, [In] byte* pinnedBuffer, [In] int len,
            [In] SocketFlags socketFlags, [In] byte[] socketAddress, [In] int socketAddressSize);

        [DllImport("ws2_32.dll", ExactSpelling = true, SetLastError = true)]
        internal static extern SocketError setsockopt([In] IntPtr handle, [In] SocketOptionLevel optionLevel,
            [In] SocketOptionName optionName, [In] ref Linger linger, [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] ref int optionValue,
            [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] byte[] optionValue,
            [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] ref IntPtr pointer,
            [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] ref Linger linger,
            [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] ref IPMulticastRequest mreq,
            [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError setsockopt([In] SafeSocketHandle socketHandle,
            [In] SocketOptionLevel optionLevel, [In] SocketOptionName optionName, [In] ref IPv6MulticastRequest mreq,
            [In] int optionLength);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError shutdown([In] SafeSocketHandle socketHandle, [In] int how);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError WSAConnect([In] IntPtr socketHandle, [In] byte[] socketAddress,
            [In] int socketAddressSize, [In] IntPtr inBuffer, [In] IntPtr outBuffer, [In] IntPtr sQOS,
            [In] IntPtr gQOS);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe bool WSAGetOverlappedResult([In] SafeSocketHandle socketHandle,
            [In] NativeOverlapped* overlapped, out uint bytesTransferred, [In] bool wait, out SocketFlags socketFlags);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern SocketError WSAIoctl([In] SafeSocketHandle socketHandle, [In] int ioControlCode,
            [In] [Out] ref Guid guid, [In] int guidSize, out IntPtr funcPtr, [In] int funcPtrSize,
            out int bytesTransferred, [In] IntPtr shouldBeNull, [In] IntPtr shouldBeNull2);

        [DllImport("ws2_32.dll", EntryPoint = "WSAIoctl", SetLastError = true)]
        internal static extern SocketError WSAIoctl_Blocking([In] IntPtr socketHandle, [In] int ioControlCode,
            [In] byte[] inBuffer, [In] int inBufferSize, [Out] byte[] outBuffer, [In] int outBufferSize,
            out int bytesTransferred, [In] IntPtr overlapped, [In] IntPtr completionRoutine);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe SocketError WSARecv(SafeHandle socketHandle, WSABuffer* buffer, int bufferCount,
            out int bytesTransferred, ref SocketFlags socketFlags, NativeOverlapped* overlapped,
            IntPtr completionRoutine);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe SocketError WSARecv(IntPtr socketHandle, WSABuffer* buffer, int bufferCount,
            out int bytesTransferred, ref SocketFlags socketFlags, NativeOverlapped* overlapped,
            IntPtr completionRoutine);

        internal static unsafe SocketError WSARecv(SafeHandle socketHandle, ref WSABuffer buffer, int bufferCount,
            out int bytesTransferred, ref SocketFlags socketFlags, NativeOverlapped* overlapped,
            IntPtr completionRoutine)
        {
            WSABuffer wSABuffer = buffer;
            return WSARecv(socketHandle, &wSABuffer, bufferCount, out bytesTransferred, ref socketFlags, overlapped,
                completionRoutine);
        }

        internal static unsafe SocketError WSARecv(SafeHandle socketHandle, Span<WSABuffer> buffers, int bufferCount,
            out int bytesTransferred, ref SocketFlags socketFlags, NativeOverlapped* overlapped,
            IntPtr completionRoutine)
        {
            fixed (WSABuffer* buffer = &MemoryMarshal.GetReference(buffers))
            {
                return WSARecv(socketHandle, buffer, bufferCount, out bytesTransferred, ref socketFlags, overlapped,
                    completionRoutine);
            }
        }

        internal static unsafe SocketError WSARecv(IntPtr socketHandle, Span<WSABuffer> buffers, int bufferCount,
            out int bytesTransferred, ref SocketFlags socketFlags, NativeOverlapped* overlapped,
            IntPtr completionRoutine)
        {
            fixed (WSABuffer* buffer = &MemoryMarshal.GetReference(buffers))
            {
                return WSARecv(socketHandle, buffer, bufferCount, out bytesTransferred, ref socketFlags, overlapped,
                    completionRoutine);
            }
        }

        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern unsafe SocketError WSARecvFrom(SafeHandle socketHandle, WSABuffer* buffers,
            int bufferCount, out int bytesTransferred, ref SocketFlags socketFlags, IntPtr socketAddressPointer,
            IntPtr socketAddressSizePointer, NativeOverlapped* overlapped, IntPtr completionRoutine);

        internal static unsafe SocketError WSARecvFrom(SafeHandle socketHandle, ref WSABuffer buffer, int bufferCount,
            out int bytesTransferred, ref SocketFlags socketFlags, IntPtr socketAddressPointer,
            IntPtr socketAddressSizePointer, NativeOverlapped* overlapped, IntPtr completionRoutine)
        {
            WSABuffer wSABuffer = buffer;
            return WSARecvFrom(socketHandle, &wSABuffer, bufferCount, out bytesTransferred, ref socketFlags,
                socketAddressPointer, socketAddressSizePointer, overlapped, completionRoutine);
        }

        internal static unsafe SocketError WSARecvFrom(SafeHandle socketHandle, WSABuffer[] buffers, int bufferCount,
            out int bytesTransferred, ref SocketFlags socketFlags, IntPtr socketAddressPointer,
            IntPtr socketAddressSizePointer, NativeOverlapped* overlapped, IntPtr completionRoutine)
        {
            fixed (WSABuffer* buffers2 = &buffers[0])
            {
                return WSARecvFrom(socketHandle, buffers2, bufferCount, out bytesTransferred, ref socketFlags,
                    socketAddressPointer, socketAddressSizePointer, overlapped, completionRoutine);
            }
        }

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe SocketError WSASend(IntPtr socketHandle, WSABuffer* buffers, int bufferCount,
            out int bytesTransferred, SocketFlags socketFlags, NativeOverlapped* overlapped, IntPtr completionRoutine);

        [DllImport("ws2_32.dll", SetLastError = true)]
        internal static extern unsafe SocketError WSASend(SafeHandle socketHandle, WSABuffer* buffers, int bufferCount,
            out int bytesTransferred, SocketFlags socketFlags, NativeOverlapped* overlapped, IntPtr completionRoutine);

        internal static unsafe SocketError WSASend(SafeHandle socketHandle, ref WSABuffer buffer, int bufferCount,
            out int bytesTransferred, SocketFlags socketFlags, NativeOverlapped* overlapped, IntPtr completionRoutine)
        {
            WSABuffer wSABuffer = buffer;
            return WSASend(socketHandle, &wSABuffer, bufferCount, out bytesTransferred, socketFlags, overlapped,
                completionRoutine);
        }

        internal static unsafe SocketError WSASend(SafeHandle socketHandle, Span<WSABuffer> buffers, int bufferCount,
            out int bytesTransferred, SocketFlags socketFlags, NativeOverlapped* overlapped, IntPtr completionRoutine)
        {
            fixed (WSABuffer* buffers2 = &MemoryMarshal.GetReference(buffers))
            {
                return WSASend(socketHandle, buffers2, bufferCount, out bytesTransferred, socketFlags, overlapped,
                    completionRoutine);
            }
        }

        internal static unsafe SocketError WSASend(IntPtr socketHandle, Span<WSABuffer> buffers, int bufferCount,
            out int bytesTransferred, SocketFlags socketFlags, NativeOverlapped* overlapped, IntPtr completionRoutine)
        {
            fixed (WSABuffer* buffers2 = &MemoryMarshal.GetReference(buffers))
            {
                return WSASend(socketHandle, buffers2, bufferCount, out bytesTransferred, socketFlags, overlapped,
                    completionRoutine);
            }
        }

        [DllImport("ws2_32.dll", SetLastError = true)]
        private static extern unsafe SocketError WSASendTo(SafeHandle socketHandle, WSABuffer* buffers, int bufferCount,
            out int bytesTransferred, SocketFlags socketFlags, IntPtr socketAddress, int socketAddressSize,
            NativeOverlapped* overlapped, IntPtr completionRoutine);

        internal static unsafe SocketError WSASendTo(SafeHandle socketHandle, ref WSABuffer buffer, int bufferCount,
            out int bytesTransferred, SocketFlags socketFlags, IntPtr socketAddress, int socketAddressSize,
            NativeOverlapped* overlapped, IntPtr completionRoutine)
        {
            WSABuffer wSABuffer = buffer;
            return WSASendTo(socketHandle, &wSABuffer, bufferCount, out bytesTransferred, socketFlags, socketAddress,
                socketAddressSize, overlapped, completionRoutine);
        }

        internal static unsafe SocketError WSASendTo(SafeHandle socketHandle, WSABuffer[] buffers, int bufferCount,
            out int bytesTransferred, SocketFlags socketFlags, IntPtr socketAddress, int socketAddressSize,
            NativeOverlapped* overlapped, IntPtr completionRoutine)
        {
            fixed (WSABuffer* buffers2 = &buffers[0])
            {
                return WSASendTo(socketHandle, buffers2, bufferCount, out bytesTransferred, socketFlags, socketAddress,
                    socketAddressSize, overlapped, completionRoutine);
            }
        }

        [DllImport("ws2_32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr WSASocketW([In] AddressFamily addressFamily, [In] SocketType socketType,
            [In] int protocolType, [In] IntPtr protocolInfo, [In] int group, [In] int flags);

        [DllImport("ws2_32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeSocketHandle.InnerSafeCloseSocket WSASocketW([In] AddressFamily addressFamily,
            [In] SocketType socketType, [In] ProtocolType protocolType, [In] IntPtr protocolInfo, [In] uint group,
            [In] SocketConstructorFlags flags);
    }

    internal static class Mswsock
    {
        internal struct TransmitFileBuffers
        {
            internal IntPtr Head;

            internal int HeadLength;

            internal IntPtr Tail;

            internal int TailLength;
        }

        [DllImport("mswsock.dll", SetLastError = true)]
        internal static extern unsafe bool TransmitFile(SafeHandle socket, SafeHandle fileHandle,
            int numberOfBytesToWrite, int numberOfBytesPerSend, NativeOverlapped* overlapped,
            TransmitFileBuffers* buffers, TransmitFileOptions flags);
    }

    internal static class Kernel32
    {
        [Flags]
        internal enum FileCompletionNotificationModes : byte
        {
            None = 0,
            SkipCompletionPortOnSuccess = 1,
            SkipSetEventOnHandle = 2
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern unsafe bool CancelIoEx(SafeHandle handle, NativeOverlapped* lpOverlapped);
        
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern unsafe bool CancelIo(SafeHandle handle, NativeOverlapped* lpOverlapped);
        

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetFileCompletionNotificationModes(SafeHandle handle,
            FileCompletionNotificationModes flags);
    }
}