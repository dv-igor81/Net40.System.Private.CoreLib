using Microsoft.Win32.SafeHandles;

namespace System.Net.Sockets;

internal sealed class SafeFreeAddrInfo : SafeHandleZeroOrMinusOneIsInvalid
{
	private SafeFreeAddrInfo()
		: base(ownsHandle: true)
	{
	}

	internal static int GetAddrInfo(string nodename, string servicename, ref AddressInfo hints, out SafeFreeAddrInfo outAddrInfo)
	{
		return Interop.Winsock.GetAddrInfoW(nodename, servicename, ref hints, out outAddrInfo);
	}

	protected override bool ReleaseHandle()
	{
		Interop.Winsock.freeaddrinfo(handle);
		return true;
	}
}
