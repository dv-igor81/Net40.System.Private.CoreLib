using System.Collections.Generic;
using System.IO;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;
using Net40.System.IO.Pipelines.Resources;

namespace System;

internal static class SR
{
	private static ResourceManager? _sResourceManager;

	private static readonly string ErrorMessage;

	private static readonly object Lock;

	private static List<string>? _currentlyLoading;

	private static int _infinitelyRecursingCount;

	private static bool _resourceManagerInited;

	private static ResourceManager ResourceManager => _sResourceManager;

	internal static string AdvanceToInvalidCursor => GetResourceString("AdvanceToInvalidCursor");

	internal static string ConcurrentOperationsNotSupported => GetResourceString("ConcurrentOperationsNotSupported");

	internal static string FlushCanceledOnPipeWriter => GetResourceString("FlushCanceledOnPipeWriter");

	internal static string GetResultBeforeCompleted => GetResourceString("GetResultBeforeCompleted");

	internal static string InvalidExaminedOrConsumedPosition => GetResourceString("InvalidExaminedOrConsumedPosition");

	internal static string InvalidExaminedPosition => GetResourceString("InvalidExaminedPosition");

	internal static string InvalidZeroByteRead => GetResourceString("InvalidZeroByteRead");

	internal static string NoReadingOperationToComplete => GetResourceString("NoReadingOperationToComplete");

	internal static string ReadCanceledOnPipeReader => GetResourceString("ReadCanceledOnPipeReader");

	internal static string ReaderAndWriterHasToBeCompleted => GetResourceString("ReaderAndWriterHasToBeCompleted");

	internal static string ReadingAfterCompleted => GetResourceString("ReadingAfterCompleted");

	internal static string ReadingIsInProgress => GetResourceString("ReadingIsInProgress");

	internal static string WritingAfterCompleted => GetResourceString("WritingAfterCompleted");

	static SR()
	{
		Lock = new object();
		_resourceManagerInited = false;
		if (_sResourceManager == null)
		{
			_sResourceManager = new ResourceManager(typeof(Strings));
		}
		ErrorMessage = "Бесконечная рекурсия во время поиска ресурсов в Net40.System.Private.CoreLib. Это может быть ошибка в Net40.System.Private.CoreLib или, возможно, в определенных точках расширения, таких как события разрешения сборки или имена CultureInfo";
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool UsingResourceKeys()
	{
		return false;
	}

	private static string? GetResourceString(string resourceKey)
	{
		return GetResourceString(resourceKey, null);
	}

	public static string Format(IFormatProvider provider, string resourceFormat, params object[]? args)
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

	public static string Format(string resourceFormat, params object[]? args)
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

	private static string? GetResourceString(string resourceKey, string? defaultString)
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

	private static string? InternalGetResourceString(string? key, string message)
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
