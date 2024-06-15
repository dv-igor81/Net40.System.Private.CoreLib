using System.Globalization;
using System.Net40;
using System.Runtime.CompilerServices;

namespace System;

internal static class DomainNameHelper
{
	private static readonly IdnMapping s_idnMapping = new IdnMapping();

	private static readonly char[] s_UnsafeForNormalizedHost = new char[8] { '\\', '/', '?', '@', '#', ':', '[', ']' };

	internal static string ParseCanonicalName(string str, int start, int end, ref bool loopback)
	{
		string text = null;
		for (int num = end - 1; num >= start; num--)
		{
			if (str[num] >= 'A' && str[num] <= 'Z')
			{
				text = str.Substring(start, end - start).ToLowerInvariant();
				break;
			}
			if (str[num] == ':')
			{
				end = num;
			}
		}
		if (text == null)
		{
			text = str.Substring(start, end - start);
		}
		if (text == "localhost" || text == "loopback")
		{
			loopback = true;
			return "localhost";
		}
		return text;
	}

	internal static unsafe bool IsValid(char* name, ushort pos, ref int returnedEnd, ref bool notCanonical, bool notImplicitFile)
	{
		char* ptr = name + (int)pos;
		char* ptr2 = ptr;
		char* ptr3;
		for (ptr3 = name + returnedEnd; ptr2 < ptr3; ptr2++)
		{
			char c = *ptr2;
			if (c > '\u007f')
			{
				return false;
			}
			if (c < 'a' && (c == '/' || c == '\\' || (notImplicitFile && (c == ':' || c == '?' || c == '#'))))
			{
				ptr3 = ptr2;
				break;
			}
		}
		if (ptr3 == ptr)
		{
			return false;
		}
		do
		{
			for (ptr2 = ptr; ptr2 < ptr3 && *ptr2 != '.'; ptr2++)
			{
			}
			if (ptr == ptr2 || ptr2 - ptr > 63 || !IsASCIILetterOrDigit(*(ptr++), ref notCanonical))
			{
				return false;
			}
			while (ptr < ptr2)
			{
				if (!IsValidDomainLabelCharacter(*(ptr++), ref notCanonical))
				{
					return false;
				}
			}
			ptr++;
		}
		while (ptr < ptr3);
		returnedEnd = (ushort)(ptr3 - name);
		return true;
	}

	internal static unsafe bool IsValidByIri(char* name, ushort pos, ref int returnedEnd, ref bool notCanonical, bool notImplicitFile)
	{
		char* ptr = name + (int)pos;
		char* ptr2 = ptr;
		char* ptr3 = name + returnedEnd;
		int num = 0;
		for (; ptr2 < ptr3; ptr2++)
		{
			char c = *ptr2;
			if (c == '/' || c == '\\' || (notImplicitFile && (c == ':' || c == '?' || c == '#')))
			{
				ptr3 = ptr2;
				break;
			}
		}
		if (ptr3 == ptr)
		{
			return false;
		}
		do
		{
			ptr2 = ptr;
			num = 0;
			bool flag = false;
			for (; ptr2 < ptr3 && *ptr2 != '.' && *ptr2 != '。' && *ptr2 != '．' && *ptr2 != '｡'; ptr2++)
			{
				num++;
				if (*ptr2 > 'ÿ')
				{
					num++;
				}
				if (*ptr2 >= '\u00a0')
				{
					flag = true;
				}
			}
			if (ptr == ptr2 || (flag ? (num + 4) : num) > 63 || (*(ptr++) < '\u00a0' && !IsASCIILetterOrDigit(*(ptr - 1), ref notCanonical)))
			{
				return false;
			}
			while (ptr < ptr2)
			{
				if (*(ptr++) < '\u00a0' && !IsValidDomainLabelCharacter(*(ptr - 1), ref notCanonical))
				{
					return false;
				}
			}
			ptr++;
		}
		while (ptr < ptr3);
		returnedEnd = (ushort)(ptr3 - name);
		return true;
	}

	internal static unsafe string IdnEquivalent(string hostname)
	{
		bool allAscii = true;
		bool atLeastOneValidIdn = false;
		fixed (char* hostname2 = hostname)
		{
			return IdnEquivalent(hostname2, 0, hostname.Length, ref allAscii, ref atLeastOneValidIdn);
		}
	}

	internal static unsafe string IdnEquivalent(char* hostname, int start, int end, ref bool allAscii, ref bool atLeastOneValidIdn)
	{
		string bidiStrippedHost = null;
		string text = IdnEquivalent(hostname, start, end, ref allAscii, ref bidiStrippedHost);
		if (text != null)
		{
			string text2 = (allAscii ? text : bidiStrippedHost);
			fixed (char* ptr = text2)
			{
				int length = text2.Length;
				int num = 0;
				int num2 = 0;
				bool flag = false;
				bool flag2 = false;
				bool flag3 = false;
				do
				{
					flag = false;
					flag2 = false;
					flag3 = false;
					num = num2;
					while (num < length)
					{
						char c = ptr[num];
						if (!flag2)
						{
							flag2 = true;
							if (num + 3 < length && IsIdnAce(ptr, num))
							{
								num += 4;
								flag = true;
								continue;
							}
						}
						if (c == '.' || c == '。' || c == '．' || c == '｡')
						{
							flag3 = true;
							break;
						}
						num++;
					}
					if (flag)
					{
						try
						{
							s_idnMapping.GetUnicode(text2, num2, num - num2);
							atLeastOneValidIdn = true;
						}
						catch (ArgumentException)
						{
							goto IL_00d6;
						}
						break;
					}
					goto IL_00d6;
					IL_00d6:
					num2 = num + (flag3 ? 1 : 0);
				}
				while (num2 < length);
			}
		}
		else
		{
			atLeastOneValidIdn = false;
		}
		return text;
	}

	internal static unsafe string IdnEquivalent(char* hostname, int start, int end, ref bool allAscii, ref string bidiStrippedHost)
	{
		string result = null;
		if (end <= start)
		{
			return result;
		}
		int i = start;
		allAscii = true;
		for (; i < end; i++)
		{
			if (hostname[i] > '\u007f')
			{
				allAscii = false;
				break;
			}
		}
		if (allAscii)
		{
			string text = new string(hostname, start, end - start);
			return text.ToLowerInvariant();
		}
		bidiStrippedHost = UriHelper.StripBidiControlCharacter(hostname, start, end - start);
		try
		{
			string ascii = s_idnMapping.GetAscii(bidiStrippedHost);
			if (ContainsCharactersUnsafeForNormalizedHost(ascii))
			{
				throw new UriFormatException("SR.net_uri_BadUnicodeHostForIdn");
			}
			return ascii;
		}
		catch (ArgumentException)
		{
			throw new UriFormatException("SR.net_uri_BadUnicodeHostForIdn");
		}
	}

	private static bool IsIdnAce(string input, int index)
	{
		if (input[index] == 'x' && input[index + 1] == 'n' && input[index + 2] == '-' && input[index + 3] == '-')
		{
			return true;
		}
		return false;
	}

	private static unsafe bool IsIdnAce(char* input, int index)
	{
		if (input[index] == 'x' && input[index + 1] == 'n' && input[index + 2] == '-' && input[index + 3] == '-')
		{
			return true;
		}
		return false;
	}

	internal static unsafe string UnicodeEquivalent(string idnHost, char* hostname, int start, int end)
	{
		try
		{
			return s_idnMapping.GetUnicode(idnHost);
		}
		catch (ArgumentException)
		{
		}
		bool allAscii = true;
		return UnicodeEquivalent(hostname, start, end, ref allAscii, ref allAscii);
	}

	internal static unsafe string UnicodeEquivalent(char* hostname, int start, int end, ref bool allAscii, ref bool atLeastOneValidIdn)
	{
		allAscii = true;
		atLeastOneValidIdn = false;
		string result = null;
		if (end <= start)
		{
			return result;
		}
		string text = UriHelper.StripBidiControlCharacter(hostname, start, end - start);
		string text2 = null;
		int num = 0;
		int num2 = 0;
		int length = text.Length;
		bool flag = true;
		bool flag2 = false;
		bool flag3 = false;
		bool flag4 = false;
		do
		{
			flag = true;
			flag2 = false;
			flag3 = false;
			flag4 = false;
			for (num2 = num; num2 < length; num2++)
			{
				char c = text[num2];
				if (!flag3)
				{
					flag3 = true;
					if (num2 + 3 < length && c == 'x' && IsIdnAce(text, num2))
					{
						flag2 = true;
					}
				}
				if (flag && c > '\u007f')
				{
					flag = false;
					allAscii = false;
				}
				if (c == '.' || c == '。' || c == '．' || c == '｡')
				{
					flag4 = true;
					break;
				}
			}
			if (!flag)
			{
				string unicode = text.Substring(num, num2 - num);
				try
				{
					unicode = s_idnMapping.GetAscii(unicode);
				}
				catch (ArgumentException)
				{
					throw new UriFormatException("SR.net_uri_BadUnicodeHostForIdn");
				}
				text2 += s_idnMapping.GetUnicode(unicode);
				if (flag4)
				{
					text2 += ".";
				}
			}
			else
			{
				bool flag5 = false;
				if (flag2)
				{
					try
					{
						text2 += s_idnMapping.GetUnicode(text, num, num2 - num);
						if (flag4)
						{
							text2 += ".";
						}
						flag5 = true;
						atLeastOneValidIdn = true;
					}
					catch (ArgumentException)
					{
					}
				}
				if (!flag5)
				{
					text2 += text.Substring(num, num2 - num).ToLowerInvariant();
					if (flag4)
					{
						text2 += ".";
					}
				}
			}
			num = num2 + (flag4 ? 1 : 0);
		}
		while (num < length);
		return text2;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsASCIILetterOrDigit(char character, ref bool notCanonical)
	{
		switch (character)
		{
		case '0':
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
		case 'a':
		case 'b':
		case 'c':
		case 'd':
		case 'e':
		case 'f':
		case 'g':
		case 'h':
		case 'i':
		case 'j':
		case 'k':
		case 'l':
		case 'm':
		case 'n':
		case 'o':
		case 'p':
		case 'q':
		case 'r':
		case 's':
		case 't':
		case 'u':
		case 'v':
		case 'w':
		case 'x':
		case 'y':
		case 'z':
			return true;
		case 'A':
		case 'B':
		case 'C':
		case 'D':
		case 'E':
		case 'F':
		case 'G':
		case 'H':
		case 'I':
		case 'J':
		case 'K':
		case 'L':
		case 'M':
		case 'N':
		case 'O':
		case 'P':
		case 'Q':
		case 'R':
		case 'S':
		case 'T':
		case 'U':
		case 'V':
		case 'W':
		case 'X':
		case 'Y':
		case 'Z':
			notCanonical = true;
			return true;
		default:
			return false;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsValidDomainLabelCharacter(char character, ref bool notCanonical)
	{
		switch (character)
		{
		case '-':
		case '0':
		case '1':
		case '2':
		case '3':
		case '4':
		case '5':
		case '6':
		case '7':
		case '8':
		case '9':
		case '_':
		case 'a':
		case 'b':
		case 'c':
		case 'd':
		case 'e':
		case 'f':
		case 'g':
		case 'h':
		case 'i':
		case 'j':
		case 'k':
		case 'l':
		case 'm':
		case 'n':
		case 'o':
		case 'p':
		case 'q':
		case 'r':
		case 's':
		case 't':
		case 'u':
		case 'v':
		case 'w':
		case 'x':
		case 'y':
		case 'z':
			return true;
		case 'A':
		case 'B':
		case 'C':
		case 'D':
		case 'E':
		case 'F':
		case 'G':
		case 'H':
		case 'I':
		case 'J':
		case 'K':
		case 'L':
		case 'M':
		case 'N':
		case 'O':
		case 'P':
		case 'Q':
		case 'R':
		case 'S':
		case 'T':
		case 'U':
		case 'V':
		case 'W':
		case 'X':
		case 'Y':
		case 'Z':
			notCanonical = true;
			return true;
		default:
			return false;
		}
	}

	internal static bool ContainsCharactersUnsafeForNormalizedHost(string host)
	{
		return host.IndexOfAny(s_UnsafeForNormalizedHost) != -1;
	}
}