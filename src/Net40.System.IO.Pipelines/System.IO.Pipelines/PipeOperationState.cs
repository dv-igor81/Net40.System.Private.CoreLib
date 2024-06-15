using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.IO.Pipelines;

[DebuggerDisplay("State: {_state}")]
internal struct PipeOperationState
{
	[Flags]
	internal enum State : byte
	{
		Reading = 1,
		ReadingTentative = 2,
		Writing = 4
	}

	private State _state;

	public bool IsWritingActive => (_state & State.Writing) == State.Writing;

	public bool IsReadingActive => (_state & State.Reading) == State.Reading;

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void BeginRead()
	{
		if ((_state & State.Reading) == State.Reading)
		{
			ThrowHelper.ThrowInvalidOperationException_AlreadyReading();
		}
		_state |= State.Reading;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void BeginReadTentative()
	{
		if ((_state & State.Reading) == State.Reading)
		{
			ThrowHelper.ThrowInvalidOperationException_AlreadyReading();
		}
		_state |= State.ReadingTentative;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void EndRead()
	{
		if ((_state & State.Reading) != State.Reading && (_state & State.ReadingTentative) != State.ReadingTentative)
		{
			ThrowHelper.ThrowInvalidOperationException_NoReadToComplete();
		}
		_state &= ~(State.Reading | State.ReadingTentative);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void BeginWrite()
	{
		_state |= State.Writing;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void EndWrite()
	{
		_state &= ~State.Writing;
	}
}
