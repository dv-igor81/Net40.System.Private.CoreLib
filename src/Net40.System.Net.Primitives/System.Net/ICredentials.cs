namespace System.Net.Net40;

using Uri = System.Net40.Uri;

public interface ICredentials
{
	Net40.NetworkCredential GetCredential(Uri uri, string authType);
}