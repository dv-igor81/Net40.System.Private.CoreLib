using System.Runtime.InteropServices;

namespace System.Net.Sockets.Net40;

[UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
internal delegate bool DisconnectExDelegateBlocking(SafeSocketHandle socketHandle, IntPtr overlapped, int flags, int reserved);