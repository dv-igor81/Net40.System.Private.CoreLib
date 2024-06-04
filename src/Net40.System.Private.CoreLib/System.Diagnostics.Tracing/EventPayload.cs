#define DEBUG
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace System.Diagnostics.Tracing;

internal class EventPayload : IDictionary<string, object?>, ICollection<KeyValuePair<string, object?>>, IEnumerable<KeyValuePair<string, object?>>, IEnumerable
{
	private List<string> m_names;

	private List<object?> m_values;

	public ICollection<string> Keys => m_names;

	public ICollection<object?> Values => m_values;

	public object? this[string key]
	{
		get
		{
			if (key == null)
			{
				throw new ArgumentNullException("key");
			}
			int position = 0;
			foreach (string name in m_names)
			{
				if (name == key)
				{
					return m_values[position];
				}
				position++;
			}
			throw new KeyNotFoundException("SR.Format(SR.Arg_KeyNotFoundWithKey, key)");
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public int Count => m_names.Count;

	public bool IsReadOnly => true;

	internal EventPayload(List<string> payloadNames, List<object?> payloadValues)
	{
		Debug.Assert(payloadNames.Count == payloadValues.Count);
		m_names = payloadNames;
		m_values = payloadValues;
	}

	public void Add(string key, object? value)
	{
		throw new NotSupportedException();
	}

	public void Add(KeyValuePair<string, object?> payloadEntry)
	{
		throw new NotSupportedException();
	}

	public void Clear()
	{
		throw new NotSupportedException();
	}

	public bool Contains(KeyValuePair<string, object?> entry)
	{
		return ContainsKey(entry.Key);
	}

	public bool ContainsKey(string key)
	{
		if (key == null)
		{
			throw new ArgumentNullException("key");
		}
		foreach (string item in m_names)
		{
			if (item == key)
			{
				return true;
			}
		}
		return false;
	}

	public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
	{
		for (int i = 0; i < Keys.Count; i++)
		{
			yield return new KeyValuePair<string, object>(m_names[i], m_values[i]);
		}
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return ((IEnumerable<KeyValuePair<string, object>>)this).GetEnumerator();
	}

	public void CopyTo(KeyValuePair<string, object?>[] payloadEntries, int count)
	{
		throw new NotSupportedException();
	}

	public bool Remove(string key)
	{
		throw new NotSupportedException();
	}

	public bool Remove(KeyValuePair<string, object?> entry)
	{
		throw new NotSupportedException();
	}

	public bool TryGetValue(string key, [MaybeNullWhen(false)] out object? value)
	{
		if (key == null)
		{
			throw new ArgumentNullException("key");
		}
		int position = 0;
		foreach (string name in m_names)
		{
			if (name == key)
			{
				value = m_values[position];
				return true;
			}
			position++;
		}
		value = null;
		return false;
	}
}
