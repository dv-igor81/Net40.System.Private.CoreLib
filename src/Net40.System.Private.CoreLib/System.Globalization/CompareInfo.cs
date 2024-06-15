/*
using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text.Unicode;
using DefaultNamespace;
using Microsoft.IO;

namespace System.Globalization.Net40;

[Serializable]
[TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
public class CompareInfo : IDeserializationCallback
{
	internal static readonly CompareInfo Invariant = CultureInfo.InvariantCulture.CompareInfo;

	[OptionalField(VersionAdded = 2)]
	private string m_name;

	[NonSerialized]
	private string _sortName;

	[OptionalField(VersionAdded = 3)]
	private SortVersion m_SortVersion;

	private int culture;

	[NonSerialized]
	private IntPtr _sortHandle;

	public virtual string Name
	{
		get
		{
			if (m_name == "zh-CHT" || m_name == "zh-CHS")
			{
				return m_name;
			}
			return _sortName;
		}
	}



	public int LCID => CultureInfo.GetCultureInfo(Name).LCID;

	internal CompareInfo(CultureInfo culture)
	{
		m_name = culture._name;
		InitSort(culture);
	}

	public static CompareInfo GetCompareInfo(int culture, Assembly assembly)
	{
		if (assembly == null)
		{
			throw new ArgumentNullException("assembly");
		}
		if (assembly != typeof(object).Module.Assembly)
		{
			throw new ArgumentException("SR.Argument_OnlyMscorlib", "assembly");
		}
		return GetCompareInfo(culture);
	}

	public static CompareInfo GetCompareInfo(string name, Assembly assembly)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		if (assembly == null)
		{
			throw new ArgumentNullException("assembly");
		}
		if (assembly != typeof(object).Module.Assembly)
		{
			throw new ArgumentException("SR.Argument_OnlyMscorlib", "assembly");
		}
		return GetCompareInfo(name);
	}

	public static CompareInfo GetCompareInfo(int culture)
	{
		if (CultureData.IsCustomCultureId(culture))
		{
			throw new ArgumentException("SR.Argument_CustomCultureCannotBePassedByNumber", "culture");
		}
		return CultureInfo.GetCultureInfo(culture).CompareInfo;
	}

	public static CompareInfo GetCompareInfo(string name)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		return CultureInfo.GetCultureInfo(name).CompareInfo;
	}

	public unsafe static bool IsSortable(char ch)
	{
		if (GlobalizationMode.Invariant)
		{
			return true;
		}
		char* text = &ch;
		return IsSortable(text, 1);
	}

	public unsafe static bool IsSortable(string text)
	{
		if (text == null)
		{
			throw new ArgumentNullException("text");
		}
		if (text.Length == 0)
		{
			return false;
		}
		if (GlobalizationMode.Invariant)
		{
			return true;
		}
		fixed (char* text2 = text)
		{
			return IsSortable(text2, text.Length);
		}
	}

	[OnDeserializing]
	private void OnDeserializing(StreamingContext ctx)
	{
		m_name = null;
	}

	void IDeserializationCallback.OnDeserialization(object sender)
	{
		OnDeserialized();
	}

	[OnDeserialized]
	private void OnDeserialized(StreamingContext ctx)
	{
		OnDeserialized();
	}

	private void OnDeserialized()
	{
		if (m_name == null)
		{
			m_name = CultureInfo.GetCultureInfo(culture)._name;
		}
		else
		{
			InitSort(CultureInfo.GetCultureInfo(m_name));
		}
	}

	[OnSerializing]
	private void OnSerializing(StreamingContext ctx)
	{
		culture = CultureInfo.GetCultureInfo(Name).LCID;
	}

	public virtual int Compare(string? string1, string? string2)
	{
		return Compare(string1, string2, CompareOptions.None);
	}

	public virtual int Compare(string? string1, string? string2, CompareOptions options)
	{
		if (options == CompareOptions.OrdinalIgnoreCase)
		{
			return string.Compare(string1, string2, StringComparison.OrdinalIgnoreCase);
		}
		if ((options & CompareOptions.Ordinal) != 0)
		{
			if (options != CompareOptions.Ordinal)
			{
				throw new ArgumentException("SR.Argument_CompareOptionOrdinal", "options");
			}
			return string.CompareOrdinal(string1, string2);
		}
		if (((uint)options & 0xDFFFFFE0u) != 0)
		{
			throw new ArgumentException("SR.Argument_InvalidFlag", "options");
		}
		if (string1 == null)
		{
			if (string2 == null)
			{
				return 0;
			}
			return -1;
		}
		if (string2 == null)
		{
			return 1;
		}
		if (GlobalizationMode.Invariant)
		{
			if ((options & CompareOptions.IgnoreCase) != 0)
			{
				return CompareOrdinalIgnoreCase(string1, string2);
			}
			return string.CompareOrdinal(string1, string2);
		}
		return CompareString(string1.AsSpan(), string2.AsSpan(), options);
	}

	internal int Compare(ReadOnlySpan<char> string1, string string2, CompareOptions options)
	{
		if (options == CompareOptions.OrdinalIgnoreCase)
		{
			return CompareOrdinalIgnoreCase(string1, string2.AsSpan());
		}
		if ((options & CompareOptions.Ordinal) != 0)
		{
			if (options != CompareOptions.Ordinal)
			{
				throw new ArgumentException("SR.Argument_CompareOptionOrdinal", "options");
			}
			return string.CompareOrdinal(string1, string2.AsSpan());
		}
		if (((uint)options & 0xDFFFFFE0u) != 0)
		{
			throw new ArgumentException("SR.Argument_InvalidFlag", "options");
		}
		if (string2 == null)
		{
			return 1;
		}
		if (GlobalizationMode.Invariant)
		{
			if ((options & CompareOptions.IgnoreCase) == 0)
			{
				return string.CompareOrdinal(string1, string2.AsSpan());
			}
			return CompareOrdinalIgnoreCase(string1, string2.AsSpan());
		}
		return CompareString(string1, string2, options);
	}

	internal int CompareOptionNone(ReadOnlySpan<char> string1, ReadOnlySpan<char> string2)
	{
		if (string1.Length == 0 || string2.Length == 0)
		{
			return string1.Length - string2.Length;
		}
		if (!GlobalizationMode.Invariant)
		{
			return CompareString(string1, string2, CompareOptions.None);
		}
		return string.CompareOrdinal(string1, string2);
	}

	internal int CompareOptionIgnoreCase(ReadOnlySpan<char> string1, ReadOnlySpan<char> string2)
	{
		if (string1.Length == 0 || string2.Length == 0)
		{
			return string1.Length - string2.Length;
		}
		if (!GlobalizationMode.Invariant)
		{
			return CompareString(string1, string2, CompareOptions.IgnoreCase);
		}
		return CompareOrdinalIgnoreCase(string1, string2);
	}

	public virtual int Compare(string? string1, int offset1, int length1, string? string2, int offset2, int length2)
	{
		return Compare(string1, offset1, length1, string2, offset2, length2, CompareOptions.None);
	}

	public virtual int Compare(string? string1, int offset1, string? string2, int offset2, CompareOptions options)
	{
		return Compare(string1, offset1, (string1 != null) ? (string1.Length - offset1) : 0, string2, offset2, (string2 != null) ? (string2.Length - offset2) : 0, options);
	}

	public virtual int Compare(string? string1, int offset1, string? string2, int offset2)
	{
		return Compare(string1, offset1, string2, offset2, CompareOptions.None);
	}

	public virtual int Compare(string? string1, int offset1, int length1, string? string2, int offset2, int length2, CompareOptions options)
	{
		if (options == CompareOptions.OrdinalIgnoreCase)
		{
			int num = string.Compare(string1, offset1, string2, offset2, (length1 < length2) ? length1 : length2, StringComparison.OrdinalIgnoreCase);
			if (length1 != length2 && num == 0)
			{
				if (length1 <= length2)
				{
					return -1;
				}
				return 1;
			}
			return num;
		}
		if (length1 < 0 || length2 < 0)
		{
			throw new ArgumentOutOfRangeException((length1 < 0) ? "length1" : "length2", SR.ArgumentOutOfRange_NeedPosNum);
		}
		if (offset1 < 0 || offset2 < 0)
		{
			throw new ArgumentOutOfRangeException((offset1 < 0) ? "offset1" : "offset2", SR.ArgumentOutOfRange_NeedPosNum);
		}
		if (offset1 > (string1?.Length ?? 0) - length1)
		{
			throw new ArgumentOutOfRangeException("string1", SR.ArgumentOutOfRange_OffsetLength);
		}
		if (offset2 > (string2?.Length ?? 0) - length2)
		{
			throw new ArgumentOutOfRangeException("string2", SR.ArgumentOutOfRange_OffsetLength);
		}
		if ((options & CompareOptions.Ordinal) != 0)
		{
			if (options != CompareOptions.Ordinal)
			{
				throw new ArgumentException(SR.Argument_CompareOptionOrdinal, "options");
			}
		}
		else if (((uint)options & 0xDFFFFFE0u) != 0)
		{
			throw new ArgumentException(SR.Argument_InvalidFlag, "options");
		}
		if (string1 == null)
		{
			if (string2 == null)
			{
				return 0;
			}
			return -1;
		}
		if (string2 == null)
		{
			return 1;
		}
		ReadOnlySpan<char> readOnlySpan = string1.AsSpan(offset1, length1);
		ReadOnlySpan<char> readOnlySpan2 = string2.AsSpan(offset2, length2);
		if (options == CompareOptions.Ordinal)
		{
			return string.CompareOrdinal(readOnlySpan, readOnlySpan2);
		}
		if (GlobalizationMode.Invariant)
		{
			if ((options & CompareOptions.IgnoreCase) != 0)
			{
				return CompareOrdinalIgnoreCase(readOnlySpan, readOnlySpan2);
			}
			return string.CompareOrdinal(readOnlySpan, readOnlySpan2);
		}
		return CompareString(readOnlySpan, readOnlySpan2, options);
	}

	internal static int CompareOrdinalIgnoreCase(string strA, int indexA, int lengthA, string strB, int indexB, int lengthB)
	{
		return CompareOrdinalIgnoreCase(ref Unsafe.Add(ref strA.GetRawStringData(), indexA), lengthA, ref Unsafe.Add(ref strB.GetRawStringData(), indexB), lengthB);
	}

	internal static int CompareOrdinalIgnoreCase(ReadOnlySpan<char> strA, ReadOnlySpan<char> strB)
	{
		return CompareOrdinalIgnoreCase(ref MemoryMarshal.GetReference(strA), strA.Length, ref MemoryMarshal.GetReference(strB), strB.Length);
	}

	internal static int CompareOrdinalIgnoreCase(string strA, string strB)
	{
		return CompareOrdinalIgnoreCase(ref strA.GetRawStringData(), strA.Length, ref strB.GetRawStringData(), strB.Length);
	}

	internal static int CompareOrdinalIgnoreCase(ref char strA, int lengthA, ref char strB, int lengthB)
	{
		int num = Math.Min(lengthA, lengthB);
		int num2 = num;
		ref char reference = ref strA;
		ref char reference2 = ref strB;
		char c = (GlobalizationMode.Invariant ? '\uffff' : '\u007f');
		while (num != 0 && reference <= c && reference2 <= c)
		{
			if (reference == reference2 || ((reference | 0x20) == (reference2 | 0x20) && (uint)((reference | 0x20) - 97) <= 25u))
			{
				num--;
				reference = ref Unsafe.Add(ref reference, 1);
				reference2 = ref Unsafe.Add(ref reference2, 1);
				continue;
			}
			int num3 = reference;
			int num4 = reference2;
			if ((uint)(reference - 97) <= 25u)
			{
				num3 -= 32;
			}
			if ((uint)(reference2 - 97) <= 25u)
			{
				num4 -= 32;
			}
			return num3 - num4;
		}
		if (num == 0)
		{
			return lengthA - lengthB;
		}
		num2 -= num;
		return CompareStringOrdinalIgnoreCase(ref reference, lengthA - num2, ref reference2, lengthB - num2);
	}

	internal static bool EqualsOrdinalIgnoreCase(ref char charA, ref char charB, int length)
	{
		IntPtr zero = IntPtr.Zero;
		while (true)
		{
			if ((uint)length >= 4u)
			{
				ulong num = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.AddByteOffset(ref charA, zero)));
				ulong num2 = Unsafe.ReadUnaligned<ulong>(ref Unsafe.As<char, byte>(ref Unsafe.AddByteOffset(ref charB, zero)));
				ulong num3 = num | num2;
				if (!Utf16Utility.AllCharsInUInt32AreAscii((uint)((int)num3 | (int)(num3 >> 32))))
				{
					break;
				}
				if (!Utf16Utility.UInt64OrdinalIgnoreCaseAscii(num, num2))
				{
					return false;
				}
				zero += 8;
				length -= 4;
				continue;
			}
			if ((uint)length >= 2u)
			{
				uint num4 = Unsafe.ReadUnaligned<uint>(ref Unsafe.As<char, byte>(ref Unsafe.AddByteOffset(ref charA, zero)));
				uint num5 = Unsafe.ReadUnaligned<uint>(ref Unsafe.As<char, byte>(ref Unsafe.AddByteOffset(ref charB, zero)));
				if (!Utf16Utility.AllCharsInUInt32AreAscii(num4 | num5))
				{
					break;
				}
				if (!Utf16Utility.UInt32OrdinalIgnoreCaseAscii(num4, num5))
				{
					return false;
				}
				zero += 4;
				length -= 2;
			}
			if (length != 0)
			{
				uint num6 = Unsafe.AddByteOffset(ref charA, zero);
				uint num7 = Unsafe.AddByteOffset(ref charB, zero);
				if ((num6 | num7) > 127)
				{
					break;
				}
				if (num6 == num7)
				{
					return true;
				}
				num6 |= 0x20u;
				if (num6 - 97 > 25)
				{
					return false;
				}
				if (num6 != (num7 | 0x20))
				{
					return false;
				}
				return true;
			}
			return true;
		}
		return EqualsOrdinalIgnoreCaseNonAscii(ref Unsafe.AddByteOffset(ref charA, zero), ref Unsafe.AddByteOffset(ref charB, zero), length);
	}

	private static bool EqualsOrdinalIgnoreCaseNonAscii(ref char charA, ref char charB, int length)
	{
		if (!GlobalizationMode.Invariant)
		{
			return CompareStringOrdinalIgnoreCase(ref charA, length, ref charB, length) == 0;
		}
		IntPtr zero = IntPtr.Zero;
		while (length != 0)
		{
			uint num = Unsafe.AddByteOffset(ref charA, zero);
			uint num2 = Unsafe.AddByteOffset(ref charB, zero);
			if (num == num2 || ((num | 0x20) == (num2 | 0x20) && (num | 0x20) - 97 <= 25))
			{
				zero += 2;
				length--;
				continue;
			}
			return false;
		}
		return true;
	}

	public virtual bool IsPrefix(string source, string prefix, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (prefix == null)
		{
			throw new ArgumentNullException("prefix");
		}
		if (prefix.Length == 0)
		{
			return true;
		}
		if (source.Length == 0)
		{
			return false;
		}
		switch (options)
		{
		case CompareOptions.OrdinalIgnoreCase:
			return source.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
		case CompareOptions.Ordinal:
			return source.StartsWith(prefix, StringComparison.Ordinal);
		default:
			if (((uint)options & 0xFFFFFFE0u) != 0)
			{
				throw new ArgumentException(SR.Argument_InvalidFlag, "options");
			}
			if (GlobalizationMode.Invariant)
			{
				return source.StartsWith(prefix, ((options & CompareOptions.IgnoreCase) != 0) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
			}
			return StartsWith(source, prefix, options);
		}
	}

	internal bool IsPrefix(ReadOnlySpan<char> source, ReadOnlySpan<char> prefix, CompareOptions options)
	{
		return StartsWith(source, prefix, options);
	}

	public virtual bool IsPrefix(string source, string prefix)
	{
		return IsPrefix(source, prefix, CompareOptions.None);
	}

	public virtual bool IsSuffix(string source, string suffix, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (suffix == null)
		{
			throw new ArgumentNullException("suffix");
		}
		if (suffix.Length == 0)
		{
			return true;
		}
		if (source.Length == 0)
		{
			return false;
		}
		switch (options)
		{
		case CompareOptions.OrdinalIgnoreCase:
			return source.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
		case CompareOptions.Ordinal:
			return source.EndsWith(suffix, StringComparison.Ordinal);
		default:
			if (((uint)options & 0xFFFFFFE0u) != 0)
			{
				throw new ArgumentException(SR.Argument_InvalidFlag, "options");
			}
			if (GlobalizationMode.Invariant)
			{
				return source.EndsWith(suffix, ((options & CompareOptions.IgnoreCase) != 0) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
			}
			return EndsWith(source, suffix, options);
		}
	}

	internal bool IsSuffix(ReadOnlySpan<char> source, ReadOnlySpan<char> suffix, CompareOptions options)
	{
		return EndsWith(source, suffix, options);
	}

	public virtual bool IsSuffix(string source, string suffix)
	{
		return IsSuffix(source, suffix, CompareOptions.None);
	}

	public virtual int IndexOf(string source, char value)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return IndexOf(source, value, 0, source.Length, CompareOptions.None);
	}

	public virtual int IndexOf(string source, string value)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return IndexOf(source, value, 0, source.Length, CompareOptions.None);
	}

	public virtual int IndexOf(string source, char value, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return IndexOf(source, value, 0, source.Length, options);
	}

	public virtual int IndexOf(string source, string value, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return IndexOf(source, value, 0, source.Length, options);
	}

	public virtual int IndexOf(string source, char value, int startIndex)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return IndexOf(source, value, startIndex, source.Length - startIndex, CompareOptions.None);
	}

	public virtual int IndexOf(string source, string value, int startIndex)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return IndexOf(source, value, startIndex, source.Length - startIndex, CompareOptions.None);
	}

	public virtual int IndexOf(string source, char value, int startIndex, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return IndexOf(source, value, startIndex, source.Length - startIndex, options);
	}

	public virtual int IndexOf(string source, string value, int startIndex, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return IndexOf(source, value, startIndex, source.Length - startIndex, options);
	}

	public virtual int IndexOf(string source, char value, int startIndex, int count)
	{
		return IndexOf(source, value, startIndex, count, CompareOptions.None);
	}

	public virtual int IndexOf(string source, string value, int startIndex, int count)
	{
		return IndexOf(source, value, startIndex, count, CompareOptions.None);
	}

	public unsafe virtual int IndexOf(string source, char value, int startIndex, int count, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (startIndex < 0 || startIndex > source.Length)
		{
			throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
		}
		if (count < 0 || startIndex > source.Length - count)
		{
			throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
		}
		if (source.Length == 0)
		{
			return -1;
		}
		if (((uint)options & 0xFFFFFFE0u) != 0 && options != CompareOptions.Ordinal && options != CompareOptions.OrdinalIgnoreCase)
		{
			throw new ArgumentException(SR.Argument_InvalidFlag, "options");
		}
		return IndexOf(source, char.ToString(value), startIndex, count, options, null);
	}

	public unsafe virtual int IndexOf(string source, string value, int startIndex, int count, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		if (startIndex > source.Length)
		{
			throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
		}
		if (source.Length == 0)
		{
			if (value.Length == 0)
			{
				return 0;
			}
			return -1;
		}
		if (startIndex < 0)
		{
			throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
		}
		if (count < 0 || startIndex > source.Length - count)
		{
			throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
		}
		if (((uint)options & 0xFFFFFFE0u) != 0 && options != CompareOptions.Ordinal && options != CompareOptions.OrdinalIgnoreCase)
		{
			throw new ArgumentException(SR.Argument_InvalidFlag, "options");
		}
		return IndexOf(source, value, startIndex, count, options, null);
	}

	internal int IndexOfOrdinalIgnoreCase(ReadOnlySpan<char> source, ReadOnlySpan<char> value)
	{
		return IndexOfOrdinalCore(source, value, ignoreCase: true, fromBeginning: true);
	}

	internal int LastIndexOfOrdinal(ReadOnlySpan<char> source, ReadOnlySpan<char> value, bool ignoreCase)
	{
		return IndexOfOrdinalCore(source, value, ignoreCase, fromBeginning: false);
	}

	internal unsafe int IndexOf(ReadOnlySpan<char> source, ReadOnlySpan<char> value, CompareOptions options)
	{
		return IndexOfCore(source, value, options, null, fromBeginning: true);
	}

	internal unsafe int LastIndexOf(ReadOnlySpan<char> source, ReadOnlySpan<char> value, CompareOptions options)
	{
		return IndexOfCore(source, value, options, null, fromBeginning: false);
	}

	internal unsafe int IndexOf(string source, string value, int startIndex, int count, CompareOptions options, int* matchLengthPtr)
	{
		if (matchLengthPtr != null)
		{
			*matchLengthPtr = 0;
		}
		if (value.Length == 0)
		{
			return startIndex;
		}
		if (startIndex >= source.Length)
		{
			return -1;
		}
		if (options == CompareOptions.OrdinalIgnoreCase)
		{
			int num = IndexOfOrdinal(source, value, startIndex, count, ignoreCase: true);
			if (num >= 0 && matchLengthPtr != null)
			{
				*matchLengthPtr = value.Length;
			}
			return num;
		}
		if (GlobalizationMode.Invariant)
		{
			int num2 = IndexOfOrdinal(source, value, startIndex, count, (options & (CompareOptions.IgnoreCase | CompareOptions.OrdinalIgnoreCase)) != 0);
			if (num2 >= 0 && matchLengthPtr != null)
			{
				*matchLengthPtr = value.Length;
			}
			return num2;
		}
		if (options == CompareOptions.Ordinal)
		{
			int num3 = SpanHelpers.IndexOf(ref Unsafe.Add(ref source.GetRawStringData(), startIndex), count, ref value.GetRawStringData(), value.Length);
			if (num3 >= 0)
			{
				num3 += startIndex;
				if (matchLengthPtr != null)
				{
					*matchLengthPtr = value.Length;
				}
			}
			return num3;
		}
		return IndexOfCore(source, value, startIndex, count, options, matchLengthPtr);
	}

	internal int IndexOfOrdinal(string source, string value, int startIndex, int count, bool ignoreCase)
	{
		if (!ignoreCase)
		{
			int num = SpanHelpers.IndexOf(ref Unsafe.Add(ref source.GetRawStringData(), startIndex), count, ref value.GetRawStringData(), value.Length);
			return ((num >= 0) ? startIndex : 0) + num;
		}
		if (GlobalizationMode.Invariant)
		{
			return InvariantIndexOf(source, value, startIndex, count, ignoreCase);
		}
		return IndexOfOrdinalCore(source, value, startIndex, count, ignoreCase);
	}

	public virtual int LastIndexOf(string source, char value)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return LastIndexOf(source, value, source.Length - 1, source.Length, CompareOptions.None);
	}

	public virtual int LastIndexOf(string source, string value)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return LastIndexOf(source, value, source.Length - 1, source.Length, CompareOptions.None);
	}

	public virtual int LastIndexOf(string source, char value, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return LastIndexOf(source, value, source.Length - 1, source.Length, options);
	}

	public virtual int LastIndexOf(string source, string value, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		return LastIndexOf(source, value, source.Length - 1, source.Length, options);
	}

	public virtual int LastIndexOf(string source, char value, int startIndex)
	{
		return LastIndexOf(source, value, startIndex, startIndex + 1, CompareOptions.None);
	}

	public virtual int LastIndexOf(string source, string value, int startIndex)
	{
		return LastIndexOf(source, value, startIndex, startIndex + 1, CompareOptions.None);
	}

	public virtual int LastIndexOf(string source, char value, int startIndex, CompareOptions options)
	{
		return LastIndexOf(source, value, startIndex, startIndex + 1, options);
	}

	public virtual int LastIndexOf(string source, string value, int startIndex, CompareOptions options)
	{
		return LastIndexOf(source, value, startIndex, startIndex + 1, options);
	}

	public virtual int LastIndexOf(string source, char value, int startIndex, int count)
	{
		return LastIndexOf(source, value, startIndex, count, CompareOptions.None);
	}

	public virtual int LastIndexOf(string source, string value, int startIndex, int count)
	{
		return LastIndexOf(source, value, startIndex, count, CompareOptions.None);
	}

	public virtual int LastIndexOf(string source, char value, int startIndex, int count, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (((uint)options & 0xFFFFFFE0u) != 0 && options != CompareOptions.Ordinal && options != CompareOptions.OrdinalIgnoreCase)
		{
			throw new ArgumentException(SR.Argument_InvalidFlag, "options");
		}
		if (source.Length == 0 && (startIndex == -1 || startIndex == 0))
		{
			return -1;
		}
		if (startIndex < 0 || startIndex > source.Length)
		{
			throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
		}
		if (startIndex == source.Length)
		{
			startIndex--;
			if (count > 0)
			{
				count--;
			}
		}
		if (count < 0 || startIndex - count + 1 < 0)
		{
			throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
		}
		if (options == CompareOptions.OrdinalIgnoreCase)
		{
			return source.LastIndexOf(value.ToString(), startIndex, count, StringComparison.OrdinalIgnoreCase);
		}
		if (GlobalizationMode.Invariant)
		{
			return InvariantLastIndexOf(source, char.ToString(value), startIndex, count, (options & (CompareOptions.IgnoreCase | CompareOptions.OrdinalIgnoreCase)) != 0);
		}
		return LastIndexOfCore(source, value.ToString(), startIndex, count, options);
	}

	public virtual int LastIndexOf(string source, string value, int startIndex, int count, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		if (((uint)options & 0xFFFFFFE0u) != 0 && options != CompareOptions.Ordinal && options != CompareOptions.OrdinalIgnoreCase)
		{
			throw new ArgumentException(SR.Argument_InvalidFlag, "options");
		}
		if (source.Length == 0 && (startIndex == -1 || startIndex == 0))
		{
			if (value.Length != 0)
			{
				return -1;
			}
			return 0;
		}
		if (startIndex < 0 || startIndex > source.Length)
		{
			throw new ArgumentOutOfRangeException("startIndex", SR.ArgumentOutOfRange_Index);
		}
		if (startIndex == source.Length)
		{
			startIndex--;
			if (count > 0)
			{
				count--;
			}
			if (value.Length == 0 && count >= 0 && startIndex - count + 1 >= 0)
			{
				return startIndex;
			}
		}
		if (count < 0 || startIndex - count + 1 < 0)
		{
			throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_Count);
		}
		if (options == CompareOptions.OrdinalIgnoreCase)
		{
			return LastIndexOfOrdinal(source, value, startIndex, count, ignoreCase: true);
		}
		if (GlobalizationMode.Invariant)
		{
			return InvariantLastIndexOf(source, value, startIndex, count, (options & (CompareOptions.IgnoreCase | CompareOptions.OrdinalIgnoreCase)) != 0);
		}
		return LastIndexOfCore(source, value, startIndex, count, options);
	}

	internal int LastIndexOfOrdinal(string source, string value, int startIndex, int count, bool ignoreCase)
	{
		if (GlobalizationMode.Invariant)
		{
			return InvariantLastIndexOf(source, value, startIndex, count, ignoreCase);
		}
		return LastIndexOfOrdinalCore(source, value, startIndex, count, ignoreCase);
	}

	public virtual SortKey GetSortKey(string source, CompareOptions options)
	{
		if (GlobalizationMode.Invariant)
		{
			return InvariantCreateSortKey(source, options);
		}
		return CreateSortKey(source, options);
	}

	public virtual SortKey GetSortKey(string source)
	{
		if (GlobalizationMode.Invariant)
		{
			return InvariantCreateSortKey(source, CompareOptions.None);
		}
		return CreateSortKey(source, CompareOptions.None);
	}

	public override bool Equals(object? value)
	{
		if (value is CompareInfo compareInfo)
		{
			return Name == compareInfo.Name;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Name.GetHashCode();
	}

	internal int GetHashCodeOfString(string source, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if ((options & ~(CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreSymbols | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth)) == 0)
		{
			if (GlobalizationMode.Invariant)
			{
				if ((options & CompareOptions.IgnoreCase) == 0)
				{
					return source.GetHashCode();
				}
				return source.GetHashCodeOrdinalIgnoreCase();
			}
			return GetHashCodeOfStringCore(source, options);
		}
		return options switch
		{
			CompareOptions.Ordinal => source.GetHashCode(), 
			CompareOptions.OrdinalIgnoreCase => source.GetHashCodeOrdinalIgnoreCase(), 
			_ => throw new ArgumentException(SR.Argument_InvalidFlag, "options"), 
		};
	}

	public virtual int GetHashCode(string source, CompareOptions options)
	{
		return GetHashCodeOfString(source, options);
	}

	public int GetHashCode(ReadOnlySpan<char> source, CompareOptions options)
	{
		if ((options & ~(CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace | CompareOptions.IgnoreSymbols | CompareOptions.IgnoreKanaType | CompareOptions.IgnoreWidth)) == 0)
		{
			if (GlobalizationMode.Invariant)
			{
				if ((options & CompareOptions.IgnoreCase) == 0)
				{
					return string.GetHashCode(source);
				}
				return string.GetHashCodeOrdinalIgnoreCase(source);
			}
			return GetHashCodeOfStringCore(source, options);
		}
		return options switch
		{
			CompareOptions.Ordinal => string.GetHashCode(source), 
			CompareOptions.OrdinalIgnoreCase => string.GetHashCodeOrdinalIgnoreCase(source), 
			_ => throw new ArgumentException(SR.Argument_InvalidFlag, "options"), 
		};
	}

	public override string ToString()
	{
		return "CompareInfo - " + Name;
	}

	internal unsafe static int InvariantIndexOf(string source, string value, int startIndex, int count, bool ignoreCase)
	{
		//The blocks IL_0021, IL_0044, IL_0049 are reachable both inside and outside the pinned region starting at IL_001e. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
		fixed (char* ptr2 = source)
		{
			char* intPtr;
			char* value2;
			char* source2;
			int num;
			if (value != null)
			{
				fixed (char* ptr = &value.GetPinnableReference())
				{
					intPtr = (value2 = ptr);
					source2 = ptr2 + startIndex;
					num = InvariantFindString(source2, count, value2, value.Length, ignoreCase, fromBeginning: true);
					if (num >= 0)
					{
						return num + startIndex;
					}
					return -1;
				}
			}
			intPtr = (value2 = null);
			source2 = ptr2 + startIndex;
			num = InvariantFindString(source2, count, value2, value.Length, ignoreCase, fromBeginning: true);
			if (num >= 0)
			{
				return num + startIndex;
			}
			return -1;
		}
	}

	internal unsafe static int InvariantIndexOf(ReadOnlySpan<char> source, ReadOnlySpan<char> value, bool ignoreCase, bool fromBeginning = true)
	{
		fixed (char* source2 = &MemoryMarshal.GetReference(source))
		{
			fixed (char* value2 = &MemoryMarshal.GetReference(value))
			{
				return InvariantFindString(source2, source.Length, value2, value.Length, ignoreCase, fromBeginning);
			}
		}
	}

	internal unsafe static int InvariantLastIndexOf(string source, string value, int startIndex, int count, bool ignoreCase)
	{
		//The blocks IL_0021, IL_0048, IL_0051 are reachable both inside and outside the pinned region starting at IL_001e. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
		fixed (char* ptr2 = source)
		{
			char* intPtr;
			char* value2;
			char* source2;
			int num;
			if (value != null)
			{
				fixed (char* ptr = &value.GetPinnableReference())
				{
					intPtr = (value2 = ptr);
					source2 = ptr2 + (startIndex - count + 1);
					num = InvariantFindString(source2, count, value2, value.Length, ignoreCase, fromBeginning: false);
					if (num >= 0)
					{
						return num + startIndex - count + 1;
					}
					return -1;
				}
			}
			intPtr = (value2 = null);
			source2 = ptr2 + (startIndex - count + 1);
			num = InvariantFindString(source2, count, value2, value.Length, ignoreCase, fromBeginning: false);
			if (num >= 0)
			{
				return num + startIndex - count + 1;
			}
			return -1;
		}
	}

	private unsafe static int InvariantFindString(char* source, int sourceCount, char* value, int valueCount, bool ignoreCase, bool fromBeginning)
	{
		int num = 0;
		int num2 = 0;
		if (valueCount == 0)
		{
			if (!fromBeginning)
			{
				return sourceCount - 1;
			}
			return 0;
		}
		if (sourceCount < valueCount)
		{
			return -1;
		}
		if (fromBeginning)
		{
			int num3 = sourceCount - valueCount;
			if (ignoreCase)
			{
				char c = InvariantToUpper(*value);
				for (num = 0; num <= num3; num++)
				{
					char c2 = InvariantToUpper(source[num]);
					if (c2 != c)
					{
						continue;
					}
					for (num2 = 1; num2 < valueCount; num2++)
					{
						c2 = InvariantToUpper(source[num + num2]);
						char c3 = InvariantToUpper(value[num2]);
						if (c2 != c3)
						{
							break;
						}
					}
					if (num2 == valueCount)
					{
						return num;
					}
				}
			}
			else
			{
				char c4 = *value;
				for (num = 0; num <= num3; num++)
				{
					char c2 = source[num];
					if (c2 != c4)
					{
						continue;
					}
					for (num2 = 1; num2 < valueCount; num2++)
					{
						c2 = source[num + num2];
						char c3 = value[num2];
						if (c2 != c3)
						{
							break;
						}
					}
					if (num2 == valueCount)
					{
						return num;
					}
				}
			}
		}
		else
		{
			int num3 = sourceCount - valueCount;
			if (ignoreCase)
			{
				char c5 = InvariantToUpper(*value);
				for (num = num3; num >= 0; num--)
				{
					char c2 = InvariantToUpper(source[num]);
					if (c2 == c5)
					{
						for (num2 = 1; num2 < valueCount; num2++)
						{
							c2 = InvariantToUpper(source[num + num2]);
							char c3 = InvariantToUpper(value[num2]);
							if (c2 != c3)
							{
								break;
							}
						}
						if (num2 == valueCount)
						{
							return num;
						}
					}
				}
			}
			else
			{
				char c6 = *value;
				for (num = num3; num >= 0; num--)
				{
					char c2 = source[num];
					if (c2 == c6)
					{
						for (num2 = 1; num2 < valueCount; num2++)
						{
							c2 = source[num + num2];
							char c3 = value[num2];
							if (c2 != c3)
							{
								break;
							}
						}
						if (num2 == valueCount)
						{
							return num;
						}
					}
				}
			}
		}
		return -1;
	}

	private static char InvariantToUpper(char c)
	{
		if ((uint)(c - 97) > 25u)
		{
			return c;
		}
		return (char)(c - 32);
	}

	private unsafe SortKey InvariantCreateSortKey(string source, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (((uint)options & 0xDFFFFFE0u) != 0)
		{
			throw new ArgumentException(SR.Argument_InvalidFlag, "options");
		}
		byte[] array;
		if (source.Length == 0)
		{
			array = Array.Empty<byte>();
		}
		else
		{
			array = new byte[source.Length * 2];
			fixed (char* source2 = source)
			{
				byte[] array2 = array;
				fixed (byte[] array3 = array2)
				{
					byte* ptr = (byte*)((array2 != null && array3.Length != 0) ? System.Runtime.CompilerServices.Unsafe.AsPointer(ref array3[0]) : null);
					if ((options & (CompareOptions.IgnoreCase | CompareOptions.OrdinalIgnoreCase)) != 0)
					{
						short* ptr2 = (short*)ptr;
						for (int i = 0; i < source.Length; i++)
						{
							ptr2[i] = (short)InvariantToUpper(source[i]);
						}
					}
					else
					{
						Buffer.MemoryCopy(source2, ptr, array.Length, array.Length);
					}
				}
			}
		}
		return new SortKey(Name, source, options, array);
	}

	internal unsafe static IntPtr GetSortHandle(string cultureName)
	{
		if (GlobalizationMode.Invariant)
		{
			return IntPtr.Zero;
		}
		System.Runtime.CompilerServices.Unsafe.SkipInit(out IntPtr intPtr);
		int num = Interop.Kernel32.LCMapStringEx(cultureName, 536870912u, null, 0, &intPtr, IntPtr.Size, null, null, IntPtr.Zero);
		if (num > 0)
		{
			int num2 = 0;
			char c = 'a';
			num = Interop.Kernel32.LCMapStringEx(null, 262144u, &c, 1, &num2, 4, null, null, intPtr);
			if (num > 1)
			{
				return intPtr;
			}
		}
		return IntPtr.Zero;
	}

	private void InitSort(CultureInfo culture)
	{
		_sortName = culture.SortName;
		_sortHandle = GetSortHandle(_sortName);
	}

	private unsafe static int FindStringOrdinal(uint dwFindStringOrdinalFlags, string stringSource, int offset, int cchSource, string value, int cchValue, bool bIgnoreCase)
	{
		//The blocks IL_0023, IL_0033, IL_0036, IL_0037, IL_0043, IL_0048 are reachable both inside and outside the pinned region starting at IL_0020. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
		fixed (char* ptr3 = stringSource)
		{
			char* intPtr;
			uint dwFindStringOrdinalFlags2;
			IntPtr lpStringSource;
			int cchSource2;
			char* lpStringValue;
			int cchValue2;
			int bIgnoreCase2;
			char* ptr2;
			int num;
			if (value != null)
			{
				fixed (char* ptr = &value.GetPinnableReference())
				{
					intPtr = (ptr2 = ptr);
					dwFindStringOrdinalFlags2 = dwFindStringOrdinalFlags;
					lpStringSource = (IntPtr)(ptr3 + offset);
					cchSource2 = cchSource;
					lpStringValue = ptr2;
					cchValue2 = cchValue;
					bIgnoreCase2 = (bIgnoreCase ? 1 : 0);
					num = Interop.Kernel32.FindStringOrdinal(dwFindStringOrdinalFlags2, (char*)lpStringSource, cchSource2, lpStringValue, cchValue2, bIgnoreCase2);
					if (num >= 0)
					{
						return num + offset;
					}
					return num;
				}
			}
			intPtr = (ptr2 = null);
			dwFindStringOrdinalFlags2 = dwFindStringOrdinalFlags;
			lpStringSource = (IntPtr)(ptr3 + offset);
			cchSource2 = cchSource;
			lpStringValue = ptr2;
			cchValue2 = cchValue;
			bIgnoreCase2 = (bIgnoreCase ? 1 : 0);
			num = Interop.Kernel32.FindStringOrdinal(dwFindStringOrdinalFlags2, (char*)lpStringSource, cchSource2, lpStringValue, cchValue2, bIgnoreCase2);
			if (num >= 0)
			{
				return num + offset;
			}
			return num;
		}
	}

	private unsafe static int FindStringOrdinal(uint dwFindStringOrdinalFlags, ReadOnlySpan<char> source, ReadOnlySpan<char> value, bool bIgnoreCase)
	{
		fixed (char* lpStringSource = &MemoryMarshal.GetReference(source))
		{
			fixed (char* lpStringValue = &MemoryMarshal.GetReference(value))
			{
				return Interop.Kernel32.FindStringOrdinal(dwFindStringOrdinalFlags, lpStringSource, source.Length, lpStringValue, value.Length, bIgnoreCase ? 1 : 0);
			}
		}
	}

	internal static int IndexOfOrdinalCore(string source, string value, int startIndex, int count, bool ignoreCase)
	{
		return FindStringOrdinal(4194304u, source, startIndex, count, value, value.Length, ignoreCase);
	}

	internal static int IndexOfOrdinalCore(ReadOnlySpan<char> source, ReadOnlySpan<char> value, bool ignoreCase, bool fromBeginning)
	{
		uint dwFindStringOrdinalFlags = (fromBeginning ? 4194304u : 8388608u);
		return FindStringOrdinal(dwFindStringOrdinalFlags, source, value, ignoreCase);
	}

	internal static int LastIndexOfOrdinalCore(string source, string value, int startIndex, int count, bool ignoreCase)
	{
		return FindStringOrdinal(8388608u, source, startIndex - count + 1, count, value, value.Length, ignoreCase);
	}

	private unsafe int GetHashCodeOfStringCore(ReadOnlySpan<char> source, CompareOptions options)
	{
		if (source.Length == 0)
		{
			return 0;
		}
		uint dwMapFlags = 0x400u | (uint)GetNativeCompareFlags(options);
		fixed (char* lpSrcStr = source)
		{
			int num = Interop.Kernel32.LCMapStringEx((_sortHandle != IntPtr.Zero) ? null : _sortName, dwMapFlags, lpSrcStr, source.Length, null, 0, null, null, _sortHandle);
			if (num == 0)
			{
				throw new ArgumentException(SR.Arg_ExternalException);
			}
			byte[] array = null;
			Span<byte> span = ((num > 512) ? ((Span<byte>)(array = ArrayPool<byte>.Shared.Rent(num))) : stackalloc byte[512]);
			Span<byte> span2 = span;
			fixed (byte* lpDestStr = &MemoryMarshal.GetReference(span2))
			{
				if (Interop.Kernel32.LCMapStringEx((_sortHandle != IntPtr.Zero) ? null : _sortName, dwMapFlags, lpSrcStr, source.Length, lpDestStr, num, null, null, _sortHandle) != num)
				{
					throw new ArgumentException(SR.Arg_ExternalException);
				}
			}
			int result = Marvin.ComputeHash32(span2.Slice(0, num), Marvin.DefaultSeed);
			if (array != null)
			{
				ArrayPool<byte>.Shared.Return(array);
			}
			return result;
		}
	}

	private unsafe static int CompareStringOrdinalIgnoreCase(ref char string1, int count1, ref char string2, int count2)
	{
		fixed (char* lpString = &string1)
		{
			fixed (char* lpString2 = &string2)
			{
				return Interop.Kernel32.CompareStringOrdinal(lpString, count1, lpString2, count2, bIgnoreCase: true) - 2;
			}
		}
	}

	private unsafe int CompareString(ReadOnlySpan<char> string1, string string2, CompareOptions options)
	{
		fixed (char* lpLocaleName = ((_sortHandle != IntPtr.Zero) ? null : _sortName))
		{
			fixed (char* lpString = &MemoryMarshal.GetReference(string1))
			{
				fixed (char* lpString2 = &string2.GetRawStringData())
				{
					int num = Interop.Kernel32.CompareStringEx(lpLocaleName, (uint)GetNativeCompareFlags(options), lpString, string1.Length, lpString2, string2.Length, null, null, _sortHandle);
					if (num == 0)
					{
						throw new ArgumentException(SR.Arg_ExternalException);
					}
					return num - 2;
				}
			}
		}
	}

	private unsafe int CompareString(ReadOnlySpan<char> string1, ReadOnlySpan<char> string2, CompareOptions options)
	{
		fixed (char* lpLocaleName = ((_sortHandle != IntPtr.Zero) ? null : _sortName))
		{
			fixed (char* lpString = &MemoryMarshal.GetReference(string1))
			{
				fixed (char* lpString2 = &MemoryMarshal.GetReference(string2))
				{
					int num = Interop.Kernel32.CompareStringEx(lpLocaleName, (uint)GetNativeCompareFlags(options), lpString, string1.Length, lpString2, string2.Length, null, null, _sortHandle);
					if (num == 0)
					{
						throw new ArgumentException(SR.Arg_ExternalException);
					}
					return num - 2;
				}
			}
		}
	}

	private unsafe int FindString(uint dwFindNLSStringFlags, ReadOnlySpan<char> lpStringSource, ReadOnlySpan<char> lpStringValue, int* pcchFound)
	{
		fixed (char* lpLocaleName = ((_sortHandle != IntPtr.Zero) ? null : _sortName))
		{
			fixed (char* lpStringSource2 = &MemoryMarshal.GetReference(lpStringSource))
			{
				fixed (char* lpStringValue2 = &MemoryMarshal.GetReference(lpStringValue))
				{
					return Interop.Kernel32.FindNLSStringEx(lpLocaleName, dwFindNLSStringFlags, lpStringSource2, lpStringSource.Length, lpStringValue2, lpStringValue.Length, pcchFound, null, null, _sortHandle);
				}
			}
		}
	}

	private unsafe int FindString(uint dwFindNLSStringFlags, string lpStringSource, int startSource, int cchSource, string lpStringValue, int startValue, int cchValue, int* pcchFound)
	{
		//The blocks IL_003f, IL_0042, IL_0054 are reachable both inside and outside the pinned region starting at IL_003a. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
		fixed (char* lpLocaleName = ((_sortHandle != IntPtr.Zero) ? null : _sortName))
		{
			char* intPtr;
			char* ptr2;
			if (lpStringSource != null)
			{
				fixed (char* ptr = &lpStringSource.GetPinnableReference())
				{
					intPtr = (ptr2 = ptr);
					fixed (char* ptr3 = lpStringValue)
					{
						char* ptr4 = ptr3;
						char* lpStringSource2 = ptr2 + startSource;
						char* lpStringValue2 = ptr4 + startValue;
						return Interop.Kernel32.FindNLSStringEx(lpLocaleName, dwFindNLSStringFlags, lpStringSource2, cchSource, lpStringValue2, cchValue, pcchFound, null, null, _sortHandle);
					}
				}
			}
			intPtr = (ptr2 = null);
			fixed (char* ptr3 = lpStringValue)
			{
				char* ptr4 = ptr3;
				char* lpStringSource2 = ptr2 + startSource;
				char* lpStringValue2 = ptr4 + startValue;
				return Interop.Kernel32.FindNLSStringEx(lpLocaleName, dwFindNLSStringFlags, lpStringSource2, cchSource, lpStringValue2, cchValue, pcchFound, null, null, _sortHandle);
			}
		}
	}

	internal unsafe int IndexOfCore(string source, string target, int startIndex, int count, CompareOptions options, int* matchLengthPtr)
	{
		int num = FindString(0x400000u | (uint)GetNativeCompareFlags(options), source, startIndex, count, target, 0, target.Length, matchLengthPtr);
		if (num >= 0)
		{
			return num + startIndex;
		}
		return -1;
	}

	internal unsafe int IndexOfCore(ReadOnlySpan<char> source, ReadOnlySpan<char> target, CompareOptions options, int* matchLengthPtr, bool fromBeginning)
	{
		uint num = (fromBeginning ? 4194304u : 8388608u);
		return FindString(num | (uint)GetNativeCompareFlags(options), source, target, matchLengthPtr);
	}

	private unsafe int LastIndexOfCore(string source, string target, int startIndex, int count, CompareOptions options)
	{
		if (target.Length == 0)
		{
			return startIndex;
		}
		if ((options & CompareOptions.Ordinal) != 0)
		{
			return FastLastIndexOfString(source, target, startIndex, count, target.Length);
		}
		int num = FindString(0x800000u | (uint)GetNativeCompareFlags(options), source, startIndex - count + 1, count, target, 0, target.Length, null);
		if (num >= 0)
		{
			return num + startIndex - (count - 1);
		}
		return -1;
	}

	private unsafe bool StartsWith(string source, string prefix, CompareOptions options)
	{
		return FindString(0x100000u | (uint)GetNativeCompareFlags(options), source, 0, source.Length, prefix, 0, prefix.Length, null) >= 0;
	}

	private unsafe bool StartsWith(ReadOnlySpan<char> source, ReadOnlySpan<char> prefix, CompareOptions options)
	{
		return FindString(0x100000u | (uint)GetNativeCompareFlags(options), source, prefix, null) >= 0;
	}

	private unsafe bool EndsWith(string source, string suffix, CompareOptions options)
	{
		return FindString(0x200000u | (uint)GetNativeCompareFlags(options), source, 0, source.Length, suffix, 0, suffix.Length, null) >= 0;
	}

	private unsafe bool EndsWith(ReadOnlySpan<char> source, ReadOnlySpan<char> suffix, CompareOptions options)
	{
		return FindString(0x200000u | (uint)GetNativeCompareFlags(options), source, suffix, null) >= 0;
	}

	private unsafe static int FastLastIndexOfString(string source, string target, int startIndex, int sourceCount, int targetCount)
	{
		//The blocks IL_002d, IL_0041, IL_0043, IL_004d, IL_005a, IL_005f, IL_0075, IL_007b, IL_0081, IL_0087, IL_008c, IL_0092, IL_0097, IL_009b are reachable both inside and outside the pinned region starting at IL_0028. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
		int num = -1;
		int num2 = startIndex - sourceCount + 1;
		fixed (char* ptr2 = source)
		{
			char* intPtr;
			if (target == null)
			{
				char* ptr;
				intPtr = (ptr = null);
				char* ptr3 = ptr2 + num2;
				int num3 = sourceCount - targetCount;
				if (num3 < 0)
				{
					return -1;
				}
				char c = *ptr;
				for (int num4 = num3; num4 >= 0; num4--)
				{
					if (ptr3[num4] == c)
					{
						int i;
						for (i = 1; i < targetCount && ptr3[num4 + i] == ptr[i]; i++)
						{
						}
						if (i == targetCount)
						{
							num = num4;
							break;
						}
					}
				}
				if (num >= 0)
				{
					num += startIndex - sourceCount + 1;
				}
			}
			else
			{
				fixed (char* ptr4 = &target.GetPinnableReference())
				{
					char* ptr;
					intPtr = (ptr = ptr4);
					char* ptr3 = ptr2 + num2;
					int num3 = sourceCount - targetCount;
					if (num3 < 0)
					{
						return -1;
					}
					char c = *ptr;
					for (int num4 = num3; num4 >= 0; num4--)
					{
						if (ptr3[num4] == c)
						{
							int i;
							for (i = 1; i < targetCount && ptr3[num4 + i] == ptr[i]; i++)
							{
							}
							if (i == targetCount)
							{
								num = num4;
								break;
							}
						}
					}
					if (num >= 0)
					{
						num += startIndex - sourceCount + 1;
					}
				}
			}
		}
		return num;
	}

	private unsafe SortKey CreateSortKey(string source, CompareOptions options)
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (((uint)options & 0xDFFFFFE0u) != 0)
		{
			throw new ArgumentException(SR.Argument_InvalidFlag, "options");
		}
		byte[] array;
		if (source.Length == 0)
		{
			array = Array.Empty<byte>();
		}
		else
		{
			uint dwMapFlags = 0x400u | (uint)GetNativeCompareFlags(options);
			fixed (char* lpSrcStr = source)
			{
				int num = Interop.Kernel32.LCMapStringEx((_sortHandle != IntPtr.Zero) ? null : _sortName, dwMapFlags, lpSrcStr, source.Length, null, 0, null, null, _sortHandle);
				if (num == 0)
				{
					throw new ArgumentException(SR.Arg_ExternalException);
				}
				array = new byte[num];
				fixed (byte* lpDestStr = array)
				{
					if (Interop.Kernel32.LCMapStringEx((_sortHandle != IntPtr.Zero) ? null : _sortName, dwMapFlags, lpSrcStr, source.Length, lpDestStr, array.Length, null, null, _sortHandle) != num)
					{
						throw new ArgumentException(SR.Arg_ExternalException);
					}
				}
			}
		}
		return new SortKey(Name, source, options, array);
	}

	private unsafe static bool IsSortable(char* text, int length)
	{
		return Interop.Kernel32.IsNLSDefinedString(1, 0u, IntPtr.Zero, text, length);
	}

	private static int GetNativeCompareFlags(CompareOptions options)
	{
		int num = 134217728;
		if ((options & CompareOptions.IgnoreCase) != 0)
		{
			num |= 1;
		}
		if ((options & CompareOptions.IgnoreKanaType) != 0)
		{
			num |= 0x10000;
		}
		if ((options & CompareOptions.IgnoreNonSpace) != 0)
		{
			num |= 2;
		}
		if ((options & CompareOptions.IgnoreSymbols) != 0)
		{
			num |= 4;
		}
		if ((options & CompareOptions.IgnoreWidth) != 0)
		{
			num |= 0x20000;
		}
		if ((options & CompareOptions.StringSort) != 0)
		{
			num |= 0x1000;
		}
		if (options == CompareOptions.Ordinal)
		{
			num = 1073741824;
		}
		return num;
	}

	// private unsafe SortVersion GetSortVersion()
	// {
	// 	Interop.Kernel32.NlsVersionInfoEx nlsVersionInfoEx = default(Interop.Kernel32.NlsVersionInfoEx);
	// 	nlsVersionInfoEx.dwNLSVersionInfoSize = sizeof(Interop.Kernel32.NlsVersionInfoEx);
	// 	Interop.Kernel32.GetNLSVersionEx(1, _sortName, &nlsVersionInfoEx);
	// 	return new SortVersion(nlsVersionInfoEx.dwNLSVersion, (nlsVersionInfoEx.dwEffectiveId == 0) ? LCID : nlsVersionInfoEx.dwEffectiveId, nlsVersionInfoEx.guidCustomVersion);
	// }
}
*/
