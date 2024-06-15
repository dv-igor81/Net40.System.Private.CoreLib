#define DEBUG
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text.Json;

internal static class JsonReaderHelper
{
	private const ulong XorPowerOfTwoToHighByte = 283686952306184uL;

	public static readonly UTF8Encoding s_utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

	public static (int, int) CountNewLines(ReadOnlySpan<byte> data)
	{
		int lastLineFeedIndex = -1;
		int newLines = 0;
		for (int i = 0; i < data.Length; i++)
		{
			if (data[i] == 10)
			{
				lastLineFeedIndex = i;
				newLines++;
			}
		}
		return (newLines, lastLineFeedIndex);
	}

	internal static JsonValueKind ToValueKind(this JsonTokenType tokenType)
	{
		switch (tokenType)
		{
		case JsonTokenType.None:
			return JsonValueKind.Undefined;
		case JsonTokenType.StartArray:
			return JsonValueKind.Array;
		case JsonTokenType.StartObject:
			return JsonValueKind.Object;
		case JsonTokenType.String:
		case JsonTokenType.Number:
		case JsonTokenType.True:
		case JsonTokenType.False:
		case JsonTokenType.Null:
			return (JsonValueKind)(tokenType - 4);
		default:
			Debug.Fail($"No mapping for token type {tokenType}");
			return JsonValueKind.Undefined;
		}
	}

	public static bool IsTokenTypePrimitive(JsonTokenType tokenType)
	{
		return (int)(tokenType - 7) <= 4;
	}

	public static bool IsHexDigit(byte nextByte)
	{
		return (uint)(nextByte - 48) <= 9u || (uint)(nextByte - 65) <= 5u || (uint)(nextByte - 97) <= 5u;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int IndexOfQuoteOrAnyControlOrBackSlash(this ReadOnlySpan<byte> span)
	{
		return IndexOfOrLessThan(ref MemoryMarshal.GetReference(span), 34, 92, 32, span.Length);
	}

	private unsafe static int IndexOfOrLessThan(ref byte searchSpace, byte value0, byte value1, byte lessThan, int length)
	{
		Debug.Assert(length >= 0);
		IntPtr index = (IntPtr)0;
		IntPtr nLength = (IntPtr)length;
		if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
		{
			int unaligned = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
			nLength = (IntPtr)((Vector<byte>.Count - unaligned) & (Vector<byte>.Count - 1));
		}
		while (true)
		{
			if ((nuint)(void*)nLength >= (nuint)8u)
			{
				nLength -= 8;
				uint lookUp = Unsafe.AddByteOffset(ref searchSpace, index);
				if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
				{
					goto IL_0434;
				}
				lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 1);
				if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
				{
					goto IL_0440;
				}
				lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 2);
				if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
				{
					goto IL_0452;
				}
				lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 3);
				if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
				{
					goto IL_0464;
				}
				lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 4);
				if (value0 != lookUp && value1 != lookUp && lessThan <= lookUp)
				{
					lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 5);
					if (value0 != lookUp && value1 != lookUp && lessThan <= lookUp)
					{
						lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 6);
						if (value0 != lookUp && value1 != lookUp && lessThan <= lookUp)
						{
							lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 7);
							if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
							{
								break;
							}
							index += 8;
							continue;
						}
						return (int)(void*)(index + 6);
					}
					return (int)(void*)(index + 5);
				}
				return (int)(void*)(index + 4);
			}
			if ((nuint)(void*)nLength >= (nuint)4u)
			{
				nLength -= 4;
				uint lookUp = Unsafe.AddByteOffset(ref searchSpace, index);
				if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
				{
					goto IL_0434;
				}
				lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 1);
				if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
				{
					goto IL_0440;
				}
				lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 2);
				if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
				{
					goto IL_0452;
				}
				lookUp = Unsafe.AddByteOffset(ref searchSpace, index + 3);
				if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
				{
					goto IL_0464;
				}
				index += 4;
			}
			while ((void*)nLength != null)
			{
				nLength -= 1;
				uint lookUp = Unsafe.AddByteOffset(ref searchSpace, index);
				if (value0 == lookUp || value1 == lookUp || lessThan > lookUp)
				{
					goto IL_0434;
				}
				index += 1;
			}
			if (Vector.IsHardwareAccelerated && (int)(void*)index < length)
			{
				nLength = (IntPtr)((length - (int)(void*)index) & ~(Vector<byte>.Count - 1));
				Vector<byte> values0 = new Vector<byte>(value0);
				Vector<byte> values1 = new Vector<byte>(value1);
				Vector<byte> valuesLessThan = new Vector<byte>(lessThan);
				for (; (void*)nLength > (void*)index; index += Vector<byte>.Count)
				{
					Vector<byte> vData = Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref searchSpace, index));
					Vector<byte> vMatches = Vector.BitwiseOr(Vector.BitwiseOr(Vector.Equals(vData, values0), Vector.Equals(vData, values1)), Vector.LessThan(vData, valuesLessThan));
					if (!Vector<byte>.Zero.Equals(vMatches))
					{
						return (int)(void*)index + LocateFirstFoundByte(vMatches);
					}
				}
				if ((int)(void*)index < length)
				{
					nLength = (IntPtr)(length - (int)(void*)index);
					continue;
				}
			}
			return -1;
			IL_0434:
			return (int)(void*)index;
			IL_0440:
			return (int)(void*)(index + 1);
			IL_0464:
			return (int)(void*)(index + 3);
			IL_0452:
			return (int)(void*)(index + 2);
		}
		return (int)(void*)(index + 7);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static int LocateFirstFoundByte(Vector<byte> match)
	{
		Vector<ulong> vector64 = Vector.AsVectorUInt64(match);
		ulong candidate = 0uL;
		int i;
		for (i = 0; i < Vector<ulong>.Count; i++)
		{
			candidate = vector64[i];
			if (candidate != 0)
			{
				break;
			}
		}
		return i * 8 + LocateFirstFoundByte(candidate);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static int LocateFirstFoundByte(ulong match)
	{
		ulong powerOfTwoFlag = match ^ (match - 1);
		return (int)(powerOfTwoFlag * 283686952306184L >> 57);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool IsValidDateTimeOffsetParseLength(int length)
	{
		return JsonHelpers.IsInRangeInclusive(length, 10, 252);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool IsValidDateTimeOffsetParseLength(long length)
	{
		return JsonHelpers.IsInRangeInclusive(length, 10L, 252L);
	}

	public static bool TryGetEscapedDateTime(ReadOnlySpan<byte> source, out DateTime value)
	{
		int backslash = source.IndexOf<byte>(92);
		Debug.Assert(backslash != -1);
		Debug.Assert(source.Length <= 252);
		Span<byte> sourceUnescaped = stackalloc byte[source.Length];
		Unescape(source, sourceUnescaped, backslash, out var written);
		Debug.Assert(written > 0);
		sourceUnescaped = sourceUnescaped.Slice(0, written);
		Debug.Assert(!sourceUnescaped.IsEmpty);
		if (sourceUnescaped.Length <= 42 && JsonHelpers.TryParseAsISO((ReadOnlySpan<byte>)sourceUnescaped, out DateTime tmp))
		{
			value = tmp;
			return true;
		}
		value = default(DateTime);
		return false;
	}

	public static bool TryGetEscapedDateTimeOffset(ReadOnlySpan<byte> source, out DateTimeOffset value)
	{
		int backslash = source.IndexOf<byte>(92);
		Debug.Assert(backslash != -1);
		Debug.Assert(source.Length <= 252);
		Span<byte> sourceUnescaped = stackalloc byte[source.Length];
		Unescape(source, sourceUnescaped, backslash, out var written);
		Debug.Assert(written > 0);
		sourceUnescaped = sourceUnescaped.Slice(0, written);
		Debug.Assert(!sourceUnescaped.IsEmpty);
		if (sourceUnescaped.Length <= 42 && JsonHelpers.TryParseAsISO((ReadOnlySpan<byte>)sourceUnescaped, out DateTimeOffset tmp))
		{
			value = tmp;
			return true;
		}
		value = default(DateTimeOffset);
		return false;
	}

	public static bool TryGetEscapedGuid(ReadOnlySpan<byte> source, out Guid value)
	{
		Debug.Assert(source.Length <= 216);
		int idx = source.IndexOf<byte>(92);
		Debug.Assert(idx != -1);
		Span<byte> utf8Unescaped = stackalloc byte[source.Length];
		Unescape(source, utf8Unescaped, idx, out var written);
		Debug.Assert(written > 0);
		utf8Unescaped = utf8Unescaped.Slice(0, written);
		Debug.Assert(!utf8Unescaped.IsEmpty);
		if (utf8Unescaped.Length == 36 && Utf8Parser.TryParse((ReadOnlySpan<byte>)utf8Unescaped, out Guid tmp, out int _, 'D'))
		{
			value = tmp;
			return true;
		}
		value = default(Guid);
		return false;
	}

	public static bool TryGetUnescapedBase64Bytes(ReadOnlySpan<byte> utf8Source, int idx, out byte[] bytes)
	{
		byte[] unescapedArray = null;
		Span<byte> span = ((utf8Source.Length > 256) ? ((Span<byte>)(unescapedArray = ArrayPool<byte>.Shared.Rent(utf8Source.Length))) : stackalloc byte[utf8Source.Length]);
		Span<byte> utf8Unescaped = span;
		Unescape(utf8Source, utf8Unescaped, idx, out var written);
		Debug.Assert(written > 0);
		utf8Unescaped = utf8Unescaped.Slice(0, written);
		Debug.Assert(!utf8Unescaped.IsEmpty);
		bool result = TryDecodeBase64InPlace(utf8Unescaped, out bytes);
		if (unescapedArray != null)
		{
			utf8Unescaped.Clear();
			ArrayPool<byte>.Shared.Return(unescapedArray);
		}
		return result;
	}

	public static string GetUnescapedString(ReadOnlySpan<byte> utf8Source, int idx)
	{
		byte[] unescapedArray = null;
		Span<byte> span = ((utf8Source.Length > 256) ? ((Span<byte>)(unescapedArray = ArrayPool<byte>.Shared.Rent(utf8Source.Length))) : stackalloc byte[utf8Source.Length]);
		Span<byte> utf8Unescaped = span;
		Unescape(utf8Source, utf8Unescaped, idx, out var written);
		Debug.Assert(written > 0);
		utf8Unescaped = utf8Unescaped.Slice(0, written);
		Debug.Assert(!utf8Unescaped.IsEmpty);
		string utf8String = TranscodeHelper(utf8Unescaped);
		if (unescapedArray != null)
		{
			utf8Unescaped.Clear();
			ArrayPool<byte>.Shared.Return(unescapedArray);
		}
		return utf8String;
	}

	public static bool UnescapeAndCompare(ReadOnlySpan<byte> utf8Source, ReadOnlySpan<byte> other)
	{
		Debug.Assert(utf8Source.Length >= other.Length && utf8Source.Length / 6 <= other.Length);
		byte[] unescapedArray = null;
		Span<byte> span = ((utf8Source.Length > 256) ? ((Span<byte>)(unescapedArray = ArrayPool<byte>.Shared.Rent(utf8Source.Length))) : stackalloc byte[utf8Source.Length]);
		Span<byte> utf8Unescaped = span;
		Unescape(utf8Source, utf8Unescaped, 0, out var written);
		Debug.Assert(written > 0);
		utf8Unescaped = utf8Unescaped.Slice(0, written);
		Debug.Assert(!utf8Unescaped.IsEmpty);
		bool result = other.SequenceEqual(utf8Unescaped);
		if (unescapedArray != null)
		{
			utf8Unescaped.Clear();
			ArrayPool<byte>.Shared.Return(unescapedArray);
		}
		return result;
	}

	public static bool UnescapeAndCompare(ReadOnlySequence<byte> utf8Source, ReadOnlySpan<byte> other)
	{
		Debug.Assert(!utf8Source.IsSingleSegment);
		Debug.Assert(utf8Source.Length >= other.Length && utf8Source.Length / 6 <= other.Length);
		byte[] escapedArray = null;
		byte[] unescapedArray = null;
		int length = checked((int)utf8Source.Length);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(unescapedArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> utf8Unescaped = span;
		Span<byte> span2 = ((length > 256) ? ((Span<byte>)(escapedArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> utf8Escaped = span2;
		utf8Source.CopyTo(utf8Escaped);
		utf8Escaped = utf8Escaped.Slice(0, length);
		Unescape(utf8Escaped, utf8Unescaped, 0, out var written);
		Debug.Assert(written > 0);
		utf8Unescaped = utf8Unescaped.Slice(0, written);
		Debug.Assert(!utf8Unescaped.IsEmpty);
		bool result = other.SequenceEqual(utf8Unescaped);
		if (unescapedArray != null)
		{
			Debug.Assert(escapedArray != null);
			utf8Unescaped.Clear();
			ArrayPool<byte>.Shared.Return(unescapedArray);
			utf8Escaped.Clear();
			ArrayPool<byte>.Shared.Return(escapedArray);
		}
		return result;
	}

	public static bool TryDecodeBase64InPlace(Span<byte> utf8Unescaped, out byte[] bytes)
	{
		if (Base64.DecodeFromUtf8InPlace(utf8Unescaped, out var bytesWritten) != 0)
		{
			bytes = null;
			return false;
		}
		bytes = utf8Unescaped.Slice(0, bytesWritten).ToArray();
		return true;
	}

	public static bool TryDecodeBase64(ReadOnlySpan<byte> utf8Unescaped, out byte[] bytes)
	{
		byte[] pooledArray = null;
		Span<byte> span = ((utf8Unescaped.Length > 256) ? ((Span<byte>)(pooledArray = ArrayPool<byte>.Shared.Rent(utf8Unescaped.Length))) : stackalloc byte[utf8Unescaped.Length]);
		Span<byte> byteSpan = span;
		if (Base64.DecodeFromUtf8(utf8Unescaped, byteSpan, out var bytesConsumed, out var bytesWritten) != 0)
		{
			bytes = null;
			if (pooledArray != null)
			{
				byteSpan.Clear();
				ArrayPool<byte>.Shared.Return(pooledArray);
			}
			return false;
		}
		Debug.Assert(bytesConsumed == utf8Unescaped.Length);
		bytes = byteSpan.Slice(0, bytesWritten).ToArray();
		if (pooledArray != null)
		{
			byteSpan.Clear();
			ArrayPool<byte>.Shared.Return(pooledArray);
		}
		return true;
	}

	public static string TranscodeHelper(ReadOnlySpan<byte> utf8Unescaped)
	{
		try
		{
			return s_utf8Encoding.GetString(utf8Unescaped.ToArray());
		}
		catch (DecoderFallbackException ex)
		{
			throw ThrowHelper.GetInvalidOperationException_ReadInvalidUTF8(ex);
		}
	}

	internal unsafe static int GetUtf8ByteCount(ReadOnlySpan<char> text)
	{
		try
		{
			if (text.IsEmpty)
			{
				return 0;
			}
			fixed (char* charPtr = text)
			{
				return s_utf8Encoding.GetByteCount(charPtr, text.Length);
			}
		}
		catch (EncoderFallbackException ex)
		{
			throw ThrowHelper.GetArgumentException_ReadInvalidUTF16(ex);
		}
	}

	internal unsafe static int GetUtf8FromText(ReadOnlySpan<char> text, Span<byte> dest)
	{
		try
		{
			if (text.IsEmpty)
			{
				return 0;
			}
			fixed (char* charPtr = text)
			{
				fixed (byte* destPtr = dest)
				{
					return s_utf8Encoding.GetBytes(charPtr, text.Length, destPtr, dest.Length);
				}
			}
		}
		catch (EncoderFallbackException ex)
		{
			throw ThrowHelper.GetArgumentException_ReadInvalidUTF16(ex);
		}
	}

	internal static string GetTextFromUtf8(ReadOnlySpan<byte> utf8Text)
	{
		return s_utf8Encoding.GetString(utf8Text.ToArray());
	}

	internal static void Unescape(ReadOnlySpan<byte> source, Span<byte> destination, int idx, out int written)
	{
		Debug.Assert(idx >= 0 && idx < source.Length);
		Debug.Assert(source[idx] == 92);
		Debug.Assert(destination.Length >= source.Length);
		source.Slice(0, idx).CopyTo(destination);
		written = idx;
		while (idx < source.Length)
		{
			byte currentByte = source[idx];
			if (currentByte == 92)
			{
				idx++;
				switch (source[idx])
				{
				case 34:
					destination[written++] = 34;
					break;
				case 110:
					destination[written++] = 10;
					break;
				case 114:
					destination[written++] = 13;
					break;
				case 92:
					destination[written++] = 92;
					break;
				case 47:
					destination[written++] = 47;
					break;
				case 116:
					destination[written++] = 9;
					break;
				case 98:
					destination[written++] = 8;
					break;
				case 102:
					destination[written++] = 12;
					break;
				case 117:
				{
					Debug.Assert(source.Length >= idx + 5);
					bool result = Utf8Parser.TryParse(source.Slice(idx + 1, 4), out int scalar, out int bytesConsumed, 'x');
					Debug.Assert(result);
					Debug.Assert(bytesConsumed == 4);
					idx += bytesConsumed;
					if (JsonHelpers.IsInRangeInclusive((uint)scalar, 55296u, 57343u))
					{
						if (scalar >= 56320)
						{
							ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16(scalar);
						}
						Debug.Assert(JsonHelpers.IsInRangeInclusive((uint)scalar, 55296u, 56319u));
						idx += 3;
						if (source.Length < idx + 4 || source[idx - 2] != 92 || source[idx - 1] != 117)
						{
							ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16();
						}
						result = Utf8Parser.TryParse(source.Slice(idx, 4), out int lowSurrogate, out bytesConsumed, 'x');
						Debug.Assert(result);
						Debug.Assert(bytesConsumed == 4);
						if (!JsonHelpers.IsInRangeInclusive((uint)lowSurrogate, 56320u, 57343u))
						{
							ThrowHelper.ThrowInvalidOperationException_ReadInvalidUTF16(lowSurrogate);
						}
						idx += bytesConsumed - 1;
						scalar = 1024 * (scalar - 55296) + (lowSurrogate - 56320) + 65536;
					}
					EncodeToUtf8Bytes((uint)scalar, destination.Slice(written), out var bytesWritten);
					Debug.Assert(bytesWritten <= 4);
					written += bytesWritten;
					break;
				}
				}
			}
			else
			{
				destination[written++] = currentByte;
			}
			idx++;
		}
	}

	private static void EncodeToUtf8Bytes(uint scalar, Span<byte> utf8Destination, out int bytesWritten)
	{
		Debug.Assert(JsonHelpers.IsValidUnicodeScalar(scalar));
		Debug.Assert(utf8Destination.Length >= 4);
		if (scalar < 128)
		{
			utf8Destination[0] = (byte)scalar;
			bytesWritten = 1;
		}
		else if (scalar < 2048)
		{
			utf8Destination[0] = (byte)(0xC0u | (scalar >> 6));
			utf8Destination[1] = (byte)(0x80u | (scalar & 0x3Fu));
			bytesWritten = 2;
		}
		else if (scalar < 65536)
		{
			utf8Destination[0] = (byte)(0xE0u | (scalar >> 12));
			utf8Destination[1] = (byte)(0x80u | ((scalar >> 6) & 0x3Fu));
			utf8Destination[2] = (byte)(0x80u | (scalar & 0x3Fu));
			bytesWritten = 3;
		}
		else
		{
			utf8Destination[0] = (byte)(0xF0u | (scalar >> 18));
			utf8Destination[1] = (byte)(0x80u | ((scalar >> 12) & 0x3Fu));
			utf8Destination[2] = (byte)(0x80u | ((scalar >> 6) & 0x3Fu));
			utf8Destination[3] = (byte)(0x80u | (scalar & 0x3Fu));
			bytesWritten = 4;
		}
	}
}
