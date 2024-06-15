namespace System.Net.Net40;

public class IPHostEntry
{
	private string _hostName;

	private string[] _aliases;

	private IPAddress[] _addressList;

	internal bool isTrustedHost = true;

	public string HostName
	{
		get
		{
			return _hostName;
		}
		set
		{
			_hostName = value;
		}
	}

	public string[] Aliases
	{
		get
		{
			return _aliases;
		}
		set
		{
			_aliases = value;
		}
	}

	public IPAddress[] AddressList
	{
		get
		{
			return _addressList;
		}
		set
		{
			_addressList = value;
		}
	}
}
