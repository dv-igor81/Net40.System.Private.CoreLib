using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SequenceReaderHelper;

public class BinaryPrimitivesNet
{
	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float ReadSingleLittleEndian(ReadOnlySpan<byte> source)
	{
		return (!BitConverter.IsLittleEndian) ? BitConverterNet.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(MemoryMarshal.Read<int>(source))) : MemoryMarshal.Read<float>(source);
	}

	public static bool TryReadSingleLittleEndian(ReadOnlySpan<byte> source, out float value)
	{
		if (!BitConverter.IsLittleEndian)
		{
			int tmp;
			bool success = MemoryMarshal.TryRead<int>(source, out tmp);
			value = BitConverterNet.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(tmp));
			return success;
		}
		return MemoryMarshal.TryRead<float>(source, out value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static double ReadDoubleLittleEndian(ReadOnlySpan<byte> source)
	{
		return (!BitConverter.IsLittleEndian) ? BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(MemoryMarshal.Read<long>(source))) : MemoryMarshal.Read<double>(source);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool TryReadDoubleLittleEndian(ReadOnlySpan<byte> source, out double value)
	{
		if (!BitConverter.IsLittleEndian)
		{
			long tmp;
			bool success = MemoryMarshal.TryRead<long>(source, out tmp);
			value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(tmp));
			return success;
		}
		return MemoryMarshal.TryRead<double>(source, out value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void WriteSingleLittleEndian(Span<byte> destination, float value)
	{
		if (!BitConverter.IsLittleEndian)
		{
			int tmp = BinaryPrimitives.ReverseEndianness(BitConverterNet.SingleToInt32Bits(value));
			MemoryMarshal.Write(destination, ref tmp);
		}
		else
		{
			MemoryMarshal.Write(destination, ref value);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool TryWriteSingleLittleEndian(Span<byte> destination, float value)
	{
		if (!BitConverter.IsLittleEndian)
		{
			int tmp = BinaryPrimitives.ReverseEndianness(BitConverterNet.SingleToInt32Bits(value));
			return MemoryMarshal.TryWrite(destination, ref tmp);
		}
		return MemoryMarshal.TryWrite(destination, ref value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void WriteDoubleLittleEndian(Span<byte> destination, double value)
	{
		if (!BitConverter.IsLittleEndian)
		{
			long tmp = BinaryPrimitives.ReverseEndianness(BitConverter.DoubleToInt64Bits(value));
			MemoryMarshal.Write(destination, ref tmp);
		}
		else
		{
			MemoryMarshal.Write(destination, ref value);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool TryWriteDoubleLittleEndian(Span<byte> destination, double value)
	{
		if (!BitConverter.IsLittleEndian)
		{
			long tmp = BinaryPrimitives.ReverseEndianness(BitConverter.DoubleToInt64Bits(value));
			return MemoryMarshal.TryWrite(destination, ref tmp);
		}
		return MemoryMarshal.TryWrite(destination, ref value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float ReadSingleBigEndian(ReadOnlySpan<byte> source)
	{
		return BitConverter.IsLittleEndian ? BitConverterNet.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(MemoryMarshal.Read<int>(source))) : MemoryMarshal.Read<float>(source);
	}

	public static bool TryReadSingleBigEndian(ReadOnlySpan<byte> source, out float value)
	{
		if (BitConverter.IsLittleEndian)
		{
			int tmp;
			bool success = MemoryMarshal.TryRead<int>(source, out tmp);
			value = BitConverterNet.Int32BitsToSingle(BinaryPrimitives.ReverseEndianness(tmp));
			return success;
		}
		return MemoryMarshal.TryRead<float>(source, out value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static double ReadDoubleBigEndian(ReadOnlySpan<byte> source)
	{
		return BitConverter.IsLittleEndian ? BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(MemoryMarshal.Read<long>(source))) : MemoryMarshal.Read<double>(source);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool TryReadDoubleBigEndian(ReadOnlySpan<byte> source, out double value)
	{
		if (BitConverter.IsLittleEndian)
		{
			long tmp;
			bool success = MemoryMarshal.TryRead<long>(source, out tmp);
			value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReverseEndianness(tmp));
			return success;
		}
		return MemoryMarshal.TryRead<double>(source, out value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void WriteSingleBigEndian(Span<byte> destination, float value)
	{
		if (BitConverter.IsLittleEndian)
		{
			int tmp = BinaryPrimitives.ReverseEndianness(BitConverterNet.SingleToInt32Bits(value));
			MemoryMarshal.Write(destination, ref tmp);
		}
		else
		{
			MemoryMarshal.Write(destination, ref value);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool TryWriteSingleBigEndian(Span<byte> destination, float value)
	{
		if (BitConverter.IsLittleEndian)
		{
			int tmp = BinaryPrimitives.ReverseEndianness(BitConverterNet.SingleToInt32Bits(value));
			return MemoryMarshal.TryWrite(destination, ref tmp);
		}
		return MemoryMarshal.TryWrite(destination, ref value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static void WriteDoubleBigEndian(Span<byte> destination, double value)
	{
		if (BitConverter.IsLittleEndian)
		{
			long tmp = BinaryPrimitives.ReverseEndianness(BitConverter.DoubleToInt64Bits(value));
			MemoryMarshal.Write(destination, ref tmp);
		}
		else
		{
			MemoryMarshal.Write(destination, ref value);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static bool TryWriteDoubleBigEndian(Span<byte> destination, double value)
	{
		if (BitConverter.IsLittleEndian)
		{
			long tmp = BinaryPrimitives.ReverseEndianness(BitConverter.DoubleToInt64Bits(value));
			return MemoryMarshal.TryWrite(destination, ref tmp);
		}
		return MemoryMarshal.TryWrite(destination, ref value);
	}
}
