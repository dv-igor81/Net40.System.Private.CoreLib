using System.Globalization;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.IO;

namespace System.Net.Net40;

using SocketError = System.Net.Sockets.Net40.SocketError;
using SocketException = System.Net.Sockets.Net40.SocketException;

internal class IPAddressParser
{
    internal static IPAddress Parse(ReadOnlySpan<char> ipSpan, bool tryParse)
    {
            long address;
            if (ipSpan.Contains(':'))
            {
                Span<ushort> span = stackalloc ushort[8];
                span.Clear();
                if (Ipv6StringToAddress(ipSpan, span, 8, out var scope))
                {
                    return new IPAddress(span, scope);
                }
            }
            else if (Ipv4StringToAddress(ipSpan, out address))
            {
                return new IPAddress(address);
            }

            if (tryParse)
            {
                return null;
            }

            throw new FormatException(SR.dns_bad_ip_address, new SocketException(SocketError.InvalidArgument));
        }

    internal static unsafe string IPv4AddressToString(uint address)
    {
            char* ptr = stackalloc char[15];
            int length = IPv4AddressToStringHelper(address, ptr);
            return new string(ptr, 0, length);
        }

    internal static unsafe void IPv4AddressToString(uint address, StringBuilder destination)
    {
            char* ptr = stackalloc char[15];
            int valueCount = IPv4AddressToStringHelper(address, ptr);
            destination.Append(*ptr, valueCount);
        }

    internal static unsafe bool IPv4AddressToString(uint address, Span<char> formatted, out int charsWritten)
    {
            if (formatted.Length < 15)
            {
                charsWritten = 0;
                return false;
            }

            fixed (char* addressString = &MemoryMarshal.GetReference(formatted))
            {
                charsWritten = IPv4AddressToStringHelper(address, addressString);
            }

            return true;
        }

    private static unsafe int IPv4AddressToStringHelper(uint address, char* addressString)
    {
            int offset = 0;
            FormatIPv4AddressNumber((int)(address & 0xFF), addressString, ref offset);
            addressString[offset++] = '.';
            FormatIPv4AddressNumber((int)((address >> 8) & 0xFF), addressString, ref offset);
            addressString[offset++] = '.';
            FormatIPv4AddressNumber((int)((address >> 16) & 0xFF), addressString, ref offset);
            addressString[offset++] = '.';
            FormatIPv4AddressNumber((int)((address >> 24) & 0xFF), addressString, ref offset);
            return offset;
        }

    internal static string IPv6AddressToString(ushort[] address, uint scopeId)
    {
            StringBuilder sb = IPv6AddressToStringHelper(address, scopeId);
            return StringBuilderCache.GetStringAndRelease(sb);
        }

    internal static bool IPv6AddressToString(ushort[] address, uint scopeId, Span<char> destination,
        out int charsWritten)
    {
            StringBuilder stringBuilder = IPv6AddressToStringHelper(address, scopeId);
            if (destination.Length < stringBuilder.Length)
            {
                StringBuilderCache.Release(stringBuilder);
                charsWritten = 0;
                return false;
            }

            stringBuilder.CopyTo(0, destination, stringBuilder.Length);
            charsWritten = stringBuilder.Length;
            StringBuilderCache.Release(stringBuilder);
            return true;
        }

    internal static StringBuilder IPv6AddressToStringHelper(ushort[] address, uint scopeId)
    {
            StringBuilder stringBuilder = StringBuilderCache.Acquire(65);
            if (IPv6AddressHelper.ShouldHaveIpv4Embedded(address))
            {
                AppendSections(address, 0, 6, stringBuilder);
                if (stringBuilder[stringBuilder.Length - 1] != ':')
                {
                    stringBuilder.Append(':');
                }

                IPv4AddressToString(ExtractIPv4Address(address), stringBuilder);
            }
            else
            {
                AppendSections(address, 0, 8, stringBuilder);
            }

            if (scopeId != 0)
            {
                stringBuilder.Append('%').Append(scopeId);
            }

            return stringBuilder;
        }

    private static unsafe void FormatIPv4AddressNumber(int number, char* addressString, ref int offset)
    {
            offset += ((number > 99) ? 3 : ((number <= 9) ? 1 : 2));
            int num = offset;
            do
            {
                number = Math.DivRem(number, 10, out var result);
                addressString[--num] = (char)(48 + result);
            } while (number != 0);
        }

    public static unsafe bool Ipv4StringToAddress(ReadOnlySpan<char> ipSpan, out long address)
    {
            int end = ipSpan.Length;
            long num;
            fixed (char* name = &MemoryMarshal.GetReference(ipSpan))
            {
                num = IPv4AddressHelper.ParseNonCanonical(name, 0, ref end, notImplicitFile: true);
            }

            if (num != -1 && end == ipSpan.Length)
            {
                address = ((0xFF000000u & num) >> 24) | ((0xFF0000 & num) >> 8) | ((0xFF00 & num) << 8) |
                          ((0xFF & num) << 24);
                return true;
            }

            address = 0L;
            return false;
        }

    public static unsafe bool Ipv6StringToAddress(ReadOnlySpan<char> ipSpan, Span<ushort> numbers,
        int numbersLength, out uint scope)
    {
            int end = ipSpan.Length;
            bool flag = false;
            fixed (char* name = &MemoryMarshal.GetReference(ipSpan))
            {
                flag = IPv6AddressHelper.IsValidStrict(name, 0, ref end);
            }

            if (flag || end != ipSpan.Length)
            {
                string scopeId = null;
                IPv6AddressHelper.Parse(ipSpan, numbers, 0, ref scopeId);
                if (scopeId != null && scopeId.Length > 1)
                {
                    if (uint.TryParse(scopeId.AsSpan(1).ToString(), NumberStyles.None, CultureInfo.InvariantCulture,
                            out scope))
                    {
                        return true;
                    }

                    uint num = InterfaceInfoPal.InterfaceNameToIndex(scopeId);
                    if (num != 0)
                    {
                        scope = num;
                        return true;
                    }
                }

                scope = 0u;
                return true;
            }

            scope = 0u;
            return false;
        }

    private static void AppendSections(ushort[] address, int fromInclusive, int toExclusive, StringBuilder buffer)
    {
            ReadOnlySpan<ushort> numbers =
                new ReadOnlySpan<ushort>(address, fromInclusive, toExclusive - fromInclusive);
            (int longestSequenceStart, int longestSequenceLength) tuple =
                IPv6AddressHelper.FindCompressionRange(numbers);
            int item = tuple.longestSequenceStart;
            int item2 = tuple.longestSequenceLength;
            bool flag = false;
            for (int i = fromInclusive; i < item; i++)
            {
                if (flag)
                {
                    buffer.Append(':');
                }

                flag = true;
                AppendHex(address[i], buffer);
            }

            if (item >= 0)
            {
                buffer.Append("::");
                flag = false;
                fromInclusive = item2;
            }

            for (int j = fromInclusive; j < toExclusive; j++)
            {
                if (flag)
                {
                    buffer.Append(':');
                }

                flag = true;
                AppendHex(address[j], buffer);
            }
        }

    private static unsafe void AppendHex(ushort value, StringBuilder buffer)
    {
            char* ptr = stackalloc char[4];
            int num = 4;
            do
            {
                int num2 = value % 16;
                value /= 16;
                ptr[--num] = ((num2 < 10) ? ((char)(48 + num2)) : ((char)(97 + (num2 - 10))));
            } while (value != 0);

            buffer.Append(*(ptr + num), 4 - num);
        }

    private static uint ExtractIPv4Address(ushort[] address)
    {
            return (uint)((Reverse(address[7]) << 16) | Reverse(address[6]));
        }

    private static ushort Reverse(ushort number)
    {
            return (ushort)(((uint)(number >> 8) & 0xFFu) | ((uint)(number << 8) & 0xFF00u));
        }
}