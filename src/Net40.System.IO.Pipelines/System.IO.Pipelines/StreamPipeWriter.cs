using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipelines;

internal class StreamPipeWriter : PipeWriter
{
	internal const int InitialSegmentPoolSize = 4;

	internal const int MaxSegmentPoolSize = 256;

	private readonly int _minimumBufferSize;

	private BufferSegment _head;

	private BufferSegment _tail;

	private Memory<byte> _tailMemory;

	private int _tailBytesBuffered;

	private int _bytesBuffered;

	private MemoryPool<byte> _pool;

	private CancellationTokenSource _internalTokenSource;

	private bool _isCompleted;

	private object _lockObject = new object();

	private BufferSegmentStack _bufferSegmentPool;

	private bool _leaveOpen;

	private CancellationTokenSource InternalTokenSource
	{
		get
		{
			lock (_lockObject)
			{
				if (_internalTokenSource == null)
				{
					_internalTokenSource = new CancellationTokenSource();
				}
				return _internalTokenSource;
			}
		}
	}

	public Stream InnerStream { get; }

	public StreamPipeWriter(Stream writingStream, StreamPipeWriterOptions options)
	{
		InnerStream = writingStream ?? throw new ArgumentNullException("writingStream");
		if (options == null)
		{
			throw new ArgumentNullException("options");
		}
		_minimumBufferSize = options.MinimumBufferSize;
		_pool = ((options.Pool == MemoryPool<byte>.Shared) ? null : options.Pool);
		_bufferSegmentPool = new BufferSegmentStack(4);
		_leaveOpen = options.LeaveOpen;
	}

	public override void Advance(int bytes)
	{
		if ((uint)bytes > (uint)_tailMemory.Length)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.bytes);
		}
		_tailBytesBuffered += bytes;
		_bytesBuffered += bytes;
		_tailMemory = _tailMemory.Slice(bytes);
	}

	public override Memory<byte> GetMemory(int sizeHint = 0)
	{
		if (_isCompleted)
		{
			ThrowHelper.ThrowInvalidOperationException_NoWritingAllowed();
		}
		if (sizeHint < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.sizeHint);
		}
		AllocateMemory(sizeHint);
		return _tailMemory;
	}

	public override Span<byte> GetSpan(int sizeHint = 0)
	{
		if (_isCompleted)
		{
			ThrowHelper.ThrowInvalidOperationException_NoWritingAllowed();
		}
		if (sizeHint < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.sizeHint);
		}
		AllocateMemory(sizeHint);
		return _tailMemory.Span;
	}

	private void AllocateMemory(int sizeHint)
	{
		if (_head == null)
		{
			BufferSegment tail = AllocateSegment(sizeHint);
			_head = (_tail = tail);
			_tailBytesBuffered = 0;
			return;
		}
		int length = _tailMemory.Length;
		if (length == 0 || length < sizeHint)
		{
			if (_tailBytesBuffered > 0)
			{
				_tail.End += _tailBytesBuffered;
				_tailBytesBuffered = 0;
			}
			BufferSegment bufferSegment = AllocateSegment(sizeHint);
			_tail.SetNext(bufferSegment);
			_tail = bufferSegment;
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
		_tailMemory = bufferSegment.AvailableMemory;
		return bufferSegment;
	}

	private int GetSegmentSize(int sizeHint, int maxBufferSize = int.MaxValue)
	{
		sizeHint = Math.Max(_minimumBufferSize, sizeHint);
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

	public override void CancelPendingFlush()
	{
		Cancel();
	}

	public override void Complete(Exception exception = null)
	{
		if (!_isCompleted)
		{
			_isCompleted = true;
			FlushInternal();
			_internalTokenSource?.Dispose();
			if (!_leaveOpen)
			{
				InnerStream.Dispose();
			}
		}
	}

	public override async ValueTask CompleteAsync(Exception exception = null)
	{
		if (!_isCompleted)
		{
			_isCompleted = true;
			await FlushAsyncInternal().ConfigureAwait(continueOnCapturedContext: false);
			_internalTokenSource?.Dispose();
			if (!_leaveOpen)
			{
				InnerStream.Dispose();
			}
		}
	}

	public override ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		if (_bytesBuffered == 0)
		{
			return new ValueTask<FlushResult>(new FlushResult(isCanceled: false, isCompleted: false));
		}
		return FlushAsyncInternal(cancellationToken);
	}

	private void Cancel()
	{
		InternalTokenSource.Cancel();
	}

	private async ValueTask<FlushResult> FlushAsyncInternal(CancellationToken cancellationToken = default(CancellationToken))
	{
		CancellationTokenRegistration cancellationTokenRegistration = default(CancellationTokenRegistration);
		if (cancellationToken.CanBeCanceled)
		{
			cancellationTokenRegistration = cancellationToken.UnsafeRegister(delegate(object state)
			{
				((StreamPipeWriter)state).Cancel();
			}, this);
		}
		if (_tailBytesBuffered > 0)
		{
			_tail.End += _tailBytesBuffered;
			_tailBytesBuffered = 0;
		}
		using (cancellationTokenRegistration)
		{
			CancellationToken localToken = InternalTokenSource.Token;
			try
			{
				BufferSegment segment = _head;
				while (segment != null)
				{
					BufferSegment returnSegment = segment;
					segment = segment.NextSegment;
					if (returnSegment.Length > 0)
					{
						await InnerStream.WriteAsync(returnSegment.Memory, localToken).ConfigureAwait(continueOnCapturedContext: false);
					}
					returnSegment.ResetMemory();
					ReturnSegmentUnsynchronized(returnSegment);
					_head = segment;
				}
				if (_bytesBuffered > 0)
				{
					await TaskTheraotExtensions.ConfigureAwait(StreamTheraotExtensions.FlushAsync(InnerStream, localToken), continueOnCapturedContext: false);
				}
				_head = null;
				_tail = null;
				_bytesBuffered = 0;
				return new FlushResult(isCanceled: false, isCompleted: false);
			}
			catch (OperationCanceledException)
			{
				lock (_lockObject)
				{
					_internalTokenSource = null;
				}
				if (localToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
				{
					return new FlushResult(isCanceled: true, isCompleted: false);
				}
				throw;
			}
		}
	}

	private void FlushInternal()
	{
		if (_tailBytesBuffered > 0)
		{
			_tail.End += _tailBytesBuffered;
			_tailBytesBuffered = 0;
		}
		BufferSegment bufferSegment = _head;
		while (bufferSegment != null)
		{
			BufferSegment bufferSegment2 = bufferSegment;
			bufferSegment = bufferSegment.NextSegment;
			if (bufferSegment2.Length > 0)
			{
				InnerStream.Write(bufferSegment2.Memory);
			}
			bufferSegment2.ResetMemory();
			ReturnSegmentUnsynchronized(bufferSegment2);
			_head = bufferSegment;
		}
		if (_bytesBuffered > 0)
		{
			InnerStream.Flush();
		}
		_head = null;
		_tail = null;
		_bytesBuffered = 0;
	}
}
