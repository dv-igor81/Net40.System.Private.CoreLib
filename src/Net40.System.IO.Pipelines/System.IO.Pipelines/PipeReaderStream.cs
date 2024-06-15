using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipelines;

internal sealed class PipeReaderStream : Stream
{
	private readonly PipeReader _pipeReader;

	public override bool CanRead => true;

	public override bool CanSeek => false;

	public override bool CanWrite => false;

	public override long Length
	{
		get
		{
			throw new NotSupportedException();
		}
	}

	public override long Position
	{
		get
		{
			throw new NotSupportedException();
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	internal bool LeaveOpen { get; set; }

	public PipeReaderStream(PipeReader pipeReader, bool leaveOpen)
	{
		_pipeReader = pipeReader;
		LeaveOpen = leaveOpen;
	}

	protected override void Dispose(bool disposing)
	{
		if (!LeaveOpen)
		{
			_pipeReader.Complete();
		}
		base.Dispose(disposing);
	}

	public override void Flush()
	{
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		return TaskTheraotExtensions.GetAwaiter(StreamTheraotExtensions.ReadAsync(this, buffer, offset, count)).GetResult();
	}

	public override long Seek(long offset, SeekOrigin origin)
	{
		throw new NotSupportedException();
	}

	public override void SetLength(long value)
	{
		throw new NotSupportedException();
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		throw new NotSupportedException();
	}

	public sealed override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
	{
		return System.Threading.Tasks.TaskToApm.Begin(ReadAsync(buffer, offset, count, default(CancellationToken)), callback, state);
	}

	public sealed override int EndRead(IAsyncResult asyncResult)
	{
		return System.Threading.Tasks.TaskToApm.End<int>(asyncResult);
	}

	public new Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		return ReadAsyncInternal(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
	}

	private async ValueTask<int> ReadAsyncInternal(Memory<byte> buffer, CancellationToken cancellationToken)
	{
		ReadResult readResult = await _pipeReader.ReadAsync(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (readResult.IsCanceled)
		{
			ThrowHelper.ThrowOperationCanceledException_ReadCanceled();
		}
		ReadOnlySequence<byte> buffer2 = readResult.Buffer;
		long length = buffer2.Length;
		SequencePosition consumed = buffer2.Start;
		try
		{
			if (length != 0)
			{
				int num = (int)Math.Min(length, buffer.Length);
				ReadOnlySequence<byte> source = ((num == length) ? buffer2 : buffer2.Slice(0, num));
				consumed = source.End;
				source.CopyTo(buffer.Span);
				return num;
			}
			if (readResult.IsCompleted)
			{
				return 0;
			}
		}
		finally
		{
			_pipeReader.AdvanceTo(consumed);
		}
		ThrowHelper.ThrowInvalidOperationException_InvalidZeroByteRead();
		return 0;
	}

	public new Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
	{
		return _pipeReader.CopyToAsync(destination, cancellationToken);
	}
}
