#define DEBUG
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System.Buffers;

public ref struct SequenceReader<T> where T : unmanaged, IEquatable<T>
{
	private SequencePosition _currentPosition;

	private SequencePosition _nextPosition;

	private bool _moreData;

	private long _length;

	public bool End => !_moreData;

	public ReadOnlySequence<T> Sequence { get; }

	private SequencePosition Position => Sequence.GetPosition(CurrentSpanIndex, _currentPosition);

	private ReadOnlySpan<T> CurrentSpan { get; set; }

	private int CurrentSpanIndex { get; set; }

	private ReadOnlySpan<T> UnreadSpan
	{
		[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
		get => CurrentSpan.Slice(CurrentSpanIndex);
	}

	public long Consumed { get; private set; }

	public long Remaining => Length - Consumed;

	public long Length
	{
		get
		{
			if (_length < 0)
			{
				Volatile.Write(ref Unsafe.AsRef(in _length), Sequence.Length);
			}
			return _length;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public SequenceReader(ReadOnlySequence<T> sequence)
	{
		CurrentSpanIndex = 0;
		Consumed = 0L;
		Sequence = sequence;
		_currentPosition = sequence.Start;
		_length = -1L;
		sequence.GetFirstSpan(out var first, out _nextPosition);
		CurrentSpan = first;
		_moreData = first.Length > 0;
		if (!_moreData && !sequence.IsSingleSegment)
		{
			_moreData = true;
			GetNextSpan();
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool TryPeek(out T value)
	{
		if (_moreData)
		{
			value = CurrentSpan[CurrentSpanIndex];
			return true;
		}
		value = default(T);
		return false;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool TryRead(out T value)
	{
		if (End)
		{
			value = default(T);
			return false;
		}
		value = CurrentSpan[CurrentSpanIndex];
		CurrentSpanIndex++;
		Consumed++;
		if (CurrentSpanIndex >= CurrentSpan.Length)
		{
			GetNextSpan();
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void Rewind(long count)
	{
		if ((ulong)count > (ulong)Consumed)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		Consumed -= count;
		if (CurrentSpanIndex >= count)
		{
			CurrentSpanIndex -= (int)count;
			_moreData = true;
		}
		else
		{
			RetreatToPreviousSpan(Consumed);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void RetreatToPreviousSpan(long consumed)
	{
		ResetReader();
		Advance(consumed);
	}

	private void ResetReader()
	{
		CurrentSpanIndex = 0;
		Consumed = 0L;
		_currentPosition = Sequence.Start;
		_nextPosition = _currentPosition;
		if (Sequence.TryGet(ref _nextPosition, out var memory))
		{
			_moreData = true;
			if (memory.Length == 0)
			{
				CurrentSpan = default(ReadOnlySpan<T>);
				GetNextSpan();
			}
			else
			{
				CurrentSpan = memory.Span;
			}
		}
		else
		{
			_moreData = false;
			CurrentSpan = default(ReadOnlySpan<T>);
		}
	}

	private void GetNextSpan()
	{
		if (!Sequence.IsSingleSegment)
		{
			SequencePosition previousNextPosition = _nextPosition;
			ReadOnlyMemory<T> memory;
			while (Sequence.TryGet(ref _nextPosition, out memory))
			{
				_currentPosition = previousNextPosition;
				if (memory.Length > 0)
				{
					CurrentSpan = memory.Span;
					CurrentSpanIndex = 0;
					return;
				}
				CurrentSpan = default(ReadOnlySpan<T>);
				CurrentSpanIndex = 0;
				previousNextPosition = _nextPosition;
			}
		}
		_moreData = false;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void Advance(long count)
	{
		if ((count & int.MinValue) == 0L && CurrentSpan.Length - CurrentSpanIndex > (int)count)
		{
			CurrentSpanIndex += (int)count;
			Consumed += count;
		}
		else
		{
			AdvanceToNextSpan(count);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal void AdvanceCurrentSpan(long count)
	{
		Debug.Assert(count >= 0);
		Consumed += count;
		CurrentSpanIndex += (int)count;
		if (CurrentSpanIndex >= CurrentSpan.Length)
		{
			GetNextSpan();
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal void AdvanceWithinSpan(long count)
	{
		Debug.Assert(count >= 0);
		Consumed += count;
		CurrentSpanIndex += (int)count;
		Debug.Assert(CurrentSpanIndex < CurrentSpan.Length);
	}

	private void AdvanceToNextSpan(long count)
	{
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		Consumed += count;
		while (_moreData)
		{
			int remaining = CurrentSpan.Length - CurrentSpanIndex;
			if (remaining > count)
			{
				CurrentSpanIndex += (int)count;
				count = 0L;
				break;
			}
			CurrentSpanIndex += remaining;
			count -= remaining;
			Debug.Assert(count >= 0);
			GetNextSpan();
			if (count == 0)
			{
				break;
			}
		}
		if (count != 0)
		{
			Consumed -= count;
			throw new ArgumentOutOfRangeException("count");
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool TryCopyTo(Span<T> destination)
	{
		ReadOnlySpan<T> firstSpan = UnreadSpan;
		if (firstSpan.Length >= destination.Length)
		{
			firstSpan.Slice(0, destination.Length).CopyTo(destination);
			return true;
		}
		return TryCopyMultisegment(destination);
	}

	internal bool TryCopyMultisegment(Span<T> destination)
	{
		if (Remaining < destination.Length)
		{
			return false;
		}
		ReadOnlySpan<T> firstSpan = UnreadSpan;
		Debug.Assert(firstSpan.Length < destination.Length);
		firstSpan.CopyTo(destination);
		int copied = firstSpan.Length;
		SequencePosition next = _nextPosition;
		ReadOnlyMemory<T> nextSegment;
		while (Sequence.TryGet(ref next, out nextSegment))
		{
			if (nextSegment.Length > 0)
			{
				ReadOnlySpan<T> nextSpan = nextSegment.Span;
				int toCopy = Math.Min(nextSpan.Length, destination.Length - copied);
				nextSpan.Slice(0, toCopy).CopyTo(destination.Slice(copied));
				copied += toCopy;
				if (copied >= destination.Length)
				{
					break;
				}
			}
		}
		return true;
	}

	public bool TryReadTo(out ReadOnlySpan<T> span, T delimiter, bool advancePastDelimiter = true)
	{
		ReadOnlySpan<T> remaining = UnreadSpan;
		int index = remaining.IndexOf(delimiter);
		if (index != -1)
		{
			span = ((index == 0) ? default(ReadOnlySpan<T>) : remaining.Slice(0, index));
			AdvanceCurrentSpan(index + (advancePastDelimiter ? 1 : 0));
			return true;
		}
		return TryReadToSlow(out span, delimiter, advancePastDelimiter);
	}

	private bool TryReadToSlow(out ReadOnlySpan<T> span, T delimiter, bool advancePastDelimiter)
	{
		if (!TryReadToInternal(out var sequence, delimiter, advancePastDelimiter, CurrentSpan.Length - CurrentSpanIndex))
		{
			span = default(ReadOnlySpan<T>);
			return false;
		}
		span = (sequence.IsSingleSegment ? sequence.First.Span : ((ReadOnlySpan<T>)BuffersExtensions.ToArray(in sequence)));
		return true;
	}

	public bool TryReadTo(out ReadOnlySpan<T> span, T delimiter, T delimiterEscape, bool advancePastDelimiter = true)
	{
		ReadOnlySpan<T> remaining = UnreadSpan;
		int index = remaining.IndexOf(delimiter);
		if ((index > 0 && !remaining[index - 1].Equals(delimiterEscape)) || index == 0)
		{
			span = remaining.Slice(0, index);
			AdvanceCurrentSpan(index + (advancePastDelimiter ? 1 : 0));
			return true;
		}
		return TryReadToSlow(out span, delimiter, delimiterEscape, index, advancePastDelimiter);
	}

	private bool TryReadToSlow(out ReadOnlySpan<T> span, T delimiter, T delimiterEscape, int index, bool advancePastDelimiter)
	{
		if (!TryReadToSlow(out ReadOnlySequence<T> sequence, delimiter, delimiterEscape, index, advancePastDelimiter))
		{
			span = default(ReadOnlySpan<T>);
			return false;
		}
		Debug.Assert(sequence.Length > 0);
		span = (sequence.IsSingleSegment ? sequence.First.Span : ((ReadOnlySpan<T>)BuffersExtensions.ToArray(in sequence)));
		return true;
	}

	private bool TryReadToSlow(out ReadOnlySequence<T> sequence, T delimiter, T delimiterEscape, int index, bool advancePastDelimiter)
	{
		SequenceReader<T> copy = this;
		ReadOnlySpan<T> remaining = UnreadSpan;
		bool priorEscape = false;
		do
		{
			if (index >= 0)
			{
				if (!(index == 0 && priorEscape))
				{
					if (index > 0 && remaining[index - 1].Equals(delimiterEscape))
					{
						int escapeCount = 1;
						int i = index - 2;
						while (i >= 0 && remaining[i].Equals(delimiterEscape))
						{
							i--;
						}
						if (i < 0 && priorEscape)
						{
							escapeCount++;
						}
						escapeCount += index - 2 - i;
						if (((uint)escapeCount & (true ? 1u : 0u)) != 0)
						{
							Advance(index + 1);
							priorEscape = false;
							remaining = UnreadSpan;
							goto IL_0231;
						}
					}
					AdvanceCurrentSpan(index);
					sequence = Sequence.Slice(copy.Position, Position);
					if (advancePastDelimiter)
					{
						Advance(1L);
					}
					return true;
				}
				priorEscape = false;
				Advance(index + 1);
				remaining = UnreadSpan;
			}
			else
			{
				if (remaining.Length > 0 && remaining[remaining.Length - 1].Equals(delimiterEscape))
				{
					int escapeCount2 = 1;
					int j = remaining.Length - 2;
					while (j >= 0 && remaining[j].Equals(delimiterEscape))
					{
						j--;
					}
					escapeCount2 += remaining.Length - 2 - j;
					priorEscape = ((!(j < 0 && priorEscape)) ? ((escapeCount2 & 1) != 0) : ((escapeCount2 & 1) == 0));
				}
				else
				{
					priorEscape = false;
				}
				AdvanceCurrentSpan(remaining.Length);
				remaining = CurrentSpan;
			}
			goto IL_0231;
			IL_0231:
			index = remaining.IndexOf(delimiter);
		}
		while (!End);
		this = copy;
		sequence = default(ReadOnlySequence<T>);
		return false;
	}

	public bool TryReadTo(out ReadOnlySequence<T> sequence, T delimiter, bool advancePastDelimiter = true)
	{
		return TryReadToInternal(out sequence, delimiter, advancePastDelimiter);
	}

	private bool TryReadToInternal(out ReadOnlySequence<T> sequence, T delimiter, bool advancePastDelimiter, int skip = 0)
	{
		Debug.Assert(skip >= 0);
		SequenceReader<T> copy = this;
		if (skip > 0)
		{
			Advance(skip);
		}
		ReadOnlySpan<T> remaining = UnreadSpan;
		while (_moreData)
		{
			int index = remaining.IndexOf(delimiter);
			if (index != -1)
			{
				if (index > 0)
				{
					AdvanceCurrentSpan(index);
				}
				sequence = Sequence.Slice(copy.Position, Position);
				if (advancePastDelimiter)
				{
					Advance(1L);
				}
				return true;
			}
			AdvanceCurrentSpan(remaining.Length);
			remaining = CurrentSpan;
		}
		this = copy;
		sequence = default(ReadOnlySequence<T>);
		return false;
	}

	public bool TryReadTo(out ReadOnlySequence<T> sequence, T delimiter, T delimiterEscape, bool advancePastDelimiter = true)
	{
		SequenceReader<T> copy = this;
		ReadOnlySpan<T> remaining = UnreadSpan;
		bool priorEscape = false;
		while (_moreData)
		{
			int index = remaining.IndexOf(delimiter);
			if (index != -1)
			{
				if (!(index == 0 && priorEscape))
				{
					if (index > 0 && remaining[index - 1].Equals(delimiterEscape))
					{
						int escapeCount2 = 0;
						int j = index;
						while (j > 0 && remaining[j - 1].Equals(delimiterEscape))
						{
							j--;
							escapeCount2++;
						}
						if (escapeCount2 == index && priorEscape)
						{
							escapeCount2++;
						}
						priorEscape = false;
						if (((uint)escapeCount2 & (true ? 1u : 0u)) != 0)
						{
							Advance(index + 1);
							remaining = UnreadSpan;
							continue;
						}
					}
					if (index > 0)
					{
						Advance(index);
					}
					sequence = Sequence.Slice(copy.Position, Position);
					if (advancePastDelimiter)
					{
						Advance(1L);
					}
					return true;
				}
				priorEscape = false;
				Advance(index + 1);
				remaining = UnreadSpan;
			}
			else
			{
				int escapeCount = 0;
				int i = remaining.Length;
				while (i > 0 && remaining[i - 1].Equals(delimiterEscape))
				{
					i--;
					escapeCount++;
				}
				if (priorEscape && escapeCount == remaining.Length)
				{
					escapeCount++;
				}
				priorEscape = escapeCount % 2 != 0;
				Advance(remaining.Length);
				remaining = CurrentSpan;
			}
		}
		this = copy;
		sequence = default(ReadOnlySequence<T>);
		return false;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool TryReadToAny(out ReadOnlySpan<T> span, ReadOnlySpan<T> delimiters, bool advancePastDelimiter = true)
	{
		ReadOnlySpan<T> remaining = UnreadSpan;
		int index = ((delimiters.Length == 2) ? remaining.IndexOfAny(delimiters[0], delimiters[1]) : remaining.IndexOfAny(delimiters));
		if (index != -1)
		{
			span = remaining.Slice(0, index);
			Advance(index + (advancePastDelimiter ? 1 : 0));
			return true;
		}
		return TryReadToAnySlow(out span, delimiters, advancePastDelimiter);
	}

	private bool TryReadToAnySlow(out ReadOnlySpan<T> span, ReadOnlySpan<T> delimiters, bool advancePastDelimiter)
	{
		if (!TryReadToAnyInternal(out var sequence, delimiters, advancePastDelimiter, CurrentSpan.Length - CurrentSpanIndex))
		{
			span = default(ReadOnlySpan<T>);
			return false;
		}
		span = (sequence.IsSingleSegment ? sequence.First.Span : ((ReadOnlySpan<T>)BuffersExtensions.ToArray(in sequence)));
		return true;
	}

	public bool TryReadToAny(out ReadOnlySequence<T> sequence, ReadOnlySpan<T> delimiters, bool advancePastDelimiter = true)
	{
		return TryReadToAnyInternal(out sequence, delimiters, advancePastDelimiter);
	}

	private bool TryReadToAnyInternal(out ReadOnlySequence<T> sequence, ReadOnlySpan<T> delimiters, bool advancePastDelimiter, int skip = 0)
	{
		SequenceReader<T> copy = this;
		if (skip > 0)
		{
			Advance(skip);
		}
		ReadOnlySpan<T> remaining = UnreadSpan;
		while (!End)
		{
			int index = ((delimiters.Length == 2) ? remaining.IndexOfAny(delimiters[0], delimiters[1]) : remaining.IndexOfAny(delimiters));
			if (index != -1)
			{
				if (index > 0)
				{
					AdvanceCurrentSpan(index);
				}
				sequence = Sequence.Slice(copy.Position, Position);
				if (advancePastDelimiter)
				{
					Advance(1L);
				}
				return true;
			}
			Advance(remaining.Length);
			remaining = CurrentSpan;
		}
		this = copy;
		sequence = default(ReadOnlySequence<T>);
		return false;
	}

	public bool TryReadTo(out ReadOnlySequence<T> sequence, ReadOnlySpan<T> delimiter, bool advancePastDelimiter = true)
	{
		if (delimiter.Length == 0)
		{
			sequence = default(ReadOnlySequence<T>);
			return true;
		}
		SequenceReader<T> copy = this;
		bool advanced = false;
		while (!End)
		{
			if (!TryReadTo(out sequence, delimiter[0], advancePastDelimiter: false))
			{
				this = copy;
				return false;
			}
			if (delimiter.Length == 1)
			{
				if (advancePastDelimiter)
				{
					Advance(1L);
				}
				return true;
			}
			if (IsNext(delimiter))
			{
				if (advanced)
				{
					sequence = copy.Sequence.Slice(copy.Consumed, Consumed - copy.Consumed);
				}
				if (advancePastDelimiter)
				{
					Advance(delimiter.Length);
				}
				return true;
			}
			Advance(1L);
			advanced = true;
		}
		this = copy;
		sequence = default(ReadOnlySequence<T>);
		return false;
	}

	public bool TryAdvanceTo(T delimiter, bool advancePastDelimiter = true)
	{
		ReadOnlySpan<T> remaining = UnreadSpan;
		int index = remaining.IndexOf(delimiter);
		if (index != -1)
		{
			Advance(advancePastDelimiter ? (index + 1) : index);
			return true;
		}
		ReadOnlySequence<T> sequence;
		return TryReadToInternal(out sequence, delimiter, advancePastDelimiter);
	}

	public bool TryAdvanceToAny(ReadOnlySpan<T> delimiters, bool advancePastDelimiter = true)
	{
		ReadOnlySpan<T> remaining = UnreadSpan;
		int index = remaining.IndexOfAny(delimiters);
		if (index != -1)
		{
			AdvanceCurrentSpan(index + (advancePastDelimiter ? 1 : 0));
			return true;
		}
		ReadOnlySequence<T> sequence;
		return TryReadToAnyInternal(out sequence, delimiters, advancePastDelimiter);
	}

	public long AdvancePast(T value)
	{
		long start = Consumed;
		do
		{
			int i;
			for (i = CurrentSpanIndex; i < CurrentSpan.Length && CurrentSpan[i].Equals(value); i++)
			{
			}
			int advanced = i - CurrentSpanIndex;
			if (advanced == 0)
			{
				break;
			}
			AdvanceCurrentSpan(advanced);
		}
		while (CurrentSpanIndex == 0 && !End);
		return Consumed - start;
	}

	public long AdvancePastAny(ReadOnlySpan<T> values)
	{
		long start = Consumed;
		do
		{
			int i;
			for (i = CurrentSpanIndex; i < CurrentSpan.Length && values.IndexOf(CurrentSpan[i]) != -1; i++)
			{
			}
			int advanced = i - CurrentSpanIndex;
			if (advanced == 0)
			{
				break;
			}
			AdvanceCurrentSpan(advanced);
		}
		while (CurrentSpanIndex == 0 && !End);
		return Consumed - start;
	}

	public long AdvancePastAny(T value0, T value1, T value2, T value3)
	{
		long start = Consumed;
		do
		{
			int i;
			for (i = CurrentSpanIndex; i < CurrentSpan.Length; i++)
			{
				T value4 = CurrentSpan[i];
				if (!value4.Equals(value0) && !value4.Equals(value1) && !value4.Equals(value2) && !value4.Equals(value3))
				{
					break;
				}
			}
			int advanced = i - CurrentSpanIndex;
			if (advanced == 0)
			{
				break;
			}
			AdvanceCurrentSpan(advanced);
		}
		while (CurrentSpanIndex == 0 && !End);
		return Consumed - start;
	}

	public long AdvancePastAny(T value0, T value1, T value2)
	{
		long start = Consumed;
		do
		{
			int i;
			for (i = CurrentSpanIndex; i < CurrentSpan.Length; i++)
			{
				T value3 = CurrentSpan[i];
				if (!value3.Equals(value0) && !value3.Equals(value1) && !value3.Equals(value2))
				{
					break;
				}
			}
			int advanced = i - CurrentSpanIndex;
			if (advanced == 0)
			{
				break;
			}
			AdvanceCurrentSpan(advanced);
		}
		while (CurrentSpanIndex == 0 && !End);
		return Consumed - start;
	}

	public long AdvancePastAny(T value0, T value1)
	{
		long start = Consumed;
		do
		{
			int i;
			for (i = CurrentSpanIndex; i < CurrentSpan.Length; i++)
			{
				T value2 = CurrentSpan[i];
				if (!value2.Equals(value0) && !value2.Equals(value1))
				{
					break;
				}
			}
			int advanced = i - CurrentSpanIndex;
			if (advanced == 0)
			{
				break;
			}
			AdvanceCurrentSpan(advanced);
		}
		while (CurrentSpanIndex == 0 && !End);
		return Consumed - start;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool IsNext(T next, bool advancePast = false)
	{
		if (End)
		{
			return false;
		}
		if (CurrentSpan[CurrentSpanIndex].Equals(next))
		{
			if (advancePast)
			{
				AdvanceCurrentSpan(1L);
			}
			return true;
		}
		return false;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool IsNext(ReadOnlySpan<T> next, bool advancePast = false)
	{
		ReadOnlySpan<T> unread = UnreadSpan;
		if (unread.StartsWith(next))
		{
			if (advancePast)
			{
				AdvanceCurrentSpan(next.Length);
			}
			return true;
		}
		return unread.Length < next.Length && IsNextSlow(next, advancePast);
	}

	private bool IsNextSlow(ReadOnlySpan<T> next, bool advancePast)
	{
		ReadOnlySpan<T> currentSpan = UnreadSpan;
		Debug.Assert(currentSpan.Length < next.Length);
		int fullLength = next.Length;
		SequencePosition nextPosition = _nextPosition;
		while (next.StartsWith(currentSpan))
		{
			if (next.Length == currentSpan.Length)
			{
				if (advancePast)
				{
					Advance(fullLength);
				}
				return true;
			}
			ReadOnlyMemory<T> nextSegment;
			do
			{
				if (!Sequence.TryGet(ref nextPosition, out nextSegment))
				{
					return false;
				}
			}
			while (nextSegment.Length <= 0);
			next = next.Slice(currentSpan.Length);
			currentSpan = nextSegment.Span;
			if (currentSpan.Length > next.Length)
			{
				currentSpan = currentSpan.Slice(0, next.Length);
			}
		}
		return false;
	}
}
