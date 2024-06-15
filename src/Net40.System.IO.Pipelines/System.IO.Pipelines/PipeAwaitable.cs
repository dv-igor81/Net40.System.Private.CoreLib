using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks.Sources;

namespace System.IO.Pipelines;

[DebuggerDisplay("CanceledState: {_awaitableState}, IsCompleted: {IsCompleted}")]
internal struct PipeAwaitable
{
	[Flags]
	private enum AwaitableState
	{
		None = 0,
		Completed = 1,
		Running = 2,
		Canceled = 4,
		UseSynchronizationContext = 8
	}

	private AwaitableState _awaitableState;

	private Action<object> _completion;

	private object _completionState;

	private CancellationTokenRegistration _cancellationTokenRegistration;

	private SynchronizationContext _synchronizationContext;

	private ExecutionContext _executionContext;

	private CancellationToken _cancellationToken;

	private CancellationToken CancellationToken => _cancellationToken;

	public bool IsCompleted => (_awaitableState & (AwaitableState.Completed | AwaitableState.Canceled)) != 0;

	public bool IsRunning => (_awaitableState & AwaitableState.Running) != 0;

	public PipeAwaitable(bool completed, bool useSynchronizationContext)
	{
		_awaitableState = (completed ? AwaitableState.Completed : AwaitableState.None) | (useSynchronizationContext ? AwaitableState.UseSynchronizationContext : AwaitableState.None);
		_completion = null;
		_completionState = null;
		_cancellationTokenRegistration = default(CancellationTokenRegistration);
		_synchronizationContext = null;
		_executionContext = null;
		_cancellationToken = CancellationToken.None;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void BeginOperation(CancellationToken cancellationToken, Action<object> callback, object state)
	{
		cancellationToken.ThrowIfCancellationRequested();
		_awaitableState |= AwaitableState.Running;
		if (cancellationToken.CanBeCanceled && !IsCompleted)
		{
			_cancellationToken = cancellationToken;
			_cancellationTokenRegistration = cancellationToken.UnsafeRegister(callback, state);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void Complete(out CompletionData completionData)
	{
		ExtractCompletion(out completionData);
		_awaitableState |= AwaitableState.Completed;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void ExtractCompletion(out CompletionData completionData)
	{
		Action<object> completion = _completion;
		object completionState = _completionState;
		ExecutionContext executionContext = _executionContext;
		SynchronizationContext synchronizationContext = _synchronizationContext;
		_completion = null;
		_completionState = null;
		_synchronizationContext = null;
		_executionContext = null;
		completionData = ((completion != null) ? new CompletionData(completion, completionState, executionContext, synchronizationContext) : default(CompletionData));
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void SetUncompleted()
	{
		_awaitableState &= ~AwaitableState.Completed;
	}

	public void OnCompleted(Action<object> continuation, object state, ValueTaskSourceOnCompletedFlags flags, out CompletionData completionData, out bool doubleCompletion)
	{
		completionData = default(CompletionData);
		doubleCompletion = _completion != null;
		if (IsCompleted | doubleCompletion)
		{
			completionData = new CompletionData(continuation, state, _executionContext, _synchronizationContext);
			return;
		}
		_completion = continuation;
		_completionState = state;
		if ((_awaitableState & AwaitableState.UseSynchronizationContext) != 0 && (flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
		{
			SynchronizationContext current = SynchronizationContext.Current;
			if (current != null && current.GetType() != typeof(SynchronizationContext))
			{
				_synchronizationContext = current;
			}
		}
		if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
		{
			_executionContext = ExecutionContext.Capture();
		}
	}

	public void Cancel(out CompletionData completionData)
	{
		ExtractCompletion(out completionData);
		_awaitableState |= AwaitableState.Canceled;
	}

	public void CancellationTokenFired(out CompletionData completionData)
	{
		if (CancellationToken.IsCancellationRequested)
		{
			Cancel(out completionData);
		}
		else
		{
			completionData = default(CompletionData);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool ObserveCancellation()
	{
		bool result = (_awaitableState & AwaitableState.Canceled) == AwaitableState.Canceled;
		_awaitableState &= ~(AwaitableState.Running | AwaitableState.Canceled);
		return result;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public CancellationTokenRegistration ReleaseCancellationTokenRegistration(out CancellationToken cancellationToken)
	{
		cancellationToken = CancellationToken;
		CancellationTokenRegistration cancellationTokenRegistration = _cancellationTokenRegistration;
		_cancellationToken = default(CancellationToken);
		_cancellationTokenRegistration = default(CancellationTokenRegistration);
		return cancellationTokenRegistration;
	}
}
