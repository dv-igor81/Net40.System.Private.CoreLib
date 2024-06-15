using System.Runtime.InteropServices;
using System.Threading;

namespace System.Net.Sockets.Net40;

[UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
internal unsafe delegate bool ConnectExDelegate(SafeSocketHandle socketHandle, IntPtr socketAddress, int socketAddressSize, IntPtr buffer, int dataLength, out int bytesSent, NativeOverlapped* overlapped);