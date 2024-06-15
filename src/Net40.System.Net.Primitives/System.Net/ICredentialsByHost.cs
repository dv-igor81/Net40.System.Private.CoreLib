namespace System.Net.Net40;

public interface ICredentialsByHost
{
	NetworkCredential GetCredential(string host, int port, string authenticationType);
}