namespace System.Net.Net40;

using AddressFamily = System.Net.Sockets.Net40.AddressFamily;

public abstract class EndPoint
{
	public virtual AddressFamily AddressFamily => throw NotImplemented.ByDesignWithMessage(SR.net_PropertyNotImplementedException);

	public virtual SocketAddress Serialize()
	{
		throw NotImplemented.ByDesignWithMessage(SR.net_MethodNotImplementedException);
	}

	public virtual EndPoint Create(SocketAddress socketAddress)
	{
		throw NotImplemented.ByDesignWithMessage(SR.net_MethodNotImplementedException);
	}
}