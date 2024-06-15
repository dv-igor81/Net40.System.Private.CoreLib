using System.Runtime.InteropServices;

namespace System.Net.Sockets.Net40;

[UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
internal delegate SocketError WSARecvMsgDelegateBlocking(IntPtr socketHandle, IntPtr msg, out int bytesTransferred, IntPtr overlapped, IntPtr completionRoutine);