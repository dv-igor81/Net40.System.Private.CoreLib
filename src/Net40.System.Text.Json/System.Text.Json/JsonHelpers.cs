#define DEBUG
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Json;

internal static class JsonHelpers
{
	private struct DateTimeParseData
	{
		public int Year;

		public int Month;

		public int Day;

		public int Hour;

		public int Minute;

		public int Second;

		public int Fraction;

		public int OffsetHours;

		public int OffsetMinutes;

		public byte OffsetToken;

		public bool OffsetNegative => OffsetToken == 45;
	}

	private static readonly int[] s_daysToMonth365 = new int[13]
	{
		0, 31, 59, 90, 120, 151, 181, 212, 243, 273,
		304, 334, 365
	};

	private static readonly int[] s_daysToMonth366 = new int[13]
	{
		0, 31, 60, 91, 121, 152, 182, 213, 244, 274,
		305, 335, 366
	};

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool IsValidUnicodeScalar(uint value)
	{
		return IsInRangeInclusive(value ^ 0xD800u, 2048u, 1114111u);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool IsInRangeInclusive(uint value, uint lowerBound, uint upperBound)
	{
		return value - lowerBound <= upperBound - lowerBound;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool IsInRangeInclusive(int value, int lowerBound, int upperBound)
	{
		return (uint)(value - lowerBound) <= (uint)(upperBound - lowerBound);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool IsInRangeInclusive(long value, long lowerBound, long upperBound)
	{
		return (ulong)(value - lowerBound) <= (ulong)(upperBound - lowerBound);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool IsInRangeInclusive(JsonTokenType value, JsonTokenType lowerBound, JsonTokenType upperBound)
	{
		return value - lowerBound <= upperBound - lowerBound;
	}

	public static bool IsDigit(byte value)
	{
		return (uint)(value - 48) <= 9u;
	}

	internal static string Utf8GetString(ReadOnlySpan<byte> bytes)
	{
		return Encoding.UTF8.GetString(bytes.ToArray());
	}

	internal static bool TryAdd<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key, TValue value)
	{
		return dictionary.TryAdd(key, value);
	}

	public static bool TryParseAsISO(ReadOnlySpan<byte> source, out DateTime value)
	{
		if (!TryParseDateTimeOffset(source, out var parseData))
		{
			value = default(DateTime);
			return false;
		}
		if (parseData.OffsetToken == 90)
		{
			return TryCreateDateTime(parseData, DateTimeKind.Utc, out value);
		}
		if (parseData.OffsetToken == 43 || parseData.OffsetToken == 45)
		{
			if (!TryCreateDateTimeOffset(ref parseData, out var dateTimeOffset))
			{
				value = default(DateTime);
				return false;
			}
			value = dateTimeOffset.LocalDateTime;
			return true;
		}
		return TryCreateDateTime(parseData, DateTimeKind.Unspecified, out value);
	}

	public static bool TryParseAsISO(ReadOnlySpan<byte> source, out DateTimeOffset value)
	{
		if (!TryParseDateTimeOffset(source, out var parseData))
		{
			value = default(DateTimeOffset);
			return false;
		}
		if (parseData.OffsetToken == 90 || parseData.OffsetToken == 43 || parseData.OffsetToken == 45)
		{
			return TryCreateDateTimeOffset(ref parseData, out value);
		}
		return TryCreateDateTimeOffsetInterpretingDataAsLocalTime(parseData, out value);
	}

	private static bool TryParseDateTimeOffset(ReadOnlySpan<byte> source, out DateTimeParseData parseData)
	{
		if (source.Length < 10)
		{
			parseData = default(DateTimeParseData);
			return false;
		}
		parseData = default(DateTimeParseData);
		uint digit1 = (uint)(source[0] - 48);
		uint digit2 = (uint)(source[1] - 48);
		uint digit3 = (uint)(source[2] - 48);
		uint digit4 = (uint)(source[3] - 48);
		if (digit1 > 9 || digit2 > 9 || digit3 > 9 || digit4 > 9)
		{
			return false;
		}
		parseData.Year = (int)(digit1 * 1000 + digit2 * 100 + digit3 * 10 + digit4);
		if (source[4] != 45 || !TryGetNextTwoDigits(source.Slice(5, 2), ref parseData.Month) || source[7] != 45 || !TryGetNextTwoDigits(source.Slice(8, 2), ref parseData.Day))
		{
			return false;
		}
		Debug.Assert(source.Length >= 10);
		if (source.Length == 10)
		{
			return true;
		}
		if (source.Length < 16)
		{
			return false;
		}
		if (source[10] != 84 || source[13] != 58 || !TryGetNextTwoDigits(source.Slice(11, 2), ref parseData.Hour) || !TryGetNextTwoDigits(source.Slice(14, 2), ref parseData.Minute))
		{
			return false;
		}
		Debug.Assert(source.Length >= 16);
		if (source.Length == 16)
		{
			return true;
		}
		byte curByte = source[16];
		int sourceIndex = 17;
		switch (curByte)
		{
		case 90:
			parseData.OffsetToken = 90;
			return sourceIndex == source.Length;
		case 43:
		case 45:
			parseData.OffsetToken = curByte;
			return ParseOffset(ref parseData, source.Slice(sourceIndex));
		default:
			return false;
		case 58:
			if (source.Length < 19 || !TryGetNextTwoDigits(source.Slice(17, 2), ref parseData.Second))
			{
				return false;
			}
			Debug.Assert(source.Length >= 19);
			if (source.Length == 19)
			{
				return true;
			}
			curByte = source[19];
			sourceIndex = 20;
			switch (curByte)
			{
			case 90:
				parseData.OffsetToken = 90;
				return sourceIndex == source.Length;
			case 43:
			case 45:
				parseData.OffsetToken = curByte;
				return ParseOffset(ref parseData, source.Slice(sourceIndex));
			default:
				return false;
			case 46:
			{
				if (source.Length < 21)
				{
					return false;
				}
				int numDigitsRead = 0;
				for (int fractionEnd = Math.Min(sourceIndex + 16, source.Length); sourceIndex < fractionEnd; sourceIndex++)
				{
					if (!IsDigit(curByte = source[sourceIndex]))
					{
						break;
					}
					if (numDigitsRead < 7)
					{
						parseData.Fraction = parseData.Fraction * 10 + (curByte - 48);
						numDigitsRead++;
					}
				}
				if (parseData.Fraction != 0)
				{
					for (; numDigitsRead < 7; numDigitsRead++)
					{
						parseData.Fraction *= 10;
					}
				}
				Debug.Assert(sourceIndex <= source.Length);
				if (sourceIndex == source.Length)
				{
					return true;
				}
				curByte = source[sourceIndex++];
				switch (curByte)
				{
				case 90:
					parseData.OffsetToken = 90;
					return sourceIndex == source.Length;
				case 43:
				case 45:
					parseData.OffsetToken = curByte;
					return ParseOffset(ref parseData, source.Slice(sourceIndex));
				default:
					return false;
				}
			}
			}
		}
		static bool ParseOffset(ref DateTimeParseData parseData, ReadOnlySpan<byte> offsetData)
		{
			if (offsetData.Length < 2 || !TryGetNextTwoDigits(offsetData.Slice(0, 2), ref parseData.OffsetHours))
			{
				return false;
			}
			if (offsetData.Length == 2)
			{
				return true;
			}
			if (offsetData.Length != 5 || offsetData[2] != 58 || !TryGetNextTwoDigits(offsetData.Slice(3), ref parseData.OffsetMinutes))
			{
				return false;
			}
			return true;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool TryGetNextTwoDigits(ReadOnlySpan<byte> source, ref int value)
	{
		Debug.Assert(source.Length == 2);
		uint digit1 = (uint)(source[0] - 48);
		uint digit2 = (uint)(source[1] - 48);
		if (digit1 > 9 || digit2 > 9)
		{
			value = 0;
			return false;
		}
		value = (int)(digit1 * 10 + digit2);
		return true;
	}

	private static bool TryCreateDateTimeOffset(DateTime dateTime, ref DateTimeParseData parseData, out DateTimeOffset value)
	{
		if ((uint)parseData.OffsetHours > 14u)
		{
			value = default(DateTimeOffset);
			return false;
		}
		if ((uint)parseData.OffsetMinutes > 59u)
		{
			value = default(DateTimeOffset);
			return false;
		}
		if (parseData.OffsetHours == 14 && parseData.OffsetMinutes != 0)
		{
			value = default(DateTimeOffset);
			return false;
		}
		long offsetTicks = ((long)parseData.OffsetHours * 3600L + (long)parseData.OffsetMinutes * 60L) * 10000000;
		if (parseData.OffsetNegative)
		{
			offsetTicks = -offsetTicks;
		}
		try
		{
			value = new DateTimeOffset(dateTime.Ticks, new TimeSpan(offsetTicks));
		}
		catch (ArgumentOutOfRangeException)
		{
			value = default(DateTimeOffset);
			return false;
		}
		return true;
	}

	private static bool TryCreateDateTimeOffset(ref DateTimeParseData parseData, out DateTimeOffset value)
	{
		if (!TryCreateDateTime(parseData, DateTimeKind.Unspecified, out var dateTime))
		{
			value = default(DateTimeOffset);
			return false;
		}
		if (!TryCreateDateTimeOffset(dateTime, ref parseData, out value))
		{
			value = default(DateTimeOffset);
			return false;
		}
		return true;
	}

	private static bool TryCreateDateTimeOffsetInterpretingDataAsLocalTime(DateTimeParseData parseData, out DateTimeOffset value)
	{
		if (!TryCreateDateTime(parseData, DateTimeKind.Local, out var dateTime))
		{
			value = default(DateTimeOffset);
			return false;
		}
		try
		{
			value = new DateTimeOffset(dateTime);
		}
		catch (ArgumentOutOfRangeException)
		{
			value = default(DateTimeOffset);
			return false;
		}
		return true;
	}

	private static bool TryCreateDateTime(DateTimeParseData parseData, DateTimeKind kind, out DateTime value)
	{
		if (parseData.Year == 0)
		{
			value = default(DateTime);
			return false;
		}
		Debug.Assert(parseData.Year <= 9999);
		if ((uint)(parseData.Month - 1) >= 12u)
		{
			value = default(DateTime);
			return false;
		}
		uint dayMinusOne = (uint)(parseData.Day - 1);
		if (dayMinusOne >= 28 && dayMinusOne >= DateTime.DaysInMonth(parseData.Year, parseData.Month))
		{
			value = default(DateTime);
			return false;
		}
		if ((uint)parseData.Hour > 23u)
		{
			value = default(DateTime);
			return false;
		}
		if ((uint)parseData.Minute > 59u)
		{
			value = default(DateTime);
			return false;
		}
		if ((uint)parseData.Second > 59u)
		{
			value = default(DateTime);
			return false;
		}
		Debug.Assert(parseData.Fraction >= 0 && parseData.Fraction <= 9999999);
		int[] days = (DateTime.IsLeapYear(parseData.Year) ? s_daysToMonth366 : s_daysToMonth365);
		int yearMinusOne = parseData.Year - 1;
		int totalDays = yearMinusOne * 365 + yearMinusOne / 4 - yearMinusOne / 100 + yearMinusOne / 400 + days[parseData.Month - 1] + parseData.Day - 1;
		long ticks = totalDays * 864000000000L;
		int totalSeconds = parseData.Hour * 3600 + parseData.Minute * 60 + parseData.Second;
		ticks += (long)totalSeconds * 10000000L;
		ticks += parseData.Fraction;
		value = new DateTime(ticks, kind);
		return true;
	}
}
