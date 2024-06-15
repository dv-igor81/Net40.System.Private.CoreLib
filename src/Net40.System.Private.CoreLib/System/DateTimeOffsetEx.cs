/*using System.Globalization;
using System.Text;

namespace System;

public static class DateTimeOffsetEx
{
    
    
    public static bool TryFormat(this DateTimeOffset dtOffset, Span<char> destination, out int charsWritten,
        string? strFormat = default(string), IFormatProvider? formatProvider = null)
    {
        ReadOnlySpan<char> format = strFormat.ToCharArray();
        
        DateTime dateTime = new DateTime(dtOffset.Ticks);
        DateTime dt = ValidateDate(dateTime, dtOffset.Offset);
        DateTime ClockDateTime = new DateTime((dt + Offset).Ticks, DateTimeKind.Unspecified);
        
        //dtOffset.Ticks
        //dtOffset.Offset
    }
    
    private static DateTime ValidateDate(DateTime dateTime, TimeSpan offset)
    {
        long num = dateTime.Ticks - offset.Ticks;
        if (num < 0 || num > 3155378975999999999L)
        {
            throw new ArgumentOutOfRangeException("offset", "SR.Argument_UTCOutOfRange");
        }
        return new DateTime(num, DateTimeKind.Unspecified);
    }

    internal static bool TryFormat(DateTime dateTime, Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format, IFormatProvider provider, TimeSpan offset)
    {
        if (format.Length == 1)
        {
            switch (format[0])
            {
                case 'O':
                case 'o':
                    return TryFormatO(dateTime, offset, destination, out charsWritten);
                case 'R':
                case 'r':
                    return TryFormatR(dateTime, offset, destination, out charsWritten);
            }
        }
        DateTimeFormatInfo instance = DateTimeFormatInfo.GetInstance(provider);
        StringBuilder stringBuilder = FormatStringBuilder(dateTime, format, instance, offset);
        bool flag = stringBuilder.Length <= destination.Length;
        if (flag)
        {
            stringBuilder.CopyTo(0, destination, stringBuilder.Length);
            charsWritten = stringBuilder.Length;
        }
        else
        {
            charsWritten = 0;
        }
        StringBuilderCache.Release(stringBuilder);
        return flag;
    }
    


}*/