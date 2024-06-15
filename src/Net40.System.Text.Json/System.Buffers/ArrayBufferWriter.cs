#define DEBUG
using System.Diagnostics;

namespace System.Buffers;

internal sealed class ArrayBufferWriter<T> : IBufferWriter<T>
{
	private T[] _buffer;

	private int _index;

	private const int DefaultInitialBufferSize = 256;

	public ReadOnlyMemory<T> WrittenMemory => _buffer.AsMemory(0, _index);

	public ReadOnlySpan<T> WrittenSpan => _buffer.AsSpan(0, _index);

	public int WrittenCount => _index;

	public int Capacity => _buffer.Length;

	public int FreeCapacity => _buffer.Length - _index;

	public ArrayBufferWriter()
	{
		_buffer = ArrayEx.Empty<T>();
		_index = 0;
	}

	public ArrayBufferWriter(int initialCapacity)
	{
		if (initialCapacity <= 0)
		{
			throw new ArgumentException("initialCapacity");
		}
		_buffer = new T[initialCapacity];
		_index = 0;
	}

	public void Clear()
	{
		Debug.Assert(_buffer.Length >= _index);
		_buffer.AsSpan(0, _index).Clear();
		_index = 0;
	}

	public void Advance(int count)
	{
		if (count < 0)
		{
			throw new ArgumentException("count");
		}
		if (_index > _buffer.Length - count)
		{
			ThrowInvalidOperationException_AdvancedTooFar(_buffer.Length);
		}
		_index += count;
	}

	public Memory<T> GetMemory(int sizeHint = 0)
	{
		CheckAndResizeBuffer(sizeHint);
		Debug.Assert(_buffer.Length > _index);
		return _buffer.AsMemory(_index);
	}

	public Span<T> GetSpan(int sizeHint = 0)
	{
		CheckAndResizeBuffer(sizeHint);
		Debug.Assert(_buffer.Length > _index);
		return _buffer.AsSpan(_index);
	}

	private void CheckAndResizeBuffer(int sizeHint)
	{
		if (sizeHint < 0)
		{
			throw new ArgumentException("sizeHint");
		}
		if (sizeHint == 0)
		{
			sizeHint = 1;
		}
		if (sizeHint > FreeCapacity)
		{
			int growBy = Math.Max(sizeHint, _buffer.Length);
			if (_buffer.Length == 0)
			{
				growBy = Math.Max(growBy, 256);
			}
			int newSize = checked(_buffer.Length + growBy);
			Array.Resize(ref _buffer, newSize);
		}
		Debug.Assert(FreeCapacity > 0 && FreeCapacity >= sizeHint);
	}

	private static void ThrowInvalidOperationException_AdvancedTooFar(int capacity)
	{
		throw new InvalidOperationException("SR.Format(SR.BufferWriterAdvancedTooFar, capacity)");
	}
}
