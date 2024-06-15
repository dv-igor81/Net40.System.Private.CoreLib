using System.Buffers;

namespace System.IO.Pipelines;

public class StreamPipeWriterOptions
{
	private const int DefaultMinimumBufferSize = 4096;

	internal static StreamPipeWriterOptions s_default = new StreamPipeWriterOptions();

	public int MinimumBufferSize { get; }

	public MemoryPool<byte> Pool { get; }

	public bool LeaveOpen { get; }

	public StreamPipeWriterOptions(MemoryPool<byte> pool = null, int minimumBufferSize = -1, bool leaveOpen = false)
	{
		Pool = pool ?? MemoryPool<byte>.Shared;
		int num;
		if (minimumBufferSize != -1)
		{
			if (minimumBufferSize <= 0)
			{
				throw new ArgumentOutOfRangeException("minimumBufferSize");
			}
			num = minimumBufferSize;
		}
		else
		{
			num = 4096;
		}
		MinimumBufferSize = num;
		LeaveOpen = leaveOpen;
	}
}
