using System.Runtime.InteropServices;
using System.Security;

namespace System.Net.Net40;

public class NetworkCredential : ICredentials, ICredentialsByHost
{
	private string _domain;

	private string _userName;

	private object _password;

	public string UserName
	{
		get
		{
			return _userName;
		}
		set
		{
			_userName = value ?? string.Empty;
		}
	}

	public string Password
	{
		get
		{
			if (_password is SecureString sstr)
			{
				return MarshalToString(sstr);
			}
			return ((string)_password) ?? string.Empty;
		}
		set
		{
			SecureString secureString = _password as SecureString;
			_password = value;
			secureString?.Dispose();
		}
	}

	[CLSCompliant(false)]
	public SecureString SecurePassword
	{
		get
		{
			if (_password is string str)
			{
				return MarshalToSecureString(str);
			}
			if (!(_password is SecureString secureString))
			{
				return new SecureString();
			}
			return secureString.Copy();
		}
		set
		{
			SecureString secureString = _password as SecureString;
			_password = value?.Copy();
			secureString?.Dispose();
		}
	}

	public string Domain
	{
		get
		{
			return _domain;
		}
		set
		{
			_domain = value ?? string.Empty;
		}
	}

	public NetworkCredential()
		: this(string.Empty, string.Empty, string.Empty)
	{
	}

	public NetworkCredential(string userName, string password)
		: this(userName, password, string.Empty)
	{
	}

	public NetworkCredential(string userName, string password, string domain)
	{
		UserName = userName;
		Password = password;
		Domain = domain;
	}

	[CLSCompliant(false)]
	public NetworkCredential(string userName, SecureString password)
		: this(userName, password, string.Empty)
	{
	}

	[CLSCompliant(false)]
	public NetworkCredential(string userName, SecureString password, string domain)
	{
		UserName = userName;
		SecurePassword = password;
		Domain = domain;
	}
	
	public NetworkCredential GetCredential(System.Net40.Uri uri, string authType)
	{
		return this;
	}

	public NetworkCredential GetCredential(string host, int port, string authenticationType)
	{
		return this;
	}

	private string MarshalToString(SecureString sstr)
	{
		if (sstr == null || sstr.Length == 0)
		{
			return string.Empty;
		}
		IntPtr intPtr = IntPtr.Zero;
		string empty = string.Empty;
		try
		{
			intPtr = Marshal.SecureStringToGlobalAllocUnicode(sstr);
			return Marshal.PtrToStringUni(intPtr);
		}
		finally
		{
			if (intPtr != IntPtr.Zero)
			{
				Marshal.ZeroFreeGlobalAllocUnicode(intPtr);
			}
		}
	}

	private unsafe SecureString MarshalToSecureString(string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return new SecureString();
		}
		fixed (char* value = str)
		{
			return new SecureString(value, str.Length);
		}
	}


}