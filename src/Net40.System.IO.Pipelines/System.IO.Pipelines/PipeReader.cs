using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipelines;

public abstract class PipeReader
{
	private PipeReaderStream _stream;

	public abstract bool TryRead(out ReadResult result);

	public abstract ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default(CancellationToken));

	public abstract void AdvanceTo(SequencePosition consumed);

	public abstract void AdvanceTo(SequencePosition consumed, SequencePosition examined);

	public virtual Stream AsStream(bool leaveOpen = false)
	{
		if (_stream == null)
		{
			_stream = new PipeReaderStream(this, leaveOpen);
		}
		else if (leaveOpen)
		{
			_stream.LeaveOpen = leaveOpen;
		}
		return _stream;
	}

	public abstract void CancelPendingRead();

	public abstract void Complete(Exception exception = null);

	public virtual ValueTask CompleteAsync(Exception exception = null)
	{
		try
		{
			Complete(exception);
			return default(ValueTask);
		}
		catch (Exception exception2)
		{
			return new ValueTask(TaskExEx.FromException(exception2));
		}
	}

	[Obsolete("OnWriterCompleted may not be invoked on all implementations of PipeReader. This will be removed in a future release.")]
	public virtual void OnWriterCompleted(Action<Exception, object> callback, object state)
	{
	}

	public static PipeReader Create(Stream stream, StreamPipeReaderOptions readerOptions = null)
	{
		return new StreamPipeReader(stream, readerOptions ?? StreamPipeReaderOptions.s_default);
	}

	public virtual Task CopyToAsync(PipeWriter destination, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (destination == null)
		{
			throw new ArgumentNullException("destination");
		}
		if (cancellationToken.IsCancellationRequested)
		{
			return TaskExEx.FromCanceled(cancellationToken);
		}
		return CopyToAsyncCore(destination, async delegate(PipeWriter destination, ReadOnlyMemory<byte> memory, CancellationToken cancellationToken)
		{
			if ((await destination.WriteAsync(memory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).IsCanceled)
			{
				ThrowHelper.ThrowOperationCanceledException_FlushCanceled();
			}
		}, cancellationToken);
	}

	public virtual Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (destination == null)
		{
			throw new ArgumentNullException("destination");
		}
		if (cancellationToken.IsCancellationRequested)
		{
			return TaskExEx.FromCanceled(cancellationToken);
		}
		return CopyToAsyncCore(destination, (Stream destination, ReadOnlyMemory<byte> memory, CancellationToken cancellationToken) => destination.WriteAsync(memory, cancellationToken), cancellationToken);
	}

	private async Task CopyToAsyncCore<TStream>(TStream destination, Func<TStream, ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeAsync, CancellationToken cancellationToken)
	{
		while (true)
		{
			ReadResult result = await ReadAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			ReadOnlySequence<byte> buffer = result.Buffer;
			SequencePosition position = buffer.Start;
			SequencePosition consumed = position;
			try
			{
				if (result.IsCanceled)
				{
					ThrowHelper.ThrowOperationCanceledException_ReadCanceled();
				}
				ReadOnlyMemory<byte> memory;
				while (buffer.TryGet(ref position, out memory))
				{
					await writeAsync(destination, memory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					consumed = position;
				}
				consumed = buffer.End;
				if (result.IsCompleted)
				{
					break;
				}
				memory = default(ReadOnlyMemory<byte>);
			}
			finally
			{
				AdvanceTo(consumed);
			}
		}
	}
}
