namespace System.Globalization.Net40;

internal struct HebrewNumberParsingContext
{
    internal HebrewNumber.HS state;

    internal int result;

    public HebrewNumberParsingContext(int result)
    {
        state = HebrewNumber.HS.Start;
        this.result = result;
    }
}