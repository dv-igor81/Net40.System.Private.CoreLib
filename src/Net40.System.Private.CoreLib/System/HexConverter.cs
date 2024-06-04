using System.Runtime.CompilerServices;

namespace System;

public static class HexConverter
{
	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static char ToCharLower(int value)
	{
		value &= 0xF;
		value += 48;
		if (value > 57)
		{
			value += 39;
		}
		return (char)value;
	}
}
