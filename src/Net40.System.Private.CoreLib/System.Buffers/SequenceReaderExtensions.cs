using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace System.Buffers;

public static class SequenceReaderExtensions
{
	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static unsafe bool TryCopyTo<T>(this ref SequenceReader<byte> reader, ref Span<byte> dest) where T : unmanaged
	{
		if (reader.Length < sizeof(T))
		{
			dest = default(Span<byte>);
			return false;
		}
		if (!reader.TryCopyTo(dest))
		{
			dest = default(Span<byte>);
			return false;
		}
		return true;
	}

	public static bool TryReadLittleEndian(this ref SequenceReader<byte> reader, out ushort value)
	{
		short val;
		bool passed = reader.TryReadLittleEndian(out val);
		value = (ushort)val;
		return passed;
	}

	public static bool TryReadBigEndian(this ref SequenceReader<byte> reader, out ushort value)
	{
		short val;
		bool passed = reader.TryReadBigEndian(out val);
		value = (ushort)val;
		return passed;
	}

	public static unsafe bool TryReadLittleEndian(this ref SequenceReader<byte> reader, out short value)
	{
		short temp = 0;
		Span<byte> buffer = new Span<byte>(&temp, 2);
		if (!reader.TryCopyTo<short>(ref buffer))
		{
			value = 0;
			return false;
		}
		reader.Advance(2L);
		value = BinaryPrimitives.ReadInt16LittleEndian(buffer);
		return true;
	}

	public static unsafe bool TryReadBigEndian(this ref SequenceReader<byte> reader, out short value)
	{
		short temp = 0;
		Span<byte> buffer = new Span<byte>(&temp, 2);
		if (!reader.TryCopyTo<short>(ref buffer))
		{
			value = 0;
			return false;
		}
		reader.Advance(2L);
		value = BinaryPrimitives.ReadInt16BigEndian(buffer);
		return true;
	}

	public static bool TryReadLittleEndian(this ref SequenceReader<byte> reader, out uint value)
	{
		int val;
		bool passed = reader.TryReadLittleEndian(out val);
		value = (uint)val;
		return passed;
	}

	public static bool TryReadBigEndian(this ref SequenceReader<byte> reader, out uint value)
	{
		int val;
		bool passed = reader.TryReadBigEndian(out val);
		value = (uint)val;
		return passed;
	}

	public static unsafe bool TryReadLittleEndian(this ref SequenceReader<byte> reader, out int value)
	{
		int temp = 0;
		Span<byte> buffer = new Span<byte>(&temp, 4);
		if (!reader.TryCopyTo<int>(ref buffer))
		{
			value = 0;
			return false;
		}
		reader.Advance(4L);
		value = BinaryPrimitives.ReadInt32LittleEndian(buffer);
		return true;
	}

	public static unsafe bool TryReadBigEndian(this ref SequenceReader<byte> reader, out int value)
	{
		int temp = 0;
		Span<byte> buffer = new Span<byte>(&temp, 4);
		if (!reader.TryCopyTo<int>(ref buffer))
		{
			value = 0;
			return false;
		}
		reader.Advance(4L);
		value = BinaryPrimitives.ReadInt32BigEndian(buffer);
		return true;
	}

	public static bool TryReadLittleEndian(this ref SequenceReader<byte> reader, out ulong value)
	{
		long val;
		bool passed = reader.TryReadLittleEndian(out val);
		value = (ulong)val;
		return passed;
	}

	public static bool TryReadBigEndian(this ref SequenceReader<byte> reader, out ulong value)
	{
		long val;
		bool passed = reader.TryReadBigEndian(out val);
		value = (ulong)val;
		return passed;
	}

	public static unsafe bool TryReadLittleEndian(this ref SequenceReader<byte> reader, out long value)
	{
		long temp = 0L;
		Span<byte> buffer = new Span<byte>(&temp, 8);
		if (!reader.TryCopyTo<long>(ref buffer))
		{
			value = 0L;
			return false;
		}
		reader.Advance(8L);
		value = BinaryPrimitives.ReadInt64LittleEndian(buffer);
		return true;
	}

	public static unsafe bool TryReadBigEndian(this ref SequenceReader<byte> reader, out long value)
	{
		long temp = 0L;
		Span<byte> buffer = new Span<byte>(&temp, 8);
		if (!reader.TryCopyTo<long>(ref buffer))
		{
			value = 0L;
			return false;
		}
		reader.Advance(8L);
		value = BinaryPrimitives.ReadInt64BigEndian(buffer);
		return true;
	}

	public static unsafe bool TryReadLittleEndian(this ref SequenceReader<byte> reader, out float value)
	{
		float temp = 0f;
		Span<byte> buffer = new Span<byte>(&temp, 4);
		if (!reader.TryCopyTo<float>(ref buffer))
		{
			value = 0f;
			return false;
		}
		reader.Advance(4L);
		value = BinaryPrimitivesEx.ReadSingleLittleEndian(buffer);
		return true;
	}

	public static unsafe bool TryReadBigEndian(this ref SequenceReader<byte> reader, out float value)
	{
		float temp = 0f;
		Span<byte> buffer = new Span<byte>(&temp, 4);
		if (!reader.TryCopyTo<float>(ref buffer))
		{
			value = 0f;
			return false;
		}
		reader.Advance(4L);
		value = BinaryPrimitivesEx.ReadSingleBigEndian(buffer);
		return true;
	}

	public static unsafe bool TryReadLittleEndian(this ref SequenceReader<byte> reader, out double value)
	{
		double temp = 0.0;
		Span<byte> buffer = new Span<byte>(&temp, 8);
		if (!reader.TryCopyTo<double>(ref buffer))
		{
			value = 0.0;
			return false;
		}
		reader.Advance(4L);
		value = BinaryPrimitivesEx.ReadDoubleLittleEndian(buffer);
		return true;
	}

	public static unsafe bool TryReadBigEndian(this ref SequenceReader<byte> reader, out double value)
	{
		double temp = 0.0;
		Span<byte> buffer = new Span<byte>(&temp, 8);
		if (!reader.TryCopyTo<double>(ref buffer))
		{
			value = 0.0;
			return false;
		}
		reader.Advance(4L);
		value = BinaryPrimitivesEx.ReadDoubleBigEndian(buffer);
		return true;
	}

	public static void GetFirstSpan<T>(this ReadOnlySequence<T> sequence, out ReadOnlySpan<T> first, out SequencePosition next)
	{
		first = sequence.First.Span;
		next = sequence.GetPosition(first.Length);
	}
	
	public static ReadOnlySpan<T> GetFirstSpan<T>(this ReadOnlySequence<T> sequence)
	{
		return sequence.First.Span;
	}
}
