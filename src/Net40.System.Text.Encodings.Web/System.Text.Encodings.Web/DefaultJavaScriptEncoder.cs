#define DEBUG
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Internal;
using System.Text.Unicode;

namespace System.Text.Encodings.Web;

internal sealed class DefaultJavaScriptEncoder : JavaScriptEncoder
{
	private AllowedCharactersBitmap _allowedCharacters;

	internal static readonly DefaultJavaScriptEncoder Singleton = new DefaultJavaScriptEncoder(new TextEncoderSettings(UnicodeRanges.BasicLatin));

	private static readonly char[] s_b = new char[2] { '\\', 'b' };

	private static readonly char[] s_t = new char[2] { '\\', 't' };

	private static readonly char[] s_n = new char[2] { '\\', 'n' };

	private static readonly char[] s_f = new char[2] { '\\', 'f' };

	private static readonly char[] s_r = new char[2] { '\\', 'r' };

	private static readonly char[] s_back = new char[2] { '\\', '\\' };

	public override int MaxOutputCharactersPerInputCharacter => 12;

	public DefaultJavaScriptEncoder(TextEncoderSettings filter)
	{
		if (filter == null)
		{
			throw new ArgumentNullException("filter");
		}
		_allowedCharacters = filter.GetAllowedCharacters();
		_allowedCharacters.ForbidUndefinedCharacters();
		DefaultHtmlEncoder.ForbidHtmlCharacters(_allowedCharacters);
		_allowedCharacters.ForbidCharacter('\\');
		_allowedCharacters.ForbidCharacter('`');
	}

	public DefaultJavaScriptEncoder(params UnicodeRange[] allowedRanges)
		: this(new TextEncoderSettings(allowedRanges))
	{
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public override bool WillEncode(int unicodeScalar)
	{
		if (UnicodeHelpers.IsSupplementaryCodePoint(unicodeScalar))
		{
			return true;
		}
		return !_allowedCharacters.IsUnicodeScalarAllowed(unicodeScalar);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public unsafe override int FindFirstCharacterToEncode(char* text, int textLength)
	{
		if (text == null)
		{
			throw new ArgumentNullException("text");
		}
		return _allowedCharacters.FindFirstCharacterToEncode(text, textLength);
	}

	public unsafe override bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
	{
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer");
		}
		if (!WillEncode(unicodeScalar))
		{
			return TextEncoder.TryWriteScalarAsChar(unicodeScalar, buffer, bufferLength, out numberOfCharactersWritten);
		}
		char[] toCopy;
		switch (unicodeScalar)
		{
		case 8:
			toCopy = s_b;
			break;
		case 9:
			toCopy = s_t;
			break;
		case 10:
			toCopy = s_n;
			break;
		case 12:
			toCopy = s_f;
			break;
		case 13:
			toCopy = s_r;
			break;
		case 92:
			toCopy = s_back;
			break;
		default:
			return TryWriteEncodedScalarAsNumericEntity(unicodeScalar, buffer, bufferLength, out numberOfCharactersWritten);
		}
		return TextEncoder.TryCopyCharacters(toCopy, buffer, bufferLength, out numberOfCharactersWritten);
	}

	private unsafe static bool TryWriteEncodedScalarAsNumericEntity(int unicodeScalar, char* buffer, int length, out int numberOfCharactersWritten)
	{
		Debug.Assert(buffer != null && length >= 0);
		if (UnicodeHelpers.IsSupplementaryCodePoint(unicodeScalar))
		{
			UnicodeHelpers.GetUtf16SurrogatePairFromAstralScalarValue(unicodeScalar, out var leadingSurrogate, out var trailingSurrogate);
			if (TryWriteEncodedSingleCharacter(leadingSurrogate, buffer, length, out var leadingSurrogateCharactersWritten) && TryWriteEncodedSingleCharacter(trailingSurrogate, buffer + leadingSurrogateCharactersWritten, length - leadingSurrogateCharactersWritten, out numberOfCharactersWritten))
			{
				numberOfCharactersWritten += leadingSurrogateCharactersWritten;
				return true;
			}
			numberOfCharactersWritten = 0;
			return false;
		}
		return TryWriteEncodedSingleCharacter(unicodeScalar, buffer, length, out numberOfCharactersWritten);
	}

	private unsafe static bool TryWriteEncodedSingleCharacter(int unicodeScalar, char* buffer, int length, out int numberOfCharactersWritten)
	{
		Debug.Assert(buffer != null && length >= 0);
		Debug.Assert(!UnicodeHelpers.IsSupplementaryCodePoint(unicodeScalar), "The incoming value should've been in the BMP.");
		if (length < 6)
		{
			numberOfCharactersWritten = 0;
			return false;
		}
		*buffer = '\\';
		buffer++;
		*buffer = 'u';
		buffer++;
		*buffer = HexUtil.Int32LsbToHexDigit(unicodeScalar >> 12);
		buffer++;
		*buffer = HexUtil.Int32LsbToHexDigit((int)((long)(unicodeScalar >> 8) & 0xFL));
		buffer++;
		*buffer = HexUtil.Int32LsbToHexDigit((int)((long)(unicodeScalar >> 4) & 0xFL));
		buffer++;
		*buffer = HexUtil.Int32LsbToHexDigit((int)((long)unicodeScalar & 0xFL));
		buffer++;
		numberOfCharactersWritten = 6;
		return true;
	}
}
