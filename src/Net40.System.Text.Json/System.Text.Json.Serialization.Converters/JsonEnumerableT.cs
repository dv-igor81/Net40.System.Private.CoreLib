using System.Collections;
using System.Collections.Generic;

namespace System.Text.Json.Serialization.Converters;

internal class JsonEnumerableT<T> : ICollection<T>, IEnumerable<T>, IEnumerable, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>
{
	private List<T> _list;

	public T this[int index]
	{
		get
		{
			return _list[index];
		}
		set
		{
			_list[index] = value;
		}
	}

	public int Count => _list.Count;

	public bool IsReadOnly => false;

	public JsonEnumerableT(IList sourceList)
	{
		_list = new List<T>();
		foreach (object item in sourceList)
		{
			_list.Add((T)item);
		}
	}

	public void Add(T item)
	{
		_list.Add(item);
	}

	public void Clear()
	{
		_list.Clear();
	}

	public bool Contains(T item)
	{
		return _list.Contains(item);
	}

	public void CopyTo(T[] array, int arrayIndex)
	{
		_list.CopyTo(array, arrayIndex);
	}

	public IEnumerator<T> GetEnumerator()
	{
		return _list.GetEnumerator();
	}

	public int IndexOf(T item)
	{
		return _list.IndexOf(item);
	}

	public void Insert(int index, T item)
	{
		_list.Insert(index, item);
	}

	public bool Remove(T item)
	{
		return _list.Remove(item);
	}

	public void RemoveAt(int index)
	{
		_list.RemoveAt(index);
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}
}
