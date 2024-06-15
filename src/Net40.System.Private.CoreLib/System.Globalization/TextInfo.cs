using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Unicode;
using Microsoft.IO;

namespace System.Globalization.Net40;

public class TextInfo : ICloneable, IDeserializationCallback
{
	private enum Tristate : byte
	{
		NotInitialized,
		False,
		True
	}

	[StructLayout(LayoutKind.Sequential, Size = 1)]
	private readonly struct ToUpperConversion
	{
	}

	[StructLayout(LayoutKind.Sequential, Size = 1)]
	private readonly struct ToLowerConversion
	{
	}

	private string _listSeparator;

	private bool _isReadOnly;

	private readonly string _cultureName;

	//private readonly CultureData _cultureData;

	private readonly string _textInfoName;

	private Tristate _isAsciiCasingSameAsInvariant;

	private static volatile TextInfo s_invariant;

	private IntPtr _sortHandle;

	//internal static TextInfo Invariant => s_invariant ?? (s_invariant = new TextInfo(CultureData.Invariant));

	// public virtual int ANSICodePage => _cultureData.ANSICodePage;
	//
	// public virtual int OEMCodePage => _cultureData.OEMCodePage;
	//
	// public virtual int MacCodePage => _cultureData.MacCodePage;
	//
	// public virtual int EBCDICCodePage => _cultureData.EBCDICCodePage;

	public int LCID => CultureInfo.GetCultureInfo(_textInfoName).LCID;

	public string CultureName => _textInfoName;

	public bool IsReadOnly => _isReadOnly;

	/*public virtual string ListSeparator
	{
		get
		{
			return _listSeparator ?? (_listSeparator = _cultureData.ListSeparator);
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			VerifyWritable();
			_listSeparator = value;
		}
	}*/

	private bool IsAsciiCasingSameAsInvariant
	{
		[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
		get
		{
			if (_isAsciiCasingSameAsInvariant == Tristate.NotInitialized)
			{
				PopulateIsAsciiCasingSameAsInvariant();
			}
			return _isAsciiCasingSameAsInvariant == Tristate.True;
		}
	}

	//public bool IsRightToLeft => _cultureData.IsRightToLeft;

	// internal TextInfo(CultureData cultureData)
	// {
	// 	_cultureData = cultureData;
	// 	_cultureName = _cultureData.CultureName;
	// 	_textInfoName = _cultureData.TextInfoName;
	// 	FinishInitialization();
	// }

	void IDeserializationCallback.OnDeserialization(object sender)
	{
		throw new PlatformNotSupportedException();
	}

	public virtual object Clone()
	{
		object obj = MemberwiseClone();
		((TextInfo)obj).SetReadOnlyState(readOnly: false);
		return obj;
	}

	public static TextInfo ReadOnly(TextInfo textInfo)
	{
		if (textInfo == null)
		{
			throw new ArgumentNullException("textInfo");
		}
		if (textInfo.IsReadOnly)
		{
			return textInfo;
		}
		TextInfo textInfo2 = (TextInfo)textInfo.MemberwiseClone();
		textInfo2.SetReadOnlyState(readOnly: true);
		return textInfo2;
	}

	private void VerifyWritable()
	{
		if (_isReadOnly)
		{
			throw new InvalidOperationException("SR.InvalidOperation_ReadOnly");
		}
	}

	internal void SetReadOnlyState(bool readOnly)
	{
		_isReadOnly = readOnly;
	}

	public virtual char ToLower(char c)
	{
		//if (GlobalizationMode.Invariant || (IsAscii(c) && IsAsciiCasingSameAsInvariant))
		{
			return ToLowerAsciiInvariant(c);
		}
		//return ChangeCase(c, toUpper: false);
	}

	public virtual string ToLower(string str)
	{
		if (str == null)
		{
			throw new ArgumentNullException("str");
		}
		//if (GlobalizationMode.Invariant)
		{
			return ToLowerAsciiInvariant(str);
		}
		//return ChangeCaseCommon<ToLowerConversion>(str);
	}

	// private unsafe char ChangeCase(char c, bool toUpper)
	// {
	// 	char result = '\0';
	// 	ChangeCase(&c, 1, &result, 1, toUpper);
	// 	return result;
	// }

	// [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	// internal void ChangeCaseToLower(ReadOnlySpan<char> source, Span<char> destination)
	// {
	// 	ChangeCaseCommon<ToLowerConversion>(ref MemoryMarshal.GetReference(source), ref MemoryMarshal.GetReference(destination), source.Length);
	// }
	//
	// [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	// internal void ChangeCaseToUpper(ReadOnlySpan<char> source, Span<char> destination)
	// {
	// 	ChangeCaseCommon<ToUpperConversion>(ref MemoryMarshal.GetReference(source), ref MemoryMarshal.GetReference(destination), source.Length);
	// }

	// [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	// private void ChangeCaseCommon<TConversion>(ReadOnlySpan<char> source, Span<char> destination) where TConversion : struct
	// {
	// 	ChangeCaseCommon<TConversion>(ref MemoryMarshal.GetReference(source), ref MemoryMarshal.GetReference(destination), source.Length);
	// }

	/*
	private unsafe void ChangeCaseCommon<TConversion>(ref char source, ref char destination, int charCount) where TConversion : struct
	{
		bool flag = typeof(TConversion) == typeof(ToUpperConversion);
		if (charCount == 0)
		{
			return;
		}
		fixed (char* ptr = &source)
		{
			fixed (char* ptr2 = &destination)
			{
				ulong num = 0uL;
				if (IsAsciiCasingSameAsInvariant)
				{
					if (charCount < 4)
					{
						goto IL_00e1;
					}
					ulong num2 = (uint)(charCount - 4);
					while (true)
					{
						uint value = Unsafe.ReadUnaligned<uint>(ptr + num);
						if (!Utf16Utility.AllCharsInUInt32AreAscii(value))
						{
							break;
						}
						value = (flag ? Utf16Utility.ConvertAllAsciiCharsInUInt32ToUppercase(value) : Utf16Utility.ConvertAllAsciiCharsInUInt32ToLowercase(value));
						Unsafe.WriteUnaligned(ptr2 + num, value);
						value = Unsafe.ReadUnaligned<uint>(ptr + num + 2);
						if (Utf16Utility.AllCharsInUInt32AreAscii(value))
						{
							value = (flag ? Utf16Utility.ConvertAllAsciiCharsInUInt32ToUppercase(value) : Utf16Utility.ConvertAllAsciiCharsInUInt32ToLowercase(value));
							Unsafe.WriteUnaligned(ptr2 + num + 2, value);
							num += 4;
							if (num <= num2)
							{
								continue;
							}
							goto IL_00e1;
						}
						num += 2;
						break;
					}
					goto IL_0169;
				}
				goto IL_0170;
				IL_0170:
				ChangeCase(ptr + num, charCount, ptr2 + num, charCount, flag);
				return;
				IL_0169:
				charCount -= (int)num;
				goto IL_0170;
				IL_00e1:
				if (((uint)charCount & 2u) != 0)
				{
					uint value2 = Unsafe.ReadUnaligned<uint>(ptr + num);
					if (!Utf16Utility.AllCharsInUInt32AreAscii(value2))
					{
						goto IL_0169;
					}
					value2 = (flag ? Utf16Utility.ConvertAllAsciiCharsInUInt32ToUppercase(value2) : Utf16Utility.ConvertAllAsciiCharsInUInt32ToLowercase(value2));
					Unsafe.WriteUnaligned(ptr2 + num, value2);
					num += 2;
				}
				if (((uint)charCount & (true ? 1u : 0u)) != 0)
				{
					uint num3 = ptr[num];
					if (num3 <= 127)
					{
						num3 = (flag ? Utf16Utility.ConvertAllAsciiCharsInUInt32ToUppercase(num3) : Utf16Utility.ConvertAllAsciiCharsInUInt32ToLowercase(num3));
						ptr2[num] = (char)num3;
						return;
					}
					goto IL_0169;
				}
			}
		}
	}
	*/

	/*private unsafe string ChangeCaseCommon<TConversion>(string source) where TConversion : struct
	{
		bool flag = typeof(TConversion) == typeof(ToUpperConversion);
		if (source.Length == 0)
		{
			return string.Empty;
		}
		fixed (char* ptr = source)
		{
			ulong num = 0uL;
			if (IsAsciiCasingSameAsInvariant)
			{
				if (source.Length < 2)
				{
					goto IL_0094;
				}
				ulong num2 = (uint)(source.Length - 2);
				while (true)
				{
					uint value = Unsafe.ReadUnaligned<uint>(ptr + num);
					if (!Utf16Utility.AllCharsInUInt32AreAscii(value))
					{
						break;
					}
					if (!(flag ? Utf16Utility.UInt32ContainsAnyLowercaseAsciiChar(value) : Utf16Utility.UInt32ContainsAnyUppercaseAsciiChar(value)))
					{
						num += 2;
						if (num <= num2)
						{
							continue;
						}
						goto IL_0094;
					}
					goto IL_00cf;
				}
			}
			goto IL_011f;
			IL_011f:
			string text = StringExtensions.FastAllocateString(source.Length);
			if (num != 0)
			{
				Span<char> destination = new Span<char>(ref text.GetRawStringData(), text.Length);
				source.AsSpan(0, (int)num).CopyTo(destination);
			}
			fixed (char* ptr2 = text)
			{
				ChangeCase(ptr + num, source.Length - (int)num, ptr2 + num, text.Length - (int)num, flag);
			}
			return text;
			IL_0094:
			if (((uint)source.Length & (true ? 1u : 0u)) != 0)
			{
				uint num3 = ptr[num];
				if (num3 > 127)
				{
					goto IL_011f;
				}
				if (flag ? (num3 - 97 <= 25) : (num3 - 65 <= 25))
				{
					goto IL_00cf;
				}
			}
			return source;
			IL_00cf:
			string text2 = StringExtensions.FastAllocateString(source.Length);
			Span<char> destination2 = new Span<char>(ref text2.GetRawStringData(), text2.Length);
			source.AsSpan(0, (int)num).CopyTo(destination2);
			ChangeCaseCommon<TConversion>(source.AsSpan((int)num), destination2.Slice((int)num));
			return text2;
		}
	}*/

	internal static unsafe string ToLowerAsciiInvariant(string s)
	{
		if (s.Length == 0)
		{
			return string.Empty;
		}
		fixed (char* ptr = s)
		{
			int i;
			for (i = 0; i < s.Length && (uint)(ptr[i] - 65) > 25u; i++)
			{
			}
			if (i >= s.Length)
			{
				return s;
			}
			string text = StringExEx.FastAllocateString(s.Length);
			fixed (char* ptr2 = text)
			{
				for (int j = 0; j < i; j++)
				{
					ptr2[j] = ptr[j];
				}
				ptr2[i] = (char)(ptr[i] | 0x20u);
				for (i++; i < s.Length; i++)
				{
					ptr2[i] = ToLowerAsciiInvariant(ptr[i]);
				}
			}
			return text;
		}
	}

	internal static void ToLowerAsciiInvariant(ReadOnlySpan<char> source, Span<char> destination)
	{
		for (int i = 0; i < source.Length; i++)
		{
			destination[i] = ToLowerAsciiInvariant(source[i]);
		}
	}

	private static unsafe string ToUpperAsciiInvariant(string s)
	{
		if (s.Length == 0)
		{
			return string.Empty;
		}
		fixed (char* ptr = s)
		{
			int i;
			for (i = 0; i < s.Length && (uint)(ptr[i] - 97) > 25u; i++)
			{
			}
			if (i >= s.Length)
			{
				return s;
			}
			string text = StringExEx.FastAllocateString(s.Length);
			fixed (char* ptr2 = text)
			{
				for (int j = 0; j < i; j++)
				{
					ptr2[j] = ptr[j];
				}
				ptr2[i] = (char)(ptr[i] & 0xFFFFFFDFu);
				for (i++; i < s.Length; i++)
				{
					ptr2[i] = ToUpperAsciiInvariant(ptr[i]);
				}
			}
			return text;
		}
	}

	internal static void ToUpperAsciiInvariant(ReadOnlySpan<char> source, Span<char> destination)
	{
		for (int i = 0; i < source.Length; i++)
		{
			destination[i] = ToUpperAsciiInvariant(source[i]);
		}
	}

	private static char ToLowerAsciiInvariant(char c)
	{
		if ((uint)(c - 65) <= 25u)
		{
			c = (char)(c | 0x20u);
		}
		return c;
	}

	public virtual char ToUpper(char c)
	{
		//if ((IsAscii(c) && IsAsciiCasingSameAsInvariant))
		{
			return ToUpperAsciiInvariant(c);
		}
		//return ChangeCase(c, toUpper: true);
	}

	public virtual string ToUpper(string str)
	{
		if (str == null)
		{
			throw new ArgumentNullException("str");
		}
		//if (GlobalizationMode.Invariant)
		{
			return ToUpperAsciiInvariant(str);
		}
		//return ChangeCaseCommon<ToUpperConversion>(str);
	}

	internal static char ToUpperAsciiInvariant(char c)
	{
		if ((uint)(c - 97) <= 25u)
		{
			c = (char)(c & 0xFFFFFFDFu);
		}
		return c;
	}

	private static bool IsAscii(char c)
	{
		return c < '\u0080';
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void PopulateIsAsciiCasingSameAsInvariant()
	{
		bool flag = CultureInfo.GetCultureInfo(_textInfoName).CompareInfo.Compare("abcdefghijklmnopqrstuvwxyz", "ABCDEFGHIJKLMNOPQRSTUVWXYZ", CompareOptions.IgnoreCase) == 0;
		_isAsciiCasingSameAsInvariant = ((!flag) ? Tristate.False : Tristate.True);
	}

	public override bool Equals(object? obj)
	{
		if (obj is TextInfo textInfo)
		{
			return CultureName.Equals(textInfo.CultureName);
		}
		return false;
	}

	public override int GetHashCode()
	{
		return CultureName.GetHashCode();
	}

	// public override string ToString()
	// {
	// 	return "TextInfo - " + _cultureData.CultureName;
	// }

	/*public string ToTitleCase(string str)
	{
		if (str == null)
		{
			throw new ArgumentNullException("str");
		}
		if (str.Length == 0)
		{
			return str;
		}
		StringBuilder result = new StringBuilder();
		string text = null;
		bool flag = CultureName.StartsWith("nl-", StringComparison.OrdinalIgnoreCase);
		int num;
		for (num = 0; num < str.Length; num++)
		{
			UnicodeCategory unicodeCategory = CharUnicodeInfo.InternalGetUnicodeCategory(str, num, out var charLength);
			if (char.CheckLetter(unicodeCategory))
			{
				if (flag && num < str.Length - 1 && (str[num] == 'i' || str[num] == 'I') && (str[num + 1] == 'j' || str[num + 1] == 'J'))
				{
					result.Append("IJ");
					num += 2;
				}
				else
				{
					num = AddTitlecaseLetter(ref result, ref str, num, charLength) + 1;
				}
				int num2 = num;
				bool flag2 = unicodeCategory == UnicodeCategory.LowercaseLetter;
				while (num < str.Length)
				{
					unicodeCategory = CharUnicodeInfo.InternalGetUnicodeCategory(str, num, out charLength);
					if (IsLetterCategory(unicodeCategory))
					{
						if (unicodeCategory == UnicodeCategory.LowercaseLetter)
						{
							flag2 = true;
						}
						num += charLength;
					}
					else if (str[num] == '\'')
					{
						num++;
						if (flag2)
						{
							if (text == null)
							{
								text = ToLower(str);
							}
							result.Append(text, num2, num - num2);
						}
						else
						{
							result.Append(str, num2, num - num2);
						}
						num2 = num;
						flag2 = true;
					}
					else
					{
						if (IsWordSeparator(unicodeCategory))
						{
							break;
						}
						num += charLength;
					}
				}
				int num3 = num - num2;
				if (num3 > 0)
				{
					if (flag2)
					{
						if (text == null)
						{
							text = ToLower(str);
						}
						result.Append(text, num2, num3);
					}
					else
					{
						result.Append(str, num2, num3);
					}
				}
				if (num < str.Length)
				{
					num = AddNonLetter(ref result, ref str, num, charLength);
				}
			}
			else
			{
				num = AddNonLetter(ref result, ref str, num, charLength);
			}
		}
		return result.ToString();
	}*/

	private static int AddNonLetter(ref StringBuilder result, ref string input, int inputIndex, int charLen)
	{
		if (charLen == 2)
		{
			result.Append(input[inputIndex++]);
			result.Append(input[inputIndex]);
		}
		else
		{
			result.Append(input[inputIndex]);
		}
		return inputIndex;
	}

	/*private int AddTitlecaseLetter(ref StringBuilder result, ref string input, int inputIndex, int charLen)
	{
		if (charLen == 2)
		{
			ReadOnlySpan<char> readOnlySpan = input.AsSpan(inputIndex, 2);
			if (GlobalizationMode.Invariant)
			{
				result.Append(readOnlySpan);
			}
			else
			{
				Span<char> span = stackalloc char[2];
				ChangeCaseToUpper(readOnlySpan, span);
				result.Append(span);
			}
			inputIndex++;
		}
		else
		{
			switch (input[inputIndex])
			{
			case 'Ǆ':
			case 'ǅ':
			case 'ǆ':
				result.Append('ǅ');
				break;
			case 'Ǉ':
			case 'ǈ':
			case 'ǉ':
				result.Append('ǈ');
				break;
			case 'Ǌ':
			case 'ǋ':
			case 'ǌ':
				result.Append('ǋ');
				break;
			case 'Ǳ':
			case 'ǲ':
			case 'ǳ':
				result.Append('ǲ');
				break;
			default:
				result.Append(ToUpper(input[inputIndex]));
				break;
			}
		}
		return inputIndex;
	}*/

	private static bool IsWordSeparator(UnicodeCategory category)
	{
		return (0x1FFCF800 & (1 << (int)category)) != 0;
	}

	private static bool IsLetterCategory(UnicodeCategory uc)
	{
		if (uc != 0 && uc != UnicodeCategory.LowercaseLetter && uc != UnicodeCategory.TitlecaseLetter && uc != UnicodeCategory.ModifierLetter)
		{
			return uc == UnicodeCategory.OtherLetter;
		}
		return true;
	}

	// private void FinishInitialization()
	// {
	// 	_sortHandle = CompareInfo.GetSortHandle(_textInfoName);
	// }

	// private unsafe void ChangeCase(char* pSource, int pSourceLen, char* pResult, int pResultLen, bool toUpper)
	// {
	// 	uint num = ((!IsInvariantLocale(_textInfoName)) ? 16777216u : 0u);
	// 	if (Interop.Kernel32.LCMapStringEx((_sortHandle != IntPtr.Zero) ? null : _textInfoName, num | (toUpper ? 512u : 256u), pSource, pSourceLen, pResult, pSourceLen, null, null, _sortHandle) == 0)
	// 	{
	// 		throw new InvalidOperationException(SR.InvalidOperation_ReadOnly);
	// 	}
	// }

	private static bool IsInvariantLocale(string localeName)
	{
		return localeName == "";
	}
}
