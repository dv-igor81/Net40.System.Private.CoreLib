﻿namespace System.Text;

using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

internal ref struct ValueStringBuilder
{
	private char[] _arrayToReturnToPool;

	private Span<char> _chars;

	private int _pos;

	public int Length
	{
		get
		{
			return _pos;
		}
		set
		{
			_pos = value;
		}
	}

	public int Capacity => _chars.Length;

	public ref char this[int index] => ref _chars[index];

	public Span<char> RawChars => _chars;

	public ValueStringBuilder(Span<char> initialBuffer)
	{
		_arrayToReturnToPool = null;
		_chars = initialBuffer;
		_pos = 0;
	}

	public ValueStringBuilder(int initialCapacity)
	{
		_arrayToReturnToPool = ArrayPool<char>.Shared.Rent(initialCapacity);
		_chars = _arrayToReturnToPool;
		_pos = 0;
	}

	public void EnsureCapacity(int capacity)
	{
		if (capacity > _chars.Length)
		{
			Grow(capacity - _pos);
		}
	}

	public ref char GetPinnableReference()
	{
		return ref MemoryMarshal.GetReference(_chars);
	}

	public ref char GetPinnableReference(bool terminate)
	{
		if (terminate)
		{
			EnsureCapacity(Length + 1);
			_chars[Length] = '\0';
		}
		return ref MemoryMarshal.GetReference(_chars);
	}

	public override string ToString()
	{
		string result = _chars.Slice(0, _pos).ToString();
		Dispose();
		return result;
	}

	public ReadOnlySpan<char> AsSpan(bool terminate)
	{
		if (terminate)
		{
			EnsureCapacity(Length + 1);
			_chars[Length] = '\0';
		}
		return _chars.Slice(0, _pos);
	}

	public ReadOnlySpan<char> AsSpan()
	{
		return _chars.Slice(0, _pos);
	}

	public ReadOnlySpan<char> AsSpan(int start)
	{
		return _chars.Slice(start, _pos - start);
	}

	public ReadOnlySpan<char> AsSpan(int start, int length)
	{
		return _chars.Slice(start, length);
	}

	public bool TryCopyTo(Span<char> destination, out int charsWritten)
	{
		if (_chars.Slice(0, _pos).TryCopyTo(destination))
		{
			charsWritten = _pos;
			Dispose();
			return true;
		}
		charsWritten = 0;
		Dispose();
		return false;
	}

	public void Insert(int index, string s)
	{
		int length = s.Length;
		if (_pos > _chars.Length - length)
		{
			Grow(length);
		}
		int length2 = _pos - index;
		_chars.Slice(index, length2).CopyTo(_chars.Slice(index + length));
		s.AsSpan().CopyTo(_chars.Slice(index));
		_pos += length;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void Append(char c)
	{
		int pos = _pos;
		if ((uint)pos < (uint)_chars.Length)
		{
			_chars[pos] = c;
			_pos = pos + 1;
		}
		else
		{
			GrowAndAppend(c);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void Append(string s)
	{
		int pos = _pos;
		if (s.Length == 1 && (uint)pos < (uint)_chars.Length)
		{
			_chars[pos] = s[0];
			_pos = pos + 1;
		}
		else
		{
			AppendSlow(s);
		}
	}

	private void AppendSlow(string s)
	{
		int pos = _pos;
		if (pos > _chars.Length - s.Length)
		{
			Grow(s.Length);
		}
		s.AsSpan().CopyTo(_chars.Slice(pos));
		_pos += s.Length;
	}

	public void Append(char c, int count)
	{
		if (_pos > _chars.Length - count)
		{
			Grow(count);
		}
		Span<char> span = _chars.Slice(_pos, count);
		for (int i = 0; i < span.Length; i++)
		{
			span[i] = c;
		}
		_pos += count;
	}

	public unsafe void Append(char* value, int length)
	{
		int pos = _pos;
		if (pos > _chars.Length - length)
		{
			Grow(length);
		}
		Span<char> span = _chars.Slice(_pos, length);
		for (int i = 0; i < span.Length; i++)
		{
			span[i] = *(value++);
		}
		_pos += length;
	}

	public void Append(ReadOnlySpan<char> value)
	{
		int pos = _pos;
		if (pos > _chars.Length - value.Length)
		{
			Grow(value.Length);
		}
		value.CopyTo(_chars.Slice(_pos));
		_pos += value.Length;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public Span<char> AppendSpan(int length)
	{
		int pos = _pos;
		if (pos > _chars.Length - length)
		{
			Grow(length);
		}
		_pos = pos + length;
		return _chars.Slice(pos, length);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void GrowAndAppend(char c)
	{
		Grow(1);
		Append(c);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void Grow(int additionalCapacityBeyondPos)
	{
		char[] array = ArrayPool<char>.Shared.Rent(Math.Max(_pos + additionalCapacityBeyondPos, _chars.Length * 2));
		_chars.CopyTo(array);
		char[] arrayToReturnToPool = _arrayToReturnToPool;
		_chars = (_arrayToReturnToPool = array);
		if (arrayToReturnToPool != null)
		{
			ArrayPool<char>.Shared.Return(arrayToReturnToPool);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void Dispose()
	{
		char[] arrayToReturnToPool = _arrayToReturnToPool;
		this = default(ValueStringBuilder);
		if (arrayToReturnToPool != null)
		{
			ArrayPool<char>.Shared.Return(arrayToReturnToPool);
		}
	}
}