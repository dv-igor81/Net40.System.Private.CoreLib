using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System;

public static class StringSpanEx
{
	[StructLayout(LayoutKind.Explicit, Size = 32)]
	private struct ProbabilisticMap
	{
	}

	public static unsafe string[] Split(this string self, char separator, StringSplitOptions options = StringSplitOptions.None)
	{
		return self.SplitInternal(new ReadOnlySpan<char>(Unsafe.AsPointer(ref separator), 1), int.MaxValue, options);
	}

	public static unsafe string[] Split(this string self, char separator, int count, StringSplitOptions options = StringSplitOptions.None)
	{
		return self.SplitInternal(new ReadOnlySpan<char>(Unsafe.AsPointer(ref separator), 1), count, options);
	}

	private static string[] SplitInternal(this string self, ReadOnlySpan<char> separators, int count, StringSplitOptions options)
	{
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count", "SR.ArgumentOutOfRange_NegativeCount");
		}
		if (options < StringSplitOptions.None || options > StringSplitOptions.RemoveEmptyEntries)
		{
			throw new ArgumentException(SR.Format(SR.Arg_EnumIllegalVal, options));
		}
		bool flag = options == StringSplitOptions.RemoveEmptyEntries;
		if (count == 0 || (flag && self.Length == 0))
		{
			return ArrayEx.Empty<string>();
		}
		if (count == 1)
		{
			return new string[1] { self };
		}
		Span<int> initialSpan = stackalloc int[128];
		ValueListBuilder<int> sepListBuilder = new ValueListBuilder<int>(initialSpan);
		self.MakeSeparatorList(separators, ref sepListBuilder);
		ReadOnlySpan<int> sepList = sepListBuilder.AsSpan();
		if (sepList.Length == 0)
		{
			return new string[1] { self };
		}
		string[] result = (flag ? self.SplitOmitEmptyEntries(sepList, default(ReadOnlySpan<int>), 1, count) : self.SplitKeepEmptyEntries(sepList, default(ReadOnlySpan<int>), 1, count));
		sepListBuilder.Dispose();
		return result;
	}

	private static string[] SplitOmitEmptyEntries(this string self, ReadOnlySpan<int> sepList, ReadOnlySpan<int> lengthList, int defaultLength, int count)
	{
		int length = sepList.Length;
		int num = ((length < count) ? (length + 1) : count);
		string[] array = new string[num];
		int num2 = 0;
		int num3 = 0;
		for (int i = 0; i < length; i++)
		{
			if (num2 >= self.Length)
			{
				break;
			}
			if (sepList[i] - num2 > 0)
			{
				array[num3++] = self.Substring(num2, sepList[i] - num2);
			}
			num2 = sepList[i] + (lengthList.IsEmpty ? defaultLength : lengthList[i]);
			if (num3 == count - 1)
			{
				while (i < length - 1 && num2 == sepList[++i])
				{
					num2 += (lengthList.IsEmpty ? defaultLength : lengthList[i]);
				}
				break;
			}
		}
		if (num2 < self.Length)
		{
			array[num3++] = self.Substring(num2);
		}
		string[] array2 = array;
		if (num3 != num)
		{
			array2 = new string[num3];
			for (int j = 0; j < num3; j++)
			{
				array2[j] = array[j];
			}
		}
		return array2;
	}

	private static string[] SplitKeepEmptyEntries(this string self, ReadOnlySpan<int> sepList, ReadOnlySpan<int> lengthList, int defaultLength, int count)
	{
		int num = 0;
		int num2 = 0;
		count--;
		int num3 = ((sepList.Length < count) ? sepList.Length : count);
		string[] array = new string[num3 + 1];
		for (int i = 0; i < num3; i++)
		{
			if (num >= self.Length)
			{
				break;
			}
			array[num2++] = self.Substring(num, sepList[i] - num);
			num = sepList[i] + (lengthList.IsEmpty ? defaultLength : lengthList[i]);
		}
		if (num < self.Length && num3 >= 0)
		{
			array[num2] = self.Substring(num);
		}
		else if (num2 == num3)
		{
			array[num2] = string.Empty;
		}
		return array;
	}

	private static unsafe void MakeSeparatorList(this string self, ReadOnlySpan<char> separators, ref ValueListBuilder<int> sepListBuilder)
	{
		switch (separators.Length)
		{
		case 0:
		{
			for (int i = 0; i < self.Length; i++)
			{
				if (char.IsWhiteSpace(self[i]))
				{
					sepListBuilder.Append(i);
				}
			}
			return;
		}
		case 1:
		{
			char c3 = separators[0];
			for (int k = 0; k < self.Length; k++)
			{
				if (self[k] == c3)
				{
					sepListBuilder.Append(k);
				}
			}
			return;
		}
		case 2:
		{
			char c2 = separators[0];
			char c5 = separators[1];
			for (int l = 0; l < self.Length; l++)
			{
				char c8 = self[l];
				if (c8 == c2 || c8 == c5)
				{
					sepListBuilder.Append(l);
				}
			}
			return;
		}
		case 3:
		{
			char c = separators[0];
			char c4 = separators[1];
			char c6 = separators[2];
			for (int j = 0; j < self.Length; j++)
			{
				char c7 = self[j];
				if (c7 == c || c7 == c4 || c7 == c6)
				{
					sepListBuilder.Append(j);
				}
			}
			return;
		}
		}
		ProbabilisticMap probabilisticMap = default(ProbabilisticMap);
		uint* charMap = (uint*)(&probabilisticMap);
		InitializeProbabilisticMap(charMap, separators);
		for (int m = 0; m < self.Length; m++)
		{
			char c9 = self[m];
			ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(new char[1] { c9 });
			if (IsCharBitSet(charMap, (byte)c9) && IsCharBitSet(charMap, (byte)((int)c9 >> 8)) && separators.Contains(readOnlySpan, StringComparison.Ordinal))
			{
				sepListBuilder.Append(m);
			}
		}
	}

	private static unsafe void InitializeProbabilisticMap(uint* charMap, ReadOnlySpan<char> anyOf)
	{
		bool flag = false;
		for (int i = 0; i < anyOf.Length; i++)
		{
			int num = anyOf[i];
			SetCharBit(charMap, (byte)num);
			num >>= 8;
			if (num == 0)
			{
				flag = true;
			}
			else
			{
				SetCharBit(charMap, (byte)num);
			}
		}
		if (flag)
		{
			*charMap |= 1u;
		}
	}

	private static unsafe void SetCharBit(uint* charMap, byte value)
	{
		charMap[value & 7] |= (uint)(1 << (value >> 3));
	}

	private static unsafe bool IsCharBitSet(uint* charMap, byte value)
	{
		return (charMap[value & 7] & (uint)(1 << (value >> 3))) != 0;
	}
}
