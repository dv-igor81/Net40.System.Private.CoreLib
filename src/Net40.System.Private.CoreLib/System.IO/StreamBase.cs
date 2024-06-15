using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO;

public abstract class StreamBase : Stream
{
    protected virtual int Read(Span<byte> buffer)
    {
        byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            int num = Read(array, 0, buffer.Length);
            if ((uint)num > (uint)buffer.Length)
            {
                throw new IOException(SR.IO_StreamTooLong);
            }
            new Span<byte>(array, 0, num).CopyTo(buffer);
            return num;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    protected virtual Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            return TaskEx.Run(() => this.Read(buffer, offset, count), cancellationToken);
        }
        return TaskExEx.FromCanceled<int>(cancellationToken);
    }

    public virtual ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out ArraySegment<byte> segment))
        {
            return new ValueTask<int>(ReadAsync(segment.Array, segment.Offset, segment.Count, cancellationToken));
        }
        byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
        return FinishReadAsync(ReadAsync(array, 0, buffer.Length, cancellationToken), array, buffer);
        static async ValueTask<int> FinishReadAsync(Task<int> readTask, byte[] localBuffer, Memory<byte> localDestination)
        {
            try
            {
                int num = await readTask.ConfigureAwait(continueOnCapturedContext: false);
                new Span<byte>(localBuffer, 0, num).CopyTo(localDestination.Span);
                return num;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(localBuffer);
            }
        }
    }
    
    protected virtual void Write(ReadOnlySpan<byte> buffer)
    {
        byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            buffer.CopyTo(array);
            Write(array, 0, buffer.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    protected virtual Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (!cancellationToken.IsCancellationRequested)
        {
            return TaskEx.Run(() => this.Write(buffer, offset, count), cancellationToken);
        }
        return TaskExEx.FromCanceled(cancellationToken);
    }
    
    public virtual ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
    {
        if (MemoryMarshal.TryGetArray(buffer, out var segment))
        {
            return new ValueTask(WriteAsync(segment.Array, segment.Offset, segment.Count, cancellationToken));
        }
        byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
        buffer.Span.CopyTo(array);
        return new ValueTask(FinishWriteAsync(WriteAsync(array, 0, buffer.Length, cancellationToken), array));
    }
    
    private async Task FinishWriteAsync(Task writeTask, byte[] localBuffer)
    {
        try
        {
            await writeTask.ConfigureAwait(continueOnCapturedContext: false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(localBuffer);
        }
    }
    
    public virtual Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.Factory.StartNew(delegate(object state)
            {
                ((Stream)state).Flush();
            }, this, cancellationToken, 
            //TaskCreationOptions.DenyChildAttach,
            TaskCreationOptions.None, // DIA-Замена
            TaskScheduler.Default);
    }
}