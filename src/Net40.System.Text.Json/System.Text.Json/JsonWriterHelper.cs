#define DEBUG
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Encodings.Web;

namespace System.Text.Json;

internal static class JsonWriterHelper
{
	public const int LastAsciiCharacter = 127;

	private static readonly StandardFormat s_hexStandardFormat = new StandardFormat('X', 4);

	private static ReadOnlySpan<byte> AllowList => new byte[256]
	{
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 1, 1, 0, 1, 1, 1, 0, 0,
		1, 1, 1, 0, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
		0, 1, 0, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 0, 1, 1, 1, 0, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
		1, 1, 1, 1, 1, 1, 1, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0
	};

	public static void WriteIndentation(Span<byte> buffer, int indent)
	{
		Debug.Assert(indent % 2 == 0);
		Debug.Assert(buffer.Length >= indent);
		if (indent < 8)
		{
			int i = 0;
			while (i < indent)
			{
				buffer[i++] = 32;
				buffer[i++] = 32;
			}
		}
		else
		{
			buffer.Slice(0, indent).Fill(32);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidateProperty(ReadOnlySpan<byte> propertyName)
	{
		if (propertyName.Length > 166666666)
		{
			ThrowHelper.ThrowArgumentException_PropertyNameTooLarge(propertyName.Length);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidateValue(ReadOnlySpan<byte> value)
	{
		if (value.Length > 166666666)
		{
			ThrowHelper.ThrowArgumentException_ValueTooLarge(value.Length);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidateBytes(ReadOnlySpan<byte> bytes)
	{
		if (bytes.Length > 125000000)
		{
			ThrowHelper.ThrowArgumentException_ValueTooLarge(bytes.Length);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidateDouble(double value)
	{
		if (double.IsNaN(value) || double.IsInfinity(value))
		{
			ThrowHelper.ThrowArgumentException_ValueNotSupported();
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidateSingle(float value)
	{
		if (float.IsNaN(value) || float.IsInfinity(value))
		{
			ThrowHelper.ThrowArgumentException_ValueNotSupported();
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidateProperty(ReadOnlySpan<char> propertyName)
	{
		if (propertyName.Length > 166666666)
		{
			ThrowHelper.ThrowArgumentException_PropertyNameTooLarge(propertyName.Length);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidateValue(ReadOnlySpan<char> value)
	{
		if (value.Length > 166666666)
		{
			ThrowHelper.ThrowArgumentException_ValueTooLarge(value.Length);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidatePropertyAndValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
	{
		if (propertyName.Length > 166666666 || value.Length > 166666666)
		{
			ThrowHelper.ThrowArgumentException(propertyName, value);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidatePropertyAndValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<char> value)
	{
		if (propertyName.Length > 166666666 || value.Length > 166666666)
		{
			ThrowHelper.ThrowArgumentException(propertyName, value);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidatePropertyAndValue(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value)
	{
		if (propertyName.Length > 166666666 || value.Length > 166666666)
		{
			ThrowHelper.ThrowArgumentException(propertyName, value);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidatePropertyAndValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
	{
		if (propertyName.Length > 166666666 || value.Length > 166666666)
		{
			ThrowHelper.ThrowArgumentException(propertyName, value);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidatePropertyAndBytes(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
	{
		if (propertyName.Length > 166666666 || bytes.Length > 125000000)
		{
			ThrowHelper.ThrowArgumentException(propertyName, bytes);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void ValidatePropertyAndBytes(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> bytes)
	{
		if (propertyName.Length > 166666666 || bytes.Length > 125000000)
		{
			ThrowHelper.ThrowArgumentException(propertyName, bytes);
		}
	}

	internal static void ValidateNumber(ReadOnlySpan<byte> utf8FormattedNumber)
	{
		Debug.Assert(!utf8FormattedNumber.IsEmpty);
		int i = 0;
		if (utf8FormattedNumber[i] == 45)
		{
			i++;
			if (utf8FormattedNumber.Length <= i)
			{
				throw new ArgumentException("SR.RequiredDigitNotFoundEndOfData", "utf8FormattedNumber");
			}
		}
		if (utf8FormattedNumber[i] == 48)
		{
			i++;
		}
		else
		{
			for (; i < utf8FormattedNumber.Length && JsonHelpers.IsDigit(utf8FormattedNumber[i]); i++)
			{
			}
		}
		if (i == utf8FormattedNumber.Length)
		{
			return;
		}
		byte val = utf8FormattedNumber[i];
		if (val == 46)
		{
			i++;
			if (utf8FormattedNumber.Length <= i)
			{
				throw new ArgumentException("SR.RequiredDigitNotFoundEndOfData", "utf8FormattedNumber");
			}
			for (; i < utf8FormattedNumber.Length && JsonHelpers.IsDigit(utf8FormattedNumber[i]); i++)
			{
			}
			if (i == utf8FormattedNumber.Length)
			{
				return;
			}
			Debug.Assert(i < utf8FormattedNumber.Length);
			val = utf8FormattedNumber[i];
		}
		if (val == 101 || val == 69)
		{
			i++;
			if (utf8FormattedNumber.Length <= i)
			{
				throw new ArgumentException("SR.RequiredDigitNotFoundEndOfData", "utf8FormattedNumber");
			}
			val = utf8FormattedNumber[i];
			if (val == 43 || val == 45)
			{
				i++;
			}
			if (utf8FormattedNumber.Length <= i)
			{
				throw new ArgumentException("SR.RequiredDigitNotFoundEndOfData", "utf8FormattedNumber");
			}
			for (; i < utf8FormattedNumber.Length && JsonHelpers.IsDigit(utf8FormattedNumber[i]); i++)
			{
			}
			if (i == utf8FormattedNumber.Length)
			{
				return;
			}
			throw new ArgumentException("SR.Format(SR.ExpectedEndOfDigitNotFound, ThrowHelper.GetPrintableString(utf8FormattedNumber[i]))", "utf8FormattedNumber");
		}
		throw new ArgumentException("SR.Format(SR.ExpectedEndOfDigitNotFound, ThrowHelper.GetPrintableString(val))", "utf8FormattedNumber");
	}

	public static void TrimDateTimeOffset(Span<byte> buffer, out int bytesWritten)
	{
		Debug.Assert(buffer.Length == 27 || buffer.Length == 28 || buffer.Length == 33);
		uint digit7 = (uint)(buffer[26] - 48);
		uint digit6 = (uint)(buffer[25] - 48);
		uint digit5 = (uint)(buffer[24] - 48);
		uint digit4 = (uint)(buffer[23] - 48);
		uint digit3 = (uint)(buffer[22] - 48);
		uint digit2 = (uint)(buffer[21] - 48);
		uint digit1 = (uint)(buffer[20] - 48);
		uint fraction = digit1 * 1000000 + digit2 * 100000 + digit3 * 10000 + digit4 * 1000 + digit5 * 100 + digit6 * 10 + digit7;
		int curIndex = 19;
		if (fraction != 0)
		{
			int numFractionDigits = 7;
			while (true)
			{
				uint remainder;
				uint quotient = DivMod(fraction, 10u, out remainder);
				if (remainder != 0)
				{
					break;
				}
				fraction = quotient;
				numFractionDigits--;
			}
			int fractionEnd = 19 + numFractionDigits;
			for (int i = fractionEnd; i > curIndex; i--)
			{
				buffer[i] = (byte)(fraction % 10 + 48);
				fraction /= 10;
			}
			curIndex = fractionEnd + 1;
		}
		bytesWritten = curIndex;
		if (buffer.Length > 27)
		{
			buffer[curIndex] = buffer[27];
			bytesWritten = curIndex + 1;
			if (buffer.Length == 33)
			{
				int bufferEnd = curIndex + 5;
				byte offsetMinDigit1 = buffer[31];
				byte offsetHourDigit2 = buffer[29];
				byte offsetHourDigit1 = buffer[28];
				Debug.Assert(buffer[30] == 58);
				buffer[bufferEnd] = buffer[32];
				buffer[bufferEnd - 1] = offsetMinDigit1;
				buffer[bufferEnd - 2] = 58;
				buffer[bufferEnd - 3] = offsetHourDigit2;
				buffer[bufferEnd - 4] = offsetHourDigit1;
				bytesWritten = bufferEnd + 1;
			}
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint DivMod(uint numerator, uint denominator, out uint modulo)
	{
		uint div = numerator / denominator;
		modulo = numerator - div * denominator;
		return div;
	}

	private static bool NeedsEscaping(byte value)
	{
		return AllowList[value] == 0;
	}

	private static bool NeedsEscapingNoBoundsCheck(char value)
	{
		return AllowList[value] == 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool NeedsEscaping(char value)
	{
		return value > '\u007f' || AllowList[value] == 0;
	}

	public static int NeedsEscaping(ReadOnlySpan<byte> value, JavaScriptEncoder encoder)
	{
		int idx;
		if (encoder != null)
		{
			idx = encoder.FindFirstCharacterToEncodeUtf8(value);
		}
		else
		{
			idx = 0;
			while (true)
			{
				if (idx < value.Length)
				{
					if (NeedsEscaping(value[idx]))
					{
						break;
					}
					idx++;
					continue;
				}
				idx = -1;
				break;
			}
		}
		return idx;
	}

	public static unsafe int NeedsEscaping(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
	{
		int idx;
		if (encoder != null && !value.IsEmpty)
		{
			fixed (char* ptr = value)
			{
				idx = encoder.FindFirstCharacterToEncode(ptr, value.Length);
			}
		}
		else
		{
			idx = 0;
			while (true)
			{
				if (idx < value.Length)
				{
					if (NeedsEscaping(value[idx]))
					{
						break;
					}
					idx++;
					continue;
				}
				idx = -1;
				break;
			}
		}
		return idx;
	}

	public static int GetMaxEscapedLength(int textLength, int firstIndexToEscape)
	{
		Debug.Assert(textLength > 0);
		Debug.Assert(firstIndexToEscape >= 0 && firstIndexToEscape < textLength);
		return firstIndexToEscape + 6 * (textLength - firstIndexToEscape);
	}

	private static void EscapeString(ReadOnlySpan<byte> value, Span<byte> destination, JavaScriptEncoder encoder, ref int written)
	{
		Debug.Assert(encoder != null);
		int encoderBytesConsumed;
		int encoderBytesWritten;
		OperationStatus result = encoder.EncodeUtf8(value, destination, out encoderBytesConsumed, out encoderBytesWritten);
		Debug.Assert(result != OperationStatus.DestinationTooSmall);
		Debug.Assert(result != OperationStatus.NeedMoreData);
		if (result != 0)
		{
			ThrowHelper.ThrowArgumentException_InvalidUTF8(value.Slice(encoderBytesWritten));
		}
		Debug.Assert(encoderBytesConsumed == value.Length);
		written += encoderBytesWritten;
	}

	public static void EscapeString(ReadOnlySpan<byte> value, Span<byte> destination, int indexOfFirstByteToEscape, JavaScriptEncoder encoder, out int written)
	{
		Debug.Assert(indexOfFirstByteToEscape >= 0 && indexOfFirstByteToEscape < value.Length);
		value.Slice(0, indexOfFirstByteToEscape).CopyTo(destination);
		written = indexOfFirstByteToEscape;
		if (encoder != null)
		{
			destination = destination.Slice(indexOfFirstByteToEscape);
			value = value.Slice(indexOfFirstByteToEscape);
			EscapeString(value, destination, encoder, ref written);
			return;
		}
		while (indexOfFirstByteToEscape < value.Length)
		{
			byte val = value[indexOfFirstByteToEscape];
			if (IsAsciiValue(val))
			{
				if (NeedsEscaping(val))
				{
					EscapeNextBytes(val, destination, ref written);
					indexOfFirstByteToEscape++;
				}
				else
				{
					destination[written] = val;
					written++;
					indexOfFirstByteToEscape++;
				}
				continue;
			}
			destination = destination.Slice(written);
			value = value.Slice(indexOfFirstByteToEscape);
			EscapeString(value, destination, JavaScriptEncoder.Default, ref written);
			break;
		}
	}

	private static void EscapeNextBytes(byte value, Span<byte> destination, ref int written)
	{
		destination[written++] = 92;
		switch (value)
		{
		case 34:
			destination[written++] = 117;
			destination[written++] = 48;
			destination[written++] = 48;
			destination[written++] = 50;
			destination[written++] = 50;
			break;
		case 10:
			destination[written++] = 110;
			break;
		case 13:
			destination[written++] = 114;
			break;
		case 9:
			destination[written++] = 116;
			break;
		case 92:
			destination[written++] = 92;
			break;
		case 8:
			destination[written++] = 98;
			break;
		case 12:
			destination[written++] = 102;
			break;
		default:
		{
			destination[written++] = 117;
			int bytesWritten;
			bool result = Utf8Formatter.TryFormat(value, destination.Slice(written), out bytesWritten, s_hexStandardFormat);
			Debug.Assert(result);
			Debug.Assert(bytesWritten == 4);
			written += bytesWritten;
			break;
		}
		}
	}

	private static bool IsAsciiValue(byte value)
	{
		return value <= 127;
	}

	private static bool IsAsciiValue(char value)
	{
		return value <= '\u007f';
	}

	private static void EscapeString(ReadOnlySpan<char> value, Span<char> destination, JavaScriptEncoder encoder, ref int written)
	{
		Debug.Assert(encoder != null);
		int encoderBytesConsumed;
		int encoderCharsWritten;
		OperationStatus result = encoder.Encode(value, destination, out encoderBytesConsumed, out encoderCharsWritten);
		Debug.Assert(result != OperationStatus.DestinationTooSmall);
		Debug.Assert(result != OperationStatus.NeedMoreData);
		if (result != 0)
		{
			ThrowHelper.ThrowArgumentException_InvalidUTF16(value[encoderCharsWritten]);
		}
		Debug.Assert(encoderBytesConsumed == value.Length);
		written += encoderCharsWritten;
	}

	public static void EscapeString(ReadOnlySpan<char> value, Span<char> destination, int indexOfFirstByteToEscape, JavaScriptEncoder encoder, out int written)
	{
		Debug.Assert(indexOfFirstByteToEscape >= 0 && indexOfFirstByteToEscape < value.Length);
		value.Slice(0, indexOfFirstByteToEscape).CopyTo(destination);
		written = indexOfFirstByteToEscape;
		if (encoder != null)
		{
			destination = destination.Slice(indexOfFirstByteToEscape);
			value = value.Slice(indexOfFirstByteToEscape);
			EscapeString(value, destination, encoder, ref written);
			return;
		}
		while (indexOfFirstByteToEscape < value.Length)
		{
			char val = value[indexOfFirstByteToEscape];
			if (IsAsciiValue(val))
			{
				if (NeedsEscapingNoBoundsCheck(val))
				{
					EscapeNextChars(val, destination, ref written);
					indexOfFirstByteToEscape++;
				}
				else
				{
					destination[written] = val;
					written++;
					indexOfFirstByteToEscape++;
				}
				continue;
			}
			destination = destination.Slice(written);
			value = value.Slice(indexOfFirstByteToEscape);
			EscapeString(value, destination, JavaScriptEncoder.Default, ref written);
			break;
		}
	}

	private static void EscapeNextChars(char value, Span<char> destination, ref int written)
	{
		Debug.Assert(IsAsciiValue(value));
		destination[written++] = '\\';
		switch ((byte)value)
		{
		case 34:
			destination[written++] = 'u';
			destination[written++] = '0';
			destination[written++] = '0';
			destination[written++] = '2';
			destination[written++] = '2';
			break;
		case 10:
			destination[written++] = 'n';
			break;
		case 13:
			destination[written++] = 'r';
			break;
		case 9:
			destination[written++] = 't';
			break;
		case 92:
			destination[written++] = '\\';
			break;
		case 8:
			destination[written++] = 'b';
			break;
		case 12:
			destination[written++] = 'f';
			break;
		default:
			destination[written++] = 'u';
			written = WriteHex(value, destination, written);
			break;
		}
	}

	private static int WriteHex(int value, Span<char> destination, int written)
	{
		destination[written++] = (char)Int32LsbToHexDigit(value >> 12);
		destination[written++] = (char)Int32LsbToHexDigit((int)((long)(value >> 8) & 0xFL));
		destination[written++] = (char)Int32LsbToHexDigit((int)((long)(value >> 4) & 0xFL));
		destination[written++] = (char)Int32LsbToHexDigit((int)((long)value & 0xFL));
		return written;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static byte Int32LsbToHexDigit(int value)
	{
		Debug.Assert(value < 16);
		return (byte)((value < 10) ? (48 + value) : (65 + (value - 10)));
	}

	public static unsafe OperationStatus ToUtf8(ReadOnlySpan<byte> utf16Source, Span<byte> utf8Destination, out int bytesConsumed, out int bytesWritten)
	{
		fixed (byte* chars = &MemoryMarshal.GetReference(utf16Source))
		{
			fixed (byte* bytes = &MemoryMarshal.GetReference(utf8Destination))
			{
				char* pSrc = (char*)chars;
				byte* pTarget = bytes;
				char* pEnd = (char*)(chars + utf16Source.Length);
				byte* pAllocatedBufferEnd = pTarget + utf8Destination.Length;
				while (true)
				{
					if (pEnd - pSrc > 13)
					{
						int available = Math.Min(PtrDiff(pEnd, pSrc), PtrDiff(pAllocatedBufferEnd, pTarget));
						char* pStop = pSrc + available - 5;
						if (pSrc < pStop)
						{
							while (true)
							{
								int ch2 = *pSrc;
								pSrc++;
								if (ch2 > 127)
								{
									goto IL_0172;
								}
								*pTarget = (byte)ch2;
								pTarget++;
								if (((uint)(int)pSrc & 2u) != 0)
								{
									ch2 = *pSrc;
									pSrc++;
									if (ch2 > 127)
									{
										goto IL_0172;
									}
									*pTarget = (byte)ch2;
									pTarget++;
								}
								while (pSrc < pStop)
								{
									ch2 = *(int*)pSrc;
									int chc = *(int*)(pSrc + 2);
									if (((uint)(ch2 | chc) & 0xFF80FF80u) != 0)
									{
										goto IL_0146;
									}
									*pTarget = (byte)ch2;
									pTarget[1] = (byte)(ch2 >> 16);
									pSrc += 4;
									pTarget[2] = (byte)chc;
									pTarget[3] = (byte)(chc >> 16);
									pTarget += 4;
								}
								goto IL_026f;
								IL_0146:
								ch2 = (ushort)ch2;
								pSrc++;
								if (ch2 > 127)
								{
									goto IL_0172;
								}
								*pTarget = (byte)ch2;
								pTarget++;
								goto IL_026f;
								IL_0172:
								int chd2;
								if (ch2 <= 2047)
								{
									chd2 = -64 | (ch2 >> 6);
								}
								else
								{
									if (!JsonHelpers.IsInRangeInclusive(ch2, 55296, 57343))
									{
										chd2 = -32 | (ch2 >> 12);
									}
									else
									{
										if (ch2 > 56319)
										{
											break;
										}
										chd2 = *pSrc;
										if (!JsonHelpers.IsInRangeInclusive(chd2, 56320, 57343))
										{
											break;
										}
										pSrc++;
										ch2 = chd2 + (ch2 << 10) + -56613888;
										*pTarget = (byte)(0xFFFFFFF0u | (uint)(ch2 >> 18));
										pTarget++;
										chd2 = -128 | ((ch2 >> 12) & 0x3F);
									}
									*pTarget = (byte)chd2;
									pStop--;
									pTarget++;
									chd2 = -128 | ((ch2 >> 6) & 0x3F);
								}
								*pTarget = (byte)chd2;
								pStop--;
								pTarget[1] = (byte)(0xFFFFFF80u | ((uint)ch2 & 0x3Fu));
								pTarget += 2;
								goto IL_026f;
								IL_026f:
								if (pSrc < pStop)
								{
									continue;
								}
								goto IL_027e;
							}
							break;
						}
					}
					while (true)
					{
						int ch;
						int chd;
						if (pSrc < pEnd)
						{
							ch = *pSrc;
							pSrc++;
							if (ch <= 127)
							{
								if (pAllocatedBufferEnd - pTarget > 0)
								{
									*pTarget = (byte)ch;
									pTarget++;
									continue;
								}
							}
							else if (ch <= 2047)
							{
								if (pAllocatedBufferEnd - pTarget > 1)
								{
									chd = -64 | (ch >> 6);
									goto IL_042f;
								}
							}
							else if (!JsonHelpers.IsInRangeInclusive(ch, 55296, 57343))
							{
								if (pAllocatedBufferEnd - pTarget > 2)
								{
									chd = -32 | (ch >> 12);
									goto IL_0416;
								}
							}
							else if (pAllocatedBufferEnd - pTarget > 3)
							{
								if (ch > 56319)
								{
									break;
								}
								if (pSrc < pEnd)
								{
									chd = *pSrc;
									if (!JsonHelpers.IsInRangeInclusive(chd, 56320, 57343))
									{
										break;
									}
									pSrc++;
									ch = chd + (ch << 10) + -56613888;
									*pTarget = (byte)(0xFFFFFFF0u | (uint)(ch >> 18));
									pTarget++;
									chd = -128 | ((ch >> 12) & 0x3F);
									goto IL_0416;
								}
								bytesConsumed = (int)((byte*)(pSrc - 1) - chars);
								bytesWritten = (int)(pTarget - bytes);
								return OperationStatus.NeedMoreData;
							}
							bytesConsumed = (int)((byte*)(pSrc - 1) - chars);
							bytesWritten = (int)(pTarget - bytes);
							return OperationStatus.DestinationTooSmall;
						}
						bytesConsumed = (int)((byte*)pSrc - chars);
						bytesWritten = (int)(pTarget - bytes);
						return OperationStatus.Done;
						IL_042f:
						*pTarget = (byte)chd;
						pTarget[1] = (byte)(0xFFFFFF80u | ((uint)ch & 0x3Fu));
						pTarget += 2;
						continue;
						IL_0416:
						*pTarget = (byte)chd;
						pTarget++;
						chd = -128 | ((ch >> 6) & 0x3F);
						goto IL_042f;
					}
					break;
					IL_027e:
					Debug.Assert(pTarget <= pAllocatedBufferEnd, "[UTF8Encoding.GetBytes]pTarget <= pAllocatedBufferEnd");
				}
				bytesConsumed = (int)((byte*)(pSrc - 1) - chars);
				bytesWritten = (int)(pTarget - bytes);
				return OperationStatus.InvalidData;
			}
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static unsafe int PtrDiff(char* a, char* b)
	{
		return (int)((uint)((byte*)a - (byte*)b) >> 1);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static unsafe int PtrDiff(byte* a, byte* b)
	{
		return (int)(a - b);
	}
}
