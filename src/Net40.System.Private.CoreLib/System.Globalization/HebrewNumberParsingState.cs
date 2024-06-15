namespace System.Globalization.Net40;

internal enum HebrewNumberParsingState
{
    InvalidHebrewNumber,
    NotHebrewDigit,
    FoundEndOfHebrewNumber,
    ContinueParsing
}
