namespace System.Text.Json;

internal static class JsonConstants
{
	public const byte OpenBrace = 123;

	public const byte CloseBrace = 125;

	public const byte OpenBracket = 91;

	public const byte CloseBracket = 93;

	public const byte Space = 32;

	public const byte CarriageReturn = 13;

	public const byte LineFeed = 10;

	public const byte Tab = 9;

	public const byte ListSeparator = 44;

	public const byte KeyValueSeperator = 58;

	public const byte Quote = 34;

	public const byte BackSlash = 92;

	public const byte Slash = 47;

	public const byte BackSpace = 8;

	public const byte FormFeed = 12;

	public const byte Asterisk = 42;

	public const byte Colon = 58;

	public const byte Period = 46;

	public const byte Plus = 43;

	public const byte Hyphen = 45;

	public const byte UtcOffsetToken = 90;

	public const byte TimePrefix = 84;

	public const byte StartingByteOfNonStandardSeparator = 226;

	public const int SpacesPerIndent = 2;

	public const int MaxWriterDepth = 1000;

	public const int RemoveFlagsBitMask = int.MaxValue;

	public const int StackallocThreshold = 256;

	public const int MaxExpansionFactorWhileEscaping = 6;

	public const int MaxExpansionFactorWhileTranscoding = 3;

	public const int MaxEscapedTokenSize = 1000000000;

	public const int MaxUnescapedTokenSize = 166666666;

	public const int MaxBase64ValueTokenSize = 125000000;

	public const int MaxCharacterTokenSize = 166666666;

	public const int MaximumFormatInt64Length = 20;

	public const int MaximumFormatUInt64Length = 20;

	public const int MaximumFormatDoubleLength = 128;

	public const int MaximumFormatSingleLength = 128;

	public const int MaximumFormatDecimalLength = 31;

	public const int MaximumFormatGuidLength = 36;

	public const int MaximumEscapedGuidLength = 216;

	public const int MaximumFormatDateTimeLength = 27;

	public const int MaximumFormatDateTimeOffsetLength = 33;

	public const int MaxDateTimeUtcOffsetHours = 14;

	public const int DateTimeNumFractionDigits = 7;

	public const int MaxDateTimeFraction = 9999999;

	public const int DateTimeParseNumFractionDigits = 16;

	public const int MaximumDateTimeOffsetParseLength = 42;

	public const int MinimumDateTimeParseLength = 10;

	public const int MaximumEscapedDateTimeOffsetParseLength = 252;

	internal const char ScientificNotationFormat = 'e';

	public const char HighSurrogateStart = '\ud800';

	public const char HighSurrogateEnd = '\udbff';

	public const char LowSurrogateStart = '\udc00';

	public const char LowSurrogateEnd = '\udfff';

	public const int UnicodePlane01StartValue = 65536;

	public const int HighSurrogateStartValue = 55296;

	public const int HighSurrogateEndValue = 56319;

	public const int LowSurrogateStartValue = 56320;

	public const int LowSurrogateEndValue = 57343;

	public const int BitShiftBy10 = 1024;

	public static ReadOnlySpan<byte> Utf8Bom => "\ufeff"u8;

	public static ReadOnlySpan<byte> TrueValue => "true"u8;

	public static ReadOnlySpan<byte> FalseValue => "false"u8;

	public static ReadOnlySpan<byte> NullValue => "null"u8;

	public static ReadOnlySpan<byte> Delimiters => ",}] \n\r\t/"u8;

	public static ReadOnlySpan<byte> EscapableChars => "\"nrt/ubf"u8;
}
