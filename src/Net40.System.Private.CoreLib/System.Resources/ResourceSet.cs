using System.Collections;
using System.IO;
using System.Reflection;

namespace System.Resources;

public class ResourceSet : IDisposable, IEnumerable
{
	protected IResourceReader Reader;

	internal Hashtable Table;

	private Hashtable _caseInsensitiveTable;

	protected ResourceSet()
	{
		Table = new Hashtable();
	}

	internal ResourceSet(bool junk)
	{
	}

	public ResourceSet(string fileName)
		: this()
	{
		Reader = new ResourceReader(fileName);
		ReadResources();
	}

	public ResourceSet(Stream stream)
		: this()
	{
		Reader = new ResourceReader(stream);
		ReadResources();
	}

	public ResourceSet(IResourceReader reader)
		: this()
	{
		if (reader == null)
		{
			throw new ArgumentNullException("reader");
		}
		Reader = reader;
		ReadResources();
	}

	public virtual void Close()
	{
		Dispose(disposing: true);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			IResourceReader reader = Reader;
			Reader = null;
			reader?.Close();
		}
		Reader = null;
		_caseInsensitiveTable = null;
		Table = null;
	}

	public void Dispose()
	{
		Dispose(disposing: true);
	}

	public virtual Type GetDefaultReader()
	{
		return typeof(ResourceReader);
	}

	public virtual Type GetDefaultWriter()
	{
		Assembly assembly = Assembly.Load("System.Resources.Writer, Version=4.0.1.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
		return assembly.GetType("System.Resources.ResourceWriter", throwOnError: true);
	}

	public virtual IDictionaryEnumerator GetEnumerator()
	{
		return GetEnumeratorHelper();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumeratorHelper();
	}

	private IDictionaryEnumerator GetEnumeratorHelper()
	{
		Hashtable table = Table;
		if (table == null)
		{
			throw new ObjectDisposedException(null, SR.ObjectDisposed_ResourceSet);
		}
		return table.GetEnumerator();
	}

	public virtual string? GetString(string name)
	{
		object objectInternal = GetObjectInternal(name);
		try
		{
			return (string)objectInternal;
		}
		catch (InvalidCastException)
		{
			throw new InvalidOperationException(SR.Format(SR.InvalidOperation_ResourceNotString_Name, name));
		}
	}

	public virtual string? GetString(string name, bool ignoreCase)
	{
		object objectInternal = GetObjectInternal(name);
		string text;
		try
		{
			text = (string)objectInternal;
		}
		catch (InvalidCastException)
		{
			throw new InvalidOperationException(SR.Format(SR.InvalidOperation_ResourceNotString_Name, name));
		}
		if (text != null || !ignoreCase)
		{
			return text;
		}
		objectInternal = GetCaseInsensitiveObjectInternal(name);
		try
		{
			return (string)objectInternal;
		}
		catch (InvalidCastException)
		{
			throw new InvalidOperationException(SR.Format(SR.InvalidOperation_ResourceNotString_Name, name));
		}
	}

	public virtual object? GetObject(string name)
	{
		return GetObjectInternal(name);
	}

	public virtual object? GetObject(string name, bool ignoreCase)
	{
		object objectInternal = GetObjectInternal(name);
		if (objectInternal != null || !ignoreCase)
		{
			return objectInternal;
		}
		return GetCaseInsensitiveObjectInternal(name);
	}

	protected virtual void ReadResources()
	{
		IDictionaryEnumerator enumerator = Reader.GetEnumerator();
		while (enumerator.MoveNext())
		{
			object value = enumerator.Value;
			Table.Add(enumerator.Key, value);
		}
	}

	private object GetObjectInternal(string name)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		Hashtable table = Table;
		if (table == null)
		{
			throw new ObjectDisposedException(null, SR.ObjectDisposed_ResourceSet);
		}
		return table[name];
	}

	private object GetCaseInsensitiveObjectInternal(string name)
	{
		Hashtable table = Table;
		if (table == null)
		{
			throw new ObjectDisposedException(null, SR.ObjectDisposed_ResourceSet);
		}
		Hashtable hashtable = _caseInsensitiveTable;
		if (hashtable == null)
		{
			hashtable = new Hashtable(StringComparer.OrdinalIgnoreCase);
			IDictionaryEnumerator enumerator = table.GetEnumerator();
			while (enumerator.MoveNext())
			{
				hashtable.Add(enumerator.Key, enumerator.Value);
			}
			_caseInsensitiveTable = hashtable;
		}
		return hashtable[name];
	}
}
