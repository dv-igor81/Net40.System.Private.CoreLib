namespace System.Net;

internal sealed class DnsResolveAsyncResult : ContextAwareResult
{
	internal string HostName { get; }

	internal Net40.IPAddress IpAddress { get; }

	internal DnsResolveAsyncResult(string hostName, object myObject, object myState, AsyncCallback myCallBack)
		: base(myObject, myState, myCallBack)
	{
		HostName = hostName;
	}

	internal DnsResolveAsyncResult(Net40.IPAddress ipAddress, object myObject, object myState, AsyncCallback myCallBack)
		: base(myObject, myState, myCallBack)
	{
		IpAddress = ipAddress;
	}
}
