using System.Runtime.CompilerServices;
using System.Text.Internal;
using System.Text.Unicode;

namespace System.Text.Encodings.Web;

internal sealed class DefaultUrlEncoder : UrlEncoder
{
	private AllowedCharactersBitmap _allowedCharacters;

	internal static readonly DefaultUrlEncoder Singleton = new DefaultUrlEncoder(new TextEncoderSettings(UnicodeRanges.BasicLatin));

	public override int MaxOutputCharactersPerInputCharacter => 12;

	public DefaultUrlEncoder(TextEncoderSettings filter)
	{
		if (filter == null)
		{
			throw new ArgumentNullException("filter");
		}
		_allowedCharacters = filter.GetAllowedCharacters();
		_allowedCharacters.ForbidUndefinedCharacters();
		DefaultHtmlEncoder.ForbidHtmlCharacters(_allowedCharacters);
		string text = " #%/:=?[\\]^`{|}";
		foreach (char character in text)
		{
			_allowedCharacters.ForbidCharacter(character);
		}
		for (int i = 0; i < 16; i++)
		{
			_allowedCharacters.ForbidCharacter((char)(0xFFF0u | (uint)i));
		}
	}

	public DefaultUrlEncoder(params UnicodeRange[] allowedRanges)
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
		numberOfCharactersWritten = 0;
		uint asUtf8 = (uint)UnicodeHelpers.GetUtf8RepresentationForScalarValue((uint)unicodeScalar);
		do
		{
			HexUtil.ByteToHexDigits((byte)asUtf8, out var highNibble, out var lowNibble);
			if (numberOfCharactersWritten + 3 > bufferLength)
			{
				numberOfCharactersWritten = 0;
				return false;
			}
			*buffer = '%';
			buffer++;
			*buffer = highNibble;
			buffer++;
			*buffer = lowNibble;
			buffer++;
			numberOfCharactersWritten += 3;
		}
		while ((asUtf8 >>= 8) != 0);
		return true;
	}
}
