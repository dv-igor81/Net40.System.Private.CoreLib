using System.Buffers;

namespace System.IO.Pipelines;

public readonly struct ReadResult
{
	internal readonly ReadOnlySequence<byte> _resultBuffer;

	internal readonly ResultFlags _resultFlags;

	public ReadOnlySequence<byte> Buffer => _resultBuffer;

	public bool IsCanceled => (_resultFlags & ResultFlags.Canceled) != 0;

	public bool IsCompleted => (_resultFlags & ResultFlags.Completed) != 0;

	public ReadResult(ReadOnlySequence<byte> buffer, bool isCanceled, bool isCompleted)
	{
		_resultBuffer = buffer;
		_resultFlags = ResultFlags.None;
		if (isCompleted)
		{
			_resultFlags |= ResultFlags.Completed;
		}
		if (isCanceled)
		{
			_resultFlags |= ResultFlags.Canceled;
		}
	}
}
