#define DEBUG
using System.Buffers;
using System.Diagnostics;
using System.Text.Encodings.Web;

namespace System.Text.Json;

public readonly struct JsonEncodedText : IEquatable<JsonEncodedText>
{
	private readonly byte[] _utf8Value;

	private readonly string _value;

	public ReadOnlySpan<byte> EncodedUtf8Bytes => _utf8Value;

	private JsonEncodedText(byte[] utf8Value)
	{
		Debug.Assert(utf8Value != null);
		_value = JsonReaderHelper.GetTextFromUtf8(utf8Value);
		_utf8Value = utf8Value;
	}

	public static JsonEncodedText Encode(string value, JavaScriptEncoder encoder = null)
	{
		if (value == null)
		{
			throw new ArgumentNullException("value");
		}
		return Encode(value.AsSpan(), encoder);
	}

	public static JsonEncodedText Encode(ReadOnlySpan<char> value, JavaScriptEncoder encoder = null)
	{
		if (value.Length == 0)
		{
			return new JsonEncodedText(ArrayEx.Empty<byte>());
		}
		return TranscodeAndEncode(value, encoder);
	}

	private static JsonEncodedText TranscodeAndEncode(ReadOnlySpan<char> value, JavaScriptEncoder encoder)
	{
		JsonWriterHelper.ValidateValue(value);
		int expectedByteCount = JsonReaderHelper.GetUtf8ByteCount(value);
		byte[] utf8Bytes = ArrayPool<byte>.Shared.Rent(expectedByteCount);
		int actualByteCount = JsonReaderHelper.GetUtf8FromText(value, utf8Bytes);
		Debug.Assert(expectedByteCount == actualByteCount);
		JsonEncodedText encodedText = EncodeHelper(utf8Bytes.AsSpan(0, actualByteCount), encoder);
		utf8Bytes.AsSpan(0, expectedByteCount).Clear();
		ArrayPool<byte>.Shared.Return(utf8Bytes);
		return encodedText;
	}

	public static JsonEncodedText Encode(ReadOnlySpan<byte> utf8Value, JavaScriptEncoder encoder = null)
	{
		if (utf8Value.Length == 0)
		{
			return new JsonEncodedText(ArrayEx.Empty<byte>());
		}
		JsonWriterHelper.ValidateValue(utf8Value);
		return EncodeHelper(utf8Value, encoder);
	}

	private static JsonEncodedText EncodeHelper(ReadOnlySpan<byte> utf8Value, JavaScriptEncoder encoder)
	{
		int idx = JsonWriterHelper.NeedsEscaping(utf8Value, encoder);
		if (idx != -1)
		{
			return new JsonEncodedText(GetEscapedString(utf8Value, idx, encoder));
		}
		return new JsonEncodedText(utf8Value.ToArray());
	}

	private static byte[] GetEscapedString(ReadOnlySpan<byte> utf8Value, int firstEscapeIndexVal, JavaScriptEncoder encoder)
	{
		Debug.Assert(357913941 >= utf8Value.Length);
		Debug.Assert(firstEscapeIndexVal >= 0 && firstEscapeIndexVal < utf8Value.Length);
		byte[] valueArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(valueArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedValue = span;
		JsonWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, encoder, out var written);
		byte[] escapedString = escapedValue.Slice(0, written).ToArray();
		if (valueArray != null)
		{
			ArrayPool<byte>.Shared.Return(valueArray);
		}
		return escapedString;
	}

	public bool Equals(JsonEncodedText other)
	{
		if (_value == null)
		{
			return other._value == null;
		}
		return _value.Equals(other._value);
	}

	public override bool Equals(object obj)
	{
		if (obj is JsonEncodedText encodedText)
		{
			return Equals(encodedText);
		}
		return false;
	}

	public override string ToString()
	{
		return _value ?? string.Empty;
	}

	public override int GetHashCode()
	{
		return (_value != null) ? _value.GetHashCode() : 0;
	}
}
