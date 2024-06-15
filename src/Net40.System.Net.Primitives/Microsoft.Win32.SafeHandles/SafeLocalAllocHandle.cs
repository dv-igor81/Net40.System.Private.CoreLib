using System;
using System.Runtime.InteropServices;

namespace Microsoft.Win32.SafeHandles;

internal sealed class SafeLocalAllocHandle : SafeBuffer, IDisposable
{
	internal static readonly SafeLocalAllocHandle Zero = new SafeLocalAllocHandle();

	internal static SafeLocalAllocHandle InvalidHandle => new SafeLocalAllocHandle(IntPtr.Zero);

	private SafeLocalAllocHandle()
		: base(ownsHandle: true)
	{
		}

	internal SafeLocalAllocHandle(IntPtr handle)
		: base(ownsHandle: true)
	{
			SetHandle(handle);
		}

	protected override bool ReleaseHandle()
	{
			return global::Interop.Kernel32.LocalFree(handle) == IntPtr.Zero;
		}
}