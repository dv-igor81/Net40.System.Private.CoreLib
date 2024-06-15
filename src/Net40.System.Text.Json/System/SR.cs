using System.Collections.Generic;
using System.IO;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System;

public static class SR
{
	private static ResourceManager? _sResourceManager;

	private static readonly string ErrorMessage;

	private static readonly object Lock;

	private static List<string>? _currentlyLoading;

	private static int _infinitelyRecursingCount;

	private static bool _resourceManagerInited;

	private static ResourceManager ResourceManager => _sResourceManager;

	public static string PropertyNameTooLarge => GetResourceString("PropertyNameTooLarge");

	public static string ValueTooLarge => GetResourceString("ValueTooLarge");

	public static string DepthTooLarge => GetResourceString("DepthTooLarge");

	public static string ZeroDepthAtEnd => GetResourceString("ZeroDepthAtEnd");

	public static string EmptyJsonIsInvalid => GetResourceString("EmptyJsonIsInvalid");

	public static string CannotSkip => GetResourceString("CannotSkip");

	public static string InvalidCast => GetResourceString("InvalidCast");

	public static string InvalidComparison => GetResourceString("InvalidComparison");

	public static string JsonElementHasWrongType => GetResourceString("JsonElementHasWrongType");

	public static string ArrayDepthTooLarge => GetResourceString("ArrayDepthTooLarge");

	public static string MismatchedObjectArray => GetResourceString("MismatchedObjectArray");

	public static string TrailingCommaNotAllowedBeforeArrayEnd => GetResourceString("TrailingCommaNotAllowedBeforeArrayEnd");

	public static string TrailingCommaNotAllowedBeforeObjectEnd => GetResourceString("TrailingCommaNotAllowedBeforeObjectEnd");

	public static string EndOfStringNotFound => GetResourceString("EndOfStringNotFound");

	public static string RequiredDigitNotFoundAfterSign => GetResourceString("RequiredDigitNotFoundAfterSign");

	public static string RequiredDigitNotFoundAfterDecimal => GetResourceString("RequiredDigitNotFoundAfterDecimal");

	public static string RequiredDigitNotFoundEndOfData => GetResourceString("RequiredDigitNotFoundEndOfData");

	public static string ExpectedEndAfterSingleJson => GetResourceString("ExpectedEndAfterSingleJson");

	public static string ExpectedEndOfDigitNotFound => GetResourceString("ExpectedEndOfDigitNotFound");

	public static string ExpectedNextDigitEValueNotFound => GetResourceString("ExpectedNextDigitEValueNotFound");

	public static string ExpectedSeparatorAfterPropertyNameNotFound => GetResourceString("ExpectedSeparatorAfterPropertyNameNotFound");

	public static string ExpectedStartOfPropertyNotFound => GetResourceString("ExpectedStartOfPropertyNotFound");

	public static string ExpectedStartOfPropertyOrValueNotFound => GetResourceString("ExpectedStartOfPropertyOrValueNotFound");

	public static string ExpectedStartOfPropertyOrValueAfterComment => GetResourceString("ExpectedStartOfPropertyOrValueAfterComment");

	public static string ExpectedStartOfValueNotFound => GetResourceString("ExpectedStartOfValueNotFound");

	public static string ExpectedValueAfterPropertyNameNotFound => GetResourceString("ExpectedValueAfterPropertyNameNotFound");

	public static string FoundInvalidCharacter => GetResourceString("FoundInvalidCharacter");

	public static string InvalidEndOfJsonNonPrimitive => GetResourceString("InvalidEndOfJsonNonPrimitive");

	public static string ObjectDepthTooLarge => GetResourceString("ObjectDepthTooLarge");

	public static string ExpectedFalse => GetResourceString("ExpectedFalse");

	public static string ExpectedNull => GetResourceString("ExpectedNull");

	public static string ExpectedTrue => GetResourceString("ExpectedTrue");

	public static string InvalidCharacterWithinString => GetResourceString("InvalidCharacterWithinString");

	public static string InvalidCharacterAfterEscapeWithinString => GetResourceString("InvalidCharacterAfterEscapeWithinString");

	public static string InvalidHexCharacterWithinString => GetResourceString("InvalidHexCharacterWithinString");

	public static string EndOfCommentNotFound => GetResourceString("EndOfCommentNotFound");

	public static string ExpectedJsonTokens => GetResourceString("ExpectedJsonTokens");

	public static string NotEnoughData => GetResourceString("NotEnoughData");

	public static string ExpectedOneCompleteToken => GetResourceString("ExpectedOneCompleteToken");

	public static string InvalidCharacterAtStartOfComment => GetResourceString("InvalidCharacterAtStartOfComment");

	public static string UnexpectedEndOfDataWhileReadingComment => GetResourceString("UnexpectedEndOfDataWhileReadingComment");

	public static string UnexpectedEndOfLineSeparator => GetResourceString("UnexpectedEndOfLineSeparator");

	public static string CannotWriteCommentWithEmbeddedDelimiter => GetResourceString("CannotWriteCommentWithEmbeddedDelimiter");

	public static string CannotEncodeInvalidUTF8 => GetResourceString("CannotEncodeInvalidUTF8");

	public static string CannotEncodeInvalidUTF16 => GetResourceString("CannotEncodeInvalidUTF16");

	public static string CannotReadInvalidUTF16 => GetResourceString("CannotReadInvalidUTF16");

	public static string CannotReadIncompleteUTF16 => GetResourceString("CannotReadIncompleteUTF16");

	public static string CannotTranscodeInvalidUtf8 => GetResourceString("CannotTranscodeInvalidUtf8");

	public static string CannotTranscodeInvalidUtf16 => GetResourceString("CannotTranscodeInvalidUtf16");

	public static string CannotWriteEndAfterProperty => GetResourceString("CannotWriteEndAfterProperty");

	public static string CannotStartObjectArrayWithoutProperty => GetResourceString("CannotStartObjectArrayWithoutProperty");

	public static string CannotStartObjectArrayAfterPrimitiveOrClose => GetResourceString("CannotStartObjectArrayAfterPrimitiveOrClose");

	public static string CannotWriteValueWithinObject => GetResourceString("CannotWriteValueWithinObject");

	public static string CannotWritePropertyAfterProperty => GetResourceString("CannotWritePropertyAfterProperty");

	public static string CannotWritePropertyWithinArray => GetResourceString("CannotWritePropertyWithinArray");

	public static string CannotWriteValueAfterPrimitiveOrClose => GetResourceString("CannotWriteValueAfterPrimitiveOrClose");

	public static string FormatByte => GetResourceString("FormatByte");

	public static string FormatSByte => GetResourceString("FormatSByte");

	public static string FormatInt16 => GetResourceString("FormatInt16");

	public static string FormatInt32 => GetResourceString("FormatInt32");

	public static string FormatInt64 => GetResourceString("FormatInt64");

	public static string FormatUInt16 => GetResourceString("FormatUInt16");

	public static string FormatUInt32 => GetResourceString("FormatUInt32");

	public static string FormatUInt64 => GetResourceString("FormatUInt64");

	public static string FormatSingle => GetResourceString("FormatSingle");

	public static string FormatDouble => GetResourceString("FormatDouble");

	public static string FormatDecimal => GetResourceString("FormatDecimal");

	public static string FormatDateTime => GetResourceString("FormatDateTime");

	public static string FormatDateTimeOffset => GetResourceString("FormatDateTimeOffset");

	public static string CannotDecodeInvalidBase64 => GetResourceString("CannotDecodeInvalidBase64");

	public static string FormatGuid => GetResourceString("FormatGuid");

	public static string MaxDepthMustBePositive => GetResourceString("MaxDepthMustBePositive");

	public static string CallFlushToAvoidDataLoss => GetResourceString("CallFlushToAvoidDataLoss");

	public static string SpecialNumberValuesNotSupported => GetResourceString("SpecialNumberValuesNotSupported");

	public static string FailedToGetLargerSpan => GetResourceString("FailedToGetLargerSpan");

	static SR()
	{
		Lock = new object();
		_resourceManagerInited = false;
		if (_sResourceManager == null)
		{
			_sResourceManager = new ResourceManager(typeof(Strings));
		}
		ErrorMessage = "Бесконечная рекурсия во время поиска ресурсов в Net40.System.Text.Json. Это может быть ошибка в Net40.System.Text.Json или, возможно, в определенных точках расширения, таких как события разрешения сборки или имена CultureInfo";
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
