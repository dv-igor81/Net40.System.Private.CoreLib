using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;

namespace System.IO.Pipelines;

[DebuggerDisplay("IsCompleted: {IsCompleted}")]
internal struct PipeCompletion
{
	private static readonly ArrayPool<PipeCompletionCallback> s_completionCallbackPool = ArrayPool<PipeCompletionCallback>.Shared;

	private const int InitialCallbacksSize = 1;

	private bool _isCompleted;

	private ExceptionDispatchInfo _exceptionInfo;

	private PipeCompletionCallback _firstCallback;

	private PipeCompletionCallback[] _callbacks;

	private int _callbackCount;

	public bool IsCompleted => _isCompleted;

	public bool IsFaulted => _exceptionInfo != null;

	public PipeCompletionCallbacks TryComplete(Exception exception = null)
	{
		if (!_isCompleted)
		{
			_isCompleted = true;
			if (exception != null)
			{
				_exceptionInfo = ExceptionDispatchInfo.Capture(exception);
			}
		}
		return GetCallbacks();
	}

	public PipeCompletionCallbacks AddCallback(Action<Exception, object> callback, object state)
	{
		if (_callbackCount == 0)
		{
			_firstCallback = new PipeCompletionCallback(callback, state);
			_callbackCount++;
		}
		else
		{
			EnsureSpace();
			int num = _callbackCount - 1;
			_callbackCount++;
			_callbacks[num] = new PipeCompletionCallback(callback, state);
		}
		if (IsCompleted)
		{
			return GetCallbacks();
		}
		return null;
	}

	private void EnsureSpace()
	{
		if (_callbacks == null)
		{
			_callbacks = s_completionCallbackPool.Rent(1);
		}
		int num = _callbackCount - 1;
		if (num == _callbacks.Length)
		{
			PipeCompletionCallback[] array = s_completionCallbackPool.Rent(_callbacks.Length * 2);
			Array.Copy(_callbacks, 0, array, 0, _callbacks.Length);
			s_completionCallbackPool.Return(_callbacks, clearArray: true);
			_callbacks = array;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool IsCompletedOrThrow()
	{
		if (!_isCompleted)
		{
			return false;
		}
		if (_exceptionInfo != null)
		{
			ThrowLatchedException();
		}
		return true;
	}

	private PipeCompletionCallbacks GetCallbacks()
	{
		if (_callbackCount == 0)
		{
			return null;
		}
		PipeCompletionCallbacks result = new PipeCompletionCallbacks(s_completionCallbackPool, _callbackCount, _exceptionInfo?.SourceException, _firstCallback, _callbacks);
		_firstCallback = default(PipeCompletionCallback);
		_callbacks = null;
		_callbackCount = 0;
		return result;
	}

	public void Reset()
	{
		_isCompleted = false;
		_exceptionInfo = null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void ThrowLatchedException()
	{
		_exceptionInfo.Throw();
	}

	public override string ToString()
	{
		return string.Format("{0}: {1}", "IsCompleted", IsCompleted);
	}
}
