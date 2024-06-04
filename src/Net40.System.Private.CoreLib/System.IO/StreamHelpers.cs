namespace System.IO;

internal static class StreamHelpers
{
	public static void ValidateCopyToArgs(Stream source, Stream destination, int bufferSize)
	{
		if (destination == null)
		{
			throw new ArgumentNullException("destination");
		}
		if (bufferSize <= 0)
		{
			throw new ArgumentOutOfRangeException("bufferSize", bufferSize, SR.ArgumentOutOfRange_NeedPosNum);
		}
		bool sourceCanRead = source.CanRead;
		if (!sourceCanRead && !source.CanWrite)
		{
			throw new ObjectDisposedException(null, SR.ObjectDisposed_StreamClosed);
		}
		bool destinationCanWrite = destination.CanWrite;
		if (!destinationCanWrite && !destination.CanRead)
		{
			throw new ObjectDisposedException("destination", SR.ObjectDisposed_StreamClosed);
		}
		if (!sourceCanRead)
		{
			throw new NotSupportedException(SR.NotSupported_UnreadableStream);
		}
		if (!destinationCanWrite)
		{
			throw new NotSupportedException(SR.NotSupported_UnwritableStream);
		}
	}
}
