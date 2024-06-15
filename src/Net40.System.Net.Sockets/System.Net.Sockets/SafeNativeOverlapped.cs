using System.Runtime.InteropServices;
using System.Threading;

namespace System.Net.Sockets.Net40;

internal sealed class SafeNativeOverlapped : SafeHandle
{
	private readonly SafeSocketHandle _socketHandle;

	public override bool IsInvalid => handle == IntPtr.Zero;

	private SafeNativeOverlapped(IntPtr handle)
		: base(IntPtr.Zero, ownsHandle: true)
	{
			SetHandle(handle);
		}

	public unsafe SafeNativeOverlapped(SafeSocketHandle socketHandle, NativeOverlapped* handle)
		: this((IntPtr)handle)
	{
			_socketHandle = socketHandle;
			if (NetEventSource.IsEnabled)
			{
				NetEventSource.Info(this, $"socketHandle:{socketHandle}", ".ctor");
			}
		}

	protected override bool ReleaseHandle()
	{
			if (NetEventSource.IsEnabled)
			{
				NetEventSource.Info(this, null);
			}
			FreeNativeOverlapped();
			return true;
		}

	private unsafe void FreeNativeOverlapped()
	{
			IntPtr intPtr = Interlocked.Exchange(ref handle, IntPtr.Zero);
			if (intPtr != IntPtr.Zero && !Environment.HasShutdownStarted)
			{
				_socketHandle.IOCPBoundHandle?.FreeNativeOverlapped((NativeOverlapped*)(void*)intPtr);
			}
		}
}