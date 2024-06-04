using System.Runtime.CompilerServices;

namespace SequenceReaderHelper;

internal class BitConverterNet
{
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
