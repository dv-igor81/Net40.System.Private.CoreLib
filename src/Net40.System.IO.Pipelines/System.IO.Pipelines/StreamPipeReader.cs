using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipelines;

internal class StreamPipeReader : PipeReader
{
	internal const int InitialSegmentPoolSize = 4;

	internal const int MaxSegmentPoolSize = 256;

	private readonly int _bufferSize;

	private readonly int _minimumReadThreshold;

	private readonly MemoryPool<byte> _pool;

	private CancellationTokenSource _internalTokenSource;

	private bool _isReaderCompleted;

	private bool _isStreamCompleted;

	private BufferSegment _readHead;

	private int _readIndex;

	private BufferSegment _readTail;

	private long _bufferedBytes;

	private bool _examinedEverything;

	private object _lock = new object();

	private BufferSegmentStack _bufferSegmentPool;

	private bool _leaveOpen;

	public Stream InnerStream { get; }

	private CancellationTokenSource InternalTokenSource
	{
		get
		{
			lock (_lock)
			{
				if (_internalTokenSource == null)
				{
					_internalTokenSource = new CancellationTokenSource();
				}
				return _internalTokenSource;
			}
		}
	}

	public StreamPipeReader(Stream readingStream, StreamPipeReaderOptions options)
	{
		InnerStream = readingStream ?? throw new ArgumentNullException("readingStream");
		if (options == null)
		{
			throw new ArgumentNullException("options");
		}
		_bufferSegmentPool = new BufferSegmentStack(4);
		_minimumReadThreshold = Math.Min(options.MinimumReadSize, options.BufferSize);
		_pool = ((options.Pool == MemoryPool<byte>.Shared) ? null : options.Pool);
		_bufferSize = ((_pool == null) ? options.BufferSize : Math.Min(options.BufferSize, _pool.MaxBufferSize));
		_leaveOpen = options.LeaveOpen;
	}

	public override void AdvanceTo(SequencePosition consumed)
	{
		AdvanceTo(consumed, consumed);
	}

	public override void AdvanceTo(SequencePosition consumed, SequencePosition examined)
	{
		ThrowIfCompleted();
		AdvanceTo((BufferSegment)consumed.GetObject(), consumed.GetInteger(), (BufferSegment)examined.GetObject(), examined.GetInteger());
	}

	private void AdvanceTo(BufferSegment consumedSegment, int consumedIndex, BufferSegment examinedSegment, int examinedIndex)
	{
		if (consumedSegment != null && examinedSegment != null)
		{
			if (_readHead == null)
			{
				ThrowHelper.ThrowInvalidOperationException_AdvanceToInvalidCursor();
			}
			BufferSegment bufferSegment = _readHead;
			BufferSegment bufferSegment2 = consumedSegment;
			long length = BufferSegment.GetLength(bufferSegment, _readIndex, consumedSegment, consumedIndex);
			_bufferedBytes -= length;
			_examinedEverything = false;
			if (examinedSegment == _readTail)
			{
				_examinedEverything = examinedIndex == _readTail.End;
			}
			if (_bufferedBytes == 0)
			{
				bufferSegment2 = null;
				_readHead = null;
				_readTail = null;
				_readIndex = 0;
			}
			else if (consumedIndex == bufferSegment2.Length)
			{
				BufferSegment bufferSegment3 = (_readHead = bufferSegment2.NextSegment);
				_readIndex = 0;
				bufferSegment2 = bufferSegment3;
			}
			else
			{
				_readHead = consumedSegment;
				_readIndex = consumedIndex;
			}
			while (bufferSegment != bufferSegment2)
			{
				BufferSegment nextSegment = bufferSegment.NextSegment;
				bufferSegment.ResetMemory();
				ReturnSegmentUnsynchronized(bufferSegment);
				bufferSegment = nextSegment;
			}
		}
	}

	public override void CancelPendingRead()
	{
		InternalTokenSource.Cancel();
	}

	public override void Complete(Exception exception = null)
	{
		if (!_isReaderCompleted)
		{
			_isReaderCompleted = true;
			BufferSegment bufferSegment = _readHead;
			while (bufferSegment != null)
			{
				BufferSegment bufferSegment2 = bufferSegment;
				bufferSegment = bufferSegment.NextSegment;
				bufferSegment2.ResetMemory();
			}
			if (!_leaveOpen)
			{
				InnerStream.Dispose();
			}
		}
	}

	public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		ThrowIfCompleted();
		CancellationTokenSource tokenSource = InternalTokenSource;
		if (TryReadInternal(tokenSource, out var result))
		{
			return result;
		}
		if (_isStreamCompleted)
		{
			return new ReadResult(default(ReadOnlySequence<byte>), isCanceled: false, isCompleted: true);
		}
		CancellationTokenRegistration cancellationTokenRegistration = default(CancellationTokenRegistration);
		if (cancellationToken.CanBeCanceled)
		{
			cancellationTokenRegistration = cancellationToken.UnsafeRegister(delegate(object state)
			{
				((StreamPipeReader)state).Cancel();
			}, this);
		}
		using (cancellationTokenRegistration)
		{
			bool isCanceled = false;
			try
			{
				AllocateReadTail();
				int num = await StreamExtensions.ReadAsync(buffer: _readTail.AvailableMemory.Slice(_readTail.End), stream: InnerStream, cancellationToken: tokenSource.Token).ConfigureAwait(continueOnCapturedContext: false);
				_readTail.End += num;
				_bufferedBytes += num;
				if (num == 0)
				{
					_isStreamCompleted = true;
				}
			}
			catch (OperationCanceledException)
			{
				ClearCancellationToken();
				if (!tokenSource.IsCancellationRequested || cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				isCanceled = true;
			}
			return new ReadResult(GetCurrentReadOnlySequence(), isCanceled, _isStreamCompleted);
		}
	}

	private void ClearCancellationToken()
	{
		lock (_lock)
		{
			_internalTokenSource = null;
		}
	}

	private void ThrowIfCompleted()
	{
		if (_isReaderCompleted)
		{
			ThrowHelper.ThrowInvalidOperationException_NoReadingAllowed();
		}
	}

	public override bool TryRead(out ReadResult result)
	{
		ThrowIfCompleted();
		return TryReadInternal(InternalTokenSource, out result);
	}

	private bool TryReadInternal(CancellationTokenSource source, out ReadResult result)
	{
		bool isCancellationRequested = source.IsCancellationRequested;
		if (isCancellationRequested || (_bufferedBytes > 0 && (!_examinedEverything || _isStreamCompleted)))
		{
			if (isCancellationRequested)
			{
				ClearCancellationToken();
			}
			ReadOnlySequence<byte> buffer = ((_readHead == null) ? default(ReadOnlySequence<byte>) : GetCurrentReadOnlySequence());
			result = new ReadResult(buffer, isCancellationRequested, _isStreamCompleted);
			return true;
		}
		result = default(ReadResult);
		return false;
	}

	private ReadOnlySequence<byte> GetCurrentReadOnlySequence()
	{
		return new ReadOnlySequence<byte>(_readHead, _readIndex, _readTail, _readTail.End);
	}

	private void AllocateReadTail()
	{
		if (_readHead == null)
		{
			_readHead = AllocateSegment();
			_readTail = _readHead;
		}
		else if (_readTail.WritableBytes < _minimumReadThreshold)
		{
			BufferSegment bufferSegment = AllocateSegment();
			_readTail.SetNext(bufferSegment);
			_readTail = bufferSegment;
		}
	}

	private BufferSegment AllocateSegment()
	{
		BufferSegment bufferSegment = CreateSegmentUnsynchronized();
		if (_pool == null)
		{
			bufferSegment.SetOwnedMemory(ArrayPool<byte>.Shared.Rent(_bufferSize));
		}
		else
		{
			bufferSegment.SetOwnedMemory(_pool.Rent(_bufferSize));
		}
		return bufferSegment;
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

	private void Cancel()
	{
		InternalTokenSource.Cancel();
	}
}
