using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO;

public static class StreamEx
{
    // public static Task CopyToAsync(this Stream selfStream, Stream destination, int bufferSize,
    //     CancellationToken cancellationToken)
    // {
    //     StreamHelpers.ValidateCopyToArgs(selfStream, destination, bufferSize);
    //
    //     return CopyToAsyncInternal(selfStream, destination, bufferSize, cancellationToken);
    // }

    private static async Task CopyToAsyncInternal(this Stream selfStream, Stream destination, int bufferSize,
        CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferSize);
        try
        {
            while (true)
            {
                int bytesRead = await ReadAsync(selfStream, new Memory<byte>(buffer), cancellationToken)
                    .ConfigureAwait(false);
                if (bytesRead == 0) break;
                await destination.WriteAsync(new ReadOnlyMemory<byte>(buffer, 0, bytesRead), cancellationToken)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public static ValueTask WriteAsync(this Stream selfStream, ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
        {
            Task task = selfStream.WriteAsync(array.Array!, array.Offset, array.Count, cancellationToken);
            return new ValueTask(task);
        }
        else
        {
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            buffer.Span.CopyTo(sharedBuffer);
            Task task = FinishWriteAsync(
                selfStream.WriteAsync(sharedBuffer, 0, buffer.Length, cancellationToken),
                sharedBuffer);
            return new ValueTask(task);
        }
    }

    public static int Read(this Stream selfStream, Span<byte> buffer)
    {
        byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            int numRead = selfStream.Read(sharedBuffer, 0, buffer.Length);
            if ((uint)numRead > (uint)buffer.Length)
            {
                throw new IOException(SR.IO_StreamTooLong);
            }

            new Span<byte>(sharedBuffer, 0, numRead).CopyTo(buffer);
            return numRead;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sharedBuffer);
        }
    }


    public static void Write(this Stream selfStream, ReadOnlySpan<byte> buffer)
    {
        byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
        try
        {
            buffer.CopyTo(sharedBuffer);
            selfStream.Write(sharedBuffer, 0, buffer.Length);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(sharedBuffer);
        }
    }

    public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (MemoryMarshal.TryGetArray(buffer, out ArraySegment<byte> array))
        {
            return new ValueTask<int>(stream.ReadAsync(array.Array!, array.Offset, array.Count, cancellationToken));
        }
        else
        {
            byte[] sharedBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            return FinishReadAsync(stream.ReadAsync(sharedBuffer, 0, buffer.Length, cancellationToken),
                sharedBuffer, buffer);

            async ValueTask<int> FinishReadAsync(Task<int> readTask, byte[] localBuffer,
                Memory<byte> localDestination)
            {
                try
                {
                    int result = await readTask.ConfigureAwait(false);
                    new Span<byte>(localBuffer, 0, result).CopyTo(localDestination.Span);
                    return result;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(localBuffer);
                }
            }
        }
    }

    private static async Task FinishWriteAsync(Task writeTask, byte[] localBuffer)
    {
        try
        {
            await writeTask.ConfigureAwait(false);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(localBuffer);
        }
    }
}