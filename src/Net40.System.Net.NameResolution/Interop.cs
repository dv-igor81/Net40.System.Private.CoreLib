using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;


internal static class Interop
{
	internal static class Winsock
	{
		[StructLayout(LayoutKind.Sequential, Size = 408)]
		private struct WSAData
		{
		}

		internal unsafe delegate void LPLOOKUPSERVICE_COMPLETION_ROUTINE([In] int dwError, [In] int dwBytes, [In] NativeOverlapped* lpOverlapped);

		[DllImport("ws2_32.dll", ExactSpelling = true, SetLastError = true)]
		internal static extern SocketError closesocket([In] IntPtr socketHandle);

		[DllImport("ws2_32.dll", SetLastError = true)]
		internal static extern unsafe SocketError gethostname(byte* name, int namelen);

		[DllImport("ws2_32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, SetLastError = true, ThrowOnUnmappableChar = true)]
		internal static extern unsafe SocketError GetNameInfoW(byte* pSockaddr, int SockaddrLength, char* pNodeBuffer, int NodeBufferSize, char* pServiceBuffer, int ServiceBufferSize, int Flags);

		[DllImport("ws2_32.dll", BestFitMapping = false, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true, ThrowOnUnmappableChar = true)]
		internal static extern int GetAddrInfoW([In] string nodename, [In] string servicename, [In] ref AddressInfo hints, out SafeFreeAddrInfo handle);

		[DllImport("ws2_32.dll", ExactSpelling = true, SetLastError = true)]
		internal static extern void freeaddrinfo([In] IntPtr info);

		internal static unsafe SocketError WSAStartup()
		{
			WSAData wSAData = default(WSAData);
			return WSAStartup(514, &wSAData);
		}

		[DllImport("ws2_32.dll", SetLastError = true)]
		private static extern unsafe SocketError WSAStartup(short wVersionRequested, WSAData* lpWSAData);

		[DllImport("ws2_32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		internal static extern IntPtr WSASocketW([In] AddressFamily addressFamily, [In] SocketType socketType, [In] int protocolType, [In] IntPtr protocolInfo, [In] int group, [In] int flags);

		[DllImport("ws2_32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
		internal static extern unsafe int GetAddrInfoExW([In] string pName, [In] string pServiceName, [In] int dwNamespace, [In] IntPtr lpNspId, [In] ref AddressInfoEx pHints, out AddressInfoEx* ppResult, [In] IntPtr timeout, [In] ref NativeOverlapped lpOverlapped, [In] LPLOOKUPSERVICE_COMPLETION_ROUTINE lpCompletionRoutine, out IntPtr lpNameHandle);

		[DllImport("ws2_32.dll", ExactSpelling = true)]
		internal static extern unsafe void FreeAddrInfoExW([In] AddressInfoEx* pAddrInfo);
	}

	internal class Kernel32
	{
		[DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Ansi)]
		public static extern IntPtr GetProcAddress(Microsoft.Win32.SafeHandles.SafeLibraryHandle hModule, string lpProcName);

		[DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
		public static extern Microsoft.Win32.SafeHandles.SafeLibraryHandle LoadLibraryExW([In] string lpwLibFileName, [In] IntPtr hFile, [In] uint dwFlags);

		[DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
		public static extern bool FreeLibrary([In] IntPtr hModule);
	}
}
