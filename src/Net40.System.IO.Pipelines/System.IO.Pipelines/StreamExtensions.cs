using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipelines;

internal static class StreamExtensions
{
	public static ValueTask<int> ReadAsync(this Stream stream, Memory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out ArraySegment<byte> segment))
		{
			return new ValueTask<int>(StreamTheraotExtensions.ReadAsync(stream, segment.Array, segment.Offset, segment.Count, cancellationToken));
		}
		byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
		return FinishReadAsync(StreamTheraotExtensions.ReadAsync(stream, array, 0, buffer.Length, cancellationToken), array, buffer);
		static async ValueTask<int> FinishReadAsync(Task<int> readTask, byte[] localBuffer, Memory<byte> localDestination)
		{
			try
			{
				int num = await TaskTheraotExtensions.ConfigureAwait(readTask, continueOnCapturedContext: false);
				new Span<byte>(localBuffer, 0, num).CopyTo(localDestination.Span);
				return num;
			}
			finally
			{
				ArrayPool<byte>.Shared.Return(localBuffer);
			}
		}
	}

	public static void Write(this Stream stream, ReadOnlyMemory<byte> buffer)
	{
		if (MemoryMarshal.TryGetArray(buffer, out var segment))
		{
			stream.Write(segment.Array, segment.Offset, segment.Count);
			return;
		}
		byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
		try
		{
			buffer.Span.CopyTo(array);
			stream.Write(array, 0, buffer.Length);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(array);
		}
	}

	public static ValueTask WriteAsync(this Stream stream, ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (MemoryMarshal.TryGetArray(buffer, out var segment))
		{
			return new ValueTask(StreamTheraotExtensions.WriteAsync(stream, segment.Array, segment.Offset, segment.Count, cancellationToken));
		}
		byte[] array = ArrayPool<byte>.Shared.Rent(buffer.Length);
		buffer.Span.CopyTo(array);
		return new ValueTask(FinishWriteAsync(StreamTheraotExtensions.WriteAsync(stream, array, 0, buffer.Length, cancellationToken), array));
	}

	private static async Task FinishWriteAsync(Task writeTask, byte[] localBuffer)
	{
		try
		{
			await TaskTheraotExtensions.ConfigureAwait(writeTask, continueOnCapturedContext: false);
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(localBuffer);
		}
	}
}
