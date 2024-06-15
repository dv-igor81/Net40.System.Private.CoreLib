using System.Buffers.Text;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using NumberFormatInfo = System.Globalization.Net40.NumberFormatInfo;

namespace System;

internal static class Number
{
    private static class DoubleHelper
    {
        public static unsafe uint Exponent(double d)
        {
            return (*(uint*)((byte*)(&d) + 4) >> 20) & 0x7FFu;
        }

        public static unsafe ulong Mantissa(double d)
        {
            return *(uint*)(&d) | ((ulong)(uint)(*(int*)((byte*)(&d) + 4) & 0xFFFFF) << 32);
        }

        public static unsafe bool Sign(double d)
        {
            return *(uint*)((byte*)(&d) + 4) >> 31 != 0;
        }
    }

    public static unsafe bool TryFormatUInt32(uint value, ReadOnlySpan<char> format, IFormatProvider provider,
        Span<char> destination, out int charsWritten)
    {
        if (format.Length == 0)
        {
            return TryUInt32ToDecStr(value, -1, destination, out charsWritten);
        }

        int digits;
        char c = ParseFormatSpecifier(format, out digits);
        char c2 = (char)(c & 0xFFDFu);
        if (c2 != 'G' || digits >= 1)
        {
            switch (c2)
            {
                case 'D':
                    break;
                case 'X':
                    return TryInt32ToHexStr((int)value, (char)(c - 33), digits, destination, out charsWritten);
                default:
                {
                    NumberFormatInfo instance = NumberFormatInfo.GetInstance(provider);
                    byte* digits2 = stackalloc byte[11];
                    NumberBuffer number = new NumberBuffer(NumberBufferKind.Integer, digits2, 11);
                    UInt32ToNumber(value, ref number);
                    char* pointer = stackalloc char[32];
                    ValueStringBuilder sb = new ValueStringBuilder(new Span<char>(pointer, 32));
                    if (c != 0)
                    {
                        NumberToString(ref sb, ref number, c, digits, instance);
                    }
                    else
                    {
                        NumberToStringFormat(ref sb, ref number, format, instance);
                    }

                    return sb.TryCopyTo(destination, out charsWritten);
                }
            }
        }

        return TryUInt32ToDecStr(value, digits, destination, out charsWritten);
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static unsafe void UInt32ToNumber(uint value, ref NumberBuffer number)
    {
        number.DigitsCount = 10;
        number.IsNegative = false;
        byte* digitsPointer = number.GetDigitsPointer();
        byte* ptr = UInt32ToDecChars(digitsPointer + 10, value, 0);
        int num = (number.Scale = (number.DigitsCount = (int)(digitsPointer + 10 - ptr)));
        byte* digitsPointer2 = number.GetDigitsPointer();
        while (--num >= 0)
        {
            *(digitsPointer2++) = *(ptr++);
        }

        *digitsPointer2 = 0;
    }


    private static unsafe bool TryInt32ToHexStr(int value, char hexBase, int digits, Span<char> destination,
        out int charsWritten)
    {
        if (digits < 1)
        {
            digits = 1;
        }

        int num = Math.Max(digits, FormattingHelpers.CountHexDigits((uint)value));
        if (num > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = num;
        fixed (char* ptr = &MemoryMarshal.GetReference(destination))
        {
            char* ptr2 = Int32ToHexChars(ptr + num, (uint)value, hexBase, digits);
        }

        return true;
    }

    private static unsafe bool TryUInt32ToDecStr(uint value, int digits, Span<char> destination, out int charsWritten)
    {
        int num = Math.Max(digits, FormattingHelpers.CountDigits(value));
        if (num > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = num;
        fixed (char* ptr = &MemoryMarshal.GetReference(destination))
        {
            char* ptr2 = ptr + num;
            if (digits <= 1)
            {
                do
                {
                    uint num2 = value / 10;
                    *(--ptr2) = (char)(48 + value - num2 * 10);
                    value = num2;
                } while (value != 0);
            }
            else
            {
                ptr2 = UInt32ToDecChars(ptr2, value, digits);
            }
        }

        return true;
    }




    private static readonly string[] s_negPercentFormats = new string[12]
    {
        "-# %", "-#%", "-%#", "%-#", "%#-", "#-%", "#%-", "-% #", "# %-", "% #-",
        "% -#", "#- %"
    };

    private static readonly string[] s_posPercentFormats = new string[4] { "# %", "#%", "%#", "% #" };

    private static readonly string[] s_negNumberFormats = new string[5] { "(#)", "-#", "- #", "#-", "# -" };

    private static readonly string[] s_negCurrencyFormats = new string[16]
    {
        "($#)", "-$#", "$-#", "$#-", "(#$)", "-#$", "#-$", "#$-", "-# $", "-$ #",
        "# $-", "$ #-", "$ -#", "#- $", "($ #)", "(# $)"
    };


    private static readonly string[] s_posCurrencyFormats = new string[4] { "$#", "#$", "$ #", "# $" };
    
    
    public static unsafe bool TryFormatInt64(long value, ReadOnlySpan<char> format, IFormatProvider provider,
        Span<char> destination, out int charsWritten)
    {
        if (value >= 0 && format.Length == 0)
        {
            return TryUInt64ToDecStr((ulong)value, -1, destination, out charsWritten);
        }

        int digits;
        char c = ParseFormatSpecifier(format, out digits);
        char c2 = (char)(c & 0xFFDFu);
        if (c2 != 'G' || digits >= 1)
        {
            switch (c2)
            {
                case 'D':
                    break;
                case 'X':
                    return TryInt64ToHexStr(value, (char)(c - 33), digits, destination, out charsWritten);
                default:
                {
                    NumberFormatInfo instance = NumberFormatInfo.GetInstance(provider);
                    byte* digits2 = stackalloc byte[20];
                    NumberBuffer number = new NumberBuffer(NumberBufferKind.Integer, digits2, 20);
                    Int64ToNumber(value, ref number);
                    char* pointer = stackalloc char[32];
                    ValueStringBuilder sb = new ValueStringBuilder(new Span<char>(pointer, 32));
                    if (c != 0)
                    {
                        NumberToString(ref sb, ref number, c, digits, instance);
                    }
                    else
                    {
                        NumberToStringFormat(ref sb, ref number, format, instance);
                    }

                    return sb.TryCopyTo(destination, out charsWritten);
                }
            }
        }

        if (value < 0)
        {
            return TryNegativeInt64ToDecStr(value, digits, NumberFormatInfo.GetInstance(provider).NegativeSign,
                destination, out charsWritten);
        }

        return TryUInt64ToDecStr((ulong)value, digits, destination, out charsWritten);
    }

    public static unsafe bool TryFormatInt32(int value, ReadOnlySpan<char> format, IFormatProvider provider,
        Span<char> destination, out int charsWritten)
    {
        if (value >= 0 && format.Length == 0)
        {
            return TryUInt32ToDecStr((uint)value, -1, destination, out charsWritten);
        }

        int digits;
        char c = ParseFormatSpecifier(format, out digits);
        char c2 = (char)(c & 0xFFDFu);
        if (c2 != 'G' || digits >= 1)
        {
            switch (c2)
            {
                case 'D':
                    break;
                case 'X':
                    return TryInt32ToHexStr(value, (char)(c - 33), digits, destination, out charsWritten);
                default:
                {
                    NumberFormatInfo instance = NumberFormatInfo.GetInstance(provider);
                    byte* digits2 = stackalloc byte[11];
                    NumberBuffer number = new NumberBuffer(NumberBufferKind.Integer, digits2, 11);
                    Int32ToNumber(value, ref number);
                    char* pointer = stackalloc char[32];
                    ValueStringBuilder sb = new ValueStringBuilder(new Span<char>(pointer, 32));
                    if (c != 0)
                    {
                        NumberToString(ref sb, ref number, c, digits, instance);
                    }
                    else
                    {
                        NumberToStringFormat(ref sb, ref number, format, instance);
                    }

                    return sb.TryCopyTo(destination, out charsWritten);
                }
            }
        }

        if (value < 0)
        {
            return TryNegativeInt32ToDecStr(value, digits, NumberFormatInfo.GetInstance(provider).NegativeSign,
                destination, out charsWritten);
        }

        return TryUInt32ToDecStr((uint)value, digits, destination, out charsWritten);
    }
    
    private static unsafe bool TryNegativeInt32ToDecStr(int value, int digits, string sNegative, Span<char> destination, out int charsWritten)
    {
        if (digits < 1)
        {
            digits = 1;
        }
        int num = Math.Max(digits, FormattingHelpers.CountDigits((uint)(-value))) + sNegative.Length;
        if (num > destination.Length)
        {
            charsWritten = 0;
            return false;
        }
        charsWritten = num;
        fixed (char* ptr = &MemoryMarshal.GetReference(destination))
        {
            char* ptr2 = UInt32ToDecChars(ptr + num, (uint)(-value), digits);
            for (int num2 = sNegative.Length - 1; num2 >= 0; num2--)
            {
                *(--ptr2) = sNegative[num2];
            }
        }
        return true;
    }

    
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static unsafe void Int32ToNumber(int value, ref NumberBuffer number)
    {
        number.DigitsCount = 10;
        if (value >= 0)
        {
            number.IsNegative = false;
        }
        else
        {
            number.IsNegative = true;
            value = -value;
        }
        byte* digitsPointer = number.GetDigitsPointer();
        byte* ptr = UInt32ToDecChars(digitsPointer + 10, (uint)value, 0);
        int num = (number.Scale = (number.DigitsCount = (int)(digitsPointer + 10 - ptr)));
        byte* digitsPointer2 = number.GetDigitsPointer();
        while (--num >= 0)
        {
            *(digitsPointer2++) = *(ptr++);
        }
        *digitsPointer2 = 0;
    }



    private static unsafe void NumberToStringFormat(ref ValueStringBuilder sb, ref NumberBuffer number,
        ReadOnlySpan<char> format, NumberFormatInfo info)
    {
        int num = 0;
        byte* digitsPointer = number.GetDigitsPointer();
        int num2 = FindSection(format, (*digitsPointer == 0) ? 2 : (number.IsNegative ? 1 : 0));
        int num3;
        int num4;
        bool flag;
        bool flag2;
        int num5;
        int num6;
        int num9;
        while (true)
        {
            num3 = 0;
            num4 = -1;
            num5 = int.MaxValue;
            num6 = 0;
            flag = false;
            int num7 = -1;
            flag2 = false;
            int num8 = 0;
            num9 = num2;
            fixed (char* ptr = &MemoryMarshal.GetReference(format))
            {
                char c;
                while (num9 < format.Length && (c = ptr[num9++]) != 0)
                {
                    switch (c)
                    {
                        case ';':
                            break;
                        case '#':
                            num3++;
                            continue;
                        case '0':
                            if (num5 == int.MaxValue)
                            {
                                num5 = num3;
                            }

                            num3++;
                            num6 = num3;
                            continue;
                        case '.':
                            if (num4 < 0)
                            {
                                num4 = num3;
                            }

                            continue;
                        case ',':
                            if (num3 <= 0 || num4 >= 0)
                            {
                                continue;
                            }

                            if (num7 >= 0)
                            {
                                if (num7 == num3)
                                {
                                    num++;
                                    continue;
                                }

                                flag2 = true;
                            }

                            num7 = num3;
                            num = 1;
                            continue;
                        case '%':
                            num8 += 2;
                            continue;
                        case '‰':
                            num8 += 3;
                            continue;
                        case '"':
                        case '\'':
                            while (num9 < format.Length && ptr[num9] != 0 && ptr[num9++] != c)
                            {
                            }

                            continue;
                        case '\\':
                            if (num9 < format.Length && ptr[num9] != 0)
                            {
                                num9++;
                            }

                            continue;
                        case 'E':
                        case 'e':
                            if ((num9 < format.Length && ptr[num9] == '0') || (num9 + 1 < format.Length &&
                                                                               (ptr[num9] == '+' || ptr[num9] == '-') &&
                                                                               ptr[num9 + 1] == '0'))
                            {
                                while (++num9 < format.Length && ptr[num9] == '0')
                                {
                                }

                                flag = true;
                            }

                            continue;
                        default:
                            continue;
                    }

                    break;
                }
            }

            if (num4 < 0)
            {
                num4 = num3;
            }

            if (num7 >= 0)
            {
                if (num7 == num4)
                {
                    num8 -= num * 3;
                }
                else
                {
                    flag2 = true;
                }
            }

            if (*digitsPointer != 0)
            {
                number.Scale += num8;
                int pos = (flag ? num3 : (number.Scale + num3 - num4));
                RoundNumber(ref number, pos, isCorrectlyRounded: false);
                if (*digitsPointer != 0)
                {
                    break;
                }

                num9 = FindSection(format, 2);
                if (num9 == num2)
                {
                    break;
                }

                num2 = num9;
                continue;
            }

            if (number.Kind != NumberBufferKind.FloatingPoint)
            {
                number.IsNegative = false;
            }

            number.Scale = 0;
            break;
        }

        num5 = ((num5 < num4) ? (num4 - num5) : 0);
        num6 = ((num6 > num4) ? (num4 - num6) : 0);
        int num10;
        int num11;
        if (flag)
        {
            num10 = num4;
            num11 = 0;
        }
        else
        {
            num10 = ((number.Scale > num4) ? number.Scale : num4);
            num11 = number.Scale - num4;
        }

        num9 = num2;
        Span<int> span = stackalloc int[4];
        int num12 = -1;
        if (flag2 && info.NumberGroupSeparator.Length > 0)
        {
            int[] numberGroupSizes = info._numberGroupSizes;
            int num13 = 0;
            int i = 0;
            int num14 = numberGroupSizes.Length;
            if (num14 != 0)
            {
                i = numberGroupSizes[num13];
            }

            int num15 = i;
            int num16 = num10 + ((num11 < 0) ? num11 : 0);
            for (int num17 = ((num5 > num16) ? num5 : num16); num17 > i; i += num15)
            {
                if (num15 == 0)
                {
                    break;
                }

                num12++;
                if (num12 >= span.Length)
                {
                    int[] array = new int[span.Length * 2];
                    span.CopyTo(array);
                    span = array;
                }

                span[num12] = i;
                if (num13 < num14 - 1)
                {
                    num13++;
                    num15 = numberGroupSizes[num13];
                }
            }
        }

        if (number.IsNegative && num2 == 0 && number.Scale != 0)
        {
            sb.Append(info.NegativeSign);
        }

        bool flag3 = false;
        fixed (char* ptr3 = &MemoryMarshal.GetReference(format))
        {
            byte* ptr2 = digitsPointer;
            char c;
            while (num9 < format.Length && (c = ptr3[num9++]) != 0 && c != ';')
            {
                if (num11 > 0 && (c == '#' || c == '.' || c == '0'))
                {
                    while (num11 > 0)
                    {
                        sb.Append((char)((*ptr2 != 0) ? (*(ptr2++)) : 48));
                        if (flag2 && num10 > 1 && num12 >= 0 && num10 == span[num12] + 1)
                        {
                            sb.Append(info.NumberGroupSeparator);
                            num12--;
                        }

                        num10--;
                        num11--;
                    }
                }

                switch (c)
                {
                    case '#':
                    case '0':
                        if (num11 < 0)
                        {
                            num11++;
                            c = ((num10 <= num5) ? '0' : '\0');
                        }
                        else
                        {
                            c = ((*ptr2 != 0) ? ((char)(*(ptr2++))) : ((num10 > num6) ? '0' : '\0'));
                        }

                        if (c != 0)
                        {
                            sb.Append(c);
                            if (flag2 && num10 > 1 && num12 >= 0 && num10 == span[num12] + 1)
                            {
                                sb.Append(info.NumberGroupSeparator);
                                num12--;
                            }
                        }

                        num10--;
                        break;
                    case '.':
                        if (!(num10 != 0 || flag3) && (num6 < 0 || (num4 < num3 && *ptr2 != 0)))
                        {
                            sb.Append(info.NumberDecimalSeparator);
                            flag3 = true;
                        }

                        break;
                    case '‰':
                        sb.Append(info.PerMilleSymbol);
                        break;
                    case '%':
                        sb.Append(info.PercentSymbol);
                        break;
                    case '"':
                    case '\'':
                        while (num9 < format.Length && ptr3[num9] != 0 && ptr3[num9] != c)
                        {
                            sb.Append(ptr3[num9++]);
                        }

                        if (num9 < format.Length && ptr3[num9] != 0)
                        {
                            num9++;
                        }

                        break;
                    case '\\':
                        if (num9 < format.Length && ptr3[num9] != 0)
                        {
                            sb.Append(ptr3[num9++]);
                        }

                        break;
                    case 'E':
                    case 'e':
                    {
                        bool positiveSign = false;
                        int num18 = 0;
                        if (flag)
                        {
                            if (num9 < format.Length && ptr3[num9] == '0')
                            {
                                num18++;
                            }
                            else if (num9 + 1 < format.Length && ptr3[num9] == '+' && ptr3[num9 + 1] == '0')
                            {
                                positiveSign = true;
                            }
                            else if (num9 + 1 >= format.Length || ptr3[num9] != '-' || ptr3[num9 + 1] != '0')
                            {
                                sb.Append(c);
                                break;
                            }

                            while (++num9 < format.Length && ptr3[num9] == '0')
                            {
                                num18++;
                            }

                            if (num18 > 10)
                            {
                                num18 = 10;
                            }

                            int value = ((*digitsPointer != 0) ? (number.Scale - num4) : 0);
                            FormatExponent(ref sb, info, value, c, num18, positiveSign);
                            flag = false;
                            break;
                        }

                        sb.Append(c);
                        if (num9 < format.Length)
                        {
                            if (ptr3[num9] == '+' || ptr3[num9] == '-')
                            {
                                sb.Append(ptr3[num9++]);
                            }

                            while (num9 < format.Length && ptr3[num9] == '0')
                            {
                                sb.Append(ptr3[num9++]);
                            }
                        }

                        break;
                    }
                    default:
                        sb.Append(c);
                        break;
                    case ',':
                        break;
                }
            }
        }

        if (number.IsNegative && num2 == 0 && number.Scale == 0 && sb.Length > 0)
        {
            sb.Insert(0, info.NegativeSign);
        }
    }

    private static unsafe int FindSection(ReadOnlySpan<char> format, int section)
    {
        if (section == 0)
        {
            return 0;
        }

        fixed (char* ptr = &MemoryMarshal.GetReference(format))
        {
            int num = 0;
            while (true)
            {
                if (num >= format.Length)
                {
                    return 0;
                }

                char c;
                char c2 = (c = ptr[num++]);
                if ((uint)c2 <= 34u)
                {
                    if (c2 == '\0')
                    {
                        break;
                    }

                    if (c2 != '"')
                    {
                        continue;
                    }
                }
                else if (c2 != '\'')
                {
                    switch (c2)
                    {
                        default:
                            continue;
                        case '\\':
                            if (num < format.Length && ptr[num] != 0)
                            {
                                num++;
                            }

                            continue;
                        case ';':
                            break;
                    }

                    if (--section == 0)
                    {
                        if (num >= format.Length || ptr[num] == '\0' || ptr[num] == ';')
                        {
                            break;
                        }

                        return num;
                    }

                    continue;
                }

                while (num < format.Length && ptr[num] != 0 && ptr[num++] != c)
                {
                }
            }

            return 0;
        }
    }


    private static void NumberToString(ref ValueStringBuilder sb, ref NumberBuffer number, char format, int nMaxDigits,
        NumberFormatInfo info)
    {
        bool isCorrectlyRounded = number.Kind == NumberBufferKind.FloatingPoint;
        bool bSuppressScientific;
        switch (format)
        {
            case 'C':
            case 'c':
                if (nMaxDigits < 0)
                {
                    nMaxDigits = info.CurrencyDecimalDigits;
                }

                RoundNumber(ref number, number.Scale + nMaxDigits, isCorrectlyRounded);
                FormatCurrency(ref sb, ref number, nMaxDigits, info);
                return;
            case 'F':
            case 'f':
                if (nMaxDigits < 0)
                {
                    nMaxDigits = info.NumberDecimalDigits;
                }

                RoundNumber(ref number, number.Scale + nMaxDigits, isCorrectlyRounded);
                if (number.IsNegative)
                {
                    sb.Append(info.NegativeSign);
                }

                FormatFixed(ref sb, ref number, nMaxDigits, info, null, info.NumberDecimalSeparator, null);
                return;
            case 'N':
            case 'n':
                if (nMaxDigits < 0)
                {
                    nMaxDigits = info.NumberDecimalDigits;
                }

                RoundNumber(ref number, number.Scale + nMaxDigits, isCorrectlyRounded);
                FormatNumber(ref sb, ref number, nMaxDigits, info);
                return;
            case 'E':
            case 'e':
                if (nMaxDigits < 0)
                {
                    nMaxDigits = 6;
                }

                nMaxDigits++;
                RoundNumber(ref number, nMaxDigits, isCorrectlyRounded);
                if (number.IsNegative)
                {
                    sb.Append(info.NegativeSign);
                }

                FormatScientific(ref sb, ref number, nMaxDigits, info, format);
                return;
            case 'G':
            case 'g':
                bSuppressScientific = false;
                if (nMaxDigits < 1)
                {
                    if (number.Kind == NumberBufferKind.Decimal && nMaxDigits == -1)
                    {
                        bSuppressScientific = true;
                        if (number.Digits[0] != 0)
                        {
                            goto IL_018b;
                        }

                        goto IL_01a0;
                    }

                    nMaxDigits = number.DigitsCount;
                }

                RoundNumber(ref number, nMaxDigits, isCorrectlyRounded);
                goto IL_018b;
            case 'P':
            case 'p':
                if (nMaxDigits < 0)
                {
                    nMaxDigits = info.PercentDecimalDigits;
                }

                number.Scale += 2;
                RoundNumber(ref number, number.Scale + nMaxDigits, isCorrectlyRounded);
                FormatPercent(ref sb, ref number, nMaxDigits, info);
                return;
            case 'R':
            case 'r':
            {
                if (number.Kind != NumberBufferKind.FloatingPoint)
                {
                    break;
                }

                format = (char)(format - 11);
                goto case 'G';
            }
                IL_018b:
                if (number.IsNegative)
                {
                    sb.Append(info.NegativeSign);
                }

                goto IL_01a0;
                IL_01a0:
                FormatGeneral(ref sb, ref number, nMaxDigits, info, (char)(format - 2), bSuppressScientific);
                return;
        }

        throw new FormatException(SR.Argument_BadFormatSpecifier);
    }

    private static unsafe void FormatGeneral(ref ValueStringBuilder sb, ref NumberBuffer number, int nMaxDigits,
        NumberFormatInfo info, char expChar, bool bSuppressScientific)
    {
        int i = number.Scale;
        bool flag = false;
        if (!bSuppressScientific && (i > nMaxDigits || i < -3))
        {
            i = 1;
            flag = true;
        }

        byte* digitsPointer = number.GetDigitsPointer();
        if (i > 0)
        {
            do
            {
                sb.Append((char)((*digitsPointer != 0) ? (*(digitsPointer++)) : 48));
            } while (--i > 0);
        }
        else
        {
            sb.Append('0');
        }

        if (*digitsPointer != 0 || i < 0)
        {
            sb.Append(info.NumberDecimalSeparator);
            for (; i < 0; i++)
            {
                sb.Append('0');
            }

            while (*digitsPointer != 0)
            {
                sb.Append((char)(*(digitsPointer++)));
            }
        }

        if (flag)
        {
            FormatExponent(ref sb, info, number.Scale - 1, expChar, 2, positiveSign: true);
        }
    }


    private static void FormatPercent(ref ValueStringBuilder sb, ref NumberBuffer number, int nMaxDigits,
        NumberFormatInfo info)
    {
        string text = (number.IsNegative
            ? s_negPercentFormats[info.PercentNegativePattern]
            : s_posPercentFormats[info.PercentPositivePattern]);
        string text2 = text;
        foreach (char c in text2)
        {
            switch (c)
            {
                case '#':
                    FormatFixed(ref sb, ref number, nMaxDigits, info, info._percentGroupSizes,
                        info.PercentDecimalSeparator, info.PercentGroupSeparator);
                    break;
                case '-':
                    sb.Append(info.NegativeSign);
                    break;
                case '%':
                    sb.Append(info.PercentSymbol);
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
    }


    private static unsafe void FormatScientific(ref ValueStringBuilder sb, ref NumberBuffer number, int nMaxDigits,
        NumberFormatInfo info, char expChar)
    {
        byte* digitsPointer = number.GetDigitsPointer();
        sb.Append((char)((*digitsPointer != 0) ? (*(digitsPointer++)) : 48));
        if (nMaxDigits != 1)
        {
            sb.Append(info.NumberDecimalSeparator);
        }

        while (--nMaxDigits > 0)
        {
            sb.Append((char)((*digitsPointer != 0) ? (*(digitsPointer++)) : 48));
        }

        int value = ((number.Digits[0] != 0) ? (number.Scale - 1) : 0);
        FormatExponent(ref sb, info, value, expChar, 3, positiveSign: true);
    }

    private static unsafe void FormatExponent(ref ValueStringBuilder sb, NumberFormatInfo info, int value, char expChar,
        int minDigits, bool positiveSign)
    {
        sb.Append(expChar);
        if (value < 0)
        {
            sb.Append(info.NegativeSign);
            value = -value;
        }
        else if (positiveSign)
        {
            sb.Append(info.PositiveSign);
        }

        char* ptr = stackalloc char[10];
        char* ptr2 = UInt32ToDecChars(ptr + 10, (uint)value, minDigits);
        int num = (int)(ptr + 10 - ptr2);
        sb.Append(ptr2, (int)(ptr + 10 - ptr2));
    }


    private static void FormatNumber(ref ValueStringBuilder sb, ref NumberBuffer number, int nMaxDigits,
        NumberFormatInfo info)
    {
        string text = (number.IsNegative ? s_negNumberFormats[info.NumberNegativePattern] : "#");
        string text2 = text;
        foreach (char c in text2)
        {
            switch (c)
            {
                case '#':
                    FormatFixed(ref sb, ref number, nMaxDigits, info, info._numberGroupSizes,
                        info.NumberDecimalSeparator, info.NumberGroupSeparator);
                    break;
                case '-':
                    sb.Append(info.NegativeSign);
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
    }


    private static unsafe void RoundNumber(ref NumberBuffer number, int pos, bool isCorrectlyRounded)
    {
        byte* digitsPointer = number.GetDigitsPointer();
        int j;
        for (j = 0; j < pos && digitsPointer[j] != 0; j++)
        {
        }

        if (j == pos && ShouldRoundUp(digitsPointer, j, number.Kind, isCorrectlyRounded))
        {
            while (j > 0 && digitsPointer[j - 1] == 57)
            {
                j--;
            }

            if (j > 0)
            {
                byte* intPtr = digitsPointer + (j - 1);
                (*intPtr)++;
            }
            else
            {
                number.Scale++;
                *digitsPointer = 49;
                j = 1;
            }
        }
        else
        {
            while (j > 0 && digitsPointer[j - 1] == 48)
            {
                j--;
            }
        }

        if (j == 0)
        {
            if (number.Kind != NumberBufferKind.FloatingPoint)
            {
                number.IsNegative = false;
            }

            number.Scale = 0;
        }

        digitsPointer[j] = 0;
        number.DigitsCount = j;

        static unsafe bool ShouldRoundUp(byte* dig, int i, NumberBufferKind numberKind, bool isCorrectlyRounded)
        {
            byte b = dig[i];
            if (b == 0 || isCorrectlyRounded)
            {
                return false;
            }

            return b >= 53;
        }
    }


    private static void FormatCurrency(ref ValueStringBuilder sb, ref NumberBuffer number, int nMaxDigits,
        NumberFormatInfo info)
    {
        string text = (number.IsNegative
            ? s_negCurrencyFormats[info.CurrencyNegativePattern]
            : s_posCurrencyFormats[info.CurrencyPositivePattern]);
        string text2 = text;
        foreach (char c in text2)
        {
            switch (c)
            {
                case '#':
                    FormatFixed(ref sb, ref number, nMaxDigits, info, info._currencyGroupSizes,
                        info.CurrencyDecimalSeparator, info.CurrencyGroupSeparator);
                    break;
                case '-':
                    sb.Append(info.NegativeSign);
                    break;
                case '$':
                    sb.Append(info.CurrencySymbol);
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }
    }

    private static unsafe void FormatFixed(ref ValueStringBuilder sb, ref NumberBuffer number, int nMaxDigits,
        NumberFormatInfo info, int[] groupDigits, string sDecimal, string sGroup)
    {
        int num = number.Scale;
        byte* ptr = number.GetDigitsPointer();
        if (num > 0)
        {
            if (groupDigits != null)
            {
                int num2 = 0;
                int num3 = num;
                int num4 = 0;
                if (groupDigits.Length != 0)
                {
                    int num5 = groupDigits[num2];
                    while (num > num5 && groupDigits[num2] != 0)
                    {
                        num3 += sGroup.Length;
                        if (num2 < groupDigits.Length - 1)
                        {
                            num2++;
                        }

                        num5 += groupDigits[num2];
                        if (num5 < 0 || num3 < 0)
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                    }

                    num4 = ((num5 != 0) ? groupDigits[0] : 0);
                }

                num2 = 0;
                int num6 = 0;
                int digitsCount = number.DigitsCount;
                int num7 = ((num < digitsCount) ? num : digitsCount);
                fixed (char* ptr2 = &MemoryMarshal.GetReference(sb.AppendSpan(num3)))
                {
                    char* ptr3 = ptr2 + num3 - 1;
                    for (int num8 = num - 1; num8 >= 0; num8--)
                    {
                        *(ptr3--) = (char)((num8 < num7) ? ptr[num8] : 48);
                        if (num4 > 0)
                        {
                            num6++;
                            if (num6 == num4 && num8 != 0)
                            {
                                for (int num9 = sGroup.Length - 1; num9 >= 0; num9--)
                                {
                                    *(ptr3--) = sGroup[num9];
                                }

                                if (num2 < groupDigits.Length - 1)
                                {
                                    num2++;
                                    num4 = groupDigits[num2];
                                }

                                num6 = 0;
                            }
                        }
                    }

                    ptr += num7;
                }
            }
            else
            {
                do
                {
                    sb.Append((char)((*ptr != 0) ? (*(ptr++)) : 48));
                } while (--num > 0);
            }
        }
        else
        {
            sb.Append('0');
        }

        if (nMaxDigits > 0)
        {
            sb.Append(sDecimal);
            if (num < 0 && nMaxDigits > 0)
            {
                int num10 = Math.Min(-num, nMaxDigits);
                sb.Append('0', num10);
                num += num10;
                nMaxDigits -= num10;
            }

            while (nMaxDigits > 0)
            {
                sb.Append((char)((*ptr != 0) ? (*(ptr++)) : 48));
                nMaxDigits--;
            }
        }
    }

    private static unsafe void Int64ToNumber(long input, ref NumberBuffer number)
    {
        ulong value = (ulong)input;
        number.IsNegative = input < 0;
        number.DigitsCount = 19;
        if (number.IsNegative)
        {
            value = (ulong)(-input);
        }

        byte* digitsPointer = number.GetDigitsPointer();
        byte* bufferEnd = digitsPointer + 19;
        while (High32(value) != 0)
        {
            bufferEnd = UInt32ToDecChars(bufferEnd, Int64DivMod1E9(ref value), 9);
        }

        bufferEnd = UInt32ToDecChars(bufferEnd, Low32(value), 0);
        int num = (number.Scale = (number.DigitsCount = (int)(digitsPointer + 19 - bufferEnd)));
        byte* digitsPointer2 = number.GetDigitsPointer();
        while (--num >= 0)
        {
            *(digitsPointer2++) = *(bufferEnd++);
        }

        *digitsPointer2 = 0;
    }

    private static char ParseFormatSpecifier(ReadOnlySpan<char> format, out int digits)
    {
        char c = '\0';
        if (format.Length > 0)
        {
            c = format[0];
            if ((uint)(c - 65) <= 25u || (uint)(c - 97) <= 25u)
            {
                if (format.Length == 1)
                {
                    digits = -1;
                    return c;
                }

                if (format.Length == 2)
                {
                    int num = format[1] - 48;
                    if ((uint)num < 10u)
                    {
                        digits = num;
                        return c;
                    }
                }
                else if (format.Length == 3)
                {
                    int num2 = format[1] - 48;
                    int num3 = format[2] - 48;
                    if ((uint)num2 < 10u && (uint)num3 < 10u)
                    {
                        digits = num2 * 10 + num3;
                        return c;
                    }
                }

                int num4 = 0;
                int num5 = 1;
                while (num5 < format.Length && (uint)(format[num5] - 48) < 10u && num4 < 10)
                {
                    num4 = num4 * 10 + format[num5++] - 48;
                }

                if (num5 == format.Length || format[num5] == '\0')
                {
                    digits = num4;
                    return c;
                }
            }
        }

        digits = -1;
        if (format.Length != 0 && c != 0)
        {
            return '\0';
        }

        return 'G';
    }

    private static unsafe bool TryInt64ToHexStr(long value, char hexBase, int digits, Span<char> destination,
        out int charsWritten)
    {
        int num = Math.Max(digits, FormattingHelpers.CountHexDigits((ulong)value));
        if (num > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = num;
        fixed (char* ptr = &MemoryMarshal.GetReference(destination))
        {
            char* buffer = ptr + num;
            if (High32((ulong)value) != 0)
            {
                buffer = Int32ToHexChars(buffer, Low32((ulong)value), hexBase, 8);
                buffer = Int32ToHexChars(buffer, High32((ulong)value), hexBase, digits - 8);
            }
            else
            {
                buffer = Int32ToHexChars(buffer, Low32((ulong)value), hexBase, Math.Max(digits, 1));
            }
        }

        return true;
    }

    private static unsafe char* Int32ToHexChars(char* buffer, uint value, int hexBase, int digits)
    {
        while (--digits >= 0 || value != 0)
        {
            byte b = (byte)(value & 0xFu);
            *(--buffer) = (char)(b + ((b < 10) ? 48 : hexBase));
            value >>= 4;
        }

        return buffer;
    }


    private static unsafe bool TryNegativeInt64ToDecStr(long input, int digits, string sNegative,
        Span<char> destination, out int charsWritten)
    {
        if (digits < 1)
        {
            digits = 1;
        }

        ulong value = (ulong)(-input);
        int num = Math.Max(digits, FormattingHelpers.CountDigits((ulong)(-input))) + sNegative.Length;
        if (num > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = num;
        fixed (char* ptr = &MemoryMarshal.GetReference(destination))
        {
            char* bufferEnd = ptr + num;
            while (High32(value) != 0)
            {
                bufferEnd = UInt32ToDecChars(bufferEnd, Int64DivMod1E9(ref value), 9);
                digits -= 9;
            }

            bufferEnd = UInt32ToDecChars(bufferEnd, Low32(value), digits);
            for (int num2 = sNegative.Length - 1; num2 >= 0; num2--)
            {
                *(--bufferEnd) = sNegative[num2];
            }
        }

        return true;
    }

    private static unsafe bool TryUInt64ToDecStr(ulong value, int digits, Span<char> destination, out int charsWritten)
    {
        if (digits < 1)
        {
            digits = 1;
        }

        int num = Math.Max(digits, FormattingHelpers.CountDigits(value));
        if (num > destination.Length)
        {
            charsWritten = 0;
            return false;
        }

        charsWritten = num;
        fixed (char* ptr = &MemoryMarshal.GetReference(destination))
        {
            char* bufferEnd = ptr + num;
            while (High32(value) != 0)
            {
                bufferEnd = UInt32ToDecChars(bufferEnd, Int64DivMod1E9(ref value), 9);
                digits -= 9;
            }

            bufferEnd = UInt32ToDecChars(bufferEnd, Low32(value), digits);
        }

        return true;
    }

    private static unsafe byte* UInt32ToDecChars(byte* bufferEnd, uint value, int digits)
    {
        while (--digits >= 0 || value != 0)
        {
            uint num = value / 10;
            *(--bufferEnd) = (byte)(value - num * 10 + 48);
            value = num;
        }

        return bufferEnd;
    }

    private static unsafe char* UInt32ToDecChars(char* bufferEnd, uint value, int digits)
    {
        while (--digits >= 0 || value != 0)
        {
            uint num = value / 10;
            *(--bufferEnd) = (char)(value - num * 10 + 48);
            value = num;
        }

        return bufferEnd;
    }


    private static uint Int64DivMod1E9(ref ulong value)
    {
        uint result = (uint)(value % 1000000000);
        value /= 1000000000uL;
        return result;
    }


    private static uint High32(ulong value)
    {
        return (uint)((value & 0xFFFFFFFF00000000uL) >> 32);
    }

    private static uint Low32(ulong value)
    {
        return (uint)value;
    }


    internal const int DECIMAL_PRECISION = 29;

    private static readonly ulong[] s_rgval64Power10 = new ulong[30]
    {
        11529215046068469760uL, 14411518807585587200uL, 18014398509481984000uL, 11258999068426240000uL,
        14073748835532800000uL, 17592186044416000000uL, 10995116277760000000uL, 13743895347200000000uL,
        17179869184000000000uL, 10737418240000000000uL,
        13421772800000000000uL, 16777216000000000000uL, 10485760000000000000uL, 13107200000000000000uL,
        16384000000000000000uL, 14757395258967641293uL, 11805916207174113035uL, 9444732965739290428uL,
        15111572745182864686uL, 12089258196146291749uL,
        9671406556917033399uL, 15474250491067253438uL, 12379400392853802751uL, 9903520314283042201uL,
        15845632502852867522uL, 12676506002282294018uL, 10141204801825835215uL, 16225927682921336344uL,
        12980742146337069075uL, 10384593717069655260uL
    };

    private static readonly sbyte[] s_rgexp64Power10 = new sbyte[15]
    {
        4, 7, 10, 14, 17, 20, 24, 27, 30, 34,
        37, 40, 44, 47, 50
    };

    private static readonly ulong[] s_rgval64Power10By16 = new ulong[42]
    {
        10240000000000000000uL, 11368683772161602974uL, 12621774483536188886uL, 14012984643248170708uL,
        15557538194652854266uL, 17272337110188889248uL, 9588073174409622172uL, 10644899600020376798uL,
        11818212630765741798uL, 13120851772591970216uL,
        14567071740625403792uL, 16172698447808779622uL, 17955302187076837696uL, 9967194951097567532uL,
        11065809325636130658uL, 12285516299433008778uL, 13639663065038175358uL, 15143067982934716296uL,
        16812182738118149112uL, 9332636185032188787uL,
        10361307573072618722uL, 16615349947311448416uL, 14965776766268445891uL, 13479973333575319909uL,
        12141680576410806707uL, 10936253623915059637uL, 9850501549098619819uL, 17745086042373215136uL,
        15983352577617880260uL, 14396524142538228461uL,
        12967236152753103031uL, 11679847981112819795uL, 10520271803096747049uL, 9475818434452569218uL,
        17070116948172427008uL, 15375394465392026135uL, 13848924157002783096uL, 12474001934591998882uL,
        11235582092889474480uL, 10120112665365530972uL,
        18230774251475056952uL, 16420821625123739930uL
    };

    private static readonly short[] s_rgexp64Power10By16 = new short[21]
    {
        54, 107, 160, 213, 266, 319, 373, 426, 479, 532,
        585, 638, 691, 745, 798, 851, 904, 957, 1010, 1064,
        1117
    };

    public static void RoundNumber(ref NumberBuffer number, int pos)
    {
        Span<byte> digits = number.Digits;
        int i;
        for (i = 0; i < pos && digits[i] != 0; i++)
        {
        }

        if (i == pos && digits[i] >= 53)
        {
            while (i > 0 && digits[i - 1] == 57)
            {
                i--;
            }

            if (i > 0)
            {
                digits[i - 1]++;
            }
            else
            {
                number.Scale++;
                digits[0] = 49;
                i = 1;
            }
        }
        else
        {
            while (i > 0 && digits[i - 1] == 48)
            {
                i--;
            }
        }

        if (i == 0)
        {
            number.Scale = 0;
            number.IsNegative = false;
        }

        digits[i] = 0;
    }

    internal static bool NumberBufferToDouble(ref NumberBuffer number, out double value)
    {
        double num = NumberToDouble(ref number);
        uint num2 = DoubleHelper.Exponent(num);
        ulong num3 = DoubleHelper.Mantissa(num);
        switch (num2)
        {
            case 2047u:
                value = 0.0;
                return false;
            case 0u:
                if (num3 == 0)
                {
                    num = 0.0;
                }

                break;
        }

        value = num;
        return true;
    }

    public static unsafe bool NumberBufferToDecimal(ref NumberBuffer number, ref decimal value)
    {
        MutableDecimal source = default(MutableDecimal);
        //byte* ptr = number.UnsafeDigits;
        byte* ptr = number.GetDigitsPointer();

        int num = number.Scale;
        if (*ptr == 0)
        {
            if (num > 0)
            {
                num = 0;
            }
        }
        else
        {
            if (num > 29)
            {
                return false;
            }

            while ((num > 0 || (*ptr != 0 && num > -28)) && (source.High < 429496729 || (source.High == 429496729 &&
                       (source.Mid < 2576980377u || (source.Mid == 2576980377u &&
                                                     (source.Low < 2576980377u ||
                                                      (source.Low == 2576980377u && *ptr <= 53)))))))
            {
                DecimalDecCalc.DecMul10(ref source);
                if (*ptr != 0)
                {
                    DecimalDecCalc.DecAddInt32(ref source, (uint)(*(ptr++) - 48));
                }

                num--;
            }

            if (*(ptr++) >= 53)
            {
                bool flag = true;
                if (*(ptr - 1) == 53 && *(ptr - 2) % 2 == 0)
                {
                    int num2 = 20;
                    while (*ptr == 48 && num2 != 0)
                    {
                        ptr++;
                        num2--;
                    }

                    if (*ptr == 0 || num2 == 0)
                    {
                        flag = false;
                    }
                }

                if (flag)
                {
                    DecimalDecCalc.DecAddInt32(ref source, 1u);
                    if ((source.High | source.Mid | source.Low) == 0)
                    {
                        source.High = 429496729u;
                        source.Mid = 2576980377u;
                        source.Low = 2576980378u;
                        num++;
                    }
                }
            }
        }

        if (num > 0)
        {
            return false;
        }

        if (num <= -29)
        {
            source.High = 0u;
            source.Low = 0u;
            source.Mid = 0u;
            source.Scale = 28;
        }
        else
        {
            source.Scale = -num;
        }

        source.IsNegative = number.IsNegative;
        value = Unsafe.As<MutableDecimal, decimal>(ref source);
        return true;
    }

    public static void DecimalToNumber(decimal value, ref NumberBuffer number)
    {
        ref MutableDecimal reference = ref Unsafe.As<decimal, MutableDecimal>(ref value);
        Span<byte> digits = number.Digits;
        number.IsNegative = reference.IsNegative;
        int num = 29;
        while ((reference.Mid != 0) | (reference.High != 0))
        {
            uint num2 = DecimalDecCalc.DecDivMod1E9(ref reference);
            for (int i = 0; i < 9; i++)
            {
                digits[--num] = (byte)(num2 % 10 + 48);
                num2 /= 10;
            }
        }

        for (uint num3 = reference.Low; num3 != 0; num3 /= 10)
        {
            digits[--num] = (byte)(num3 % 10 + 48);
        }

        int num4 = 29 - num;
        number.Scale = num4 - reference.Scale;
        Span<byte> digits2 = number.Digits;
        int index = 0;
        while (--num4 >= 0)
        {
            digits2[index++] = digits[num++];
        }

        digits2[index] = 0;
    }

    private static uint DigitsToInt(ReadOnlySpan<byte> digits, int count)
    {
        uint value;
        int bytesConsumed;
        bool flag = Utf8Parser.TryParse(digits.Slice(0, count), out value, out bytesConsumed, 'D');
        return value;
    }

    private static ulong Mul32x32To64(uint a, uint b)
    {
        return (ulong)a * (ulong)b;
    }

    private static ulong Mul64Lossy(ulong a, ulong b, ref int pexp)
    {
        ulong num = Mul32x32To64((uint)(a >> 32), (uint)(b >> 32)) + (Mul32x32To64((uint)(a >> 32), (uint)b) >> 32) +
                    (Mul32x32To64((uint)a, (uint)(b >> 32)) >> 32);
        if ((num & 0x8000000000000000uL) == 0)
        {
            num <<= 1;
            pexp--;
        }

        return num;
    }

    private static int abs(int value)
    {
        if (value < 0)
        {
            return -value;
        }

        return value;
    }

    private static unsafe double NumberToDouble(ref NumberBuffer number)
    {
        ReadOnlySpan<byte> digits = number.Digits;
        int i = 0;
        int numDigits = number.NumDigits;
        int num = numDigits;
        for (; digits[i] == 48; i++)
        {
            num--;
        }

        if (num == 0)
        {
            return 0.0;
        }

        int num3 = Math.Min(num, 9);
        num -= num3;
        ulong num4 = DigitsToInt(digits, num3);
        if (num > 0)
        {
            num3 = Math.Min(num, 9);
            num -= num3;
            uint b = (uint)(s_rgval64Power10[num3 - 1] >> 64 - s_rgexp64Power10[num3 - 1]);
            num4 = Mul32x32To64((uint)num4, b) + DigitsToInt(digits.Slice(9), num3);
        }

        int num5 = number.Scale - (numDigits - num);
        int num6 = abs(num5);
        if (num6 >= 352)
        {
            ulong num7 = ((num5 > 0) ? 9218868437227405312uL : 0);
            if (number.IsNegative)
            {
                num7 |= 0x8000000000000000uL;
            }

            return *(double*)(&num7);
        }

        int pexp = 64;
        if ((num4 & 0xFFFFFFFF00000000uL) == 0)
        {
            num4 <<= 32;
            pexp -= 32;
        }

        if ((num4 & 0xFFFF000000000000uL) == 0)
        {
            num4 <<= 16;
            pexp -= 16;
        }

        if ((num4 & 0xFF00000000000000uL) == 0)
        {
            num4 <<= 8;
            pexp -= 8;
        }

        if ((num4 & 0xF000000000000000uL) == 0)
        {
            num4 <<= 4;
            pexp -= 4;
        }

        if ((num4 & 0xC000000000000000uL) == 0)
        {
            num4 <<= 2;
            pexp -= 2;
        }

        if ((num4 & 0x8000000000000000uL) == 0)
        {
            num4 <<= 1;
            pexp--;
        }

        int num8 = num6 & 0xF;
        if (num8 != 0)
        {
            int num9 = s_rgexp64Power10[num8 - 1];
            pexp += ((num5 < 0) ? (-num9 + 1) : num9);
            ulong b2 = s_rgval64Power10[num8 + ((num5 < 0) ? 15 : 0) - 1];
            num4 = Mul64Lossy(num4, b2, ref pexp);
        }

        num8 = num6 >> 4;
        if (num8 != 0)
        {
            int num10 = s_rgexp64Power10By16[num8 - 1];
            pexp += ((num5 < 0) ? (-num10 + 1) : num10);
            ulong b3 = s_rgval64Power10By16[num8 + ((num5 < 0) ? 21 : 0) - 1];
            num4 = Mul64Lossy(num4, b3, ref pexp);
        }

        if (((uint)(int)num4 & 0x400u) != 0)
        {
            ulong num2 = num4 + 1023 + (ulong)(((int)num4 >> 11) & 1);
            if (num2 < num4)
            {
                num2 = (num2 >> 1) | 0x8000000000000000uL;
                pexp++;
            }

            num4 = num2;
        }

        pexp += 1022;
        num4 = ((pexp > 0)
            ? ((pexp < 2047) ? ((ulong)((long)pexp << 52) + ((num4 >> 11) & 0xFFFFFFFFFFFFFL)) : 9218868437227405312uL)
            : ((pexp == -52 && num4 >= 9223372036854775896uL) ? 1 : ((pexp > -52) ? (num4 >> -pexp + 11 + 1) : 0)));
        if (number.IsNegative)
        {
            num4 |= 0x8000000000000000uL;
        }

        return *(double*)(&num4);
    }
}