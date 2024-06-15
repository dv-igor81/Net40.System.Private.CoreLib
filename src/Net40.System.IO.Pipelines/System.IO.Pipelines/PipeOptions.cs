using System.Buffers;

namespace System.IO.Pipelines;

public class PipeOptions
{
	private const int DefaultMinimumSegmentSize = 4096;

	private const int DefaultResumeWriterThreshold = 32768;

	private const int DefaultPauseWriterThreshold = 65536;

	public static PipeOptions Default { get; } = new PipeOptions(null, null, null, -1L, -1L);


	public bool UseSynchronizationContext { get; }

	public long PauseWriterThreshold { get; }

	public long ResumeWriterThreshold { get; }

	public int MinimumSegmentSize { get; }

	public PipeScheduler WriterScheduler { get; }

	public PipeScheduler ReaderScheduler { get; }

	public MemoryPool<byte> Pool { get; }

	public PipeOptions(MemoryPool<byte> pool = null, PipeScheduler readerScheduler = null, PipeScheduler writerScheduler = null, long pauseWriterThreshold = -1L, long resumeWriterThreshold = -1L, int minimumSegmentSize = -1, bool useSynchronizationContext = true)
	{
		if (pauseWriterThreshold == -1)
		{
			pauseWriterThreshold = 65536L;
		}
		else if (pauseWriterThreshold < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.pauseWriterThreshold);
		}
		if (resumeWriterThreshold == -1)
		{
			resumeWriterThreshold = 32768L;
		}
		else if (resumeWriterThreshold < 0 || resumeWriterThreshold > pauseWriterThreshold)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.resumeWriterThreshold);
		}
		Pool = pool ?? MemoryPool<byte>.Shared;
		ReaderScheduler = readerScheduler ?? PipeScheduler.ThreadPool;
		WriterScheduler = writerScheduler ?? PipeScheduler.ThreadPool;
		PauseWriterThreshold = pauseWriterThreshold;
		ResumeWriterThreshold = resumeWriterThreshold;
		MinimumSegmentSize = ((minimumSegmentSize == -1) ? 4096 : minimumSegmentSize);
		UseSynchronizationContext = useSynchronizationContext;
	}
}
