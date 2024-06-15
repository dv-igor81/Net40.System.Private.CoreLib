using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Buffers;

[DebuggerTypeProxy(typeof(ReadOnlySequenceDebugView<>))]
[DebuggerDisplay("{ToString(),raw}")]
public readonly struct ReadOnlySequence<T>
{
	public struct Enumerator
	{
		private readonly ReadOnlySequence<T> _sequence;

		private SequencePosition _next;

		private ReadOnlyMemory<T> _currentMemory;

		public ReadOnlyMemory<T> Current => _currentMemory;
		
		public Enumerator(in ReadOnlySequence<T> sequence)
		{
			_currentMemory = default(ReadOnlyMemory<T>);
			_next = sequence.Start;
			_sequence = sequence;
		}

		public bool MoveNext()
		{
			if (_next.GetObject() == null)
			{
				return false;
			}
			return _sequence.TryGet(ref _next, out _currentMemory);
		}
	}

	private enum SequenceType
	{
		MultiSegment,
		Array,
		MemoryManager,
		String,
		Empty
	}

	private readonly SequencePosition _sequenceStart;

	private readonly SequencePosition _sequenceEnd;

	public static readonly ReadOnlySequence<T> Empty = new ReadOnlySequence<T>(SpanHelpers.PerTypeValues<T>.EmptyArray);
	
	public ReadOnlySpan<T> FirstSpan => this.GetFirstSpan();

	public long Length => GetLength();

	public bool IsEmpty => Length == 0;

	public bool IsSingleSegment
	{
		[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
		get
		{
			return _sequenceStart.GetObject() == _sequenceEnd.GetObject();
		}
	}

	public ReadOnlyMemory<T> First => GetFirstBuffer();

	public SequencePosition Start => _sequenceStart;

	public SequencePosition End => _sequenceEnd;

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private ReadOnlySequence(object startSegment, int startIndexAndFlags, object endSegment, int endIndexAndFlags)
	{
		_sequenceStart = new SequencePosition(startSegment, startIndexAndFlags);
		_sequenceEnd = new SequencePosition(endSegment, endIndexAndFlags);
	}

	public ReadOnlySequence(ReadOnlySequenceSegment<T> startSegment, int startIndex, ReadOnlySequenceSegment<T> endSegment, int endIndex)
	{
		if (startSegment == null || endSegment == null || (startSegment != endSegment && startSegment.RunningIndex > endSegment.RunningIndex) || (uint)startSegment.Memory.Length < (uint)startIndex || (uint)endSegment.Memory.Length < (uint)endIndex || (startSegment == endSegment && endIndex < startIndex))
		{
			ThrowHelper.ThrowArgumentValidationException(startSegment, startIndex, endSegment);
		}
		_sequenceStart = new SequencePosition(startSegment, ReadOnlySequence.SegmentToSequenceStart(startIndex));
		_sequenceEnd = new SequencePosition(endSegment, ReadOnlySequence.SegmentToSequenceEnd(endIndex));
	}

	public ReadOnlySequence(T[] array)
	{
		if (array == null)
		{
			ThrowHelper.ThrowArgumentNullException(ExceptionArgument.array);
		}
		_sequenceStart = new SequencePosition(array, ReadOnlySequence.ArrayToSequenceStart(0));
		_sequenceEnd = new SequencePosition(array, ReadOnlySequence.ArrayToSequenceEnd(array.Length));
	}

	public ReadOnlySequence(T[] array, int start, int length)
	{
		if (array == null || (uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
		{
			ThrowHelper.ThrowArgumentValidationException(array, start);
		}
		_sequenceStart = new SequencePosition(array, ReadOnlySequence.ArrayToSequenceStart(start));
		_sequenceEnd = new SequencePosition(array, ReadOnlySequence.ArrayToSequenceEnd(start + length));
	}

	public ReadOnlySequence(ReadOnlyMemory<T> memory)
	{
		ArraySegment<T> segment;
		if (MemoryMarshal.TryGetMemoryManager<T, MemoryManager<T>>(memory, out var manager, out var start, out var length))
		{
			_sequenceStart = new SequencePosition(manager, ReadOnlySequence.MemoryManagerToSequenceStart(start));
			_sequenceEnd = new SequencePosition(manager, ReadOnlySequence.MemoryManagerToSequenceEnd(start + length));
		}
		else if (MemoryMarshal.TryGetArray(memory, out segment))
		{
			T[] array = segment.Array;
			int offset = segment.Offset;
			_sequenceStart = new SequencePosition(array, ReadOnlySequence.ArrayToSequenceStart(offset));
			_sequenceEnd = new SequencePosition(array, ReadOnlySequence.ArrayToSequenceEnd(offset + segment.Count));
		}
		else if (typeof(T) == typeof(char))
		{
			if (!MemoryMarshal.TryGetString((ReadOnlyMemory<char>)(object)memory, out string text, out int start2, out length))
			{
				ThrowHelper.ThrowInvalidOperationException();
			}
			_sequenceStart = new SequencePosition(text, ReadOnlySequence.StringToSequenceStart(start2));
			_sequenceEnd = new SequencePosition(text, ReadOnlySequence.StringToSequenceEnd(start2 + length));
		}
		else
		{
			ThrowHelper.ThrowInvalidOperationException();
			_sequenceStart = default(SequencePosition);
			_sequenceEnd = default(SequencePosition);
		}
	}

	public ReadOnlySequence<T> Slice(long start, long length)
	{
		if (start < 0 || length < 0)
		{
			ThrowHelper.ThrowStartOrEndArgumentValidationException(start);
		}
		int index = GetIndex(in _sequenceStart);
		int index2 = GetIndex(in _sequenceEnd);
		object @object = _sequenceStart.GetObject();
		object object2 = _sequenceEnd.GetObject();
		SequencePosition position;
		SequencePosition end;
		if (@object != object2)
		{
			ReadOnlySequenceSegment<T> readOnlySequenceSegment = (ReadOnlySequenceSegment<T>)@object;
			int num = readOnlySequenceSegment.Memory.Length - index;
			if (num > start)
			{
				index += (int)start;
				position = new SequencePosition(@object, index);
				end = GetEndPosition(readOnlySequenceSegment, @object, index, object2, index2, length);
			}
			else
			{
				if (num < 0)
				{
					ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
				}
				position = SeekMultiSegment(readOnlySequenceSegment.Next, object2, index2, start - num, ExceptionArgument.start);
				int index3 = GetIndex(in position);
				object object3 = position.GetObject();
				if (object3 != object2)
				{
					end = GetEndPosition((ReadOnlySequenceSegment<T>)object3, object3, index3, object2, index2, length);
				}
				else
				{
					if (index2 - index3 < length)
					{
						ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
					}
					end = new SequencePosition(object3, index3 + (int)length);
				}
			}
		}
		else
		{
			if (index2 - index < start)
			{
				ThrowHelper.ThrowStartOrEndArgumentValidationException(-1L);
			}
			index += (int)start;
			position = new SequencePosition(@object, index);
			if (index2 - index < length)
			{
				ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
			}
			end = new SequencePosition(@object, index + (int)length);
		}
		return SliceImpl(in position, in end);
	}

	public ReadOnlySequence<T> Slice(long start, SequencePosition end)
	{
		if (start < 0)
		{
			ThrowHelper.ThrowStartOrEndArgumentValidationException(start);
		}
		uint index = (uint)GetIndex(in end);
		object @object = end.GetObject();
		uint index2 = (uint)GetIndex(in _sequenceStart);
		object object2 = _sequenceStart.GetObject();
		uint index3 = (uint)GetIndex(in _sequenceEnd);
		object object3 = _sequenceEnd.GetObject();
		if (object2 == object3)
		{
			if (!InRange(index, index2, index3))
			{
				ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
			}
			if (index - index2 < start)
			{
				ThrowHelper.ThrowStartOrEndArgumentValidationException(-1L);
			}
		}
		else
		{
			ReadOnlySequenceSegment<T> readOnlySequenceSegment = (ReadOnlySequenceSegment<T>)object2;
			ulong num = (ulong)(readOnlySequenceSegment.RunningIndex + index2);
			ulong num2 = (ulong)(((ReadOnlySequenceSegment<T>)@object).RunningIndex + index);
			if (!InRange(num2, num, (ulong)(((ReadOnlySequenceSegment<T>)object3).RunningIndex + index3)))
			{
				ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
			}
			if ((ulong)((long)num + start) > num2)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
			}
			int num3 = readOnlySequenceSegment.Memory.Length - (int)index2;
			if (num3 <= start)
			{
				if (num3 < 0)
				{
					ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
				}
				SequencePosition start2 = SeekMultiSegment(readOnlySequenceSegment.Next, @object, (int)index, start - num3, ExceptionArgument.start);
				return SliceImpl(in start2, in end);
			}
		}
		SequencePosition start3 = new SequencePosition(object2, (int)index2 + (int)start);
		return SliceImpl(in start3, in end);
	}

	public ReadOnlySequence<T> Slice(SequencePosition start, long length)
	{
		uint index = (uint)GetIndex(in start);
		object @object = start.GetObject();
		uint index2 = (uint)GetIndex(in _sequenceStart);
		object object2 = _sequenceStart.GetObject();
		uint index3 = (uint)GetIndex(in _sequenceEnd);
		object object3 = _sequenceEnd.GetObject();
		if (object2 == object3)
		{
			if (!InRange(index, index2, index3))
			{
				ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
			}
			if (length < 0)
			{
				ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
			}
			if (index3 - index < length)
			{
				ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
			}
		}
		else
		{
			ReadOnlySequenceSegment<T> readOnlySequenceSegment = (ReadOnlySequenceSegment<T>)@object;
			ulong num = (ulong)(readOnlySequenceSegment.RunningIndex + index);
			ulong start2 = (ulong)(((ReadOnlySequenceSegment<T>)object2).RunningIndex + index2);
			ulong num2 = (ulong)(((ReadOnlySequenceSegment<T>)object3).RunningIndex + index3);
			if (!InRange(num, start2, num2))
			{
				ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
			}
			if (length < 0)
			{
				ThrowHelper.ThrowStartOrEndArgumentValidationException(0L);
			}
			if ((ulong)((long)num + length) > num2)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.length);
			}
			int num3 = readOnlySequenceSegment.Memory.Length - (int)index;
			if (num3 < length)
			{
				if (num3 < 0)
				{
					ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
				}
				SequencePosition end = SeekMultiSegment(readOnlySequenceSegment.Next, object3, (int)index3, length - num3, ExceptionArgument.length);
				return SliceImpl(in start, in end);
			}
		}
		SequencePosition end2 = new SequencePosition(@object, (int)index + (int)length);
		return SliceImpl(in start, in end2);
	}

	public ReadOnlySequence<T> Slice(int start, int length)
	{
		return Slice((long)start, (long)length);
	}

	public ReadOnlySequence<T> Slice(int start, SequencePosition end)
	{
		return Slice((long)start, end);
	}

	public ReadOnlySequence<T> Slice(SequencePosition start, int length)
	{
		return Slice(start, (long)length);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public ReadOnlySequence<T> Slice(SequencePosition start, SequencePosition end)
	{
		BoundsCheck((uint)GetIndex(in start), start.GetObject(), (uint)GetIndex(in end), end.GetObject());
		return SliceImpl(in start, in end);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public ReadOnlySequence<T> Slice(SequencePosition start)
	{
		BoundsCheck(in start);
		return SliceImpl(in start, in _sequenceEnd);
	}

	public ReadOnlySequence<T> Slice(long start)
	{
		if (start < 0)
		{
			ThrowHelper.ThrowStartOrEndArgumentValidationException(start);
		}
		if (start == 0)
		{
			return this;
		}
		SequencePosition start2 = Seek(in _sequenceStart, in _sequenceEnd, start, ExceptionArgument.start);
		return SliceImpl(in start2, in _sequenceEnd);
	}

	public override string ToString()
	{
		if (typeof(T) == typeof(char))
		{
			ReadOnlySequence<T> source = this;
			ReadOnlySequence<char> sequence = Unsafe.As<ReadOnlySequence<T>, ReadOnlySequence<char>>(ref source);
			if (SequenceMarshal.TryGetString(sequence, out string text, out int start, out int length))
			{
				return text.Substring(start, length);
			}
			if (Length < int.MaxValue)
			{
				return new string(BuffersExtensions.ToArray(in sequence));
			}
		}
		return $"System.Buffers.ReadOnlySequence<{typeof(T).Name}>[{Length}]";
	}

	public Enumerator GetEnumerator()
	{
		return new Enumerator(in this);
	}

	public SequencePosition GetPosition(long offset)
	{
		return GetPosition(offset, _sequenceStart);
	}

	public SequencePosition GetPosition(long offset, SequencePosition origin)
	{
		if (offset < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException_OffsetOutOfRange();
		}
		return Seek(in origin, in _sequenceEnd, offset, ExceptionArgument.offset);
	}

	public bool TryGet(ref SequencePosition position, out ReadOnlyMemory<T> memory, bool advance = true)
	{
		SequencePosition next;
		bool result = TryGetBuffer(in position, out memory, out next);
		if (advance)
		{
			position = next;
		}
		return result;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal bool TryGetBuffer(in SequencePosition position, out ReadOnlyMemory<T> memory, out SequencePosition next)
	{
		object @object = position.GetObject();
		next = default(SequencePosition);
		if (@object == null)
		{
			memory = default(ReadOnlyMemory<T>);
			return false;
		}
		SequenceType sequenceType = GetSequenceType();
		object object2 = _sequenceEnd.GetObject();
		int index = GetIndex(in position);
		int index2 = GetIndex(in _sequenceEnd);
		if (sequenceType == SequenceType.MultiSegment)
		{
			ReadOnlySequenceSegment<T> readOnlySequenceSegment = (ReadOnlySequenceSegment<T>)@object;
			if (readOnlySequenceSegment != object2)
			{
				ReadOnlySequenceSegment<T> next2 = readOnlySequenceSegment.Next;
				if (next2 == null)
				{
					ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();
				}
				next = new SequencePosition(next2, 0);
				memory = readOnlySequenceSegment.Memory.Slice(index);
			}
			else
			{
				memory = readOnlySequenceSegment.Memory.Slice(index, index2 - index);
			}
		}
		else
		{
			if (@object != object2)
			{
				ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();
			}
			if (sequenceType == SequenceType.Array)
			{
				memory = new ReadOnlyMemory<T>((T[])@object, index, index2 - index);
			}
			else if (typeof(T) == typeof(char) && sequenceType == SequenceType.String)
			{
				memory = (ReadOnlyMemory<T>)(object)((string)@object).AsMemory(index, index2 - index);
			}
			else
			{
				memory = ((MemoryManager<T>)@object).Memory.Slice(index, index2 - index);
			}
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private ReadOnlyMemory<T> GetFirstBuffer()
	{
		object @object = _sequenceStart.GetObject();
		if (@object == null)
		{
			return default(ReadOnlyMemory<T>);
		}
		int integer = _sequenceStart.GetInteger();
		int integer2 = _sequenceEnd.GetInteger();
		bool flag = @object != _sequenceEnd.GetObject();
		if (integer >= 0)
		{
			if (integer2 >= 0)
			{
				ReadOnlyMemory<T> memory = ((ReadOnlySequenceSegment<T>)@object).Memory;
				if (flag)
				{
					return memory.Slice(integer);
				}
				return memory.Slice(integer, integer2 - integer);
			}
			if (flag)
			{
				ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();
			}
			return new ReadOnlyMemory<T>((T[])@object, integer, (integer2 & 0x7FFFFFFF) - integer);
		}
		if (flag)
		{
			ThrowHelper.ThrowInvalidOperationException_EndPositionNotReached();
		}
		if (typeof(T) == typeof(char) && integer2 < 0)
		{
			return (ReadOnlyMemory<T>)(object)((string)@object).AsMemory(integer & 0x7FFFFFFF, integer2 - integer);
		}
		integer &= 0x7FFFFFFF;
		return ((MemoryManager<T>)@object).Memory.Slice(integer, integer2 - integer);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private SequencePosition Seek(in SequencePosition start, in SequencePosition end, long offset, ExceptionArgument argument)
	{
		int index = GetIndex(in start);
		int index2 = GetIndex(in end);
		object @object = start.GetObject();
		object object2 = end.GetObject();
		if (@object != object2)
		{
			ReadOnlySequenceSegment<T> readOnlySequenceSegment = (ReadOnlySequenceSegment<T>)@object;
			int num = readOnlySequenceSegment.Memory.Length - index;
			if (num <= offset)
			{
				if (num < 0)
				{
					ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
				}
				return SeekMultiSegment(readOnlySequenceSegment.Next, object2, index2, offset - num, argument);
			}
		}
		else if (index2 - index < offset)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(argument);
		}
		return new SequencePosition(@object, index + (int)offset);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static SequencePosition SeekMultiSegment(ReadOnlySequenceSegment<T> currentSegment, object endObject, int endIndex, long offset, ExceptionArgument argument)
	{
		while (true)
		{
			if (currentSegment != null && currentSegment != endObject)
			{
				int length = currentSegment.Memory.Length;
				if (length > offset)
				{
					break;
				}
				offset -= length;
				currentSegment = currentSegment.Next;
				continue;
			}
			if (currentSegment == null || endIndex < offset)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(argument);
			}
			break;
		}
		return new SequencePosition(currentSegment, (int)offset);
	}

	private void BoundsCheck(in SequencePosition position)
	{
		uint index = (uint)GetIndex(in position);
		uint index2 = (uint)GetIndex(in _sequenceStart);
		uint index3 = (uint)GetIndex(in _sequenceEnd);
		object @object = _sequenceStart.GetObject();
		object object2 = _sequenceEnd.GetObject();
		if (@object == object2)
		{
			if (!InRange(index, index2, index3))
			{
				ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
			}
			return;
		}
		ulong start = (ulong)(((ReadOnlySequenceSegment<T>)@object).RunningIndex + index2);
		if (!InRange((ulong)(((ReadOnlySequenceSegment<T>)position.GetObject()).RunningIndex + index), start, (ulong)(((ReadOnlySequenceSegment<T>)object2).RunningIndex + index3)))
		{
			ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
		}
	}

	private void BoundsCheck(uint sliceStartIndex, object sliceStartObject, uint sliceEndIndex, object sliceEndObject)
	{
		uint index = (uint)GetIndex(in _sequenceStart);
		uint index2 = (uint)GetIndex(in _sequenceEnd);
		object @object = _sequenceStart.GetObject();
		object object2 = _sequenceEnd.GetObject();
		if (@object == object2)
		{
			if (sliceStartObject != sliceEndObject || sliceStartObject != @object || sliceStartIndex > sliceEndIndex || sliceStartIndex < index || sliceEndIndex > index2)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
			}
			return;
		}
		ulong num = (ulong)(((ReadOnlySequenceSegment<T>)sliceStartObject).RunningIndex + sliceStartIndex);
		ulong num2 = (ulong)(((ReadOnlySequenceSegment<T>)sliceEndObject).RunningIndex + sliceEndIndex);
		if (num > num2)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
		}
		if (num < (ulong)(((ReadOnlySequenceSegment<T>)@object).RunningIndex + index) || num2 > (ulong)(((ReadOnlySequenceSegment<T>)object2).RunningIndex + index2))
		{
			ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
		}
	}

	private static SequencePosition GetEndPosition(ReadOnlySequenceSegment<T> startSegment, object startObject, int startIndex, object endObject, int endIndex, long length)
	{
		int num = startSegment.Memory.Length - startIndex;
		if (num > length)
		{
			return new SequencePosition(startObject, startIndex + (int)length);
		}
		if (num < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException_PositionOutOfRange();
		}
		return SeekMultiSegment(startSegment.Next, endObject, endIndex, length - num, ExceptionArgument.length);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private SequenceType GetSequenceType()
	{
		return (SequenceType)(-(2 * (_sequenceStart.GetInteger() >> 31) + (_sequenceEnd.GetInteger() >> 31)));
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static int GetIndex(in SequencePosition position)
	{
		return position.GetInteger() & 0x7FFFFFFF;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private ReadOnlySequence<T> SliceImpl(in SequencePosition start, in SequencePosition end)
	{
		return new ReadOnlySequence<T>(start.GetObject(), GetIndex(in start) | (_sequenceStart.GetInteger() & int.MinValue), end.GetObject(), GetIndex(in end) | (_sequenceEnd.GetInteger() & int.MinValue));
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private long GetLength()
	{
		int index = GetIndex(in _sequenceStart);
		int index2 = GetIndex(in _sequenceEnd);
		object @object = _sequenceStart.GetObject();
		object object2 = _sequenceEnd.GetObject();
		if (@object != object2)
		{
			ReadOnlySequenceSegment<T> readOnlySequenceSegment = (ReadOnlySequenceSegment<T>)@object;
			ReadOnlySequenceSegment<T> readOnlySequenceSegment2 = (ReadOnlySequenceSegment<T>)object2;
			return readOnlySequenceSegment2.RunningIndex + index2 - (readOnlySequenceSegment.RunningIndex + index);
		}
		return index2 - index;
	}

	internal bool TryGetReadOnlySequenceSegment(out ReadOnlySequenceSegment<T> startSegment, out int startIndex, out ReadOnlySequenceSegment<T> endSegment, out int endIndex)
	{
		object @object = _sequenceStart.GetObject();
		if (@object == null || GetSequenceType() != 0)
		{
			startSegment = null;
			startIndex = 0;
			endSegment = null;
			endIndex = 0;
			return false;
		}
		startSegment = (ReadOnlySequenceSegment<T>)@object;
		startIndex = GetIndex(in _sequenceStart);
		endSegment = (ReadOnlySequenceSegment<T>)_sequenceEnd.GetObject();
		endIndex = GetIndex(in _sequenceEnd);
		return true;
	}

	internal bool TryGetArray(out ArraySegment<T> segment)
	{
		if (GetSequenceType() != SequenceType.Array)
		{
			segment = default(ArraySegment<T>);
			return false;
		}
		int index = GetIndex(in _sequenceStart);
		segment = new ArraySegment<T>((T[])_sequenceStart.GetObject(), index, GetIndex(in _sequenceEnd) - index);
		return true;
	}

	internal bool TryGetString(out string text, out int start, out int length)
	{
		if (typeof(T) != typeof(char) || GetSequenceType() != SequenceType.String)
		{
			start = 0;
			length = 0;
			text = null;
			return false;
		}
		start = GetIndex(in _sequenceStart);
		length = GetIndex(in _sequenceEnd) - start;
		text = (string)_sequenceStart.GetObject();
		return true;
	}

	private static bool InRange(uint value, uint start, uint end)
	{
		return value - start <= end - start;
	}

	private static bool InRange(ulong value, ulong start, ulong end)
	{
		return value - start <= end - start;
	}
}
internal static class ReadOnlySequence
{
	public const int FlagBitMask = int.MinValue;

	public const int IndexBitMask = int.MaxValue;

	public const int SegmentStartMask = 0;

	public const int SegmentEndMask = 0;

	public const int ArrayStartMask = 0;

	public const int ArrayEndMask = int.MinValue;

	public const int MemoryManagerStartMask = int.MinValue;

	public const int MemoryManagerEndMask = 0;

	public const int StringStartMask = int.MinValue;

	public const int StringEndMask = int.MinValue;

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int SegmentToSequenceStart(int startIndex)
	{
		return startIndex | 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int SegmentToSequenceEnd(int endIndex)
	{
		return endIndex | 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int ArrayToSequenceStart(int startIndex)
	{
		return startIndex | 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int ArrayToSequenceEnd(int endIndex)
	{
		return endIndex | int.MinValue;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int MemoryManagerToSequenceStart(int startIndex)
	{
		return startIndex | int.MinValue;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int MemoryManagerToSequenceEnd(int endIndex)
	{
		return endIndex | 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int StringToSequenceStart(int startIndex)
	{
		return startIndex | int.MinValue;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int StringToSequenceEnd(int endIndex)
	{
		return endIndex | int.MinValue;
	}
}
