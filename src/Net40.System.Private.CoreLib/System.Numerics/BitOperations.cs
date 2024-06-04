using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Numerics;

public static class BitOperations
{
	private static ReadOnlySpan<byte> s_TrailingZeroCountDeBruijn => new byte[32]
	{
		0, 1, 28, 2, 29, 14, 24, 3, 30, 22,
		20, 15, 25, 17, 4, 8, 31, 27, 13, 23,
		21, 19, 16, 7, 26, 12, 18, 6, 11, 5,
		10, 9
	};

	private static ReadOnlySpan<byte> s_Log2DeBruijn => new byte[32]
	{
		0, 9, 1, 10, 13, 21, 2, 29, 11, 14,
		16, 18, 22, 25, 3, 30, 8, 12, 20, 28,
		15, 17, 24, 7, 19, 27, 23, 6, 26, 5,
		4, 31
	};

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static int LeadingZeroCount(uint value)
	{
		if (value == 0)
		{
			return 32;
		}
		return 31 - Log2SoftwareFallback(value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static int LeadingZeroCount(ulong value)
	{
		uint hi = (uint)(value >> 32);
		if (hi == 0)
		{
			return 32 + LeadingZeroCount((uint)value);
		}
		return LeadingZeroCount(hi);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static int Log2(uint value)
	{
		return Log2SoftwareFallback(value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static int Log2(ulong value)
	{
		uint hi = (uint)(value >> 32);
		if (hi == 0)
		{
			return Log2((uint)value);
		}
		return 32 + Log2(hi);
	}

	private static int Log2SoftwareFallback(uint value)
	{
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		return Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(s_Log2DeBruijn), (IntPtr)(int)(value * 130329821 >> 27));
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static int PopCount(uint value)
	{
		return SoftwareFallback(value);
		static int SoftwareFallback(uint value)
		{
			value -= (value >> 1) & 0x55555555;
			value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
			value = ((value + (value >> 4)) & 0xF0F0F0F) * 16843009 >> 24;
			return (int)value;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static int PopCount(ulong value)
	{
		return SoftwareFallback(value);
		static int SoftwareFallback(ulong value)
		{
			value -= (value >> 1) & 0x5555555555555555L;
			value = (value & 0x3333333333333333L) + ((value >> 2) & 0x3333333333333333L);
			value = ((value + (value >> 4)) & 0xF0F0F0F0F0F0F0FL) * 72340172838076673L >> 56;
			return (int)value;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int TrailingZeroCount(int value)
	{
		return TrailingZeroCount((uint)value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static int TrailingZeroCount(uint value)
	{
		if (value == 0)
		{
			return 32;
		}
		return Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(s_TrailingZeroCountDeBruijn), (IntPtr)(int)((value & (0 - value)) * 125613361 >> 27));
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int TrailingZeroCount(long value)
	{
		return TrailingZeroCount((ulong)value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static int TrailingZeroCount(ulong value)
	{
		uint lo = (uint)value;
		if (lo == 0)
		{
			return 32 + TrailingZeroCount((uint)(value >> 32));
		}
		return TrailingZeroCount(lo);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static uint RotateLeft(uint value, int offset)
	{
		return (value << offset) | (value >> 32 - offset);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static ulong RotateLeft(ulong value, int offset)
	{
		return (value << offset) | (value >> 64 - offset);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static uint RotateRight(uint value, int offset)
	{
		return (value >> offset) | (value << 32 - offset);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static ulong RotateRight(ulong value, int offset)
	{
		return (value >> offset) | (value << 64 - offset);
	}
}
