using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System;

public class BitConverterEx
{
	public static bool TryWriteBytes(Span<byte> destination, uint value)
	{
		if (destination.Length < 4)
		{
			return false;
		}
		Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), value);
		return true;
	}
	
	public static bool TryWriteBytes(Span<byte> destination, int value)
	{
		if (destination.Length < 4)
		{
			return false;
		}
		Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), value);
		return true;
	}

	
	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static unsafe int SingleToInt32Bits(float value)
	{
		return *(int*)(&value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static unsafe float Int32BitsToSingle(int value)
	{
		return *(float*)(&value);
	}
}
