#define DEBUG
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json;

internal sealed class PooledByteBufferWriter : IBufferWriter<byte>, IDisposable
{
	private byte[] _rentedBuffer;

	private int _index;

	private const int MinimumBufferSize = 256;

	public ReadOnlyMemory<byte> WrittenMemory
	{
		get
		{
			Debug.Assert(_rentedBuffer != null);
			Debug.Assert(_index <= _rentedBuffer.Length);
			return _rentedBuffer.AsMemory(0, _index);
		}
	}

	public int WrittenCount
	{
		get
		{
			Debug.Assert(_rentedBuffer != null);
			return _index;
		}
	}

	public int Capacity
	{
		get
		{
			Debug.Assert(_rentedBuffer != null);
			return _rentedBuffer.Length;
		}
	}

	public int FreeCapacity
	{
		get
		{
			Debug.Assert(_rentedBuffer != null);
			return _rentedBuffer.Length - _index;
		}
	}

	public PooledByteBufferWriter(int initialCapacity)
	{
		Debug.Assert(initialCapacity > 0);
		_rentedBuffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
		_index = 0;
	}

	public void Clear()
	{
		ClearHelper();
	}

	private void ClearHelper()
	{
		Debug.Assert(_rentedBuffer != null);
		Debug.Assert(_index <= _rentedBuffer.Length);
		_rentedBuffer.AsSpan(0, _index).Clear();
		_index = 0;
	}

	public void Dispose()
	{
		if (_rentedBuffer != null)
		{
			ClearHelper();
			ArrayPool<byte>.Shared.Return(_rentedBuffer);
			_rentedBuffer = null;
		}
	}

	public void Advance(int count)
	{
		Debug.Assert(_rentedBuffer != null);
		Debug.Assert(count >= 0);
		Debug.Assert(_index <= _rentedBuffer.Length - count);
		_index += count;
	}

	public Memory<byte> GetMemory(int sizeHint = 0)
	{
		CheckAndResizeBuffer(sizeHint);
		return _rentedBuffer.AsMemory(_index);
	}

	public Span<byte> GetSpan(int sizeHint = 0)
	{
		CheckAndResizeBuffer(sizeHint);
		return _rentedBuffer.AsSpan(_index);
	}

	internal Task WriteToStreamAsync(Stream destination, CancellationToken cancellationToken)
	{
		return StreamTheraotExtensions.WriteAsync(destination, _rentedBuffer, 0, _index, cancellationToken);
	}

	private void CheckAndResizeBuffer(int sizeHint)
	{
		Debug.Assert(_rentedBuffer != null);
		Debug.Assert(sizeHint >= 0);
		if (sizeHint == 0)
		{
			sizeHint = 256;
		}
		int availableSpace = _rentedBuffer.Length - _index;
		if (sizeHint > availableSpace)
		{
			int growBy = Math.Max(sizeHint, _rentedBuffer.Length);
			int newSize = checked(_rentedBuffer.Length + growBy);
			byte[] oldBuffer = _rentedBuffer;
			_rentedBuffer = ArrayPool<byte>.Shared.Rent(newSize);
			Debug.Assert(oldBuffer.Length >= _index);
			Debug.Assert(_rentedBuffer.Length >= _index);
			Span<byte> previousBuffer = oldBuffer.AsSpan(0, _index);
			previousBuffer.CopyTo(_rentedBuffer);
			previousBuffer.Clear();
			ArrayPool<byte>.Shared.Return(oldBuffer);
		}
		Debug.Assert(_rentedBuffer.Length - _index > 0);
		Debug.Assert(_rentedBuffer.Length - _index >= sizeHint);
	}
}
