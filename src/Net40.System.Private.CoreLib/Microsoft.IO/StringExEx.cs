using System;

namespace Microsoft.IO;

public static class StringExEx
{
	public delegate void SpanAction<T, in TArg>(Span<T> span, TArg arg);

	public static bool Contains(this string self, char value)
	{
		ref char firstChar = ref self.GetRawStringData();
		return SpanHelpers.Contains(ref firstChar, value, self.Length);
	}
	
	// public static bool Contains(this string str, char value)
	// {
	// 	return str.AsSpan(0, str.Length).Contains(value);
	// }
	
	public static ref readonly char GetPinnableReference(this string self)
	{
		return ref self.GetRawStringData();
	}

	public static ref char GetRawStringData(this string self)
	{
		unsafe
		{
			fixed (char* firstCharPtr = self)
			{
				return ref *firstCharPtr;
			}
		}
	}
	
	public static string FastAllocateString(int length)
	{
		return new string(new char[length]);
	}

	public static unsafe string Create<TState>(int length, TState state, SpanAction<char, TState> action)
	{
		if (action == null)
		{
			throw new ArgumentNullException("action");
		}
		if (length <= 0)
		{
			if (length == 0)
			{
				return string.Empty;
			}
			throw new ArgumentOutOfRangeException("length");
		}
		string result = new string('\0', length);
		fixed (char* r = result)
		{
			action(new Span<char>(r, length), state);
		}
		return result;
	}
}
