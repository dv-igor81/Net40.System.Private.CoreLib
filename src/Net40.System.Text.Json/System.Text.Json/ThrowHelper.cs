#define DEBUG
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace System.Text.Json;

internal static class ThrowHelper
{
	public const string ExceptionSourceValueToRethrowAsJsonException = "System.Text.Json.Rethrowable";

	public static ArgumentOutOfRangeException GetArgumentOutOfRangeException_MaxDepthMustBePositive(string parameterName)
	{
		return GetArgumentOutOfRangeException(parameterName, SR.MaxDepthMustBePositive);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static ArgumentOutOfRangeException GetArgumentOutOfRangeException(string parameterName, string message)
	{
		return new ArgumentOutOfRangeException(parameterName, message);
	}

	public static ArgumentOutOfRangeException GetArgumentOutOfRangeException_CommentEnumMustBeInRange(string parameterName)
	{
		return GetArgumentOutOfRangeException(parameterName, "SR.CommentHandlingMustBeValid");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static ArgumentException GetArgumentException(string message)
	{
		return new ArgumentException(message);
	}

	private static void ThrowArgumentException(string message)
	{
		throw GetArgumentException(message);
	}

	public static InvalidOperationException GetInvalidOperationException_CallFlushFirst(int _buffered)
	{
		return GetInvalidOperationException(SR.Format(SR.CallFlushToAvoidDataLoss, _buffered));
	}

	public static void ThrowArgumentException_PropertyNameTooLarge(int tokenLength)
	{
		throw GetArgumentException(SR.Format(SR.PropertyNameTooLarge, tokenLength));
	}

	public static void ThrowArgumentException_ValueTooLarge(int tokenLength)
	{
		throw GetArgumentException(SR.Format(SR.ValueTooLarge, tokenLength));
	}

	public static void ThrowArgumentException_ValueNotSupported()
	{
		throw GetArgumentException(SR.SpecialNumberValuesNotSupported);
	}

	public static void ThrowInvalidOperationException_NeedLargerSpan()
	{
		throw GetInvalidOperationException(SR.FailedToGetLargerSpan);
	}

	public static void ThrowArgumentException(ReadOnlySpan<byte> propertyName, ReadOnlySpan<byte> value)
	{
		if (propertyName.Length > 166666666)
		{
			ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
			return;
		}
		Debug.Assert(value.Length > 166666666);
		ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
	}

	public static void ThrowArgumentException(ReadOnlySpan<byte> propertyName, ReadOnlySpan<char> value)
	{
		if (propertyName.Length > 166666666)
		{
			ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
			return;
		}
		Debug.Assert(value.Length > 166666666);
		ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
	}

	public static void ThrowArgumentException(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
	{
		if (propertyName.Length > 166666666)
		{
			ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
			return;
		}
		Debug.Assert(value.Length > 166666666);
		ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
	}

	public static void ThrowArgumentException(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
	{
		if (propertyName.Length > 166666666)
		{
			ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
			return;
		}
		Debug.Assert(value.Length > 166666666);
		ThrowArgumentException(SR.Format(SR.ValueTooLarge, value.Length));
	}

	public static void ThrowInvalidOperationOrArgumentException(ReadOnlySpan<byte> propertyName, int currentDepth)
	{
		currentDepth &= 0x7FFFFFFF;
		if (currentDepth >= 1000)
		{
			ThrowInvalidOperationException(SR.Format(SR.DepthTooLarge, currentDepth, 1000));
			return;
		}
		Debug.Assert(propertyName.Length > 166666666);
		ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
	}

	public static void ThrowInvalidOperationException(int currentDepth)
	{
		currentDepth &= 0x7FFFFFFF;
		Debug.Assert(currentDepth >= 1000);
		ThrowInvalidOperationException(SR.Format(SR.DepthTooLarge, currentDepth, 1000));
	}

	public static void ThrowInvalidOperationException(string message)
	{
		throw GetInvalidOperationException(message);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static InvalidOperationException GetInvalidOperationException(string message)
	{
		InvalidOperationException ex = new InvalidOperationException(message);
		ex.Source = "System.Text.Json.Rethrowable";
		return ex;
	}

	public static void ThrowInvalidOperationException_DepthNonZeroOrEmptyJson(int currentDepth)
	{
		throw GetInvalidOperationException(currentDepth);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static InvalidOperationException GetInvalidOperationException(int currentDepth)
	{
		currentDepth &= 0x7FFFFFFF;
		if (currentDepth != 0)
		{
			return GetInvalidOperationException(SR.Format(SR.ZeroDepthAtEnd, currentDepth));
		}
		return GetInvalidOperationException(SR.EmptyJsonIsInvalid);
	}

	public static void ThrowInvalidOperationOrArgumentException(ReadOnlySpan<char> propertyName, int currentDepth)
	{
		currentDepth &= 0x7FFFFFFF;
		if (currentDepth >= 1000)
		{
			ThrowInvalidOperationException(SR.Format(SR.DepthTooLarge, currentDepth, 1000));
			return;
		}
		Debug.Assert(propertyName.Length > 166666666);
		ThrowArgumentException(SR.Format(SR.PropertyNameTooLarge, propertyName.Length));
	}

	public static InvalidOperationException GetInvalidOperationException_ExpectedNumber(JsonTokenType tokenType)
	{
		return GetInvalidOperationException("number", tokenType);
	}

	public static InvalidOperationException GetInvalidOperationException_ExpectedBoolean(JsonTokenType tokenType)
	{
		return GetInvalidOperationException("boolean", tokenType);
	}

	public static InvalidOperationException GetInvalidOperationException_ExpectedString(JsonTokenType tokenType)
	{
		return GetInvalidOperationException("string", tokenType);
	}

	public static InvalidOperationException GetInvalidOperationException_ExpectedStringComparison(JsonTokenType tokenType)
	{
		return GetInvalidOperationException(tokenType);
	}

	public static InvalidOperationException GetInvalidOperationException_ExpectedComment(JsonTokenType tokenType)
	{
		return GetInvalidOperationException("comment", tokenType);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static InvalidOperationException GetInvalidOperationException_CannotSkipOnPartial()
	{
		return GetInvalidOperationException(SR.CannotSkip);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static InvalidOperationException GetInvalidOperationException(string message, JsonTokenType tokenType)
	{
		return GetInvalidOperationException(SR.Format(SR.InvalidCast, tokenType, message));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static InvalidOperationException GetInvalidOperationException(JsonTokenType tokenType)
	{
		return GetInvalidOperationException(SR.Format(SR.InvalidComparison, tokenType));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal static InvalidOperationException GetJsonElementWrongTypeException(JsonTokenType expectedType, JsonTokenType actualType)
	{
		return GetInvalidOperationException(SR.Format(SR.JsonElementHasWrongType, expectedType.ToValueKind(), actualType.ToValueKind()));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	internal static InvalidOperationException GetJsonElementWrongTypeException(string expectedTypeName, JsonTokenType actualType)
	{
		return GetInvalidOperationException(SR.Format(SR.JsonElementHasWrongType, expectedTypeName, actualType.ToValueKind()));
	}

	public static void ThrowJsonReaderException(ref Utf8JsonReader json, ExceptionResource resource, byte nextByte = 0, ReadOnlySpan<byte> bytes = default(ReadOnlySpan<byte>))
	{
		throw GetJsonReaderException(ref json, resource, nextByte, bytes);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static JsonException GetJsonReaderException(ref Utf8JsonReader json, ExceptionResource resource, byte nextByte, ReadOnlySpan<byte> bytes)
	{
		string message = GetResourceString(ref json, resource, nextByte, JsonHelpers.Utf8GetString(bytes));
		long lineNumber = json.CurrentState._lineNumber;
		long bytePositionInLine = json.CurrentState._bytePositionInLine;
		message += $" LineNumber: {lineNumber} | BytePositionInLine: {bytePositionInLine}.";
		return new JsonReaderException(message, lineNumber, bytePositionInLine);
	}

	private static bool IsPrintable(byte value)
	{
		return value >= 32 && value < 127;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static string GetPrintableString(byte value)
	{
		string result;
		if (!IsPrintable(value))
		{
			result = $"0x{value:X2}";
		}
		else
		{
			char c = (char)value;
			result = c.ToString();
		}
		return result;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string GetResourceString(ref Utf8JsonReader json, ExceptionResource resource, byte nextByte, string characters)
	{
		string character = GetPrintableString(nextByte);
		string message = "";
		switch (resource)
		{
		case ExceptionResource.ArrayDepthTooLarge:
			message = SR.Format(SR.ArrayDepthTooLarge, json.CurrentState.Options.MaxDepth);
			break;
		case ExceptionResource.MismatchedObjectArray:
			message = SR.Format(SR.MismatchedObjectArray, character);
			break;
		case ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd:
			message = SR.TrailingCommaNotAllowedBeforeArrayEnd;
			break;
		case ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd:
			message = SR.TrailingCommaNotAllowedBeforeObjectEnd;
			break;
		case ExceptionResource.EndOfStringNotFound:
			message = SR.EndOfStringNotFound;
			break;
		case ExceptionResource.RequiredDigitNotFoundAfterSign:
			message = SR.Format(SR.RequiredDigitNotFoundAfterSign, character);
			break;
		case ExceptionResource.RequiredDigitNotFoundAfterDecimal:
			message = SR.Format(SR.RequiredDigitNotFoundAfterDecimal, character);
			break;
		case ExceptionResource.RequiredDigitNotFoundEndOfData:
			message = SR.RequiredDigitNotFoundEndOfData;
			break;
		case ExceptionResource.ExpectedEndAfterSingleJson:
			message = SR.Format(SR.ExpectedEndAfterSingleJson, character);
			break;
		case ExceptionResource.ExpectedEndOfDigitNotFound:
			message = SR.Format(SR.ExpectedEndOfDigitNotFound, character);
			break;
		case ExceptionResource.ExpectedNextDigitEValueNotFound:
			message = SR.Format(SR.ExpectedNextDigitEValueNotFound, character);
			break;
		case ExceptionResource.ExpectedSeparatorAfterPropertyNameNotFound:
			message = SR.Format(SR.ExpectedSeparatorAfterPropertyNameNotFound, character);
			break;
		case ExceptionResource.ExpectedStartOfPropertyNotFound:
			message = SR.Format(SR.ExpectedStartOfPropertyNotFound, character);
			break;
		case ExceptionResource.ExpectedStartOfPropertyOrValueNotFound:
			message = SR.ExpectedStartOfPropertyOrValueNotFound;
			break;
		case ExceptionResource.ExpectedStartOfPropertyOrValueAfterComment:
			message = SR.Format(SR.ExpectedStartOfPropertyOrValueAfterComment, character);
			break;
		case ExceptionResource.ExpectedStartOfValueNotFound:
			message = SR.Format(SR.ExpectedStartOfValueNotFound, character);
			break;
		case ExceptionResource.ExpectedValueAfterPropertyNameNotFound:
			message = SR.ExpectedValueAfterPropertyNameNotFound;
			break;
		case ExceptionResource.FoundInvalidCharacter:
			message = SR.Format(SR.FoundInvalidCharacter, character);
			break;
		case ExceptionResource.InvalidEndOfJsonNonPrimitive:
			message = SR.Format(SR.InvalidEndOfJsonNonPrimitive, json.TokenType);
			break;
		case ExceptionResource.ObjectDepthTooLarge:
			message = SR.Format(SR.ObjectDepthTooLarge, json.CurrentState.Options.MaxDepth);
			break;
		case ExceptionResource.ExpectedFalse:
			message = SR.Format(SR.ExpectedFalse, characters);
			break;
		case ExceptionResource.ExpectedNull:
			message = SR.Format(SR.ExpectedNull, characters);
			break;
		case ExceptionResource.ExpectedTrue:
			message = SR.Format(SR.ExpectedTrue, characters);
			break;
		case ExceptionResource.InvalidCharacterWithinString:
			message = SR.Format(SR.InvalidCharacterWithinString, character);
			break;
		case ExceptionResource.InvalidCharacterAfterEscapeWithinString:
			message = SR.Format(SR.InvalidCharacterAfterEscapeWithinString, character);
			break;
		case ExceptionResource.InvalidHexCharacterWithinString:
			message = SR.Format(SR.InvalidHexCharacterWithinString, character);
			break;
		case ExceptionResource.EndOfCommentNotFound:
			message = SR.EndOfCommentNotFound;
			break;
		case ExceptionResource.ZeroDepthAtEnd:
			message = SR.Format(SR.ZeroDepthAtEnd);
			break;
		case ExceptionResource.ExpectedJsonTokens:
			message = SR.ExpectedJsonTokens;
			break;
		case ExceptionResource.NotEnoughData:
			message = SR.NotEnoughData;
			break;
		case ExceptionResource.ExpectedOneCompleteToken:
			message = SR.ExpectedOneCompleteToken;
			break;
		case ExceptionResource.InvalidCharacterAtStartOfComment:
			message = SR.Format(SR.InvalidCharacterAtStartOfComment, character);
			break;
		case ExceptionResource.UnexpectedEndOfDataWhileReadingComment:
			message = SR.Format(SR.UnexpectedEndOfDataWhileReadingComment);
			break;
		case ExceptionResource.UnexpectedEndOfLineSeparator:
			message = SR.Format(SR.UnexpectedEndOfLineSeparator);
			break;
		default:
			Debug.Fail($"The ExceptionResource enum value: {resource} is not part of the switch. Add the appropriate case and exception message.");
			break;
		}
		return message;
	}

	public static void ThrowInvalidOperationException(ExceptionResource resource, int currentDepth, byte token, JsonTokenType tokenType)
	{
		throw GetInvalidOperationException(resource, currentDepth, token, tokenType);
	}

	public static void ThrowArgumentException_InvalidCommentValue()
	{
		throw new ArgumentException(SR.CannotWriteCommentWithEmbeddedDelimiter);
	}

	public static void ThrowArgumentException_InvalidUTF8(ReadOnlySpan<byte> value)
	{
		StringBuilder builder = new StringBuilder();
		int printFirst10 = Math.Min(value.Length, 10);
		for (int i = 0; i < printFirst10; i++)
		{
			byte nextByte = value[i];
			if (IsPrintable(nextByte))
			{
				builder.Append((char)nextByte);
			}
			else
			{
				builder.Append($"0x{nextByte:X2}");
			}
		}
		if (printFirst10 < value.Length)
		{
			builder.Append("...");
		}
		throw new ArgumentException(SR.Format(SR.CannotEncodeInvalidUTF8, builder));
	}

	public static void ThrowArgumentException_InvalidUTF16(int charAsInt)
	{
		throw new ArgumentException(SR.Format(SR.CannotEncodeInvalidUTF16, $"0x{charAsInt:X2}"));
	}

	public static void ThrowInvalidOperationException_ReadInvalidUTF16(int charAsInt)
	{
		throw GetInvalidOperationException(SR.Format(SR.CannotReadInvalidUTF16, $"0x{charAsInt:X2}"));
	}

	public static void ThrowInvalidOperationException_ReadInvalidUTF16()
	{
		throw GetInvalidOperationException(SR.CannotReadIncompleteUTF16);
	}

	public static InvalidOperationException GetInvalidOperationException_ReadInvalidUTF8(DecoderFallbackException innerException)
	{
		return GetInvalidOperationException(SR.CannotTranscodeInvalidUtf8, innerException);
	}

	public static ArgumentException GetArgumentException_ReadInvalidUTF16(EncoderFallbackException innerException)
	{
		return new ArgumentException(SR.CannotTranscodeInvalidUtf16, innerException);
	}

	public static InvalidOperationException GetInvalidOperationException(string message, Exception innerException)
	{
		InvalidOperationException ex = new InvalidOperationException(message, innerException);
		ex.Source = "System.Text.Json.Rethrowable";
		return ex;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static InvalidOperationException GetInvalidOperationException(ExceptionResource resource, int currentDepth, byte token, JsonTokenType tokenType)
	{
		string message = GetResourceString(resource, currentDepth, token, tokenType);
		InvalidOperationException ex = GetInvalidOperationException(message);
		ex.Source = "System.Text.Json.Rethrowable";
		return ex;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string GetResourceString(ExceptionResource resource, int currentDepth, byte token, JsonTokenType tokenType)
	{
		string message = "";
		switch (resource)
		{
		case ExceptionResource.MismatchedObjectArray:
			Debug.Assert(token == 93 || token == 125);
			message = ((tokenType == JsonTokenType.PropertyName) ? SR.Format(SR.CannotWriteEndAfterProperty, (char)token) : SR.Format(SR.MismatchedObjectArray, (char)token));
			break;
		case ExceptionResource.DepthTooLarge:
			message = SR.Format(SR.DepthTooLarge, currentDepth & 0x7FFFFFFF, 1000);
			break;
		case ExceptionResource.CannotStartObjectArrayWithoutProperty:
			message = SR.Format(SR.CannotStartObjectArrayWithoutProperty, tokenType);
			break;
		case ExceptionResource.CannotStartObjectArrayAfterPrimitiveOrClose:
			message = SR.Format(SR.CannotStartObjectArrayAfterPrimitiveOrClose, tokenType);
			break;
		case ExceptionResource.CannotWriteValueWithinObject:
			message = SR.Format(SR.CannotWriteValueWithinObject, tokenType);
			break;
		case ExceptionResource.CannotWritePropertyWithinArray:
			message = ((tokenType == JsonTokenType.PropertyName) ? SR.Format(SR.CannotWritePropertyAfterProperty) : SR.Format(SR.CannotWritePropertyWithinArray, tokenType));
			break;
		case ExceptionResource.CannotWriteValueAfterPrimitiveOrClose:
			message = SR.Format(SR.CannotWriteValueAfterPrimitiveOrClose, tokenType);
			break;
		default:
			Debug.Fail($"The ExceptionResource enum value: {resource} is not part of the switch. Add the appropriate case and exception message.");
			break;
		}
		return message;
	}

	public static FormatException GetFormatException()
	{
		FormatException ex = new FormatException();
		ex.Source = "System.Text.Json.Rethrowable";
		return ex;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static FormatException GetFormatException(NumericType numericType)
	{
		string message = "";
		switch (numericType)
		{
		case NumericType.Byte:
			message = SR.FormatByte;
			break;
		case NumericType.SByte:
			message = SR.FormatSByte;
			break;
		case NumericType.Int16:
			message = SR.FormatInt16;
			break;
		case NumericType.Int32:
			message = SR.FormatInt32;
			break;
		case NumericType.Int64:
			message = SR.FormatInt64;
			break;
		case NumericType.UInt16:
			message = SR.FormatUInt16;
			break;
		case NumericType.UInt32:
			message = SR.FormatUInt32;
			break;
		case NumericType.UInt64:
			message = SR.FormatUInt64;
			break;
		case NumericType.Single:
			message = SR.FormatSingle;
			break;
		case NumericType.Double:
			message = SR.FormatDouble;
			break;
		case NumericType.Decimal:
			message = SR.FormatDecimal;
			break;
		default:
			Debug.Fail($"The NumericType enum value: {numericType} is not part of the switch. Add the appropriate case and exception message.");
			break;
		}
		FormatException ex = new FormatException(message);
		ex.Source = "System.Text.Json.Rethrowable";
		return ex;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static FormatException GetFormatException(DataType dateType)
	{
		string message = "";
		switch (dateType)
		{
		case DataType.DateTime:
			message = SR.FormatDateTime;
			break;
		case DataType.DateTimeOffset:
			message = SR.FormatDateTimeOffset;
			break;
		case DataType.Base64String:
			message = SR.CannotDecodeInvalidBase64;
			break;
		case DataType.Guid:
			message = SR.FormatGuid;
			break;
		default:
			Debug.Fail($"The DateType enum value: {dateType} is not part of the switch. Add the appropriate case and exception message.");
			break;
		}
		FormatException ex = new FormatException(message);
		ex.Source = "System.Text.Json.Rethrowable";
		return ex;
	}

	public static InvalidOperationException GetInvalidOperationException_ExpectedChar(JsonTokenType tokenType)
	{
		return GetInvalidOperationException("char", tokenType);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowArgumentException_DeserializeWrongType(Type type, object value)
	{
		throw new ArgumentException("SR.Format(SR.DeserializeWrongType, type, value.GetType())");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static NotSupportedException GetNotSupportedException_SerializationNotSupportedCollection(Type propertyType, Type parentType, MemberInfo memberInfo)
	{
		if (parentType != null && parentType != typeof(object) && memberInfo != null)
		{
			return new NotSupportedException("SR.Format(SR.SerializationNotSupportedCollection, propertyType, $\"{parentType}.{memberInfo.Name}\")");
		}
		return new NotSupportedException("SR.Format(SR.SerializationNotSupportedCollectionType, propertyType)");
	}

	public static void ThrowInvalidOperationException_SerializerCycleDetected(int maxDepth)
	{
		throw new JsonException("SR.Format(SR.SerializerCycleDetected, maxDepth)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowJsonException_DeserializeUnableToConvertValue(Type propertyType)
	{
		JsonException ex = new JsonException("SR.Format(SR.DeserializeUnableToConvertValue, propertyType)");
		ex.AppendPathInformation = true;
		throw ex;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowJsonException_DeserializeUnableToConvertValue(Type propertyType, string path, Exception innerException = null)
	{
		string message = "SR.Format(SR.DeserializeUnableToConvertValue, propertyType) + $\" Path: {path}.\"";
		throw new JsonException(message, path, null, null, innerException);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowJsonException_DepthTooLarge(int currentDepth, JsonSerializerOptions options)
	{
		throw new JsonException("SR.Format(SR.DepthTooLarge, currentDepth, options.EffectiveMaxDepth)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowJsonException_SerializationConverterRead(JsonConverter converter)
	{
		JsonException ex = new JsonException("SR.Format(SR.SerializationConverterRead, converter)");
		ex.AppendPathInformation = true;
		throw ex;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowJsonException_SerializationConverterWrite(JsonConverter converter)
	{
		JsonException ex = new JsonException("SR.Format(SR.SerializationConverterWrite, converter)");
		ex.AppendPathInformation = true;
		throw ex;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowJsonException()
	{
		throw new JsonException();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializationConverterNotCompatible(Type converterType, Type type)
	{
		throw new InvalidOperationException("SR.Format(SR.SerializationConverterNotCompatible, converterType, type)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializationConverterOnAttributeInvalid(Type classType, PropertyInfo propertyInfo)
	{
		string location = classType.ToString();
		if (propertyInfo != null)
		{
			location = location + "." + propertyInfo.Name;
		}
		throw new InvalidOperationException("SR.Format(SR.SerializationConverterOnAttributeInvalid, location)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(Type classTypeAttributeIsOn, PropertyInfo propertyInfo, Type typeToConvert)
	{
		string location = classTypeAttributeIsOn.ToString();
		if (propertyInfo != null)
		{
			location = location + "." + propertyInfo.Name;
		}
		throw new InvalidOperationException("SR.Format(SR.SerializationConverterOnAttributeNotCompatible, location, typeToConvert)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializerOptionsImmutable()
	{
		throw new InvalidOperationException("SR.SerializerOptionsImmutable");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializerPropertyNameConflict(JsonClassInfo jsonClassInfo, JsonPropertyInfo jsonPropertyInfo)
	{
		throw new InvalidOperationException("SR.Format(SR.SerializerPropertyNameConflict, jsonClassInfo.Type, jsonPropertyInfo.PropertyInfo.Name)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializerPropertyNameNull(Type parentType, JsonPropertyInfo jsonPropertyInfo)
	{
		throw new InvalidOperationException("SR.Format(SR.SerializerPropertyNameNull, parentType, jsonPropertyInfo.PropertyInfo.Name)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializerDictionaryKeyNull(Type policyType)
	{
		throw new InvalidOperationException("SR.Format(SR.SerializerDictionaryKeyNull, policyType)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ReThrowWithPath(in ReadStack readStack, JsonReaderException ex)
	{
		Debug.Assert(ex.Path == null);
		string path = readStack.JsonPath();
		string message = ex.Message;
		int iPos = message.LastIndexOf(" LineNumber: ", StringComparison.InvariantCulture);
		message = ((iPos < 0) ? (message + " Path: " + path + ".") : (message.Substring(0, iPos) + " Path: " + path + " |" + message.Substring(iPos)));
		throw new JsonException(message, path, ex.LineNumber, ex.BytePositionInLine, ex);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ReThrowWithPath(in ReadStack readStack, in Utf8JsonReader reader, Exception ex)
	{
		JsonException jsonException = new JsonException(null, ex);
		AddExceptionInformation(in readStack, in reader, jsonException);
		throw jsonException;
	}

	public static void AddExceptionInformation(in ReadStack readStack, in Utf8JsonReader reader, JsonException ex)
	{
		long lineNumber = reader.CurrentState._lineNumber;
		ex.LineNumber = lineNumber;
		long bytePositionInLine = reader.CurrentState._bytePositionInLine;
		ex.BytePositionInLine = bytePositionInLine;
		string path = (ex.Path = readStack.JsonPath());
		string message = ex.Message;
		if (string.IsNullOrEmpty(message))
		{
			Type propertyType = readStack.Current.JsonPropertyInfo?.RuntimePropertyType;
			if (propertyType == null)
			{
				propertyType = readStack.Current.JsonClassInfo.Type;
			}
			message = "SR.Format(SR.DeserializeUnableToConvertValue, propertyType)";
			ex.AppendPathInformation = true;
		}
		if (ex.AppendPathInformation)
		{
			message += $" Path: {path} | LineNumber: {lineNumber} | BytePositionInLine: {bytePositionInLine}.";
			ex.SetMessage(message);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ReThrowWithPath(in WriteStack writeStack, Exception ex)
	{
		JsonException jsonException = new JsonException(null, ex);
		AddExceptionInformation(in writeStack, jsonException);
		throw jsonException;
	}

	public static void AddExceptionInformation(in WriteStack writeStack, JsonException ex)
	{
		string path = (ex.Path = writeStack.PropertyPath());
		string message = ex.Message;
		if (string.IsNullOrEmpty(message))
		{
			message = "SR.Format(SR.SerializeUnableToSerialize)";
			ex.AppendPathInformation = true;
		}
		if (ex.AppendPathInformation)
		{
			message = message + " Path: " + path + ".";
			ex.SetMessage(message);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializationDuplicateAttribute(Type attribute, Type classType, PropertyInfo propertyInfo)
	{
		string location = classType.ToString();
		if (propertyInfo != null)
		{
			location = location + "." + propertyInfo.Name;
		}
		throw new InvalidOperationException("SR.Format(SR.SerializationDuplicateAttribute, attribute, location)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializationDuplicateTypeAttribute(Type classType, Type attribute)
	{
		throw new InvalidOperationException("SR.Format(SR.SerializationDuplicateTypeAttribute, classType, attribute)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowInvalidOperationException_SerializationDataExtensionPropertyInvalid(JsonClassInfo jsonClassInfo, JsonPropertyInfo jsonPropertyInfo)
	{
		throw new InvalidOperationException("SR.Format(SR.SerializationDataExtensionPropertyInvalid, jsonClassInfo.Type, jsonPropertyInfo.PropertyInfo.Name)");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void ThrowNotSupportedException_DeserializeCreateObjectDelegateIsNull(Type invalidType)
	{
		if (invalidType.IsInterface)
		{
			throw new NotSupportedException("SR.Format(SR.DeserializePolymorphicInterface, invalidType)");
		}
		throw new NotSupportedException("SR.Format(SR.DeserializeMissingParameterlessConstructor, invalidType)");
	}
}
