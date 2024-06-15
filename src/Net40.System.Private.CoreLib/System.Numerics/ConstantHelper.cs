using System.Runtime.CompilerServices;

namespace System.Numerics;

internal class ConstantHelper
{
	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static byte GetByteWithAllBitsSet()
	{
		byte result = 0;
		return byte.MaxValue;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static sbyte GetSByteWithAllBitsSet()
	{
		sbyte result = 0;
		return -1;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static ushort GetUInt16WithAllBitsSet()
	{
		ushort result = 0;
		return ushort.MaxValue;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static short GetInt16WithAllBitsSet()
	{
		short result = 0;
		return -1;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static uint GetUInt32WithAllBitsSet()
	{
		uint result = 0u;
		return uint.MaxValue;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int GetInt32WithAllBitsSet()
	{
		int result = 0;
		return -1;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static ulong GetUInt64WithAllBitsSet()
	{
		ulong result = 0uL;
		return ulong.MaxValue;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static long GetInt64WithAllBitsSet()
	{
		long result = 0L;
		return -1L;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static unsafe float GetSingleWithAllBitsSet()
	{
		float result = 0f;
		*(int*)(&result) = -1;
		return result;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static unsafe double GetDoubleWithAllBitsSet()
	{
		double result = 0.0;
		*(long*)(&result) = -1L;
		return result;
	}
}
