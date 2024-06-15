namespace System.Net.Net40;

using Uri = System.Net40.Uri;


public interface IWebProxy
{
	ICredentials Credentials { get; set; }

	Uri GetProxy(Uri destination);

	bool IsBypassed(Uri host);
}