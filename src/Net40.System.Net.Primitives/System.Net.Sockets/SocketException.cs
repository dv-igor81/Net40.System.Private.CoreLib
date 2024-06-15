using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace System.Net.Sockets.Net40;

[Serializable]
public class SocketException : Win32Exception
{
	private readonly SocketError _errorCode;

	public override string Message => base.Message;

	public SocketError SocketErrorCode => _errorCode;

	public override int ErrorCode => base.NativeErrorCode;

	public SocketException(int errorCode)
		: this((SocketError)errorCode)
	{
	}

	internal SocketException(SocketError socketError)
		: base(GetNativeErrorForSocketError(socketError))
	{
		if (NetEventSource.IsEnabled)
		{
			NetEventSource.Enter(this, socketError, Message);
		}
		_errorCode = socketError;
	}

	protected SocketException(SerializationInfo serializationInfo, StreamingContext streamingContext)
		: base(serializationInfo, streamingContext)
	{
		if (NetEventSource.IsEnabled)
		{
			NetEventSource.Info(this, $"{base.NativeErrorCode}:{Message}", ".ctor");
		}
	}

	public SocketException()
		: this(Marshal.GetLastWin32Error())
	{
	}

	private static int GetNativeErrorForSocketError(SocketError error)
	{
		return (int)error;
	}
}