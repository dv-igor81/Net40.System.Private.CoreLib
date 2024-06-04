using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace System;

[System.Diagnostics.StackTraceHidden]
internal static class ThrowHelper
{
	public static bool TryFormatThrowFormatException(out int bytesWritten)
	{
		bytesWritten = 0;
		ThrowFormatException_BadFormatSpecifier();
		return false;
	}

	public static bool TryParseThrowFormatException<T>(out T value, out int bytesConsumed)
	{
		value = default(T);
		bytesConsumed = 0;
		ThrowFormatException_BadFormatSpecifier();
		return false;
	}

	internal static void ThrowInvalidOperationException_EndPositionNotReached()
	{
		throw CreateInvalidOperationException_EndPositionNotReached();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateInvalidOperationException_EndPositionNotReached()
	{
		return new InvalidOperationException(SR.EndPositionNotReached);
	}

	internal static void ThrowArgumentOutOfRangeException_OffsetOutOfRange()
	{
		throw CreateArgumentOutOfRangeException_OffsetOutOfRange();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException_OffsetOutOfRange()
	{
		return new ArgumentOutOfRangeException("offset");
	}

	internal static void ThrowArgumentOutOfRangeException_PositionOutOfRange()
	{
		throw CreateArgumentOutOfRangeException_PositionOutOfRange();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException_PositionOutOfRange()
	{
		return new ArgumentOutOfRangeException("position");
	}

	public static void ThrowStartOrEndArgumentValidationException(long start)
	{
		throw CreateStartOrEndArgumentValidationException(start);
	}

	private static Exception CreateStartOrEndArgumentValidationException(long start)
	{
		if (start < 0)
		{
			return CreateArgumentOutOfRangeException(ExceptionArgument.start);
		}
		return CreateArgumentOutOfRangeException(ExceptionArgument.length);
	}

	public static void ThrowArgumentValidationException(Array array, int start)
	{
		throw CreateArgumentValidationException(array, start);
	}

	private static Exception CreateArgumentValidationException(Array array, int start)
	{
		if (array == null)
		{
			return CreateArgumentNullException(ExceptionArgument.array);
		}
		if ((uint)start > (uint)array.Length)
		{
			return CreateArgumentOutOfRangeException(ExceptionArgument.start);
		}
		return CreateArgumentOutOfRangeException(ExceptionArgument.length);
	}

	public static void ThrowArgumentValidationException<T>(ReadOnlySequenceSegment<T> startSegment, int startIndex, ReadOnlySequenceSegment<T> endSegment)
	{
		throw CreateArgumentValidationException(startSegment, startIndex, endSegment);
	}

	private static Exception CreateArgumentValidationException<T>(ReadOnlySequenceSegment<T> startSegment, int startIndex, ReadOnlySequenceSegment<T> endSegment)
	{
		if (startSegment == null)
		{
			return CreateArgumentNullException(ExceptionArgument.startSegment);
		}
		if (endSegment == null)
		{
			return CreateArgumentNullException(ExceptionArgument.endSegment);
		}
		if (startSegment != endSegment && startSegment.RunningIndex > endSegment.RunningIndex)
		{
			return CreateArgumentOutOfRangeException(ExceptionArgument.endSegment);
		}
		if ((uint)startSegment.Memory.Length < (uint)startIndex)
		{
			return CreateArgumentOutOfRangeException(ExceptionArgument.startIndex);
		}
		return CreateArgumentOutOfRangeException(ExceptionArgument.endIndex);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentNullException(ExceptionArgument argument)
	{
		return new ArgumentNullException(argument.ToString());
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateArgumentOutOfRangeException(ExceptionArgument argument)
	{
		return new ArgumentOutOfRangeException(argument.ToString());
	}

	internal static void ThrowObjectDisposedException_ArrayMemoryPoolBuffer()
	{
		throw CreateObjectDisposedException_ArrayMemoryPoolBuffer();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Exception CreateObjectDisposedException_ArrayMemoryPoolBuffer()
	{
		return new ObjectDisposedException("ArrayMemoryPoolBuffer");
	}

	[DoesNotReturn]
	internal static void ThrowArrayTypeMismatchException()
	{
		throw new ArrayTypeMismatchException();
	}

	[DoesNotReturn]
	internal static void ThrowInvalidTypeWithPointersNotSupported(Type targetType)
	{
		throw new ArgumentException(SR.Format(SR.Argument_InvalidTypeWithPointersNotSupported, targetType));
	}

	[DoesNotReturn]
	internal static void ThrowIndexOutOfRangeException()
	{
		throw new IndexOutOfRangeException();
	}

	[DoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException()
	{
		throw new ArgumentOutOfRangeException();
	}

	[DoesNotReturn]
	internal static void ThrowArgumentException_DestinationTooShort()
	{
		throw new ArgumentException(SR.Argument_DestinationTooShort, "destination");
	}

	[DoesNotReturn]
	internal static void ThrowArgumentException_OverlapAlignmentMismatch()
	{
		throw new ArgumentException(SR.Argument_OverlapAlignmentMismatch);
	}

	[DoesNotReturn]
	internal static void ThrowArgumentException_CannotExtractScalar(ExceptionArgument argument)
	{
		throw GetArgumentException(System.ExceptionResource.Argument_CannotExtractScalar, argument);
	}

	[DoesNotReturn]
	internal static void ThrowArgumentOutOfRange_IndexException()
	{
		throw GetArgumentOutOfRangeException(ExceptionArgument.index, System.ExceptionResource.ArgumentOutOfRange_Index);
	}

	[DoesNotReturn]
	internal static void ThrowIndexArgumentOutOfRange_NeedNonNegNumException()
	{
		throw GetArgumentOutOfRangeException(ExceptionArgument.index, System.ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
	}

	[DoesNotReturn]
	internal static void ThrowValueArgumentOutOfRange_NeedNonNegNumException()
	{
		throw GetArgumentOutOfRangeException(ExceptionArgument.value, System.ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
	}

	[DoesNotReturn]
	internal static void ThrowLengthArgumentOutOfRange_ArgumentOutOfRange_NeedNonNegNum()
	{
		throw GetArgumentOutOfRangeException(ExceptionArgument.length, System.ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
	}

	[DoesNotReturn]
	internal static void ThrowStartIndexArgumentOutOfRange_ArgumentOutOfRange_Index()
	{
		throw GetArgumentOutOfRangeException(ExceptionArgument.startIndex, System.ExceptionResource.ArgumentOutOfRange_Index);
	}

	[DoesNotReturn]
	internal static void ThrowCountArgumentOutOfRange_ArgumentOutOfRange_Count()
	{
		throw GetArgumentOutOfRangeException(ExceptionArgument.count, System.ExceptionResource.ArgumentOutOfRange_Count);
	}

	[DoesNotReturn]
	internal static void ThrowWrongKeyTypeArgumentException<T>(T key, Type targetType)
	{
		throw GetWrongKeyTypeArgumentException(key, targetType);
	}

	[DoesNotReturn]
	internal static void ThrowWrongValueTypeArgumentException<T>(T value, Type targetType)
	{
		throw GetWrongValueTypeArgumentException(value, targetType);
	}

	private static ArgumentException GetAddingDuplicateWithKeyArgumentException(object key)
	{
		return new ArgumentException(SR.Format(SR.Argument_AddingDuplicateWithKey, key));
	}

	[DoesNotReturn]
	internal static void ThrowAddingDuplicateWithKeyArgumentException<T>(T key)
	{
		throw GetAddingDuplicateWithKeyArgumentException(key);
	}

	[DoesNotReturn]
	internal static void ThrowKeyNotFoundException<T>(T key)
	{
		throw GetKeyNotFoundException(key);
	}

	[DoesNotReturn]
	internal static void ThrowArgumentException(System.ExceptionResource resource)
	{
		throw GetArgumentException(resource);
	}

	[DoesNotReturn]
	internal static void ThrowArgumentException(System.ExceptionResource resource, ExceptionArgument argument)
	{
		throw GetArgumentException(resource, argument);
	}

	private static ArgumentNullException GetArgumentNullException(ExceptionArgument argument)
	{
		return new ArgumentNullException(GetArgumentName(argument));
	}

	[DoesNotReturn]
	internal static void ThrowArgumentNullException(ExceptionArgument argument)
	{
		throw GetArgumentNullException(argument);
	}

	[DoesNotReturn]
	internal static void ThrowArgumentNullException(ExceptionArgument argument, System.ExceptionResource resource)
	{
		throw new ArgumentNullException(GetArgumentName(argument), GetResourceString(resource));
	}

	[DoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument)
	{
		throw new ArgumentOutOfRangeException(GetArgumentName(argument));
	}

	[DoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument, System.ExceptionResource resource)
	{
		throw GetArgumentOutOfRangeException(argument, resource);
	}

	[DoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument, int paramNumber, System.ExceptionResource resource)
	{
		throw GetArgumentOutOfRangeException(argument, paramNumber, resource);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException()
	{
		throw new InvalidOperationException();
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException(System.ExceptionResource resource)
	{
		throw GetInvalidOperationException(resource);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException(System.ExceptionResource resource, Exception e)
	{
		throw new InvalidOperationException(GetResourceString(resource), e);
	}

	[DoesNotReturn]
	internal static void ThrowSerializationException(System.ExceptionResource resource)
	{
		throw new SerializationException(GetResourceString(resource));
	}

	[DoesNotReturn]
	internal static void ThrowRankException(System.ExceptionResource resource)
	{
		throw new RankException(GetResourceString(resource));
	}

	[DoesNotReturn]
	internal static void ThrowNotSupportedException(System.ExceptionResource resource)
	{
		throw new NotSupportedException(GetResourceString(resource));
	}

	[DoesNotReturn]
	internal static void ThrowObjectDisposedException(System.ExceptionResource resource)
	{
		throw new ObjectDisposedException(null, GetResourceString(resource));
	}

	[DoesNotReturn]
	internal static void ThrowNotSupportedException()
	{
		throw new NotSupportedException();
	}

	[DoesNotReturn]
	internal static void ThrowAggregateException(List<Exception> exceptions)
	{
		throw new AggregateException(exceptions);
	}

	[DoesNotReturn]
	internal static void ThrowArgumentException_Argument_InvalidArrayType()
	{
		throw new ArgumentException(SR.Argument_InvalidArrayType);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_InvalidOperation_EnumNotStarted()
	{
		throw new InvalidOperationException(SR.InvalidOperation_EnumNotStarted);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_InvalidOperation_EnumEnded()
	{
		throw new InvalidOperationException(SR.InvalidOperation_EnumEnded);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_EnumCurrent(int index)
	{
		throw GetInvalidOperationException_EnumCurrent(index);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_InvalidOperation_EnumFailedVersion()
	{
		throw new InvalidOperationException(SR.InvalidOperation_EnumFailedVersion);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_InvalidOperation_EnumOpCantHappen()
	{
		throw new InvalidOperationException(SR.InvalidOperation_EnumOpCantHappen);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_InvalidOperation_NoValue()
	{
		throw new InvalidOperationException(SR.InvalidOperation_NoValue);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_ConcurrentOperationsNotSupported()
	{
		throw new InvalidOperationException(SR.InvalidOperation_ConcurrentOperationsNotSupported);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_HandleIsNotInitialized()
	{
		throw new InvalidOperationException(SR.InvalidOperation_HandleIsNotInitialized);
	}

	[DoesNotReturn]
	internal static void ThrowInvalidOperationException_HandleIsNotPinned()
	{
		throw new InvalidOperationException(SR.InvalidOperation_HandleIsNotPinned);
	}

	[DoesNotReturn]
	internal static void ThrowArraySegmentCtorValidationFailedExceptions(Array array, int offset, int count)
	{
		throw GetArraySegmentCtorValidationFailedException(array, offset, count);
	}

	[DoesNotReturn]
	internal static void ThrowFormatException_BadFormatSpecifier()
	{
		throw new FormatException(SR.Argument_BadFormatSpecifier);
	}

	[DoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException_PrecisionTooLarge()
	{
		throw new ArgumentOutOfRangeException("precision", SR.Format(SR.Argument_PrecisionTooLarge, (byte)99));
	}

	[DoesNotReturn]
	internal static void ThrowArgumentOutOfRangeException_SymbolDoesNotFit()
	{
		throw new ArgumentOutOfRangeException("symbol", SR.Argument_BadFormatSpecifier);
	}

	private static Exception GetArraySegmentCtorValidationFailedException(Array array, int offset, int count)
	{
		if (array == null)
		{
			return new ArgumentNullException("array");
		}
		if (offset < 0)
		{
			return new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (count < 0)
		{
			return new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		return new ArgumentException(SR.Argument_InvalidOffLen);
	}

	private static ArgumentException GetArgumentException(System.ExceptionResource resource)
	{
		return new ArgumentException(GetResourceString(resource));
	}

	private static InvalidOperationException GetInvalidOperationException(System.ExceptionResource resource)
	{
		return new InvalidOperationException(GetResourceString(resource));
	}

	private static ArgumentException GetWrongKeyTypeArgumentException(object key, Type targetType)
	{
		return new ArgumentException(SR.Format(SR.Arg_WrongType, key, targetType), "key");
	}

	private static ArgumentException GetWrongValueTypeArgumentException(object value, Type targetType)
	{
		return new ArgumentException(SR.Format(SR.Arg_WrongType, value, targetType), "value");
	}

	private static KeyNotFoundException GetKeyNotFoundException(object key)
	{
		return new KeyNotFoundException(SR.Format(SR.Arg_KeyNotFoundWithKey, key));
	}

	private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(ExceptionArgument argument, System.ExceptionResource resource)
	{
		return new ArgumentOutOfRangeException(GetArgumentName(argument), GetResourceString(resource));
	}

	private static ArgumentException GetArgumentException(System.ExceptionResource resource, ExceptionArgument argument)
	{
		return new ArgumentException(GetResourceString(resource), GetArgumentName(argument));
	}

	private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(ExceptionArgument argument, int paramNumber, System.ExceptionResource resource)
	{
		return new ArgumentOutOfRangeException(GetArgumentName(argument) + "[" + paramNumber + "]", GetResourceString(resource));
	}

	private static InvalidOperationException GetInvalidOperationException_EnumCurrent(int index)
	{
		return new InvalidOperationException((index < 0) ? SR.InvalidOperation_EnumNotStarted : SR.InvalidOperation_EnumEnded);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static void IfNullAndNullsAreIllegalThenThrow<T>(object value, ExceptionArgument argName)
	{
		if (default(T) != null && value == null)
		{
			ThrowArgumentNullException(argName);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static void ThrowForUnsupportedVectorBaseType<T>() where T : struct
	{
		if (typeof(T) != typeof(byte) && typeof(T) != typeof(sbyte) && typeof(T) != typeof(short) && typeof(T) != typeof(ushort) && typeof(T) != typeof(int) && typeof(T) != typeof(uint) && typeof(T) != typeof(long) && typeof(T) != typeof(ulong) && typeof(T) != typeof(float) && typeof(T) != typeof(double))
		{
			ThrowNotSupportedException(System.ExceptionResource.Arg_TypeNotSupported);
		}
	}

	private static string GetArgumentName(ExceptionArgument argument)
	{
		if (1 == 0)
		{
		}
		string result = argument switch
		{
			ExceptionArgument.obj => "obj", 
			ExceptionArgument.dictionary => "dictionary", 
			ExceptionArgument.array => "array", 
			ExceptionArgument.info => "info", 
			ExceptionArgument.key => "key", 
			ExceptionArgument.text => "text", 
			ExceptionArgument.values => "values", 
			ExceptionArgument.value => "value", 
			ExceptionArgument.startIndex => "startIndex", 
			ExceptionArgument.task => "task", 
			ExceptionArgument.bytes => "bytes", 
			ExceptionArgument.byteIndex => "byteIndex", 
			ExceptionArgument.byteCount => "byteCount", 
			ExceptionArgument.ch => "ch", 
			ExceptionArgument.chars => "chars", 
			ExceptionArgument.charIndex => "charIndex", 
			ExceptionArgument.charCount => "charCount", 
			ExceptionArgument.s => "s", 
			ExceptionArgument.input => "input", 
			ExceptionArgument.ownedMemory => "ownedMemory", 
			ExceptionArgument.list => "list", 
			ExceptionArgument.index => "index", 
			ExceptionArgument.capacity => "capacity", 
			ExceptionArgument.collection => "collection", 
			ExceptionArgument.item => "item", 
			ExceptionArgument.converter => "converter", 
			ExceptionArgument.match => "match", 
			ExceptionArgument.count => "count", 
			ExceptionArgument.action => "action", 
			ExceptionArgument.comparison => "comparison", 
			ExceptionArgument.exceptions => "exceptions", 
			ExceptionArgument.exception => "exception", 
			ExceptionArgument.pointer => "pointer", 
			ExceptionArgument.start => "start", 
			ExceptionArgument.format => "format", 
			ExceptionArgument.culture => "culture", 
			ExceptionArgument.comparer => "comparer", 
			ExceptionArgument.comparable => "comparable", 
			ExceptionArgument.source => "source", 
			ExceptionArgument.state => "state", 
			ExceptionArgument.length => "length", 
			ExceptionArgument.comparisonType => "comparisonType", 
			ExceptionArgument.manager => "manager", 
			ExceptionArgument.sourceBytesToCopy => "sourceBytesToCopy", 
			ExceptionArgument.callBack => "callBack", 
			ExceptionArgument.creationOptions => "creationOptions", 
			ExceptionArgument.function => "function", 
			ExceptionArgument.scheduler => "scheduler", 
			ExceptionArgument.continuationAction => "continuationAction", 
			ExceptionArgument.continuationFunction => "continuationFunction", 
			ExceptionArgument.tasks => "tasks", 
			ExceptionArgument.asyncResult => "asyncResult", 
			ExceptionArgument.beginMethod => "beginMethod", 
			ExceptionArgument.endMethod => "endMethod", 
			ExceptionArgument.endFunction => "endFunction", 
			ExceptionArgument.cancellationToken => "cancellationToken", 
			ExceptionArgument.continuationOptions => "continuationOptions", 
			ExceptionArgument.delay => "delay", 
			ExceptionArgument.millisecondsDelay => "millisecondsDelay", 
			ExceptionArgument.millisecondsTimeout => "millisecondsTimeout", 
			ExceptionArgument.stateMachine => "stateMachine", 
			ExceptionArgument.timeout => "timeout", 
			ExceptionArgument.type => "type", 
			ExceptionArgument.sourceIndex => "sourceIndex", 
			ExceptionArgument.sourceArray => "sourceArray", 
			ExceptionArgument.destinationIndex => "destinationIndex", 
			ExceptionArgument.destinationArray => "destinationArray", 
			ExceptionArgument.pHandle => "pHandle", 
			ExceptionArgument.other => "other", 
			ExceptionArgument.newSize => "newSize", 
			ExceptionArgument.lowerBounds => "lowerBounds", 
			ExceptionArgument.lengths => "lengths", 
			ExceptionArgument.len => "len", 
			ExceptionArgument.keys => "keys", 
			ExceptionArgument.indices => "indices", 
			ExceptionArgument.index1 => "index1", 
			ExceptionArgument.index2 => "index2", 
			ExceptionArgument.index3 => "index3", 
			ExceptionArgument.length1 => "length1", 
			ExceptionArgument.length2 => "length2", 
			ExceptionArgument.length3 => "length3", 
			ExceptionArgument.endIndex => "endIndex", 
			ExceptionArgument.elementType => "elementType", 
			ExceptionArgument.arrayIndex => "arrayIndex", 
			_ => "", 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static string GetResourceString(System.ExceptionResource resource)
	{
		if (1 == 0)
		{
		}
		string result = resource switch
		{
			System.ExceptionResource.ArgumentOutOfRange_Index => SR.ArgumentOutOfRange_Index, 
			System.ExceptionResource.ArgumentOutOfRange_IndexCount => SR.ArgumentOutOfRange_IndexCount, 
			System.ExceptionResource.ArgumentOutOfRange_IndexCountBuffer => SR.ArgumentOutOfRange_IndexCountBuffer, 
			System.ExceptionResource.ArgumentOutOfRange_Count => SR.ArgumentOutOfRange_Count, 
			System.ExceptionResource.Arg_ArrayPlusOffTooSmall => SR.Arg_ArrayPlusOffTooSmall, 
			System.ExceptionResource.NotSupported_ReadOnlyCollection => SR.NotSupported_ReadOnlyCollection, 
			System.ExceptionResource.Arg_RankMultiDimNotSupported => SR.Arg_RankMultiDimNotSupported, 
			System.ExceptionResource.Arg_NonZeroLowerBound => SR.Arg_NonZeroLowerBound, 
			System.ExceptionResource.ArgumentOutOfRange_ListInsert => SR.ArgumentOutOfRange_ListInsert, 
			System.ExceptionResource.ArgumentOutOfRange_NeedNonNegNum => SR.ArgumentOutOfRange_NeedNonNegNum, 
			System.ExceptionResource.ArgumentOutOfRange_SmallCapacity => SR.ArgumentOutOfRange_SmallCapacity, 
			System.ExceptionResource.Argument_InvalidOffLen => SR.Argument_InvalidOffLen, 
			System.ExceptionResource.Argument_CannotExtractScalar => SR.Argument_CannotExtractScalar, 
			System.ExceptionResource.ArgumentOutOfRange_BiggerThanCollection => SR.ArgumentOutOfRange_BiggerThanCollection, 
			System.ExceptionResource.Serialization_MissingKeys => SR.Serialization_MissingKeys, 
			System.ExceptionResource.Serialization_NullKey => SR.Serialization_NullKey, 
			System.ExceptionResource.NotSupported_KeyCollectionSet => SR.NotSupported_KeyCollectionSet, 
			System.ExceptionResource.NotSupported_ValueCollectionSet => SR.NotSupported_ValueCollectionSet, 
			System.ExceptionResource.InvalidOperation_NullArray => SR.InvalidOperation_NullArray, 
			System.ExceptionResource.TaskT_TransitionToFinal_AlreadyCompleted => SR.TaskT_TransitionToFinal_AlreadyCompleted, 
			System.ExceptionResource.TaskCompletionSourceT_TrySetException_NullException => SR.TaskCompletionSourceT_TrySetException_NullException, 
			System.ExceptionResource.TaskCompletionSourceT_TrySetException_NoExceptions => SR.TaskCompletionSourceT_TrySetException_NoExceptions, 
			System.ExceptionResource.NotSupported_StringComparison => SR.NotSupported_StringComparison, 
			System.ExceptionResource.ConcurrentCollection_SyncRoot_NotSupported => SR.ConcurrentCollection_SyncRoot_NotSupported, 
			System.ExceptionResource.Task_MultiTaskContinuation_NullTask => SR.Task_MultiTaskContinuation_NullTask, 
			System.ExceptionResource.InvalidOperation_WrongAsyncResultOrEndCalledMultiple => SR.InvalidOperation_WrongAsyncResultOrEndCalledMultiple, 
			System.ExceptionResource.Task_MultiTaskContinuation_EmptyTaskList => SR.Task_MultiTaskContinuation_EmptyTaskList, 
			System.ExceptionResource.Task_Start_TaskCompleted => SR.Task_Start_TaskCompleted, 
			System.ExceptionResource.Task_Start_Promise => SR.Task_Start_Promise, 
			System.ExceptionResource.Task_Start_ContinuationTask => SR.Task_Start_ContinuationTask, 
			System.ExceptionResource.Task_Start_AlreadyStarted => SR.Task_Start_AlreadyStarted, 
			System.ExceptionResource.Task_RunSynchronously_Continuation => SR.Task_RunSynchronously_Continuation, 
			System.ExceptionResource.Task_RunSynchronously_Promise => SR.Task_RunSynchronously_Promise, 
			System.ExceptionResource.Task_RunSynchronously_TaskCompleted => SR.Task_RunSynchronously_TaskCompleted, 
			System.ExceptionResource.Task_RunSynchronously_AlreadyStarted => SR.Task_RunSynchronously_AlreadyStarted, 
			System.ExceptionResource.AsyncMethodBuilder_InstanceNotInitialized => SR.AsyncMethodBuilder_InstanceNotInitialized, 
			System.ExceptionResource.Task_ContinueWith_ESandLR => SR.Task_ContinueWith_ESandLR, 
			System.ExceptionResource.Task_ContinueWith_NotOnAnything => SR.Task_ContinueWith_NotOnAnything, 
			System.ExceptionResource.Task_Delay_InvalidDelay => SR.Task_Delay_InvalidDelay, 
			System.ExceptionResource.Task_Delay_InvalidMillisecondsDelay => SR.Task_Delay_InvalidMillisecondsDelay, 
			System.ExceptionResource.Task_Dispose_NotCompleted => SR.Task_Dispose_NotCompleted, 
			System.ExceptionResource.Task_ThrowIfDisposed => SR.Task_ThrowIfDisposed, 
			System.ExceptionResource.Task_WaitMulti_NullTask => SR.Task_WaitMulti_NullTask, 
			System.ExceptionResource.ArgumentException_OtherNotArrayOfCorrectLength => SR.ArgumentException_OtherNotArrayOfCorrectLength, 
			System.ExceptionResource.ArgumentNull_Array => SR.ArgumentNull_Array, 
			System.ExceptionResource.ArgumentNull_SafeHandle => SR.ArgumentNull_SafeHandle, 
			System.ExceptionResource.ArgumentOutOfRange_EndIndexStartIndex => SR.ArgumentOutOfRange_EndIndexStartIndex, 
			System.ExceptionResource.ArgumentOutOfRange_Enum => SR.ArgumentOutOfRange_Enum, 
			System.ExceptionResource.ArgumentOutOfRange_HugeArrayNotSupported => SR.ArgumentOutOfRange_HugeArrayNotSupported, 
			System.ExceptionResource.Argument_AddingDuplicate => SR.Argument_AddingDuplicate, 
			System.ExceptionResource.Argument_InvalidArgumentForComparison => SR.Argument_InvalidArgumentForComparison, 
			System.ExceptionResource.Arg_LowerBoundsMustMatch => SR.Arg_LowerBoundsMustMatch, 
			System.ExceptionResource.Arg_MustBeType => SR.Arg_MustBeType, 
			System.ExceptionResource.Arg_Need1DArray => SR.Arg_Need1DArray, 
			System.ExceptionResource.Arg_Need2DArray => SR.Arg_Need2DArray, 
			System.ExceptionResource.Arg_Need3DArray => SR.Arg_Need3DArray, 
			System.ExceptionResource.Arg_NeedAtLeast1Rank => SR.Arg_NeedAtLeast1Rank, 
			System.ExceptionResource.Arg_RankIndices => SR.Arg_RankIndices, 
			System.ExceptionResource.Arg_RanksAndBounds => SR.Arg_RanksAndBounds, 
			System.ExceptionResource.InvalidOperation_IComparerFailed => SR.InvalidOperation_IComparerFailed, 
			System.ExceptionResource.NotSupported_FixedSizeCollection => SR.NotSupported_FixedSizeCollection, 
			System.ExceptionResource.Rank_MultiDimNotSupported => SR.Rank_MultiDimNotSupported, 
			System.ExceptionResource.Arg_TypeNotSupported => SR.Arg_TypeNotSupported, 
			_ => "", 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
