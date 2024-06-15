#define DEBUG
using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Unicode;

namespace System.Text.Encodings.Web;

public abstract class TextEncoder
{
	private byte[][] _asciiEscape = new byte[128][];

	private static readonly byte[] s_noEscape = ArrayEx.Empty<byte>();

	[EditorBrowsable(EditorBrowsableState.Never)]
	public abstract int MaxOutputCharactersPerInputCharacter { get; }

	[CLSCompliant(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public unsafe abstract bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten);

	[CLSCompliant(false)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public unsafe abstract int FindFirstCharacterToEncode(char* text, int textLength);

	[EditorBrowsable(EditorBrowsableState.Never)]
	public abstract bool WillEncode(int unicodeScalar);

	public unsafe virtual string Encode(string value)
	{
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		fixed (char* valuePointer = value)
		{
			int firstCharacterToEncode = FindFirstCharacterToEncode(valuePointer, value.Length);
			if (firstCharacterToEncode == -1)
			{
				return value;
			}
			int bufferSize = MaxOutputCharactersPerInputCharacter * value.Length;
			int charsConsumed;
			string result;
			if (bufferSize < 1024)
			{
				char* wholebuffer = stackalloc char[bufferSize];
				if (EncodeIntoBuffer(wholebuffer, bufferSize, valuePointer, value.Length, out charsConsumed, out var totalWritten, firstCharacterToEncode) != 0)
				{
					ThrowArgumentException_MaxOutputCharsPerInputChar();
				}
				result = new string(wholebuffer, 0, totalWritten);
			}
			else
			{
				char[] wholebuffer2 = new char[bufferSize];
				fixed (char* buffer = &wholebuffer2[0])
				{
					if (EncodeIntoBuffer(buffer, bufferSize, valuePointer, value.Length, out charsConsumed, out var totalWritten2, firstCharacterToEncode) != 0)
					{
						ThrowArgumentException_MaxOutputCharsPerInputChar();
					}
					result = new string(wholebuffer2, 0, totalWritten2);
				}
			}
			return result;
		}
	}

	private unsafe OperationStatus EncodeIntoBuffer(char* buffer, int bufferLength, char* value, int valueLength, out int charsConsumed, out int charsWritten, int firstCharacterToEncode, bool isFinalBlock = true)
	{
		Debug.Assert(value != null);
		Debug.Assert(firstCharacterToEncode >= 0);
		char* originalBuffer = buffer;
		charsWritten = 0;
		if (firstCharacterToEncode > 0)
		{
			Debug.Assert(firstCharacterToEncode <= valueLength);
			BufferEx.MemoryCopy(value, buffer, 2 * bufferLength, 2 * firstCharacterToEncode);
			charsWritten += firstCharacterToEncode;
			bufferLength -= firstCharacterToEncode;
			buffer += firstCharacterToEncode;
		}
		char firstChar = value[firstCharacterToEncode];
		char secondChar = firstChar;
		bool wasSurrogatePair = false;
		int secondCharIndex;
		for (secondCharIndex = firstCharacterToEncode + 1; secondCharIndex < valueLength; secondCharIndex++)
		{
			firstChar = (wasSurrogatePair ? value[secondCharIndex - 1] : secondChar);
			secondChar = value[secondCharIndex];
			if (!WillEncode(firstChar))
			{
				wasSurrogatePair = false;
				*buffer = firstChar;
				buffer++;
				bufferLength--;
				charsWritten++;
				continue;
			}
			bool needsMoreData;
			int nextScalar2 = UnicodeHelpers.GetScalarValueFromUtf16(firstChar, secondChar, out wasSurrogatePair, out needsMoreData);
			if (!TryEncodeUnicodeScalar(nextScalar2, buffer, bufferLength, out var charsWrittenThisTime2))
			{
				charsConsumed = (int)(originalBuffer - buffer);
				return OperationStatus.DestinationTooSmall;
			}
			if (wasSurrogatePair)
			{
				secondCharIndex++;
			}
			buffer += charsWrittenThisTime2;
			bufferLength -= charsWrittenThisTime2;
			charsWritten += charsWrittenThisTime2;
		}
		if (secondCharIndex == valueLength)
		{
			firstChar = value[valueLength - 1];
			bool needMoreData;
			int nextScalar = UnicodeHelpers.GetScalarValueFromUtf16(firstChar, null, out wasSurrogatePair, out needMoreData);
			if (!isFinalBlock && needMoreData)
			{
				Debug.Assert(!wasSurrogatePair);
				charsConsumed = (int)(buffer - originalBuffer);
				return OperationStatus.NeedMoreData;
			}
			if (!TryEncodeUnicodeScalar(nextScalar, buffer, bufferLength, out var charsWrittenThisTime))
			{
				charsConsumed = (int)(buffer - originalBuffer);
				return OperationStatus.DestinationTooSmall;
			}
			buffer += charsWrittenThisTime;
			bufferLength -= charsWrittenThisTime;
			charsWritten += charsWrittenThisTime;
		}
		charsConsumed = valueLength;
		return OperationStatus.Done;
	}

	public void Encode(TextWriter output, string value)
	{
		Encode(output, value, 0, value.Length);
	}

	public unsafe virtual void Encode(TextWriter output, string value, int startIndex, int characterCount)
	{
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		if (output == null)
		{
			throw new ArgumentNullException("output");
		}
		ValidateRanges(startIndex, characterCount, value.Length);
		fixed (char* valuePointer = value)
		{
			char* substring = valuePointer + startIndex;
			int firstIndexToEncode = FindFirstCharacterToEncode(substring, characterCount);
			if (firstIndexToEncode == -1)
			{
				if (startIndex == 0 && characterCount == value.Length)
				{
					output.Write(value);
					return;
				}
				for (int i = 0; i < characterCount; i++)
				{
					output.Write(*substring);
					substring++;
				}
				return;
			}
			for (int j = 0; j < firstIndexToEncode; j++)
			{
				output.Write(*substring);
				substring++;
			}
			EncodeCore(output, substring, characterCount - firstIndexToEncode);
		}
	}

	public unsafe virtual void Encode(TextWriter output, char[] value, int startIndex, int characterCount)
	{
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		if (output == null)
		{
			throw new ArgumentNullException("output");
		}
		ValidateRanges(startIndex, characterCount, value.Length);
		fixed (char* valuePointer = value)
		{
			char* substring = valuePointer + startIndex;
			int firstIndexToEncode = FindFirstCharacterToEncode(substring, characterCount);
			if (firstIndexToEncode == -1)
			{
				if (startIndex == 0 && characterCount == value.Length)
				{
					output.Write(value);
					return;
				}
				for (int i = 0; i < characterCount; i++)
				{
					output.Write(*substring);
					substring++;
				}
				return;
			}
			for (int j = 0; j < firstIndexToEncode; j++)
			{
				output.Write(*substring);
				substring++;
			}
			EncodeCore(output, substring, characterCount - firstIndexToEncode);
		}
	}

	public unsafe virtual OperationStatus EncodeUtf8(ReadOnlySpan<byte> utf8Source, Span<byte> utf8Destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock = true)
	{
		int originalUtf8SourceLength = utf8Source.Length;
		int originalUtf8DestinationLength = utf8Destination.Length;
		char* pTempCharBuffer = stackalloc char[24];
		byte* pTempUtf8Buffer = stackalloc byte[72];
		int utf8BytesConsumedForScalar = 0;
		int nonEscapedByteCount = 0;
		OperationStatus opStatus = OperationStatus.Done;
		while (true)
		{
			if (!utf8Source.IsEmpty)
			{
				uint nextScalarValue;
				do
				{
					nextScalarValue = utf8Source[nonEscapedByteCount];
					if (UnicodeUtility.IsAsciiCodePoint(nextScalarValue))
					{
						byte[] encodedBytes = GetAsciiEncoding((byte)nextScalarValue);
						if (encodedBytes == s_noEscape)
						{
							if (++nonEscapedByteCount > utf8Destination.Length)
							{
								nonEscapedByteCount--;
								opStatus = OperationStatus.DestinationTooSmall;
								break;
							}
							continue;
						}
						if (encodedBytes == null)
						{
							opStatus = OperationStatus.Done;
							utf8BytesConsumedForScalar = 1;
							break;
						}
						if (nonEscapedByteCount > 0)
						{
							Debug.Assert(nonEscapedByteCount <= utf8Destination.Length);
							utf8Source.Slice(0, nonEscapedByteCount).CopyTo(utf8Destination);
							utf8Source = utf8Source.Slice(nonEscapedByteCount);
							utf8Destination = utf8Destination.Slice(nonEscapedByteCount);
							nonEscapedByteCount = 0;
						}
						if (!((ReadOnlySpan<byte>)encodedBytes).TryCopyTo(utf8Destination))
						{
							opStatus = OperationStatus.DestinationTooSmall;
							break;
						}
						utf8Destination = utf8Destination.Slice(encodedBytes.Length);
						utf8Source = utf8Source.Slice(1);
					}
					else
					{
						opStatus = UnicodeHelpers.DecodeScalarValueFromUtf8(utf8Source.Slice(nonEscapedByteCount), out nextScalarValue, out utf8BytesConsumedForScalar);
						if (opStatus != 0 || WillEncode((int)nextScalarValue))
						{
							break;
						}
						nonEscapedByteCount += utf8BytesConsumedForScalar;
						if (nonEscapedByteCount > utf8Destination.Length)
						{
							nonEscapedByteCount -= utf8BytesConsumedForScalar;
							opStatus = OperationStatus.DestinationTooSmall;
							break;
						}
					}
				}
				while (nonEscapedByteCount < utf8Source.Length);
				if (nonEscapedByteCount > 0)
				{
					Debug.Assert(nonEscapedByteCount <= utf8Destination.Length);
					utf8Source.Slice(0, nonEscapedByteCount).CopyTo(utf8Destination);
					utf8Source = utf8Source.Slice(nonEscapedByteCount);
					utf8Destination = utf8Destination.Slice(nonEscapedByteCount);
					nonEscapedByteCount = 0;
				}
				if (!utf8Source.IsEmpty)
				{
					if (opStatus != 0)
					{
						if (opStatus == OperationStatus.NeedMoreData)
						{
							if (!isFinalBlock)
							{
								bytesConsumed = originalUtf8SourceLength - utf8Source.Length;
								bytesWritten = originalUtf8DestinationLength - utf8Destination.Length;
								return OperationStatus.NeedMoreData;
							}
						}
						else if (opStatus == OperationStatus.DestinationTooSmall)
						{
							break;
						}
					}
					if (TryEncodeUnicodeScalar((int)nextScalarValue, pTempCharBuffer, 24, out var charsWrittenJustNow))
					{
						int transcodedByteCountThisIteration = Encoding.UTF8.GetBytes(pTempCharBuffer, charsWrittenJustNow, pTempUtf8Buffer, 72);
						ReadOnlySpan<byte> transcodedUtf8BytesThisIteration = new ReadOnlySpan<byte>(pTempUtf8Buffer, transcodedByteCountThisIteration);
						if (UnicodeUtility.IsAsciiCodePoint(nextScalarValue))
						{
							_asciiEscape[nextScalarValue] = transcodedUtf8BytesThisIteration.ToArray();
						}
						if (!transcodedUtf8BytesThisIteration.TryCopyTo(utf8Destination))
						{
							break;
						}
						utf8Destination = utf8Destination.Slice(transcodedByteCountThisIteration);
						utf8Source = utf8Source.Slice(utf8BytesConsumedForScalar);
						continue;
					}
					bytesConsumed = originalUtf8SourceLength - utf8Source.Length;
					bytesWritten = originalUtf8DestinationLength - utf8Destination.Length;
					return OperationStatus.InvalidData;
				}
			}
			bytesConsumed = originalUtf8SourceLength;
			bytesWritten = originalUtf8DestinationLength - utf8Destination.Length;
			return OperationStatus.Done;
		}
		bytesConsumed = originalUtf8SourceLength - utf8Source.Length;
		bytesWritten = originalUtf8DestinationLength - utf8Destination.Length;
		return OperationStatus.DestinationTooSmall;
	}

	internal static OperationStatus EncodeUtf8Shim(TextEncoder encoder, ReadOnlySpan<byte> utf8Source, Span<byte> utf8Destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock)
	{
		return encoder.EncodeUtf8(utf8Source, utf8Destination, out bytesConsumed, out bytesWritten, isFinalBlock);
	}

	public unsafe virtual OperationStatus Encode(ReadOnlySpan<char> source, Span<char> destination, out int charsConsumed, out int charsWritten, bool isFinalBlock = true)
	{
		fixed (char* sourcePtr = source)
		{
			int firstCharacterToEncode;
			if (source.IsEmpty || (firstCharacterToEncode = FindFirstCharacterToEncode(sourcePtr, source.Length)) == -1)
			{
				if (source.TryCopyTo(destination))
				{
					charsConsumed = source.Length;
					charsWritten = source.Length;
					return OperationStatus.Done;
				}
				charsConsumed = 0;
				charsWritten = 0;
				return OperationStatus.DestinationTooSmall;
			}
			if (destination.IsEmpty)
			{
				charsConsumed = 0;
				charsWritten = 0;
				return OperationStatus.DestinationTooSmall;
			}
			fixed (char* destinationPtr = destination)
			{
				return EncodeIntoBuffer(destinationPtr, destination.Length, sourcePtr, source.Length, out charsConsumed, out charsWritten, firstCharacterToEncode, isFinalBlock);
			}
		}
	}

	private unsafe void EncodeCore(TextWriter output, char* value, int valueLength)
	{
		Debug.Assert(value != null && output != null);
		Debug.Assert(valueLength >= 0);
		int bufferLength = MaxOutputCharactersPerInputCharacter;
		char* buffer = stackalloc char[bufferLength];
		char firstChar = *value;
		char secondChar = firstChar;
		bool wasSurrogatePair = false;
		int secondCharIndex;
		bool needsMoreData;
		int charsWritten;
		for (secondCharIndex = 1; secondCharIndex < valueLength; secondCharIndex++)
		{
			firstChar = (wasSurrogatePair ? value[secondCharIndex - 1] : secondChar);
			secondChar = value[secondCharIndex];
			if (!WillEncode(firstChar))
			{
				wasSurrogatePair = false;
				output.Write(firstChar);
				continue;
			}
			int nextScalar2 = UnicodeHelpers.GetScalarValueFromUtf16(firstChar, secondChar, out wasSurrogatePair, out needsMoreData);
			if (!TryEncodeUnicodeScalar(nextScalar2, buffer, bufferLength, out charsWritten))
			{
				ThrowArgumentException_MaxOutputCharsPerInputChar();
			}
			Write(output, buffer, charsWritten);
			if (wasSurrogatePair)
			{
				secondCharIndex++;
			}
		}
		if (!wasSurrogatePair || secondCharIndex == valueLength)
		{
			firstChar = value[valueLength - 1];
			int nextScalar = UnicodeHelpers.GetScalarValueFromUtf16(firstChar, null, out wasSurrogatePair, out needsMoreData);
			if (!TryEncodeUnicodeScalar(nextScalar, buffer, bufferLength, out charsWritten))
			{
				ThrowArgumentException_MaxOutputCharsPerInputChar();
			}
			Write(output, buffer, charsWritten);
		}
	}

	private unsafe int FindFirstCharacterToEncode(ReadOnlySpan<char> text)
	{
		fixed (char* pText = &MemoryMarshal.GetReference(text))
		{
			return FindFirstCharacterToEncode(pText, text.Length);
		}
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	public virtual int FindFirstCharacterToEncodeUtf8(ReadOnlySpan<byte> utf8Text)
	{
		int originalUtf8TextLength = utf8Text.Length;
		int i = 0;
		while (i < utf8Text.Length)
		{
			byte value = utf8Text[i];
			if (UnicodeUtility.IsAsciiCodePoint(value))
			{
				if (GetAsciiEncoding(value) != s_noEscape)
				{
					return originalUtf8TextLength - utf8Text.Length + i;
				}
				i++;
				continue;
			}
			if (i > 0)
			{
				utf8Text = utf8Text.Slice(i);
			}
			if (UnicodeHelpers.DecodeScalarValueFromUtf8(utf8Text, out var nextScalarValue, out var bytesConsumedThisIteration) != 0 || WillEncode((int)nextScalarValue))
			{
				return originalUtf8TextLength - utf8Text.Length;
			}
			i = bytesConsumedThisIteration;
		}
		return -1;
	}

	internal static int FindFirstCharacterToEncodeUtf8Shim(TextEncoder encoder, ReadOnlySpan<byte> utf8Text)
	{
		return encoder.FindFirstCharacterToEncodeUtf8(utf8Text);
	}

	internal unsafe static bool TryCopyCharacters(char[] source, char* destination, int destinationLength, out int numberOfCharactersWritten)
	{
		Debug.Assert(source != null && destination != null && destinationLength >= 0);
		if (destinationLength < source.Length)
		{
			numberOfCharactersWritten = 0;
			return false;
		}
		for (int i = 0; i < source.Length; i++)
		{
			destination[i] = source[i];
		}
		numberOfCharactersWritten = source.Length;
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal unsafe static bool TryWriteScalarAsChar(int unicodeScalar, char* destination, int destinationLength, out int numberOfCharactersWritten)
	{
		Debug.Assert(destination != null && destinationLength >= 0);
		Debug.Assert(unicodeScalar < 65535);
		if (destinationLength < 1)
		{
			numberOfCharactersWritten = 0;
			return false;
		}
		*destination = (char)unicodeScalar;
		numberOfCharactersWritten = 1;
		return true;
	}

	private static void ValidateRanges(int startIndex, int characterCount, int actualInputLength)
	{
		if (startIndex < 0 || startIndex > actualInputLength)
		{
			throw new ArgumentOutOfRangeException("startIndex");
		}
		if (characterCount < 0 || characterCount > actualInputLength - startIndex)
		{
			throw new ArgumentOutOfRangeException("characterCount");
		}
	}

	private unsafe static void Write(TextWriter output, char* input, int inputLength)
	{
		Debug.Assert(output != null && input != null && inputLength >= 0);
		while (inputLength-- > 0)
		{
			output.Write(*input);
			input++;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private byte[] GetAsciiEncoding(byte value)
	{
		byte[] encoding = _asciiEscape[value];
		if (encoding == null && !WillEncode(value))
		{
			encoding = s_noEscape;
			_asciiEscape[value] = encoding;
		}
		return encoding;
	}

	private static void ThrowArgumentException_MaxOutputCharsPerInputChar()
	{
		throw new ArgumentException("Argument encoder does not implement MaxOutputCharsPerInputChar correctly.");
	}
}
