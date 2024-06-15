namespace System;

public static class TimeSpanEx 
{
    public static TimeSpan Div(TimeSpan timeSpan, double divisor)
    {
        if (double.IsNaN(divisor))
        {
            throw new ArgumentException("SR.Arg_CannotBeNaN", "divisor");
        }
        double num = Math.Round((double)timeSpan.Ticks / divisor);
        if (num > 9.223372036854776E+18 || num < -9.223372036854776E+18 || double.IsNaN(num))
        {
            throw new OverflowException("SR.Overflow_TimeSpanTooLong");
        }
        return TimeSpan.FromTicks((long)num);
    }
}