using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipelines;

public abstract class PipeWriter : IBufferWriter<byte>
{
	private PipeWriterStream _stream;

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

	public abstract void CancelPendingFlush();

	[Obsolete("OnReaderCompleted may not be invoked on all implementations of PipeWriter. This will be removed in a future release.")]
	public virtual void OnReaderCompleted(Action<Exception, object> callback, object state)
	{
	}

	public abstract ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default(CancellationToken));

	public abstract void Advance(int bytes);

	public abstract Memory<byte> GetMemory(int sizeHint = 0);

	public abstract Span<byte> GetSpan(int sizeHint = 0);

	public virtual Stream AsStream(bool leaveOpen = false)
	{
		if (_stream == null)
		{
			_stream = new PipeWriterStream(this, leaveOpen);
		}
		else if (leaveOpen)
		{
			_stream.LeaveOpen = leaveOpen;
		}
		return _stream;
	}

	public static PipeWriter Create(Stream stream, StreamPipeWriterOptions writerOptions = null)
	{
		return new StreamPipeWriter(stream, writerOptions ?? StreamPipeWriterOptions.s_default);
	}

	public virtual ValueTask<FlushResult> WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default(CancellationToken))
	{
		this.Write(source.Span);
		return FlushAsync(cancellationToken);
	}

	protected internal virtual async Task CopyFromAsync(Stream source, CancellationToken cancellationToken = default(CancellationToken))
	{
		FlushResult flushResult;
		do
		{
			Memory<byte> memory = GetMemory();
			int num = await source.ReadAsync(memory, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (num != 0)
			{
				Advance(num);
				flushResult = await FlushAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (flushResult.IsCanceled)
				{
					ThrowHelper.ThrowOperationCanceledException_FlushCanceled();
				}
				continue;
			}
			break;
		}
		while (!flushResult.IsCompleted);
	}
}
