using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace System.Net40;

internal static class UriHelper
{
    internal static readonly char[] s_hexUpperChars = new char[16]
    {
        '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
        'A', 'B', 'C', 'D', 'E', 'F'
    };

    internal static readonly Encoding s_noFallbackCharUTF8 = Encoding.GetEncoding(Encoding.UTF8.CodePage,
        new EncoderReplacementFallback(""), new DecoderReplacementFallback(""));

    internal static readonly char[] s_WSchars = new char[4] { ' ', '\n', '\r', '\t' };

    internal static unsafe bool TestForSubPath(char* selfPtr, ushort selfLength, char* otherPtr, ushort otherLength,
        bool ignoreCase)
    {
        ushort num = 0;
        bool flag = true;
        for (; num < selfLength && num < otherLength; num++)
        {
            char c = selfPtr[(int)num];
            char c2 = otherPtr[(int)num];
            switch (c)
            {
                case '#':
                case '?':
                    return true;
                case '/':
                    if (c2 != '/')
                    {
                        return false;
                    }

                    if (!flag)
                    {
                        return false;
                    }

                    flag = true;
                    continue;
                default:
                    if (c2 == '?' || c2 == '#')
                    {
                        break;
                    }

                    if (!ignoreCase)
                    {
                        if (c != c2)
                        {
                            flag = false;
                        }
                    }
                    else if (char.ToLowerInvariant(c) != char.ToLowerInvariant(c2))
                    {
                        flag = false;
                    }

                    continue;
            }

            break;
        }

        for (; num < selfLength; num++)
        {
            char c;
            if ((c = selfPtr[(int)num]) != '?')
            {
                switch (c)
                {
                    case '#':
                        break;
                    case '/':
                        return false;
                    default:
                        continue;
                }
            }

            return true;
        }

        return true;
    }

    [return: NotNullIfNotNull("dest")]
    internal static unsafe char[] EscapeString(string input, int start, int end, char[] dest, ref int destPos,
        bool isUriString, char force1, char force2, char rsvd)
    {
        if (end - start >= 65520)
        {
            throw new UriFormatException("SR.net_uri_SizeLimit");
        }

        int i = start;
        int num = start;
        byte* ptr = stackalloc byte[160];
        fixed (char* ptr2 = input)
        {
            for (; i < end; i++)
            {
                char c = ptr2[i];
                if (c > '\u007f')
                {
                    short num2 = (short)Math.Min(end - i, 39);
                    short num3 = 1;
                    while (num3 < num2 && ptr2[i + num3] > '\u007f')
                    {
                        num3++;
                    }

                    if (ptr2[i + num3 - 1] >= '\ud800' && ptr2[i + num3 - 1] <= '\udbff')
                    {
                        if (num3 == 1 || num3 == end - i)
                        {
                            throw new UriFormatException("SR.net_uri_BadString");
                        }

                        num3++;
                    }

                    dest = EnsureDestinationSize(ptr2, dest, i, (short)(num3 * 4 * 3), 480, ref destPos, num);
                    short num4 = (short)Encoding.UTF8.GetBytes(ptr2 + i, num3, ptr, 160);
                    if (num4 == 0)
                    {
                        throw new UriFormatException("SR.net_uri_BadString");
                    }

                    i += num3 - 1;
                    for (num3 = 0; num3 < num4; num3++)
                    {
                        EscapeAsciiChar((char)ptr[num3], dest, ref destPos);
                    }

                    num = i + 1;
                }
                else if (c == '%' && rsvd == '%')
                {
                    dest = EnsureDestinationSize(ptr2, dest, i, 3, 120, ref destPos, num);
                    if (i + 2 < end && EscapedAscii(ptr2[i + 1], ptr2[i + 2]) != '\uffff')
                    {
                        dest[destPos++] = '%';
                        dest[destPos++] = ptr2[i + 1];
                        dest[destPos++] = ptr2[i + 2];
                        i += 2;
                    }
                    else
                    {
                        EscapeAsciiChar('%', dest, ref destPos);
                    }

                    num = i + 1;
                }
                else if (c == force1 || c == force2)
                {
                    dest = EnsureDestinationSize(ptr2, dest, i, 3, 120, ref destPos, num);
                    EscapeAsciiChar(c, dest, ref destPos);
                    num = i + 1;
                }
                else if (c != rsvd && (isUriString ? (!IsReservedUnreservedOrHash(c)) : (!IsUnreserved(c))))
                {
                    dest = EnsureDestinationSize(ptr2, dest, i, 3, 120, ref destPos, num);
                    EscapeAsciiChar(c, dest, ref destPos);
                    num = i + 1;
                }
            }

            if (num != i && (num != start || dest != null))
            {
                dest = EnsureDestinationSize(ptr2, dest, i, 0, 0, ref destPos, num);
            }
        }

        return dest;
    }

    private static unsafe char[] EnsureDestinationSize(char* pStr, char[] dest, int currentInputPos, short charsToAdd,
        short minReallocateChars, ref int destPos, int prevInputPos)
    {
        if (dest == null || dest.Length < destPos + (currentInputPos - prevInputPos) + charsToAdd)
        {
            char[] array = new char[destPos + (currentInputPos - prevInputPos) + minReallocateChars];
            if (dest != null && destPos != 0)
            {
                Buffer.BlockCopy(dest, 0, array, 0, destPos << 1);
            }

            dest = array;
        }

        while (prevInputPos != currentInputPos)
        {
            dest[destPos++] = pStr[prevInputPos++];
        }

        return dest;
    }

    internal static unsafe char[] UnescapeString(string input, int start, int end, char[] dest, ref int destPosition,
        char rsvd1, char rsvd2, char rsvd3, UnescapeMode unescapeMode, UriParser syntax, bool isQuery)
    {
        fixed (char* pStr = input)
        {
            return UnescapeString(pStr, start, end, dest, ref destPosition, rsvd1, rsvd2, rsvd3, unescapeMode, syntax,
                isQuery);
        }
    }

    internal static unsafe char[] UnescapeString(char* pStr, int start, int end, char[] dest, ref int destPosition,
        char rsvd1, char rsvd2, char rsvd3, UnescapeMode unescapeMode, UriParser syntax, bool isQuery)
    {
        byte[] array = null;
        byte b = 0;
        bool flag = false;
        int i = start;
        bool flag2 = Uri.IriParsingStatic(syntax) &&
                     (unescapeMode & UnescapeMode.EscapeUnescape) == UnescapeMode.EscapeUnescape;
        char[] array2 = null;
        while (true)
        {
            fixed (char* ptr = dest)
            {
                if ((unescapeMode & UnescapeMode.EscapeUnescape) == 0)
                {
                    while (start < end)
                    {
                        ptr[destPosition++] = pStr[start++];
                    }

                    return dest;
                }

                while (true)
                {
                    char c = '\0';
                    for (; i < end; i++)
                    {
                        if ((c = pStr[i]) == '%')
                        {
                            if ((unescapeMode & UnescapeMode.Unescape) == 0)
                            {
                                flag = true;
                                break;
                            }

                            if (i + 2 < end)
                            {
                                c = EscapedAscii(pStr[i + 1], pStr[i + 2]);
                                if (unescapeMode < UnescapeMode.UnescapeAll)
                                {
                                    switch (c)
                                    {
                                        case '\uffff':
                                            if ((unescapeMode & UnescapeMode.Escape) == 0)
                                            {
                                                continue;
                                            }

                                            flag = true;
                                            break;
                                        case '%':
                                            i += 2;
                                            continue;
                                        default:
                                            if (c == rsvd1 || c == rsvd2 || c == rsvd3)
                                            {
                                                i += 2;
                                                continue;
                                            }

                                            if ((unescapeMode & UnescapeMode.V1ToStringFlag) == 0 &&
                                                IsNotSafeForUnescape(c))
                                            {
                                                i += 2;
                                                continue;
                                            }

                                            if (flag2 && ((c <= '\u009f' && IsNotSafeForUnescape(c)) ||
                                                          (c > '\u009f' &&
                                                           !IriHelper.CheckIriUnicodeRange(c, isQuery))))
                                            {
                                                i += 2;
                                                continue;
                                            }

                                            break;
                                    }

                                    break;
                                }

                                if (c != '\uffff')
                                {
                                    break;
                                }

                                if (unescapeMode >= UnescapeMode.UnescapeAllOrThrow)
                                {
                                    throw new UriFormatException("SR.net_uri_BadString");
                                }
                            }
                            else
                            {
                                if (unescapeMode < UnescapeMode.UnescapeAll)
                                {
                                    flag = true;
                                    break;
                                }

                                if (unescapeMode >= UnescapeMode.UnescapeAllOrThrow)
                                {
                                    throw new UriFormatException("SR.net_uri_BadString");
                                }
                            }
                        }
                        else if ((unescapeMode & (UnescapeMode.Unescape | UnescapeMode.UnescapeAll)) !=
                                 (UnescapeMode.Unescape | UnescapeMode.UnescapeAll) &&
                                 (unescapeMode & UnescapeMode.Escape) != 0)
                        {
                            if (c == rsvd1 || c == rsvd2 || c == rsvd3)
                            {
                                flag = true;
                                break;
                            }

                            if ((unescapeMode & UnescapeMode.V1ToStringFlag) == 0 &&
                                (c <= '\u001f' || (c >= '\u007f' && c <= '\u009f')))
                            {
                                flag = true;
                                break;
                            }
                        }
                    }

                    while (start < i)
                    {
                        ptr[destPosition++] = pStr[start++];
                    }

                    if (i != end)
                    {
                        if (flag)
                        {
                            if (b == 0)
                            {
                                break;
                            }

                            b--;
                            EscapeAsciiChar(pStr[i], dest, ref destPosition);
                            flag = false;
                            start = ++i;
                            continue;
                        }

                        if (c <= '\u007f')
                        {
                            dest[destPosition++] = c;
                            i += 3;
                            start = i;
                            continue;
                        }

                        int byteCount = 1;
                        if (array == null)
                        {
                            array = new byte[end - i];
                        }

                        array[0] = (byte)c;
                        for (i += 3; i < end; i += 3)
                        {
                            if ((c = pStr[i]) != '%')
                            {
                                break;
                            }

                            if (i + 2 >= end)
                            {
                                break;
                            }

                            c = EscapedAscii(pStr[i + 1], pStr[i + 2]);
                            if (c == '\uffff' || c < '\u0080')
                            {
                                break;
                            }

                            array[byteCount++] = (byte)c;
                        }

                        if (array2 == null || array2.Length < array.Length)
                        {
                            array2 = new char[array.Length];
                        }

                        int chars = s_noFallbackCharUTF8.GetChars(array, 0, byteCount, array2, 0);
                        start = i;
                        MatchUTF8Sequence(ptr, dest, ref destPosition, array2.AsSpan(0, chars), chars, array, byteCount,
                            isQuery, flag2);
                    }

                    if (i == end)
                    {
                        return dest;
                    }
                }

                b = 30;
                char[] array3 = new char[dest.Length + b * 3];
                fixed (char* ptr2 = &array3[0])
                {
                    for (int j = 0; j < destPosition; j++)
                    {
                        ptr2[j] = ptr[j];
                    }
                }

                dest = array3;
            }
        }
    }

        //
        // Need to check for invalid utf sequences that may not have given any chars.
        // We got the unescaped chars, we then re-encode them and match off the bytes
        // to get the invalid sequence bytes that we just copy off
        //
        internal static unsafe void MatchUTF8Sequence(char* pDest, char[] dest, ref int destOffset, Span<char> unescapedChars,
            int charCount, byte[] bytes, int byteCount, bool isQuery, bool iriParsing)
        {
            Span<byte> maxUtf8EncodedSpan = stackalloc byte[4];

            int count = 0;
            fixed (char* unescapedCharsPtr = unescapedChars)
            {
                for (int j = 0; j < charCount; ++j)
                {
                    bool isHighSurr = char.IsHighSurrogate(unescapedCharsPtr[j]);
                    Span<byte> encodedBytes = maxUtf8EncodedSpan;
                    int bytesWritten = Encoding.UTF8.GetBytes(unescapedChars.Slice(j, isHighSurr ? 2 : 1), encodedBytes);
                    encodedBytes = encodedBytes.Slice(0, bytesWritten);

                    // we have to keep unicode chars outside Iri range escaped
                    bool inIriRange = false;
                    if (iriParsing)
                    {
                        if (!isHighSurr)
                            inIriRange = IriHelper.CheckIriUnicodeRange(unescapedChars[j], isQuery);
                        else
                        {
                            bool surrPair = false;
                            inIriRange = IriHelper.CheckIriUnicodeRange(unescapedChars[j], unescapedChars[j + 1],
                                                                   ref surrPair, isQuery);
                        }
                    }

                    while (true)
                    {
                        // Escape any invalid bytes that were before this character
                        while (bytes[count] != encodedBytes[0])
                        {
                            Debug.Assert(dest.Length > destOffset, "Destination length exceeded destination offset.");
                            EscapeAsciiChar((char)bytes[count++], dest, ref destOffset);
                        }

                        // check if all bytes match
                        bool allBytesMatch = true;
                        int k = 0;
                        for (; k < encodedBytes.Length; ++k)
                        {
                            if (bytes[count + k] != encodedBytes[k])
                            {
                                allBytesMatch = false;
                                break;
                            }
                        }

                        if (allBytesMatch)
                        {
                            count += encodedBytes.Length;
                            if (iriParsing)
                            {
                                if (!inIriRange)
                                {
                                    // need to keep chars not allowed as escaped
                                    for (int l = 0; l < encodedBytes.Length; ++l)
                                    {
                                        Debug.Assert(dest.Length > destOffset, "Destination length exceeded destination offset.");
                                        EscapeAsciiChar((char)encodedBytes[l], dest, ref destOffset);
                                    }
                                }
                                else if (!UriHelper.IsBidiControlCharacter(unescapedCharsPtr[j]) || !UriParser.DontKeepUnicodeBidiFormattingCharacters)
                                {
                                    //copy chars
                                    Debug.Assert(dest.Length > destOffset, "Destination length exceeded destination offset.");
                                    pDest[destOffset++] = unescapedCharsPtr[j];
                                    if (isHighSurr)
                                    {
                                        Debug.Assert(dest.Length > destOffset, "Destination length exceeded destination offset.");
                                        pDest[destOffset++] = unescapedCharsPtr[j + 1];
                                    }
                                }
                            }
                            else
                            {
                                //copy chars
                                Debug.Assert(dest.Length > destOffset, "Destination length exceeded destination offset.");
                                pDest[destOffset++] = unescapedCharsPtr[j];

                                if (isHighSurr)
                                {
                                    Debug.Assert(dest.Length > destOffset, "Destination length exceeded destination offset.");
                                    pDest[destOffset++] = unescapedCharsPtr[j + 1];
                                }
                            }

                            break; // break out of while (true) since we've matched this char bytes
                        }
                        else
                        {
                            // copy bytes till place where bytes don't match
                            for (int l = 0; l < k; ++l)
                            {
                                Debug.Assert(dest.Length > destOffset, "Destination length exceeded destination offset.");
                                EscapeAsciiChar((char)bytes[count++], dest, ref destOffset);
                            }
                        }
                    }

                    if (isHighSurr) j++;
                }
            }

            // Include any trailing invalid sequences
            while (count < byteCount)
            {
                Debug.Assert(dest.Length > destOffset, "Destination length exceeded destination offset.");
                EscapeAsciiChar((char)bytes[count++], dest, ref destOffset);
            }
        }

    internal static void EscapeAsciiChar(char ch, char[] to, ref int pos)
    {
        to[pos++] = '%';
        to[pos++] = s_hexUpperChars[(ch & 0xF0) >> 4];
        to[pos++] = s_hexUpperChars[ch & 0xF];
    }

    internal static char EscapedAscii(char digit, char next)
    {
        if ((digit < '0' || digit > '9') && (digit < 'A' || digit > 'F') && (digit < 'a' || digit > 'f'))
        {
            return '\uffff';
        }

        int num = ((digit <= '9') ? (digit - 48) : (((digit <= 'F') ? (digit - 65) : (digit - 97)) + 10));
        if ((next < '0' || next > '9') && (next < 'A' || next > 'F') && (next < 'a' || next > 'f'))
        {
            return '\uffff';
        }

        return (char)((num << 4) + ((next <= '9') ? (next - 48) : (((next <= 'F') ? (next - 65) : (next - 97)) + 10)));
    }

    internal static bool IsNotSafeForUnescape(char ch)
    {
        if (ch <= '\u001f' || (ch >= '\u007f' && ch <= '\u009f'))
        {
            return true;
        }

        if (UriParser.DontEnableStrictRFC3986ReservedCharacterSets)
        {
            if ((ch != ':' && ";/?:@&=+$,".IndexOf(ch) >= 0) || "%\\#".IndexOf(ch) >= 0)
            {
                return true;
            }
        }
        else if (";/?:@&=+$,#[]!'()*".IndexOf(ch) >= 0 || "%\\#".IndexOf(ch) >= 0)
        {
            return true;
        }

        return false;
    }

    private static bool IsReservedUnreservedOrHash(char c)
    {
        if (IsUnreserved(c))
        {
            return true;
        }

        return ";/?:@&=+$,#[]!'()*".IndexOf(c) >= 0;
    }

    internal static bool IsUnreserved(char c)
    {
        if (IsAsciiLetterOrDigit(c))
        {
            return true;
        }

        return "-_.~".IndexOf(c) >= 0;
    }

    internal static bool Is3986Unreserved(char c)
    {
        if (IsAsciiLetterOrDigit(c))
        {
            return true;
        }

        return "-_.~".IndexOf(c) >= 0;
    }

    internal static bool IsGenDelim(char ch)
    {
        if (ch != ':' && ch != '/' && ch != '?' && ch != '#' && ch != '[' && ch != ']')
        {
            return ch == '@';
        }

        return true;
    }

    internal static bool IsLWS(char ch)
    {
        if (ch <= ' ')
        {
            if (ch != ' ' && ch != '\n' && ch != '\r')
            {
                return ch == '\t';
            }

            return true;
        }

        return false;
    }

    internal static bool IsAsciiLetter(char character)
    {
        if (character < 'a' || character > 'z')
        {
            if (character >= 'A')
            {
                return character <= 'Z';
            }

            return false;
        }

        return true;
    }

    internal static bool IsAsciiLetterOrDigit(char character)
    {
        if (!IsAsciiLetter(character))
        {
            if (character >= '0')
            {
                return character <= '9';
            }

            return false;
        }

        return true;
    }

    internal static bool IsBidiControlCharacter(char ch)
    {
        if (ch != '\u200e' && ch != '\u200f' && ch != '\u202a' && ch != '\u202b' && ch != '\u202c' && ch != '\u202d')
        {
            return ch == '\u202e';
        }

        return true;
    }

    internal static unsafe string StripBidiControlCharacter(char* strToClean, int start, int length)
    {
        if (length <= 0)
        {
            return "";
        }

        char[] array = new char[length];
        int length2 = 0;
        for (int i = 0; i < length; i++)
        {
            char c = strToClean[start + i];
            if (c < '\u200e' || c > '\u202e' || !IsBidiControlCharacter(c))
            {
                array[length2++] = c;
            }
        }

        return new string(array, 0, length2);
    }
}