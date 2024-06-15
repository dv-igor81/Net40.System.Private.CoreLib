using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace System.IO.Pipelines;

public sealed class Pipe
{
	private sealed class DefaultPipeReader : PipeReader, IValueTaskSource<ReadResult>
	{
		private readonly Pipe _pipe;

		public DefaultPipeReader(Pipe pipe)
		{
			_pipe = pipe;
		}

		public override bool TryRead(out ReadResult result)
		{
			return _pipe.TryRead(out result);
		}

		public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return _pipe.ReadAsync(cancellationToken);
		}

		public override void AdvanceTo(SequencePosition consumed)
		{
			_pipe.AdvanceReader(in consumed);
		}

		public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
		{
			_pipe.AdvanceReader(in consumed, in examined);
		}

		public override void CancelPendingRead()
		{
			_pipe.CancelPendingRead();
		}

		public override void Complete(Exception exception = null)
		{
			_pipe.CompleteReader(exception);
		}

		public override void OnWriterCompleted(Action<Exception, object> callback, object state)
		{
			_pipe.OnWriterCompleted(callback, state);
		}

		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _pipe.GetReadAsyncStatus();
		}

		public ReadResult GetResult(short token)
		{
			return _pipe.GetReadAsyncResult();
		}

		public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
		{
			_pipe.OnReadAsyncCompleted(continuation, state, flags);
		}
	}

	private sealed class DefaultPipeWriter : PipeWriter, IValueTaskSource<FlushResult>
	{
		private readonly Pipe _pipe;

		public DefaultPipeWriter(Pipe pipe)
		{
			_pipe = pipe;
		}

		public override void Complete(Exception exception = null)
		{
			_pipe.CompleteWriter(exception);
		}

		public override void CancelPendingFlush()
		{
			_pipe.CancelPendingFlush();
		}

		public override void OnReaderCompleted(Action<Exception, object> callback, object state)
		{
			_pipe.OnReaderCompleted(callback, state);
		}

		public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			return _pipe.FlushAsync(cancellationToken);
		}

		public override void Advance(int bytes)
		{
			_pipe.Advance(bytes);
		}

		public override Memory<byte> GetMemory(int sizeHint = 0)
		{
			return _pipe.GetMemory(sizeHint);
		}

		public override Span<byte> GetSpan(int sizeHint = 0)
		{
			return _pipe.GetSpan(sizeHint);
		}

		public ValueTaskSourceStatus GetStatus(short token)
		{
			return _pipe.GetFlushAsyncStatus();
		}

		public FlushResult GetResult(short token)
		{
			return _pipe.GetFlushAsyncResult();
		}

		public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
		{
			_pipe.OnFlushAsyncCompleted(continuation, state, flags);
		}

		public override ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default(CancellationToken))
		{
			return _pipe.WriteAsync(source, cancellationToken);
		}
	}

	internal const int InitialSegmentPoolSize = 16;

	internal const int MaxSegmentPoolSize = 256;

	private static readonly Action<object> s_signalReaderAwaitable = delegate(object state)
	{
		((Pipe)state).ReaderCancellationRequested();
	};

	private static readonly Action<object> s_signalWriterAwaitable = delegate(object state)
	{
		((Pipe)state).WriterCancellationRequested();
	};

	private static readonly Action<object> s_invokeCompletionCallbacks = delegate(object state)
	{
		((PipeCompletionCallbacks)state).Execute();
	};

	private static readonly ContextCallback s_executionContextRawCallback = ExecuteWithoutExecutionContext;

	private static readonly SendOrPostCallback s_syncContextExecutionContextCallback = ExecuteWithExecutionContext;

	private static readonly SendOrPostCallback s_syncContextExecuteWithoutExecutionContextCallback = ExecuteWithoutExecutionContext;

	private static readonly Action<object> s_scheduleWithExecutionContextCallback = ExecuteWithExecutionContext;

	private readonly object _sync = new object();

	private readonly MemoryPool<byte> _pool;

	private readonly int _minimumSegmentSize;

	private readonly long _pauseWriterThreshold;

	private readonly long _resumeWriterThreshold;

	private readonly PipeScheduler _readerScheduler;

	private readonly PipeScheduler _writerScheduler;

	private BufferSegmentStack _bufferSegmentPool;

	private readonly DefaultPipeReader _reader;

	private readonly DefaultPipeWriter _writer;

	private readonly bool _useSynchronizationContext;

	private long _unconsumedBytes;

	private long _unflushedBytes;

	private PipeAwaitable _readerAwaitable;

	private PipeAwaitable _writerAwaitable;

	private PipeCompletion _writerCompletion;

	private PipeCompletion _readerCompletion;

	private long _lastExaminedIndex = -1L;

	private BufferSegment _readHead;

	private int _readHeadIndex;

	private BufferSegment _readTail;

	private int _readTailIndex;

	private BufferSegment _writingHead;

	private Memory<byte> _writingHeadMemory;

	private int _writingHeadBytesBuffered;

	private PipeOperationState _operationState;

	private bool _disposed;

	internal long Length => _unconsumedBytes;

	public PipeReader Reader => _reader;

	public PipeWriter Writer => _writer;

	public Pipe()
		: this(PipeOptions.Default)
	{
	}

	public Pipe(PipeOptions options)
	{
		if (options == null)
		{
			ThrowHelper.ThrowArgumentNullException(ExceptionArgument.options);
		}
		_bufferSegmentPool = new BufferSegmentStack(16);
		_operationState = default(PipeOperationState);
		_readerCompletion = default(PipeCompletion);
		_writerCompletion = default(PipeCompletion);
		_pool = ((options.Pool == MemoryPool<byte>.Shared) ? null : options.Pool);
		_minimumSegmentSize = options.MinimumSegmentSize;
		_pauseWriterThreshold = options.PauseWriterThreshold;
		_resumeWriterThreshold = options.ResumeWriterThreshold;
		_readerScheduler = options.ReaderScheduler;
		_writerScheduler = options.WriterScheduler;
		_useSynchronizationContext = options.UseSynchronizationContext;
		_readerAwaitable = new PipeAwaitable(completed: false, _useSynchronizationContext);
		_writerAwaitable = new PipeAwaitable(completed: true, _useSynchronizationContext);
		_reader = new DefaultPipeReader(this);
		_writer = new DefaultPipeWriter(this);
	}

	private void ResetState()
	{
		_readerCompletion.Reset();
		_writerCompletion.Reset();
		_readerAwaitable = new PipeAwaitable(completed: false, _useSynchronizationContext);
		_writerAwaitable = new PipeAwaitable(completed: true, _useSynchronizationContext);
		_readTailIndex = 0;
		_readHeadIndex = 0;
		_lastExaminedIndex = -1L;
		_unflushedBytes = 0L;
		_unconsumedBytes = 0L;
	}

	internal Memory<byte> GetMemory(int sizeHint)
	{
		if (_writerCompletion.IsCompleted)
		{
			ThrowHelper.ThrowInvalidOperationException_NoWritingAllowed();
		}
		if (sizeHint < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.minimumSize);
		}
		AllocateWriteHeadIfNeeded(sizeHint);
		return _writingHeadMemory;
	}

	internal Span<byte> GetSpan(int sizeHint)
	{
		if (_writerCompletion.IsCompleted)
		{
			ThrowHelper.ThrowInvalidOperationException_NoWritingAllowed();
		}
		if (sizeHint < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.minimumSize);
		}
		AllocateWriteHeadIfNeeded(sizeHint);
		return _writingHeadMemory.Span;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void AllocateWriteHeadIfNeeded(int sizeHint)
	{
		if (!_operationState.IsWritingActive || _writingHeadMemory.Length == 0 || _writingHeadMemory.Length < sizeHint)
		{
			AllocateWriteHeadSynchronized(sizeHint);
		}
	}

	private void AllocateWriteHeadSynchronized(int sizeHint)
	{
		lock (_sync)
		{
			_operationState.BeginWrite();
			if (_writingHead == null)
			{
				BufferSegment readTail = AllocateSegment(sizeHint);
				_writingHead = (_readHead = (_readTail = readTail));
				_lastExaminedIndex = 0L;
				return;
			}
			int length = _writingHeadMemory.Length;
			if (length == 0 || length < sizeHint)
			{
				if (_writingHeadBytesBuffered > 0)
				{
					_writingHead.End += _writingHeadBytesBuffered;
					_writingHeadBytesBuffered = 0;
				}
				BufferSegment bufferSegment = AllocateSegment(sizeHint);
				_writingHead.SetNext(bufferSegment);
				_writingHead = bufferSegment;
			}
		}
	}

	private BufferSegment AllocateSegment(int sizeHint)
	{
		BufferSegment bufferSegment = CreateSegmentUnsynchronized();
		if (_pool == null)
		{
			bufferSegment.SetOwnedMemory(ArrayPool<byte>.Shared.Rent(GetSegmentSize(sizeHint)));
		}
		else if (sizeHint <= _pool.MaxBufferSize)
		{
			bufferSegment.SetOwnedMemory(_pool.Rent(GetSegmentSize(sizeHint, _pool.MaxBufferSize)));
		}
		else
		{
			bufferSegment.SetUnownedMemory(new byte[sizeHint]);
		}
		_writingHeadMemory = bufferSegment.AvailableMemory;
		return bufferSegment;
	}

	private int GetSegmentSize(int sizeHint, int maxBufferSize = int.MaxValue)
	{
		sizeHint = Math.Max(_minimumSegmentSize, sizeHint);
		return Math.Min(maxBufferSize, sizeHint);
	}

	private BufferSegment CreateSegmentUnsynchronized()
	{
		if (_bufferSegmentPool.TryPop(out var result))
		{
			return result;
		}
		return new BufferSegment();
	}

	private void ReturnSegmentUnsynchronized(BufferSegment segment)
	{
		if (_bufferSegmentPool.Count < 256)
		{
			_bufferSegmentPool.Push(segment);
		}
	}

	internal bool CommitUnsynchronized()
	{
		_operationState.EndWrite();
		if (_unflushedBytes == 0)
		{
			return true;
		}
		_writingHead.End += _writingHeadBytesBuffered;
		_readTail = _writingHead;
		_readTailIndex = _writingHead.End;
		long unconsumedBytes = _unconsumedBytes;
		_unconsumedBytes += _unflushedBytes;
		if (_pauseWriterThreshold > 0 && unconsumedBytes < _pauseWriterThreshold && _unconsumedBytes >= _pauseWriterThreshold && !_readerCompletion.IsCompleted)
		{
			_writerAwaitable.SetUncompleted();
		}
		_unflushedBytes = 0L;
		_writingHeadBytesBuffered = 0;
		return false;
	}

	internal void Advance(int bytes)
	{
		lock (_sync)
		{
			if ((uint)bytes > (uint)_writingHeadMemory.Length)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.bytes);
			}
			if (!_readerCompletion.IsCompleted)
			{
				AdvanceCore(bytes);
			}
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void AdvanceCore(int bytesWritten)
	{
		_unflushedBytes += bytesWritten;
		_writingHeadBytesBuffered += bytesWritten;
		_writingHeadMemory = _writingHeadMemory.Slice(bytesWritten);
	}

	internal ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken)
	{
		CompletionData completionData;
		ValueTask<FlushResult> result;
		lock (_sync)
		{
			PrepareFlush(out completionData, out result, cancellationToken);
		}
		TrySchedule(_readerScheduler, in completionData);
		return result;
	}

	private void PrepareFlush(out CompletionData completionData, out ValueTask<FlushResult> result, CancellationToken cancellationToken)
	{
		bool flag = CommitUnsynchronized();
		_writerAwaitable.BeginOperation(cancellationToken, s_signalWriterAwaitable, this);
		if (_writerAwaitable.IsCompleted)
		{
			FlushResult result2 = default(FlushResult);
			GetFlushResult(ref result2);
			result = new ValueTask<FlushResult>(result2);
		}
		else
		{
			result = new ValueTask<FlushResult>(_writer, 0);
		}
		if (!flag)
		{
			_readerAwaitable.Complete(out completionData);
		}
		else
		{
			completionData = default(CompletionData);
		}
	}

	internal void CompleteWriter(Exception exception)
	{
		PipeCompletionCallbacks pipeCompletionCallbacks;
		CompletionData completionData;
		bool isCompleted;
		lock (_sync)
		{
			CommitUnsynchronized();
			pipeCompletionCallbacks = _writerCompletion.TryComplete(exception);
			_readerAwaitable.Complete(out completionData);
			isCompleted = _readerCompletion.IsCompleted;
		}
		if (isCompleted)
		{
			CompletePipe();
		}
		if (pipeCompletionCallbacks != null)
		{
			ScheduleCallbacks(_readerScheduler, pipeCompletionCallbacks);
		}
		TrySchedule(_readerScheduler, in completionData);
	}

	internal void AdvanceReader(in SequencePosition consumed)
	{
		AdvanceReader(in consumed, in consumed);
	}

	internal void AdvanceReader(in SequencePosition consumed, in SequencePosition examined)
	{
		if (_readerCompletion.IsCompleted)
		{
			ThrowHelper.ThrowInvalidOperationException_NoReadingAllowed();
		}
		AdvanceReader((BufferSegment)consumed.GetObject(), consumed.GetInteger(), (BufferSegment)examined.GetObject(), examined.GetInteger());
	}

	private void AdvanceReader(BufferSegment consumedSegment, int consumedIndex, BufferSegment examinedSegment, int examinedIndex)
	{
		if (consumedSegment != null && examinedSegment != null && BufferSegment.GetLength(consumedSegment, consumedIndex, examinedSegment, examinedIndex) < 0)
		{
			ThrowHelper.ThrowInvalidOperationException_InvalidExaminedOrConsumedPosition();
		}
		BufferSegment bufferSegment = null;
		BufferSegment returnEnd = null;
		CompletionData completionData = default(CompletionData);
		lock (_sync)
		{
			bool flag = false;
			if (examinedSegment == _readTail)
			{
				flag = examinedIndex == _readTailIndex;
			}
			if (examinedSegment != null && _lastExaminedIndex >= 0)
			{
				long length = BufferSegment.GetLength(_lastExaminedIndex, examinedSegment, examinedIndex);
				long unconsumedBytes = _unconsumedBytes;
				if (length < 0)
				{
					ThrowHelper.ThrowInvalidOperationException_InvalidExaminedPosition();
				}
				_unconsumedBytes -= length;
				_lastExaminedIndex = examinedSegment.RunningIndex + examinedIndex;
				if (unconsumedBytes >= _resumeWriterThreshold && _unconsumedBytes < _resumeWriterThreshold)
				{
					_writerAwaitable.Complete(out completionData);
				}
			}
			if (consumedSegment != null)
			{
				if (_readHead == null)
				{
					ThrowHelper.ThrowInvalidOperationException_AdvanceToInvalidCursor();
					return;
				}
				bufferSegment = _readHead;
				returnEnd = consumedSegment;
				if (consumedIndex == returnEnd.Length)
				{
					if (_writingHead != returnEnd)
					{
						MoveReturnEndToNextBlock();
					}
					else if (_writingHeadBytesBuffered == 0 && !_operationState.IsWritingActive)
					{
						_writingHead = null;
						_writingHeadMemory = default(Memory<byte>);
						MoveReturnEndToNextBlock();
					}
					else
					{
						_readHead = consumedSegment;
						_readHeadIndex = consumedIndex;
					}
				}
				else
				{
					_readHead = consumedSegment;
					_readHeadIndex = consumedIndex;
				}
			}
			if (flag && !_writerCompletion.IsCompleted)
			{
				_readerAwaitable.SetUncompleted();
			}
			while (bufferSegment != null && bufferSegment != returnEnd)
			{
				BufferSegment nextSegment = bufferSegment.NextSegment;
				bufferSegment.ResetMemory();
				ReturnSegmentUnsynchronized(bufferSegment);
				bufferSegment = nextSegment;
			}
			_operationState.EndRead();
		}
		TrySchedule(_writerScheduler, in completionData);
		void MoveReturnEndToNextBlock()
		{
			BufferSegment nextSegment2 = returnEnd.NextSegment;
			if (_readTail == returnEnd)
			{
				_readTail = nextSegment2;
				_readTailIndex = 0;
			}
			_readHead = nextSegment2;
			_readHeadIndex = 0;
			returnEnd = nextSegment2;
		}
	}

	internal void CompleteReader(Exception exception)
	{
		PipeCompletionCallbacks pipeCompletionCallbacks;
		CompletionData completionData;
		bool isCompleted;
		lock (_sync)
		{
			if (_operationState.IsReadingActive)
			{
				_operationState.EndRead();
			}
			pipeCompletionCallbacks = _readerCompletion.TryComplete(exception);
			_writerAwaitable.Complete(out completionData);
			isCompleted = _writerCompletion.IsCompleted;
		}
		if (isCompleted)
		{
			CompletePipe();
		}
		if (pipeCompletionCallbacks != null)
		{
			ScheduleCallbacks(_writerScheduler, pipeCompletionCallbacks);
		}
		TrySchedule(_writerScheduler, in completionData);
	}

	internal void OnWriterCompleted(Action<Exception, object> callback, object state)
	{
		if (callback == null)
		{
			ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback);
		}
		PipeCompletionCallbacks pipeCompletionCallbacks;
		lock (_sync)
		{
			pipeCompletionCallbacks = _writerCompletion.AddCallback(callback, state);
		}
		if (pipeCompletionCallbacks != null)
		{
			ScheduleCallbacks(_readerScheduler, pipeCompletionCallbacks);
		}
	}

	internal void CancelPendingRead()
	{
		CompletionData completionData;
		lock (_sync)
		{
			_readerAwaitable.Cancel(out completionData);
		}
		TrySchedule(_readerScheduler, in completionData);
	}

	internal void CancelPendingFlush()
	{
		CompletionData completionData;
		lock (_sync)
		{
			_writerAwaitable.Cancel(out completionData);
		}
		TrySchedule(_writerScheduler, in completionData);
	}

	internal void OnReaderCompleted(Action<Exception, object> callback, object state)
	{
		if (callback == null)
		{
			ThrowHelper.ThrowArgumentNullException(ExceptionArgument.callback);
		}
		PipeCompletionCallbacks pipeCompletionCallbacks;
		lock (_sync)
		{
			pipeCompletionCallbacks = _readerCompletion.AddCallback(callback, state);
		}
		if (pipeCompletionCallbacks != null)
		{
			ScheduleCallbacks(_writerScheduler, pipeCompletionCallbacks);
		}
	}

	internal ValueTask<ReadResult> ReadAsync(CancellationToken token)
	{
		if (_readerCompletion.IsCompleted)
		{
			ThrowHelper.ThrowInvalidOperationException_NoReadingAllowed();
		}
		lock (_sync)
		{
			_readerAwaitable.BeginOperation(token, s_signalReaderAwaitable, this);
			if (_readerAwaitable.IsCompleted)
			{
				GetReadResult(out var result);
				return new ValueTask<ReadResult>(result);
			}
			return new ValueTask<ReadResult>(_reader, 0);
		}
	}

	internal bool TryRead(out ReadResult result)
	{
		lock (_sync)
		{
			if (_readerCompletion.IsCompleted)
			{
				ThrowHelper.ThrowInvalidOperationException_NoReadingAllowed();
			}
			if (_unconsumedBytes > 0 || _readerAwaitable.IsCompleted)
			{
				GetReadResult(out result);
				return true;
			}
			if (_readerAwaitable.IsRunning)
			{
				ThrowHelper.ThrowInvalidOperationException_AlreadyReading();
			}
			_operationState.BeginReadTentative();
			result = default(ReadResult);
			return false;
		}
	}

	private static void ScheduleCallbacks(PipeScheduler scheduler, PipeCompletionCallbacks completionCallbacks)
	{
		scheduler.UnsafeSchedule(s_invokeCompletionCallbacks, completionCallbacks);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static void TrySchedule(PipeScheduler scheduler, in CompletionData completionData)
	{
		Action<object> completion = completionData.Completion;
		if (completion != null)
		{
			if (completionData.SynchronizationContext == null && completionData.ExecutionContext == null)
			{
				scheduler.UnsafeSchedule(completion, completionData.CompletionState);
			}
			else
			{
				ScheduleWithContext(scheduler, in completionData);
			}
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ScheduleWithContext(PipeScheduler scheduler, in CompletionData completionData)
	{
		if (completionData.SynchronizationContext == null)
		{
			scheduler.UnsafeSchedule(s_scheduleWithExecutionContextCallback, completionData);
		}
		else if (completionData.ExecutionContext == null)
		{
			completionData.SynchronizationContext.Post(s_syncContextExecuteWithoutExecutionContextCallback, completionData);
		}
		else
		{
			completionData.SynchronizationContext.Post(s_syncContextExecutionContextCallback, completionData);
		}
	}

	private static void ExecuteWithoutExecutionContext(object state)
	{
		CompletionData completionData = (CompletionData)state;
		completionData.Completion(completionData.CompletionState);
	}

	private static void ExecuteWithExecutionContext(object state)
	{
		ExecutionContext.Run(((CompletionData)state).ExecutionContext, s_executionContextRawCallback, state);
	}

	private void CompletePipe()
	{
		lock (_sync)
		{
			if (!_disposed)
			{
				_disposed = true;
				BufferSegment bufferSegment = _readHead ?? _readTail;
				while (bufferSegment != null)
				{
					BufferSegment bufferSegment2 = bufferSegment;
					bufferSegment = bufferSegment.NextSegment;
					bufferSegment2.ResetMemory();
				}
				_writingHead = null;
				_readHead = null;
				_readTail = null;
				_lastExaminedIndex = -1L;
			}
		}
	}

	internal ValueTaskSourceStatus GetReadAsyncStatus()
	{
		if (_readerAwaitable.IsCompleted)
		{
			if (_writerCompletion.IsFaulted)
			{
				return ValueTaskSourceStatus.Faulted;
			}
			return ValueTaskSourceStatus.Succeeded;
		}
		return ValueTaskSourceStatus.Pending;
	}

	internal void OnReadAsyncCompleted(Action<object> continuation, object state, ValueTaskSourceOnCompletedFlags flags)
	{
		CompletionData completionData;
		bool doubleCompletion;
		lock (_sync)
		{
			_readerAwaitable.OnCompleted(continuation, state, flags, out completionData, out doubleCompletion);
		}
		if (doubleCompletion)
		{
			Writer.Complete(ThrowHelper.CreateInvalidOperationException_NoConcurrentOperation());
		}
		TrySchedule(_readerScheduler, in completionData);
	}

	internal ReadResult GetReadAsyncResult()
	{
		CancellationTokenRegistration cancellationTokenRegistration = default(CancellationTokenRegistration);
		CancellationToken cancellationToken = default(CancellationToken);
		try
		{
			lock (_sync)
			{
				if (!_readerAwaitable.IsCompleted)
				{
					ThrowHelper.ThrowInvalidOperationException_GetResultNotCompleted();
				}
				cancellationTokenRegistration = _readerAwaitable.ReleaseCancellationTokenRegistration(out cancellationToken);
				GetReadResult(out var result);
				return result;
			}
		}
		finally
		{
			cancellationTokenRegistration.Dispose();
			cancellationToken.ThrowIfCancellationRequested();
		}
	}

	private void GetReadResult(out ReadResult result)
	{
		bool isCompleted = _writerCompletion.IsCompletedOrThrow();
		bool flag = _readerAwaitable.ObserveCancellation();
		BufferSegment readHead = _readHead;
		if (readHead != null)
		{
			ReadOnlySequence<byte> buffer = new ReadOnlySequence<byte>(readHead, _readHeadIndex, _readTail, _readTailIndex);
			result = new ReadResult(buffer, flag, isCompleted);
		}
		else
		{
			result = new ReadResult(default(ReadOnlySequence<byte>), flag, isCompleted);
		}
		if (flag)
		{
			_operationState.BeginReadTentative();
		}
		else
		{
			_operationState.BeginRead();
		}
	}

	internal ValueTaskSourceStatus GetFlushAsyncStatus()
	{
		if (_writerAwaitable.IsCompleted)
		{
			if (_readerCompletion.IsFaulted)
			{
				return ValueTaskSourceStatus.Faulted;
			}
			return ValueTaskSourceStatus.Succeeded;
		}
		return ValueTaskSourceStatus.Pending;
	}

	internal FlushResult GetFlushAsyncResult()
	{
		FlushResult result = default(FlushResult);
		CancellationToken cancellationToken = default(CancellationToken);
		CancellationTokenRegistration cancellationTokenRegistration = default(CancellationTokenRegistration);
		try
		{
			lock (_sync)
			{
				if (!_writerAwaitable.IsCompleted)
				{
					ThrowHelper.ThrowInvalidOperationException_GetResultNotCompleted();
				}
				GetFlushResult(ref result);
				cancellationTokenRegistration = _writerAwaitable.ReleaseCancellationTokenRegistration(out cancellationToken);
				return result;
			}
		}
		finally
		{
			cancellationTokenRegistration.Dispose();
			cancellationToken.ThrowIfCancellationRequested();
		}
	}

	private void GetFlushResult(ref FlushResult result)
	{
		if (_writerAwaitable.ObserveCancellation())
		{
			result._resultFlags |= ResultFlags.Canceled;
		}
		if (_readerCompletion.IsCompletedOrThrow())
		{
			result._resultFlags |= ResultFlags.Completed;
		}
	}

	internal ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken)
	{
		if (_writerCompletion.IsCompleted)
		{
			ThrowHelper.ThrowInvalidOperationException_NoWritingAllowed();
		}
		if (_readerCompletion.IsCompleted)
		{
			return new ValueTask<FlushResult>(new FlushResult(isCanceled: false, isCompleted: true));
		}
		CompletionData completionData;
		ValueTask<FlushResult> result;
		lock (_sync)
		{
			AllocateWriteHeadIfNeeded(0);
			if (source.Length <= _writingHeadMemory.Length)
			{
				source.CopyTo(_writingHeadMemory);
				AdvanceCore(source.Length);
			}
			else
			{
				WriteMultiSegment(source.Span);
			}
			PrepareFlush(out completionData, out result, cancellationToken);
		}
		TrySchedule(_readerScheduler, in completionData);
		return result;
	}

	private void WriteMultiSegment(ReadOnlySpan<byte> source)
	{
		Span<byte> span = _writingHeadMemory.Span;
		while (true)
		{
			int num = Math.Min(span.Length, source.Length);
			source.Slice(0, num).CopyTo(span);
			source = source.Slice(num);
			AdvanceCore(num);
			if (source.Length != 0)
			{
				_writingHead.End += num;
				_writingHeadBytesBuffered = 0;
				BufferSegment bufferSegment = AllocateSegment(0);
				_writingHead.SetNext(bufferSegment);
				_writingHead = bufferSegment;
				span = _writingHeadMemory.Span;
				continue;
			}
			break;
		}
	}

	internal void OnFlushAsyncCompleted(Action<object> continuation, object state, ValueTaskSourceOnCompletedFlags flags)
	{
		CompletionData completionData;
		bool doubleCompletion;
		lock (_sync)
		{
			_writerAwaitable.OnCompleted(continuation, state, flags, out completionData, out doubleCompletion);
		}
		if (doubleCompletion)
		{
			Reader.Complete(ThrowHelper.CreateInvalidOperationException_NoConcurrentOperation());
		}
		TrySchedule(_writerScheduler, in completionData);
	}

	private void ReaderCancellationRequested()
	{
		CompletionData completionData;
		lock (_sync)
		{
			_readerAwaitable.CancellationTokenFired(out completionData);
		}
		TrySchedule(_readerScheduler, in completionData);
	}

	private void WriterCancellationRequested()
	{
		CompletionData completionData;
		lock (_sync)
		{
			_writerAwaitable.CancellationTokenFired(out completionData);
		}
		TrySchedule(_writerScheduler, in completionData);
	}

	public void Reset()
	{
		lock (_sync)
		{
			if (!_disposed)
			{
				ThrowHelper.ThrowInvalidOperationException_ResetIncompleteReaderWriter();
			}
			_disposed = false;
			ResetState();
		}
	}
}
