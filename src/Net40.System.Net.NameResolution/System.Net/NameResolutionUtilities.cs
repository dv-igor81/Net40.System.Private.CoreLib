namespace System.Net;

internal static class NameResolutionUtilities
{
	public static Net40.IPHostEntry GetUnresolvedAnswer(Net40.IPAddress address)
	{
		Net40.IPHostEntry iPHostEntry = new Net40.IPHostEntry();
		iPHostEntry.HostName = address.ToString();
		iPHostEntry.Aliases = ArrayEx.Empty<string>();
		iPHostEntry.AddressList = new Net40.IPAddress[1] { address };
		return iPHostEntry;
	}

	public static Net40.IPHostEntry GetUnresolvedAnswer(string name)
	{
		return new Net40.IPHostEntry
		{
			HostName = name,
			Aliases = ArrayEx.Empty<string>(),
			AddressList = ArrayEx.Empty<Net40.IPAddress>()
		};
	}
}
