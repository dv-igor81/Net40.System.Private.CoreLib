#define DEBUG
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Encodings.Web;

internal static class HexUtil
{
	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static char UInt32LsbToHexDigit(uint value)
	{
		Debug.Assert(value < 16);
		return (value < 10) ? ((char)(48 + value)) : ((char)(65 + (value - 10)));
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static char Int32LsbToHexDigit(int value)
	{
		Debug.Assert(value < 16);
		return (char)((value < 10) ? (48 + value) : (65 + (value - 10)));
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static void ByteToHexDigits(byte value, out char firstHexChar, out char secondHexChar)
	{
		firstHexChar = UInt32LsbToHexDigit((uint)value >> 4);
		secondHexChar = UInt32LsbToHexDigit(value & 0xFu);
	}
}
