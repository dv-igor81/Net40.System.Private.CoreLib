using System.Runtime.CompilerServices;

namespace System.IO.Pipelines;

internal static class ThrowHelper
{
	internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument)
	{
		throw CreateArgumentOutOfRangeException(argument);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException(ExceptionArgument argument)
	{
		return new ArgumentOutOfRangeException(argument.ToString());
	}

	internal static void ThrowArgumentNullException(ExceptionArgument argument)
	{
		throw CreateArgumentNullException(argument);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentNullException(ExceptionArgument argument)
	{
		return new ArgumentNullException(argument.ToString());
	}

	public static void ThrowInvalidOperationException_AlreadyReading()
	{
		throw CreateInvalidOperationException_AlreadyReading();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_AlreadyReading()
	{
		return new InvalidOperationException(SR.ReadingIsInProgress);
	}

	public static void ThrowInvalidOperationException_NoReadToComplete()
	{
		throw CreateInvalidOperationException_NoReadToComplete();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_NoReadToComplete()
	{
		return new InvalidOperationException(SR.NoReadingOperationToComplete);
	}

	public static void ThrowInvalidOperationException_NoConcurrentOperation()
	{
		throw CreateInvalidOperationException_NoConcurrentOperation();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_NoConcurrentOperation()
	{
		return new InvalidOperationException(SR.ConcurrentOperationsNotSupported);
	}

	public static void ThrowInvalidOperationException_GetResultNotCompleted()
	{
		throw CreateInvalidOperationException_GetResultNotCompleted();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_GetResultNotCompleted()
	{
		return new InvalidOperationException(SR.GetResultBeforeCompleted);
	}

	public static void ThrowInvalidOperationException_NoWritingAllowed()
	{
		throw CreateInvalidOperationException_NoWritingAllowed();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_NoWritingAllowed()
	{
		return new InvalidOperationException(SR.WritingAfterCompleted);
	}

	public static void ThrowInvalidOperationException_NoReadingAllowed()
	{
		throw CreateInvalidOperationException_NoReadingAllowed();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_NoReadingAllowed()
	{
		return new InvalidOperationException(SR.ReadingAfterCompleted);
	}

	public static void ThrowInvalidOperationException_InvalidExaminedPosition()
	{
		throw CreateInvalidOperationException_InvalidExaminedPosition();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_InvalidExaminedPosition()
	{
		return new InvalidOperationException(SR.InvalidExaminedPosition);
	}

	public static void ThrowInvalidOperationException_InvalidExaminedOrConsumedPosition()
	{
		throw CreateInvalidOperationException_InvalidExaminedOrConsumedPosition();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_InvalidExaminedOrConsumedPosition()
	{
		return new InvalidOperationException(SR.InvalidExaminedOrConsumedPosition);
	}

	public static void ThrowInvalidOperationException_AdvanceToInvalidCursor()
	{
		throw CreateInvalidOperationException_AdvanceToInvalidCursor();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_AdvanceToInvalidCursor()
	{
		return new InvalidOperationException(SR.AdvanceToInvalidCursor);
	}

	public static void ThrowInvalidOperationException_ResetIncompleteReaderWriter()
	{
		throw CreateInvalidOperationException_ResetIncompleteReaderWriter();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_ResetIncompleteReaderWriter()
	{
		return new InvalidOperationException(SR.ReaderAndWriterHasToBeCompleted);
	}

	public static void ThrowOperationCanceledException_ReadCanceled()
	{
		throw CreateOperationCanceledException_ReadCanceled();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateOperationCanceledException_ReadCanceled()
	{
		return new OperationCanceledException(SR.ReadCanceledOnPipeReader);
	}

	public static void ThrowOperationCanceledException_FlushCanceled()
	{
		throw CreateOperationCanceledException_FlushCanceled();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateOperationCanceledException_FlushCanceled()
	{
		return new OperationCanceledException(SR.FlushCanceledOnPipeWriter);
	}

	public static void ThrowInvalidOperationException_InvalidZeroByteRead()
	{
		throw CreateInvalidOperationException_InvalidZeroByteRead();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static Exception CreateInvalidOperationException_InvalidZeroByteRead()
	{
		return new InvalidOperationException(SR.InvalidZeroByteRead);
	}
}
