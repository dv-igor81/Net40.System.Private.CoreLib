using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;
using Net40.System.Private.CoreLib.Resources;

namespace System;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class SR
{
	private static ResourceManager _sResourceManager;

	private static readonly string ErrorMessage;

	private static readonly object Lock;

	private static List<string> _currentlyLoading;

	private static int _infinitelyRecursingCount;

	private static bool _resourceManagerInited;

	private static ResourceManager ResourceManager => _sResourceManager;

	internal static string EndPositionNotReached => GetResourceString("EndPositionNotReached");

	internal static string Arg_ArgumentOutOfRangeException => GetResourceString("Arg_ArgumentOutOfRangeException");

	internal static string Arg_ArrayPlusOffTooSmall => GetResourceString("Arg_ArrayPlusOffTooSmall");

	internal static string Arg_EnumIllegalVal => GetResourceString("Arg_EnumIllegalVal");

	internal static string Arg_KeyNotFoundWithKey => GetResourceString("Arg_KeyNotFoundWithKey");

	internal static string Arg_LowerBoundsMustMatch => GetResourceString("Arg_LowerBoundsMustMatch");

	internal static string Arg_MustBeType => GetResourceString("Arg_MustBeType");

	internal static string Arg_Need1DArray => GetResourceString("Arg_Need1DArray");

	internal static string Arg_Need2DArray => GetResourceString("Arg_Need2DArray");

	internal static string Arg_Need3DArray => GetResourceString("Arg_Need3DArray");

	internal static string Arg_NeedAtLeast1Rank => GetResourceString("Arg_NeedAtLeast1Rank");

	internal static string Arg_NonZeroLowerBound => GetResourceString("Arg_NonZeroLowerBound");

	internal static string Arg_RankIndices => GetResourceString("Arg_RankIndices");

	internal static string Arg_RankMultiDimNotSupported => GetResourceString("Arg_RankMultiDimNotSupported");

	internal static string Arg_RanksAndBounds => GetResourceString("Arg_RanksAndBounds");

	internal static string Arg_WrongType => GetResourceString("Arg_WrongType");

	internal static string Argument_AddingDuplicate => GetResourceString("Argument_AddingDuplicate");

	internal static string Argument_AddingDuplicateWithKey => GetResourceString("Argument_AddingDuplicateWithKey");

	internal static string Argument_BadFormatSpecifier => GetResourceString("Argument_BadFormatSpecifier");

	internal static string Argument_DestinationTooShort => GetResourceString("Argument_DestinationTooShort");

	internal static string Argument_InvalidArgumentForComparison => GetResourceString("Argument_InvalidArgumentForComparison");

	internal static string Argument_InvalidArrayType => GetResourceString("Argument_InvalidArrayType");

	internal static string Argument_InvalidOffLen => GetResourceString("Argument_InvalidOffLen");

	internal static string Argument_InvalidSeekOrigin => GetResourceString("Argument_InvalidSeekOrigin");

	internal static string Argument_InvalidTypeName => GetResourceString("Argument_InvalidTypeName");

	internal static string Argument_InvalidTypeWithPointersNotSupported => GetResourceString("Argument_InvalidTypeWithPointersNotSupported");

	internal static string ArgumentException_BufferNotFromPool => GetResourceString("ArgumentException_BufferNotFromPool");

	internal static string ArgumentException_OtherNotArrayOfCorrectLength => GetResourceString("ArgumentException_OtherNotArrayOfCorrectLength");

	internal static string ArgumentException_ValueTupleIncorrectType => GetResourceString("ArgumentException_ValueTupleIncorrectType");

	internal static string ArgumentException_ValueTupleLastArgumentNotAValueTuple => GetResourceString("ArgumentException_ValueTupleLastArgumentNotAValueTuple");

	internal static string ArgumentNull_Array => GetResourceString("ArgumentNull_Array");

	internal static string ArgumentNull_Buffer => GetResourceString("ArgumentNull_Buffer");

	internal static string ArgumentNull_SafeHandle => GetResourceString("ArgumentNull_SafeHandle");

	internal static string ArgumentNull_Stream => GetResourceString("ArgumentNull_Stream");

	internal static string ArgumentOutOfRange_BiggerThanCollection => GetResourceString("ArgumentOutOfRange_BiggerThanCollection");

	internal static string ArgumentOutOfRange_Count => GetResourceString("ArgumentOutOfRange_Count");

	internal static string ArgumentOutOfRange_EndIndexStartIndex => GetResourceString("ArgumentOutOfRange_EndIndexStartIndex");

	internal static string ArgumentOutOfRange_Enum => GetResourceString("ArgumentOutOfRange_Enum");

	internal static string ArgumentOutOfRange_HugeArrayNotSupported => GetResourceString("ArgumentOutOfRange_HugeArrayNotSupported");

	internal static string ArgumentOutOfRange_Index => GetResourceString("ArgumentOutOfRange_Index");

	internal static string ArgumentOutOfRange_IndexCount => GetResourceString("ArgumentOutOfRange_IndexCount");

	internal static string ArgumentOutOfRange_IndexCountBuffer => GetResourceString("ArgumentOutOfRange_IndexCountBuffer");

	internal static string ArgumentOutOfRange_ListInsert => GetResourceString("ArgumentOutOfRange_ListInsert");

	internal static string ArgumentOutOfRange_NeedNonNegNum => GetResourceString("ArgumentOutOfRange_NeedNonNegNum");

	internal static string ArgumentOutOfRange_NeedPosNum => GetResourceString("ArgumentOutOfRange_NeedPosNum");

	internal static string ArgumentOutOfRange_NeedValidId => GetResourceString("ArgumentOutOfRange_NeedValidId");

	internal static string ArgumentOutOfRange_SmallCapacity => GetResourceString("ArgumentOutOfRange_SmallCapacity");

	internal static string ArgumentOutOfRange_StreamLength => GetResourceString("ArgumentOutOfRange_StreamLength");

	internal static string AsyncMethodBuilder_InstanceNotInitialized => GetResourceString("AsyncMethodBuilder_InstanceNotInitialized");

	internal static string ConcurrentCollection_SyncRoot_NotSupported => GetResourceString("ConcurrentCollection_SyncRoot_NotSupported");

	internal static string EventSource_AbstractMustNotDeclareEventMethods => GetResourceString("EventSource_AbstractMustNotDeclareEventMethods");

	internal static string EventSource_AbstractMustNotDeclareKTOC => GetResourceString("EventSource_AbstractMustNotDeclareKTOC");

	internal static string EventSource_DuplicateStringKey => GetResourceString("EventSource_DuplicateStringKey");

	internal static string EventSource_EnumKindMismatch => GetResourceString("EventSource_EnumKindMismatch");

	internal static string EventSource_EventIdReused => GetResourceString("EventSource_EventIdReused");

	internal static string EventSource_EventMustHaveTaskIfNonDefaultOpcode => GetResourceString("EventSource_EventMustHaveTaskIfNonDefaultOpcode");

	internal static string EventSource_EventMustNotBeExplicitImplementation => GetResourceString("EventSource_EventMustNotBeExplicitImplementation");

	internal static string EventSource_EventNameReused => GetResourceString("EventSource_EventNameReused");

	internal static string EventSource_EventParametersMismatch => GetResourceString("EventSource_EventParametersMismatch");

	internal static string EventSource_EventSourceGuidInUse => GetResourceString("EventSource_EventSourceGuidInUse");

	internal static string EventSource_EventTooBig => GetResourceString("EventSource_EventTooBig");

	internal static string EventSource_IllegalKeywordsValue => GetResourceString("EventSource_IllegalKeywordsValue");

	internal static string EventSource_IllegalOpcodeValue => GetResourceString("EventSource_IllegalOpcodeValue");

	internal static string EventSource_IllegalTaskValue => GetResourceString("EventSource_IllegalTaskValue");

	internal static string EventSource_InvalidCommand => GetResourceString("EventSource_InvalidCommand");

	internal static string EventSource_InvalidEventFormat => GetResourceString("EventSource_InvalidEventFormat");

	internal static string EventSource_KeywordCollision => GetResourceString("EventSource_KeywordCollision");

	internal static string EventSource_KeywordNeedPowerOfTwo => GetResourceString("EventSource_KeywordNeedPowerOfTwo");

	internal static string EventSource_ListenerCreatedInsideCallback => GetResourceString("EventSource_ListenerCreatedInsideCallback");

	internal static string EventSource_ListenerNotFound => GetResourceString("EventSource_ListenerNotFound");

	internal static string EventSource_ListenerWriteFailure => GetResourceString("EventSource_ListenerWriteFailure");

	internal static string EventSource_MismatchIdToWriteEvent => GetResourceString("EventSource_MismatchIdToWriteEvent");

	internal static string EventSource_NeedGuid => GetResourceString("EventSource_NeedGuid");

	internal static string EventSource_NeedName => GetResourceString("EventSource_NeedName");

	internal static string EventSource_NeedPositiveId => GetResourceString("EventSource_NeedPositiveId");

	internal static string EventSource_NoFreeBuffers => GetResourceString("EventSource_NoFreeBuffers");

	internal static string EventSource_NoRelatedActivityId => GetResourceString("EventSource_NoRelatedActivityId");

	internal static string EventSource_OpcodeCollision => GetResourceString("EventSource_OpcodeCollision");

	internal static string EventSource_StopsFollowStarts => GetResourceString("EventSource_StopsFollowStarts");

	internal static string EventSource_TaskCollision => GetResourceString("EventSource_TaskCollision");

	internal static string EventSource_TaskOpcodePairReused => GetResourceString("EventSource_TaskOpcodePairReused");

	public static string EventSource_ToString => GetResourceString("EventSource_ToString");

	internal static string EventSource_TraitEven => GetResourceString("EventSource_TraitEven");

	internal static string EventSource_TypeMustBeSealedOrAbstract => GetResourceString("EventSource_TypeMustBeSealedOrAbstract");

	internal static string EventSource_TypeMustDeriveFromEventSource => GetResourceString("EventSource_TypeMustDeriveFromEventSource");

	internal static string EventSource_UndefinedKeyword => GetResourceString("EventSource_UndefinedKeyword");

	internal static string EventSource_UndefinedOpcode => GetResourceString("EventSource_UndefinedOpcode");

	internal static string EventSource_UnsupportedEventTypeInManifest => GetResourceString("EventSource_UnsupportedEventTypeInManifest");

	internal static string EventSource_UnsupportedMessageProperty => GetResourceString("EventSource_UnsupportedMessageProperty");

	internal static string EventSource_VarArgsParameterMismatch => GetResourceString("EventSource_VarArgsParameterMismatch");

	internal static string InvalidOperation_ConcurrentOperationsNotSupported => GetResourceString("InvalidOperation_ConcurrentOperationsNotSupported");

	internal static string InvalidOperation_EnumEnded => GetResourceString("InvalidOperation_EnumEnded");

	internal static string InvalidOperation_EnumFailedVersion => GetResourceString("InvalidOperation_EnumFailedVersion");

	internal static string InvalidOperation_EnumNotStarted => GetResourceString("InvalidOperation_EnumNotStarted");

	internal static string InvalidOperation_EnumOpCantHappen => GetResourceString("InvalidOperation_EnumOpCantHappen");

	internal static string InvalidOperation_HandleIsNotInitialized => GetResourceString("InvalidOperation_HandleIsNotInitialized");

	internal static string InvalidOperation_HandleIsNotPinned => GetResourceString("InvalidOperation_HandleIsNotPinned");

	internal static string InvalidOperation_IComparerFailed => GetResourceString("InvalidOperation_IComparerFailed");

	internal static string InvalidOperation_NoValue => GetResourceString("InvalidOperation_NoValue");

	internal static string InvalidOperation_NullArray => GetResourceString("InvalidOperation_NullArray");

	internal static string InvalidOperation_ResourceNotString_Name => GetResourceString("InvalidOperation_ResourceNotString_Name");

	internal static string InvalidOperation_WrongAsyncResultOrEndCalledMultiple => GetResourceString("InvalidOperation_WrongAsyncResultOrEndCalledMultiple");

	internal static string IO_EOF_ReadBeyondEOF => GetResourceString("IO_EOF_ReadBeyondEOF");

	internal static string IO_SeekBeforeBegin => GetResourceString("IO_SeekBeforeBegin");

	internal static string IO_StreamTooLong => GetResourceString("IO_StreamTooLong");

	internal static string NotSupported_CannotCallEqualsOnSpan => GetResourceString("NotSupported_CannotCallEqualsOnSpan");

	internal static string NotSupported_CannotCallGetHashCodeOnSpan => GetResourceString("NotSupported_CannotCallGetHashCodeOnSpan");

	internal static string NotSupported_FixedSizeCollection => GetResourceString("NotSupported_FixedSizeCollection");

	internal static string NotSupported_KeyCollectionSet => GetResourceString("NotSupported_KeyCollectionSet");

	internal static string NotSupported_MemStreamNotExpandable => GetResourceString("NotSupported_MemStreamNotExpandable");

	internal static string NotSupported_ReadOnlyCollection => GetResourceString("NotSupported_ReadOnlyCollection");

	internal static string NotSupported_StringComparison => GetResourceString("NotSupported_StringComparison");

	internal static string NotSupported_UnreadableStream => GetResourceString("NotSupported_UnreadableStream");

	internal static string NotSupported_UnseekableStream => GetResourceString("NotSupported_UnseekableStream");

	internal static string NotSupported_UnwritableStream => GetResourceString("NotSupported_UnwritableStream");

	internal static string NotSupported_ValueCollectionSet => GetResourceString("NotSupported_ValueCollectionSet");

	internal static string ObjectDisposed_FileClosed => GetResourceString("ObjectDisposed_FileClosed");

	internal static string ObjectDisposed_ResourceSet => GetResourceString("ObjectDisposed_ResourceSet");

	internal static string ObjectDisposed_StreamClosed => GetResourceString("ObjectDisposed_StreamClosed");

	internal static string Rank_MultiDimNotSupported => GetResourceString("Rank_MultiDimNotSupported");

	internal static string Serialization_MissingKeys => GetResourceString("Serialization_MissingKeys");

	internal static string Serialization_NullKey => GetResourceString("Serialization_NullKey");

	internal static string Task_ContinueWith_ESandLR => GetResourceString("Task_ContinueWith_ESandLR");

	internal static string Task_ContinueWith_NotOnAnything => GetResourceString("Task_ContinueWith_NotOnAnything");

	internal static string Task_Delay_InvalidDelay => GetResourceString("Task_Delay_InvalidDelay");

	internal static string Task_Delay_InvalidMillisecondsDelay => GetResourceString("Task_Delay_InvalidMillisecondsDelay");

	internal static string Task_Dispose_NotCompleted => GetResourceString("Task_Dispose_NotCompleted");

	internal static string Task_MultiTaskContinuation_EmptyTaskList => GetResourceString("Task_MultiTaskContinuation_EmptyTaskList");

	internal static string Task_MultiTaskContinuation_NullTask => GetResourceString("Task_MultiTaskContinuation_NullTask");

	internal static string Task_RunSynchronously_AlreadyStarted => GetResourceString("Task_RunSynchronously_AlreadyStarted");

	internal static string Task_RunSynchronously_Continuation => GetResourceString("Task_RunSynchronously_Continuation");

	internal static string Task_RunSynchronously_Promise => GetResourceString("Task_RunSynchronously_Promise");

	internal static string Task_RunSynchronously_TaskCompleted => GetResourceString("Task_RunSynchronously_TaskCompleted");

	internal static string Task_Start_AlreadyStarted => GetResourceString("Task_Start_AlreadyStarted");

	internal static string Task_Start_ContinuationTask => GetResourceString("Task_Start_ContinuationTask");

	internal static string Task_Start_Promise => GetResourceString("Task_Start_Promise");

	internal static string Task_Start_TaskCompleted => GetResourceString("Task_Start_TaskCompleted");

	internal static string Task_ThrowIfDisposed => GetResourceString("Task_ThrowIfDisposed");

	internal static string Task_WaitMulti_NullTask => GetResourceString("Task_WaitMulti_NullTask");

	internal static string TaskCompletionSourceT_TrySetException_NoExceptions => GetResourceString("TaskCompletionSourceT_TrySetException_NoExceptions");

	internal static string TaskCompletionSourceT_TrySetException_NullException => GetResourceString("TaskCompletionSourceT_TrySetException_NullException");

	internal static string TaskT_TransitionToFinal_AlreadyCompleted => GetResourceString("TaskT_TransitionToFinal_AlreadyCompleted");

	internal static string Arg_TypeNotSupported => GetResourceString("Arg_TypeNotSupported");

	internal static string Arg_ElementsInSourceIsGreaterThanDestination => GetResourceString("Arg_ElementsInSourceIsGreaterThanDestination");

	internal static string Arg_NullArgumentNullRef => GetResourceString("Arg_NullArgumentNullRef");

	internal static string Argument_OverlapAlignmentMismatch => GetResourceString("Argument_OverlapAlignmentMismatch");

	internal static string Arg_InsufficientNumberOfElements => GetResourceString("Arg_InsufficientNumberOfElements");

	internal static string Argument_CannotExtractScalar => GetResourceString("Argument_CannotExtractScalar");

	internal static string Argument_CannotParsePrecision => GetResourceString("Argument_CannotParsePrecision");

	internal static string Argument_GWithPrecisionNotSupported => GetResourceString("Argument_GWithPrecisionNotSupported");

	internal static string Argument_PrecisionTooLarge => GetResourceString("Argument_PrecisionTooLarge");

	static SR()
	{
		Lock = new object();
		_resourceManagerInited = false;
		_sResourceManager ??= new ResourceManager(typeof(global::Net40.System.Private.CoreLib.Resources.Strings));
		ErrorMessage = "Бесконечная рекурсия во время поиска ресурсов в Net40.System.Private.CoreLib. Это может быть ошибка в Net40.System.Private.CoreLib или, возможно, в определенных точках расширения, таких как события разрешения сборки или имена CultureInfo";
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool UsingResourceKeys()
	{
		return false;
	}

	public static string GetResourceString(string resourceKey)
	{
		return GetResourceString(resourceKey, null);
	}

	public static string Format(IFormatProvider provider, string resourceFormat, params object[] args)
	{
		if (args != null)
		{
			if (UsingResourceKeys())
			{
				return resourceFormat + ", " + string.Join(", ", args);
			}
			return string.Format(provider, resourceFormat, args);
		}
		return resourceFormat;
	}

	public static string Format(string resourceFormat, params object[] args)
	{
		if (args != null)
		{
			if (UsingResourceKeys())
			{
				return resourceFormat + ", " + string.Join(", ", args);
			}
			return string.Format(resourceFormat, args);
		}
		return resourceFormat;
	}

	public static string Format(string resourceFormat, object p1)
	{
		if (UsingResourceKeys())
		{
			return string.Join(", ", resourceFormat, p1);
		}
		return string.Format(resourceFormat, p1);
	}

	public static string Format(string resourceFormat, object p1, object p2)
	{
		if (UsingResourceKeys())
		{
			return string.Join(", ", resourceFormat, p1, p2);
		}
		return string.Format(resourceFormat, p1, p2);
	}

	public static string Format(string resourceFormat, object p1, object p2, object p3)
	{
		if (UsingResourceKeys())
		{
			return string.Join(", ", resourceFormat, p1, p2, p3);
		}
		return string.Format(resourceFormat, p1, p2, p3);
	}

	private static string GetResourceString(string resourceKey, string defaultString)
	{
		string text = null;
		try
		{
			text = InternalGetResourceString(resourceKey, ErrorMessage);
		}
		catch (MissingManifestResourceException)
		{
		}
		if (defaultString != null && resourceKey.Equals(text, StringComparison.Ordinal))
		{
			return defaultString;
		}
		return text;
	}

	private static string InternalGetResourceString(string key, string message)
	{
		if (string.IsNullOrEmpty(key))
		{
			return key;
		}
		bool lockTaken = false;
		try
		{
			Monitor.Enter(Lock, ref lockTaken);
			if (_currentlyLoading != null && _currentlyLoading.Count > 0 && _currentlyLoading.LastIndexOf(key) != -1)
			{
				if (_infinitelyRecursingCount > 0)
				{
					return key;
				}
				_infinitelyRecursingCount++;
				Environment.FailFast(message + " Resource name: " + key + ".");
			}
			if (_currentlyLoading == null)
			{
				_currentlyLoading = new List<string>();
			}
			if (!_resourceManagerInited)
			{
				RuntimeHelpers.RunClassConstructor(typeof(ResourceManager).TypeHandle);
				RuntimeHelpers.RunClassConstructor(typeof(ResourceReader).TypeHandle);
				RuntimeHelpers.RunClassConstructor(typeof(BinaryReader).TypeHandle);
				_resourceManagerInited = true;
			}
			_currentlyLoading.Add(key);
			string @string = ResourceManager.GetString(key, null);
			_currentlyLoading.RemoveAt(_currentlyLoading.Count - 1);
			return @string ?? key;
		}
		catch
		{
			if (lockTaken)
			{
				_sResourceManager = null;
				_currentlyLoading = null;
			}
			throw;
		}
		finally
		{
			if (lockTaken)
			{
				Monitor.Exit(Lock);
			}
		}
	}
}
