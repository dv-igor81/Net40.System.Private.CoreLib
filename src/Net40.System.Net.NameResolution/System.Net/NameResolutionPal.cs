using System.Collections.Generic;
using System.Net.Net40;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace System.Net;

using IPHostEntry = Net40.IPHostEntry;

internal static class NameResolutionPal
{
	private sealed class GetAddrInfoExState
	{
		private DnsResolveAsyncResult _asyncResult;

		private object _result;

		public string HostName => _asyncResult.HostName;

		public GetAddrInfoExState(DnsResolveAsyncResult asyncResult)
		{
			_asyncResult = asyncResult;
		}

		public void CompleteAsyncResult(object o)
		{
			_result = o;
			Task.Factory.StartNew(delegate(object s)
			{
				GetAddrInfoExState getAddrInfoExState = (GetAddrInfoExState)s;
				getAddrInfoExState._asyncResult.InvokeCallback(getAddrInfoExState._result);
			}, this, CancellationToken.None, 
				//TaskCreationOptions.DenyChildAttach,
				TaskCreationOptions.None,
				TaskScheduler.Default);
		}

		public IntPtr CreateHandle()
		{
			GCHandle value = GCHandle.Alloc(this, GCHandleType.Normal);
			return GCHandle.ToIntPtr(value);
		}

		public static GetAddrInfoExState FromHandleAndFree(IntPtr handle)
		{
			GCHandle gCHandle = GCHandle.FromIntPtr(handle);
			GetAddrInfoExState result = (GetAddrInfoExState)gCHandle.Target;
			gCHandle.Free();
			return result;
		}
	}

	private struct GetAddrInfoExContext
	{
		public NativeOverlapped Overlapped;

		public unsafe AddressInfoEx* Result;

		public IntPtr CancelHandle;

		public IntPtr QueryStateHandle;

		public static unsafe GetAddrInfoExContext* AllocateContext()
		{
			GetAddrInfoExContext* ptr = (GetAddrInfoExContext*)(void*)Marshal.AllocHGlobal(sizeof(GetAddrInfoExContext));
			*ptr = default(GetAddrInfoExContext);
			return ptr;
		}

		public static unsafe void FreeContext(GetAddrInfoExContext* context)
		{
			if (context->Result != null)
			{
				Interop.Winsock.FreeAddrInfoExW(context->Result);
			}
			Marshal.FreeHGlobal((IntPtr)context);
		}
	}

	private static bool s_initialized;

	private static readonly object s_initializedLock = new object();

	private static readonly unsafe Interop.Winsock.LPLOOKUPSERVICE_COMPLETION_ROUTINE s_getAddrInfoExCallback = GetAddressInfoExCallback;

	private static bool s_getAddrInfoExSupported;

	public static bool SupportsGetAddrInfoAsync
	{
		get
		{
			EnsureSocketsAreInitialized();
			return s_getAddrInfoExSupported;
		}
	}

	public static unsafe SocketError TryGetAddrInfo(string name, out Net40.IPHostEntry hostinfo, out int nativeErrorCode)
	{
		SafeFreeAddrInfo outAddrInfo = null;
		List<Net40.IPAddress> list = new List<Net40.IPAddress>();
		string text = null;
		AddressInfo hints = default(AddressInfo);
		hints.ai_flags = AddressInfoHints.AI_CANONNAME;
		hints.ai_family = AddressFamily.Unspecified;
		nativeErrorCode = 0;
		try
		{
			SocketError addrInfo = (SocketError)SafeFreeAddrInfo.GetAddrInfo(name, null, ref hints, out outAddrInfo);
			if (addrInfo != 0)
			{
				hostinfo = NameResolutionUtilities.GetUnresolvedAnswer(name);
				return addrInfo;
			}
			for (AddressInfo* ptr = (AddressInfo*)(void*)outAddrInfo.DangerousGetHandle(); ptr != null; ptr = ptr->ai_next)
			{
				if (text == null && ptr->ai_canonname != null)
				{
					text = Marshal.PtrToStringUni((IntPtr)ptr->ai_canonname);
				}
				ReadOnlySpan<byte> socketAddress = new ReadOnlySpan<byte>(ptr->ai_addr, ptr->ai_addrlen);
				if (ptr->ai_family == AddressFamily.InterNetwork)
				{
					if (socketAddress.Length == 16)
					{
						list.Add(CreateIPv4Address(socketAddress));
					}
				}
				else if (ptr->ai_family == AddressFamily.InterNetworkV6 && SocketProtocolSupportPal.OSSupportsIPv6 && socketAddress.Length == 28)
				{
					list.Add(CreateIPv6Address(socketAddress));
				}
			}
		}
		finally
		{
			outAddrInfo?.Dispose();
		}
		hostinfo = new Net40.IPHostEntry();
		hostinfo.HostName = ((text != null) ? text : name);
		hostinfo.Aliases = ArrayEx.Empty<string>();
		hostinfo.AddressList = list.ToArray();
		return SocketError.Success;
	}

	public static unsafe string TryGetNameInfo(Net40.IPAddress addr, out SocketError errorCode, out int nativeErrorCode)
	{
		Net40.SocketAddress socketAddress = new Net40.IPEndPoint(addr, 0).Serialize();
		Span<byte> span = ((socketAddress.Size > 64) ? ((Span<byte>)new byte[socketAddress.Size]) : stackalloc byte[64]);
		Span<byte> span2 = span;
		for (int i = 0; i < socketAddress.Size; i++)
		{
			span2[i] = socketAddress[i];
		}
		char* ptr = stackalloc char[1025];
		nativeErrorCode = 0;
		fixed (byte* pSockaddr = span2)
		{
			errorCode = Interop.Winsock.GetNameInfoW(pSockaddr, socketAddress.Size, ptr, 1025, null, 0, 4);
		}
		if (errorCode != 0)
		{
			return null;
		}
		return new string(ptr);
	}

	public static unsafe string GetHostName()
	{
		byte* ptr = stackalloc byte[256];
		if (Interop.Winsock.gethostname(ptr, 256) != 0)
		{
			throw new SocketException();
		}
		return new string((sbyte*)ptr);
	}

	public static void EnsureSocketsAreInitialized()
	{
		if (Volatile.Read(ref s_initialized))
		{
			return;
		}
		lock (s_initializedLock)
		{
			if (!s_initialized)
			{
				SocketError socketError = Interop.Winsock.WSAStartup();
				if (socketError != 0)
				{
					throw new SocketException((int)socketError);
				}
				s_getAddrInfoExSupported = GetAddrInfoExSupportsOverlapped();
				Volatile.Write(ref s_initialized, value: true);
			}
		}
	}

	public static unsafe void GetAddrInfoAsync(DnsResolveAsyncResult asyncResult)
	{
		GetAddrInfoExContext* ptr = GetAddrInfoExContext.AllocateContext();
		try
		{
			GetAddrInfoExState getAddrInfoExState = new GetAddrInfoExState(asyncResult);
			ptr->QueryStateHandle = getAddrInfoExState.CreateHandle();
		}
		catch
		{
			GetAddrInfoExContext.FreeContext(ptr);
			throw;
		}
		AddressInfoEx pHints = default(AddressInfoEx);
		pHints.ai_flags = AddressInfoHints.AI_CANONNAME;
		pHints.ai_family = AddressFamily.Unspecified;
		SocketError addrInfoExW = (SocketError)Interop.Winsock.GetAddrInfoExW(asyncResult.HostName, null, 0, IntPtr.Zero, ref pHints, out ptr->Result, IntPtr.Zero, ref ptr->Overlapped, s_getAddrInfoExCallback, out ptr->CancelHandle);
		if (addrInfoExW != SocketError.IOPending)
		{
			ProcessResult(addrInfoExW, ptr);
		}
	}

	private static unsafe void GetAddressInfoExCallback([In] int error, [In] int bytes, [In] NativeOverlapped* overlapped)
	{
		ProcessResult((SocketError)error, (GetAddrInfoExContext*)overlapped);
	}

	private static unsafe void ProcessResult(SocketError errorCode, GetAddrInfoExContext* context)
	{
		try
		{
			GetAddrInfoExState getAddrInfoExState = GetAddrInfoExState.FromHandleAndFree(context->QueryStateHandle);
			if (errorCode != 0)
			{
				getAddrInfoExState.CompleteAsyncResult(new SocketException((int)errorCode));
				return;
			}
			AddressInfoEx* ptr = context->Result;
			string text = null;
			List<Net40.IPAddress> list = new List<Net40.IPAddress>();
			while (ptr != null)
			{
				if (text == null && ptr->ai_canonname != IntPtr.Zero)
				{
					text = Marshal.PtrToStringUni(ptr->ai_canonname);
				}
				ReadOnlySpan<byte> socketAddress = new ReadOnlySpan<byte>(ptr->ai_addr, ptr->ai_addrlen);
				if (ptr->ai_family == AddressFamily.InterNetwork)
				{
					if (socketAddress.Length == 16)
					{
						list.Add(CreateIPv4Address(socketAddress));
					}
				}
				else if (SocketProtocolSupportPal.OSSupportsIPv6 && ptr->ai_family == AddressFamily.InterNetworkV6 && socketAddress.Length == 28)
				{
					list.Add(CreateIPv6Address(socketAddress));
				}
				ptr = ptr->ai_next;
			}
			if (text == null)
			{
				text = getAddrInfoExState.HostName;
			}
			getAddrInfoExState.CompleteAsyncResult(new Net40.IPHostEntry
			{
				HostName = text,
				Aliases = ArrayEx.Empty<string>(),
				AddressList = list.ToArray()
			});
		}
		finally
		{
			GetAddrInfoExContext.FreeContext(context);
		}
	}

	private static Net40.IPAddress CreateIPv4Address(ReadOnlySpan<byte> socketAddress)
	{
		long newAddress = (long)SocketAddressPal.GetIPv4Address(socketAddress) & 0xFFFFFFFFL;
		return new Net40.IPAddress(newAddress);
	}

	private static Net40.IPAddress CreateIPv6Address(ReadOnlySpan<byte> socketAddress)
	{
		Span<byte> span = stackalloc byte[16];
		SocketAddressPal.GetIPv6Address(socketAddress, span, out var scope);
		return new Net40.IPAddress(span, scope);
	}

	private static bool GetAddrInfoExSupportsOverlapped()
	{
		using SafeLibraryHandle safeLibraryHandle = Interop.Kernel32.LoadLibraryExW("ws2_32.dll", IntPtr.Zero, 2048u);
		if (safeLibraryHandle.IsInvalid)
		{
			return false;
		}
		return Interop.Kernel32.GetProcAddress(safeLibraryHandle, "GetAddrInfoExCancel") != IntPtr.Zero;
	}
}
