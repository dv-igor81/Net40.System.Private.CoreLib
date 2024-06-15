#define DEBUG
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Internal;
using System.Text.Unicode;

namespace System.Text.Encodings.Web;

internal sealed class DefaultHtmlEncoder : HtmlEncoder
{
	private AllowedCharactersBitmap _allowedCharacters;

	internal static readonly DefaultHtmlEncoder Singleton = new DefaultHtmlEncoder(new TextEncoderSettings(UnicodeRanges.BasicLatin));

	private static readonly char[] s_quote = "&quot;".ToCharArray();

	private static readonly char[] s_ampersand = "&amp;".ToCharArray();

	private static readonly char[] s_lessthan = "&lt;".ToCharArray();

	private static readonly char[] s_greaterthan = "&gt;".ToCharArray();

	public override int MaxOutputCharactersPerInputCharacter => 10;

	public DefaultHtmlEncoder(TextEncoderSettings settings)
	{
		if (settings == null)
		{
			throw new ArgumentNullException("settings");
		}
		_allowedCharacters = settings.GetAllowedCharacters();
		_allowedCharacters.ForbidUndefinedCharacters();
		ForbidHtmlCharacters(_allowedCharacters);
	}

	internal static void ForbidHtmlCharacters(AllowedCharactersBitmap allowedCharacters)
	{
		allowedCharacters.ForbidCharacter('<');
		allowedCharacters.ForbidCharacter('>');
		allowedCharacters.ForbidCharacter('&');
		allowedCharacters.ForbidCharacter('\'');
		allowedCharacters.ForbidCharacter('"');
		allowedCharacters.ForbidCharacter('+');
	}

	public DefaultHtmlEncoder(params UnicodeRange[] allowedRanges)
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
		return unicodeScalar switch
		{
			34 => TextEncoder.TryCopyCharacters(s_quote, buffer, bufferLength, out numberOfCharactersWritten), 
			38 => TextEncoder.TryCopyCharacters(s_ampersand, buffer, bufferLength, out numberOfCharactersWritten), 
			60 => TextEncoder.TryCopyCharacters(s_lessthan, buffer, bufferLength, out numberOfCharactersWritten), 
			62 => TextEncoder.TryCopyCharacters(s_greaterthan, buffer, bufferLength, out numberOfCharactersWritten), 
			_ => TryWriteEncodedScalarAsNumericEntity(unicodeScalar, buffer, bufferLength, out numberOfCharactersWritten), 
		};
	}

	private unsafe static bool TryWriteEncodedScalarAsNumericEntity(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
	{
		Debug.Assert(buffer != null && bufferLength >= 0);
		int numberOfHexCharacters = 0;
		int compareUnicodeScalar = unicodeScalar;
		do
		{
			Debug.Assert(numberOfHexCharacters < 8, "Couldn't have written 8 characters out by this point.");
			numberOfHexCharacters++;
			compareUnicodeScalar >>= 4;
		}
		while (compareUnicodeScalar != 0);
		numberOfCharactersWritten = numberOfHexCharacters + 4;
		Debug.Assert(numberOfHexCharacters > 0, "At least one character should've been written.");
		if (numberOfHexCharacters + 4 > bufferLength)
		{
			numberOfCharactersWritten = 0;
			return false;
		}
		*buffer = '&';
		buffer++;
		*buffer = '#';
		buffer++;
		*buffer = 'x';
		buffer += numberOfHexCharacters;
		do
		{
			*buffer = HexUtil.Int32LsbToHexDigit(unicodeScalar & 0xF);
			unicodeScalar >>= 4;
			buffer--;
		}
		while (unicodeScalar != 0);
		buffer += numberOfHexCharacters + 1;
		*buffer = ';';
		return true;
	}
}
