using System.Buffers;
using System.Runtime.CompilerServices;

namespace System.IO.Pipelines;

internal sealed class BufferSegment : ReadOnlySequenceSegment<byte>
{
	private object _memoryOwner;

	private BufferSegment _next;

	private int _end;

	public int End
	{
		get => _end;
		set
		{
			_end = value;
			Memory = AvailableMemory.Slice(0, value);
		}
	}

	public BufferSegment NextSegment
	{
		get => _next;
		set
		{
			Next = value;
			_next = value;
		}
	}

	internal object MemoryOwner => _memoryOwner;

	public Memory<byte> AvailableMemory { get; private set; }

	public int Length => End;

	public int WritableBytes
	{
		[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
		get => AvailableMemory.Length - End;
	}

	public void SetOwnedMemory(IMemoryOwner<byte> memoryOwner)
	{
		_memoryOwner = memoryOwner;
		AvailableMemory = memoryOwner.Memory;
	}

	public void SetOwnedMemory(byte[] arrayPoolBuffer)
	{
		_memoryOwner = arrayPoolBuffer;
		AvailableMemory = arrayPoolBuffer;
	}

	public void SetUnownedMemory(Memory<byte> memory)
	{
		AvailableMemory = memory;
	}

	public void ResetMemory()
	{
		if (_memoryOwner is IMemoryOwner<byte> memoryOwner)
		{
			memoryOwner.Dispose();
		}
		else if (_memoryOwner is byte[] array)
		{
			ArrayPool<byte>.Shared.Return(array);
		}
		Next = null;
		RunningIndex = 0L;
		Memory = default(ReadOnlyMemory<byte>);
		_memoryOwner = null;
		_next = null;
		_end = 0;
		AvailableMemory = default(Memory<byte>);
	}

	public void SetNext(BufferSegment segment)
	{
		NextSegment = segment;
		segment = this;
		while (segment.Next != null)
		{
			segment.NextSegment.RunningIndex = segment.RunningIndex + segment.Length;
			segment = segment.NextSegment;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static long GetLength(BufferSegment startSegment, int startIndex, BufferSegment endSegment, int endIndex)
	{
		return endSegment.RunningIndex + (uint)endIndex - (startSegment.RunningIndex + (uint)startIndex);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static long GetLength(long startPosition, BufferSegment endSegment, int endIndex)
	{
		return endSegment.RunningIndex + (uint)endIndex - startPosition;
	}
}
