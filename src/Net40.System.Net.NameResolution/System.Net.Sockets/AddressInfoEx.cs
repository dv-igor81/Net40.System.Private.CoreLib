using System.Net.Internals;

namespace System.Net.Sockets;

internal struct AddressInfoEx
{
	internal AddressInfoHints ai_flags;

	internal AddressFamily ai_family;

	internal SocketType ai_socktype;

	internal ProtocolFamily ai_protocol;

	internal int ai_addrlen;

	internal IntPtr ai_canonname;

	internal unsafe byte* ai_addr;

	internal IntPtr ai_blob;

	internal int ai_bloblen;

	internal IntPtr ai_provider;

	internal unsafe AddressInfoEx* ai_next;
}
