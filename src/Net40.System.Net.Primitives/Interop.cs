using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class Interop
{
	internal static class IpHlpApi
	{
		public struct FIXED_INFO
		{
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 132)]
			public string hostName;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 132)]
			public string domainName;

			public IntPtr currentDnsServer;

			public IP_ADDR_STRING DnsServerList;

			public uint nodeType;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string scopeId;

			public bool enableRouting;

			public bool enableProxy;

			public bool enableDns;
		}

		public struct IP_ADDR_STRING
		{
			public IntPtr Next;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
			public string IpAddress;

			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
			public string IpMask;

			public uint Context;
		}

		[DllImport("iphlpapi.dll", ExactSpelling = true)]
		internal static extern uint GetNetworkParams(SafeLocalAllocHandle pFixedInfo, ref uint pOutBufLen);

		[DllImport("iphlpapi.dll", SetLastError = true)]
		internal static extern uint if_nametoindex(string name);
	}

	internal static class Kernel32
	{
		[DllImport("kernel32.dll", SetLastError = true)]
		internal static extern SafeLocalAllocHandle LocalAlloc(int uFlags, UIntPtr sizetdwBytes);

		[DllImport("kernel32.dll", SetLastError = true)]
		internal static extern IntPtr LocalFree(IntPtr handle);
	}
}
