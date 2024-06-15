using System.Runtime.CompilerServices;

namespace System;

public static class EnumEx
{
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    public static bool TryParse<TEnum>(string value, bool ignoreCase, out TEnum result) where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase, out result);
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    public static bool TryParse<TEnum>(string value, out TEnum result) where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, out result);
    }
}