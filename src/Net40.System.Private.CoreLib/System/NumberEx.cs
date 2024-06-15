namespace System;

public static class NumberEx
{
    public static bool TryFormat(this Int64 self, Span<char> destination, out int charsWritten,
        string? format = null, IFormatProvider? provider = null)
    {
        format ??= string.Empty;
        return Number.TryFormatInt64(self, format.AsSpan(), provider, destination, out charsWritten);
    }

    // public static bool TryFormat(this Int64 self, Span<char> destination, out int charsWritten,
    //     ReadOnlySpan<char> format = default(ReadOnlySpan<char>), IFormatProvider? provider = null)
    // {
    //     return Number.TryFormatInt64(self, format, provider, destination, out charsWritten);
    // }
    
    public static bool TryFormat(this Int32 self, Span<char> destination, out int charsWritten,
        string? format, IFormatProvider? provider = null)
    {
        format ??= string.Empty;
        return Number.TryFormatInt32(self, format.AsSpan(), provider, destination, out charsWritten);
    }


    public static bool TryFormat(this Int32 self, Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format = default(ReadOnlySpan<char>), IFormatProvider? provider = null)
    {
        return Number.TryFormatInt32(self, format, provider, destination, out charsWritten);
    }

    
    public static bool TryFormat(this UInt16 self, Span<char> destination, out int charsWritten,
        string? format, IFormatProvider? provider = null)
    {
        format ??= string.Empty;
        return Number.TryFormatUInt32(self, format.AsSpan(), provider, destination, out charsWritten);
    }

    

    public static bool TryFormat(this UInt16 self, Span<char> destination, out int charsWritten,
        ReadOnlySpan<char> format = default(ReadOnlySpan<char>), IFormatProvider? provider = null)
    {
        return Number.TryFormatUInt32(self, format, provider, destination, out charsWritten);
    }


    
    public static bool TryFormat(this Byte self, Span<char> destination, out int charsWritten,
        string? format = null, IFormatProvider? provider = null)
    {
        format ??= string.Empty;
        return Number.TryFormatUInt32(self, format.AsSpan(), provider, destination, out charsWritten);
    }
    
    // public static bool TryFormat(this Byte self, Span<char> destination, out int charsWritten,
    //     ReadOnlySpan<char> format = default(ReadOnlySpan<char>), IFormatProvider? provider = null)
    // {
    //     return Number.TryFormatUInt32(self, format, provider, destination, out charsWritten);
    // }
}