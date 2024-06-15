using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace System.Net.NetworkInformation;

internal static class HostInformationPal
{
	private static Interop.IpHlpApi.FIXED_INFO s_fixedInfo;

	private static bool s_fixedInfoInitialized;

	private static object s_syncObject = new object();

	public static string GetDomainName()
	{
		EnsureFixedInfo();
		return s_fixedInfo.domainName;
	}

	public static Interop.IpHlpApi.FIXED_INFO GetFixedInfo()
	{
		uint pOutBufLen = 0u;
		SafeLocalAllocHandle safeLocalAllocHandle = null;
		Interop.IpHlpApi.FIXED_INFO result = default(Interop.IpHlpApi.FIXED_INFO);
		uint networkParams = Interop.IpHlpApi.GetNetworkParams(SafeLocalAllocHandle.InvalidHandle, ref pOutBufLen);
		while (true)
		{
			switch (networkParams)
			{
				case 111u:
					using (safeLocalAllocHandle = Interop.Kernel32.LocalAlloc(0, (UIntPtr)pOutBufLen))
					{
						if (safeLocalAllocHandle.IsInvalid)
						{
							throw new OutOfMemoryException();
						}
						networkParams = Interop.IpHlpApi.GetNetworkParams(safeLocalAllocHandle, ref pOutBufLen);
						if (networkParams == 0)
						{
							result = MarshalEx.PtrToStructure<Interop.IpHlpApi.FIXED_INFO>(safeLocalAllocHandle.DangerousGetHandle());
						}
					}
					break;
				default:
					throw new Win32Exception((int)networkParams);
				case 0u:
					return result;
			}
		}
	}

	private static void EnsureFixedInfo()
	{
		LazyInitializer.EnsureInitialized(ref s_fixedInfo, ref s_fixedInfoInitialized, ref s_syncObject, () => GetFixedInfo());
	}
}