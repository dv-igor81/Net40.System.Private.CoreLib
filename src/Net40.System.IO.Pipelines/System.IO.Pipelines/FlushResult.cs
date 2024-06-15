namespace System.IO.Pipelines;

public struct FlushResult
{
	internal ResultFlags _resultFlags;

	public bool IsCanceled => (_resultFlags & ResultFlags.Canceled) != 0;

	public bool IsCompleted => (_resultFlags & ResultFlags.Completed) != 0;

	public FlushResult(bool isCanceled, bool isCompleted)
	{
		_resultFlags = ResultFlags.None;
		if (isCanceled)
		{
			_resultFlags |= ResultFlags.Canceled;
		}
		if (isCompleted)
		{
			_resultFlags |= ResultFlags.Completed;
		}
	}
}
