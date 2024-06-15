using System.Runtime.InteropServices;
using System.Threading;

namespace System.Net.Sockets.Net40;

[UnmanagedFunctionPointer(CallingConvention.StdCall, SetLastError = true)]
internal unsafe delegate bool TransmitPacketsDelegate(SafeSocketHandle socketHandle, IntPtr packetArray, int elementCount, int sendSize, NativeOverlapped* overlapped, TransmitFileOptions flags);