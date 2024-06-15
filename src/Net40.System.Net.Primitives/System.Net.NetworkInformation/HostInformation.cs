namespace System.Net.NetworkInformation;

internal class HostInformation
{
	internal static string DomainName => HostInformationPal.GetDomainName();
}