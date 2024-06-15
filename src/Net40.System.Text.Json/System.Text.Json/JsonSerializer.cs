#define DEBUG
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json;

public static class JsonSerializer
{
	private static void ReadCore(JsonSerializerOptions options, ref Utf8JsonReader reader, ref ReadStack readStack)
	{
		try
		{
			JsonReaderState initialState = default(JsonReaderState);
			long initialBytesConsumed = 0L;
			while (true)
			{
				if (readStack.ReadAhead)
				{
					initialState = reader.CurrentState;
					initialBytesConsumed = reader.BytesConsumed;
				}
				if (!reader.Read())
				{
					break;
				}
				JsonTokenType tokenType = reader.TokenType;
				if (JsonHelpers.IsInRangeInclusive(tokenType, JsonTokenType.String, JsonTokenType.False))
				{
					Debug.Assert(tokenType == JsonTokenType.String || tokenType == JsonTokenType.Number || tokenType == JsonTokenType.True || tokenType == JsonTokenType.False);
					HandleValue(tokenType, options, ref reader, ref readStack);
					continue;
				}
				switch (tokenType)
				{
				case JsonTokenType.PropertyName:
					HandlePropertyName(options, ref reader, ref readStack);
					break;
				case JsonTokenType.StartObject:
					if (readStack.Current.SkipProperty)
					{
						readStack.Push();
						readStack.Current.Drain = true;
					}
					else if (readStack.Current.IsProcessingValue())
					{
						if (!HandleObjectAsValue(tokenType, options, ref reader, ref readStack, ref initialState, initialBytesConsumed))
						{
							goto end_IL_0200;
						}
					}
					else if (readStack.Current.IsProcessingDictionaryOrIDictionaryConstructible())
					{
						HandleStartDictionary(options, ref readStack);
					}
					else
					{
						HandleStartObject(options, ref readStack);
					}
					break;
				case JsonTokenType.EndObject:
					if (readStack.Current.Drain)
					{
						readStack.Pop();
						readStack.Current.EndProperty();
					}
					else if (readStack.Current.IsProcessingDictionaryOrIDictionaryConstructible())
					{
						HandleEndDictionary(options, ref readStack);
					}
					else
					{
						HandleEndObject(ref readStack);
					}
					break;
				case JsonTokenType.StartArray:
					if (!readStack.Current.IsProcessingValue())
					{
						HandleStartArray(options, ref reader, ref readStack);
					}
					else if (!HandleObjectAsValue(tokenType, options, ref reader, ref readStack, ref initialState, initialBytesConsumed))
					{
						goto end_IL_0200;
					}
					break;
				case JsonTokenType.EndArray:
					HandleEndArray(options, ref readStack);
					break;
				case JsonTokenType.Null:
					HandleNull(ref reader, ref readStack);
					break;
				}
				continue;
				end_IL_0200:
				break;
			}
		}
		catch (JsonReaderException ex2)
		{
			ThrowHelper.ReThrowWithPath(in readStack, ex2);
		}
		catch (FormatException ex3) when (ex3.Source == "System.Text.Json.Rethrowable")
		{
			ThrowHelper.ReThrowWithPath(in readStack, in reader, ex3);
		}
		catch (InvalidOperationException ex4) when (ex4.Source == "System.Text.Json.Rethrowable")
		{
			ThrowHelper.ReThrowWithPath(in readStack, in reader, ex4);
		}
		catch (JsonException ex)
		{
			ThrowHelper.AddExceptionInformation(in readStack, in reader, ex);
			throw;
		}
		readStack.BytesConsumed += reader.BytesConsumed;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool HandleObjectAsValue(JsonTokenType tokenType, JsonSerializerOptions options, ref Utf8JsonReader reader, ref ReadStack readStack, ref JsonReaderState initialState, long initialBytesConsumed)
	{
		if (readStack.ReadAhead)
		{
			bool complete = reader.TrySkip();
			reader = new Utf8JsonReader(reader.OriginalSpan.Slice(checked((int)initialBytesConsumed)), reader.IsFinalBlock, initialState);
			Debug.Assert(reader.BytesConsumed == 0);
			readStack.BytesConsumed += initialBytesConsumed;
			if (!complete)
			{
				return false;
			}
			reader.Read();
			Debug.Assert(tokenType == reader.TokenType);
		}
		HandleValue(tokenType, options, ref reader, ref readStack);
		return true;
	}

	private static ReadOnlySpan<byte> GetUnescapedString(ReadOnlySpan<byte> utf8Source, int idx)
	{
		int length = utf8Source.Length;
		byte[] pooledName = null;
		Span<byte> span = ((length > 256) ? ((Span<byte>)(pooledName = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> unescapedName = span;
		JsonReaderHelper.Unescape(utf8Source, unescapedName, idx, out var written);
		ReadOnlySpan<byte> propertyName = unescapedName.Slice(0, written).ToArray();
		if (pooledName != null)
		{
			new Span<byte>(pooledName, 0, written).Clear();
			ArrayPool<byte>.Shared.Return(pooledName);
		}
		return propertyName;
	}

	private static void HandleStartArray(JsonSerializerOptions options, ref Utf8JsonReader reader, ref ReadStack state)
	{
		if (state.Current.SkipProperty)
		{
			state.Push();
			state.Current.Drain = true;
			return;
		}
		JsonPropertyInfo jsonPropertyInfo = state.Current.JsonPropertyInfo;
		if (jsonPropertyInfo == null)
		{
			jsonPropertyInfo = state.Current.JsonClassInfo.CreateRootObject(options);
		}
		if (((ClassType)56 & jsonPropertyInfo.ClassType) == 0)
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(jsonPropertyInfo.RuntimePropertyType);
		}
		if (state.Current.CollectionPropertyInitialized)
		{
			Type elementType = jsonPropertyInfo.ElementClassInfo.Type;
			state.Push();
			state.Current.Initialize(elementType, options);
		}
		state.Current.CollectionPropertyInitialized = true;
		if (state.Current.JsonPropertyInfo == null || state.Current.JsonPropertyInfo.ClassType != ClassType.Enumerable)
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(state.Current.JsonClassInfo.Type);
		}
		object value = ReadStackFrame.CreateEnumerableValue(ref reader, ref state);
		if (value != null)
		{
			if (state.Current.ReturnValue != null)
			{
				state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, value);
			}
			else
			{
				state.Current.SetReturnValue(value);
			}
		}
	}

	private static bool HandleEndArray(JsonSerializerOptions options, ref ReadStack state)
	{
		bool lastFrame = state.IsLastFrame;
		if (state.Current.Drain)
		{
			state.Pop();
			return lastFrame;
		}
		IEnumerable value = ReadStackFrame.GetEnumerableValue(in state.Current);
		if (state.Current.TempEnumerableValues != null)
		{
			JsonEnumerableConverter converter = state.Current.JsonPropertyInfo.EnumerableConverter;
			Debug.Assert(converter != null);
			value = converter.CreateFromList(ref state, (IList)value, options);
			state.Current.TempEnumerableValues = null;
		}
		else if (state.Current.IsProcessingProperty(ClassType.Enumerable))
		{
			state.Current.EndProperty();
			return false;
		}
		if (lastFrame)
		{
			if (state.Current.ReturnValue == null)
			{
				state.Current.Reset();
				state.Current.ReturnValue = value;
				return true;
			}
			if (state.Current.IsProcessingCollectionObject())
			{
				return true;
			}
		}
		else if (state.Current.IsProcessingObject(ClassType.Enumerable))
		{
			state.Pop();
		}
		ApplyObjectToEnumerable(value, ref state);
		return false;
	}

	internal static void ApplyObjectToEnumerable(object value, ref ReadStack state, bool setPropertyDirectly = false)
	{
		Debug.Assert(!state.Current.SkipProperty);
		if (state.Current.IsProcessingObject(ClassType.Enumerable))
		{
			if (state.Current.TempEnumerableValues != null)
			{
				state.Current.TempEnumerableValues.Add(value);
			}
			else if (!(state.Current.ReturnValue is IList list))
			{
				ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(value.GetType());
			}
			else
			{
				list.Add(value);
			}
		}
		else if (!setPropertyDirectly && state.Current.IsProcessingProperty(ClassType.Enumerable))
		{
			Debug.Assert(state.Current.JsonPropertyInfo != null);
			Debug.Assert(state.Current.ReturnValue != null);
			if (state.Current.TempEnumerableValues != null)
			{
				state.Current.TempEnumerableValues.Add(value);
				return;
			}
			IList list2 = (IList)state.Current.JsonPropertyInfo.GetValueAsObject(state.Current.ReturnValue);
			if (list2 == null || state.Current.JsonPropertyInfo.RuntimePropertyType.FullName.StartsWith("System.Collections.Immutable.ImmutableArray`1"))
			{
				state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, value);
			}
			else
			{
				list2.Add(value);
			}
		}
		else if (state.Current.IsProcessingObject(ClassType.Dictionary) || (state.Current.IsProcessingProperty(ClassType.Dictionary) && !setPropertyDirectly))
		{
			Debug.Assert(state.Current.ReturnValue != null);
			IDictionary dictionary2 = (IDictionary)state.Current.JsonPropertyInfo.GetValueAsObject(state.Current.ReturnValue);
			string key = state.Current.KeyName;
			Debug.Assert(!string.IsNullOrEmpty(key));
			dictionary2[key] = value;
		}
		else if (state.Current.IsProcessingObject(ClassType.IDictionaryConstructible) || (state.Current.IsProcessingProperty(ClassType.IDictionaryConstructible) && !setPropertyDirectly))
		{
			Debug.Assert(state.Current.TempDictionaryValues != null);
			IDictionary dictionary = state.Current.TempDictionaryValues;
			string key2 = state.Current.KeyName;
			Debug.Assert(!string.IsNullOrEmpty(key2));
			dictionary[key2] = value;
		}
		else
		{
			Debug.Assert(state.Current.JsonPropertyInfo != null);
			state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, value);
		}
	}

	internal static void ApplyValueToEnumerable<TProperty>(ref TProperty value, ref ReadStack state)
	{
		Debug.Assert(!state.Current.SkipProperty);
		if (state.Current.IsProcessingObject(ClassType.Enumerable))
		{
			if (state.Current.TempEnumerableValues != null)
			{
				((IList<TProperty>)state.Current.TempEnumerableValues).Add(value);
			}
			else
			{
				((IList<TProperty>)state.Current.ReturnValue).Add(value);
			}
		}
		else if (state.Current.IsProcessingProperty(ClassType.Enumerable))
		{
			Debug.Assert(state.Current.JsonPropertyInfo != null);
			Debug.Assert(state.Current.ReturnValue != null);
			if (state.Current.TempEnumerableValues != null)
			{
				((IList<TProperty>)state.Current.TempEnumerableValues).Add(value);
				return;
			}
			IList<TProperty> list = (IList<TProperty>)state.Current.JsonPropertyInfo.GetValueAsObject(state.Current.ReturnValue);
			if (list == null)
			{
				state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, value);
			}
			else
			{
				list.Add(value);
			}
		}
		else if (state.Current.IsProcessingDictionary())
		{
			Debug.Assert(state.Current.ReturnValue != null);
			object currentDictionary = state.Current.JsonPropertyInfo.GetValueAsObject(state.Current.ReturnValue);
			string key = state.Current.KeyName;
			Debug.Assert(!string.IsNullOrEmpty(key));
			if (currentDictionary is IDictionary<string, TProperty> genericDict)
			{
				Debug.Assert(!genericDict.IsReadOnly);
				genericDict[key] = value;
				return;
			}
			if (!(currentDictionary is IDictionary dict))
			{
				throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(currentDictionary.GetType(), null, null);
			}
			Debug.Assert(!dict.IsReadOnly);
			dict[key] = value;
		}
		else if (state.Current.IsProcessingIDictionaryConstructible())
		{
			Debug.Assert(state.Current.TempDictionaryValues != null);
			IDictionary<string, TProperty> dictionary = (IDictionary<string, TProperty>)state.Current.TempDictionaryValues;
			string key2 = state.Current.KeyName;
			Debug.Assert(!string.IsNullOrEmpty(key2));
			dictionary[key2] = value;
		}
		else
		{
			Debug.Assert(state.Current.JsonPropertyInfo != null);
			state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, value);
		}
	}

	private static void HandleStartDictionary(JsonSerializerOptions options, ref ReadStack state)
	{
		Debug.Assert(!state.Current.IsProcessingEnumerable());
		JsonPropertyInfo jsonPropertyInfo = state.Current.JsonPropertyInfo;
		if (jsonPropertyInfo == null)
		{
			jsonPropertyInfo = state.Current.JsonClassInfo.CreateRootObject(options);
		}
		Debug.Assert(jsonPropertyInfo != null);
		if (state.Current.CollectionPropertyInitialized)
		{
			state.Push();
			state.Current.JsonClassInfo = jsonPropertyInfo.ElementClassInfo;
			state.Current.InitializeJsonPropertyInfo();
			JsonClassInfo classInfo = state.Current.JsonClassInfo;
			if (state.Current.IsProcessingIDictionaryConstructible())
			{
				state.Current.TempDictionaryValues = (IDictionary)classInfo.CreateConcreteDictionary();
				state.Current.CollectionPropertyInitialized = true;
			}
			else if (state.Current.IsProcessingDictionary())
			{
				if (classInfo.CreateObject == null)
				{
					throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(classInfo.Type, null, null);
				}
				state.Current.ReturnValue = classInfo.CreateObject();
				state.Current.CollectionPropertyInitialized = true;
			}
			else if (state.Current.IsProcessingObject(ClassType.Object))
			{
				if (classInfo.CreateObject == null)
				{
					ThrowHelper.ThrowNotSupportedException_DeserializeCreateObjectDelegateIsNull(classInfo.Type);
				}
				state.Current.ReturnValue = classInfo.CreateObject();
			}
			else
			{
				ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(classInfo.Type);
			}
			return;
		}
		state.Current.CollectionPropertyInitialized = true;
		if (state.Current.IsProcessingIDictionaryConstructible())
		{
			JsonClassInfo dictionaryClassInfo = ((!(jsonPropertyInfo.DeclaredPropertyType == jsonPropertyInfo.ImplementedPropertyType)) ? options.GetOrAddClass(jsonPropertyInfo.DeclaredPropertyType) : options.GetOrAddClass(jsonPropertyInfo.RuntimePropertyType));
			state.Current.TempDictionaryValues = (IDictionary)dictionaryClassInfo.CreateConcreteDictionary();
			return;
		}
		JsonClassInfo dictionaryClassInfo2 = jsonPropertyInfo.RuntimeClassInfo;
		IDictionary value = (IDictionary)dictionaryClassInfo2.CreateObject();
		if (value != null)
		{
			if (state.Current.ReturnValue != null)
			{
				state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, value);
			}
			else
			{
				state.Current.SetReturnValue(value);
			}
		}
	}

	private static void HandleEndDictionary(JsonSerializerOptions options, ref ReadStack state)
	{
		Debug.Assert(!state.Current.SkipProperty);
		if (state.Current.IsProcessingProperty(ClassType.Dictionary))
		{
			if (state.Current.JsonClassInfo.DataExtensionProperty == state.Current.JsonPropertyInfo)
			{
				HandleEndObject(ref state);
			}
			else
			{
				state.Current.EndProperty();
			}
			return;
		}
		if (state.Current.IsProcessingProperty(ClassType.IDictionaryConstructible))
		{
			Debug.Assert(state.Current.TempDictionaryValues != null);
			JsonDictionaryConverter converter = state.Current.JsonPropertyInfo.DictionaryConverter;
			state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, converter.CreateFromDictionary(ref state, state.Current.TempDictionaryValues, options));
			state.Current.EndProperty();
			return;
		}
		object value;
		if (state.Current.TempDictionaryValues != null)
		{
			JsonDictionaryConverter converter2 = state.Current.JsonPropertyInfo.DictionaryConverter;
			value = converter2.CreateFromDictionary(ref state, state.Current.TempDictionaryValues, options);
		}
		else
		{
			value = state.Current.ReturnValue;
		}
		if (state.IsLastFrame)
		{
			state.Current.Reset();
			state.Current.ReturnValue = value;
		}
		else
		{
			state.Pop();
			ApplyObjectToEnumerable(value, ref state);
		}
	}

	private static bool HandleNull(ref Utf8JsonReader reader, ref ReadStack state)
	{
		if (state.Current.SkipProperty)
		{
			state.Current.EndProperty();
			return false;
		}
		JsonPropertyInfo jsonPropertyInfo = state.Current.JsonPropertyInfo;
		if (jsonPropertyInfo == null || (reader.CurrentDepth == 0 && jsonPropertyInfo.CanBeNull))
		{
			Debug.Assert(state.IsLastFrame);
			Debug.Assert(state.Current.ReturnValue == null);
			return true;
		}
		Debug.Assert(jsonPropertyInfo != null);
		if (state.Current.IsProcessingCollectionObject())
		{
			AddNullToCollection(jsonPropertyInfo, ref reader, ref state);
			return false;
		}
		if (state.Current.IsProcessingCollectionProperty())
		{
			if (state.Current.CollectionPropertyInitialized)
			{
				AddNullToCollection(jsonPropertyInfo, ref reader, ref state);
			}
			else
			{
				ApplyObjectToEnumerable(null, ref state, setPropertyDirectly: true);
				state.Current.EndProperty();
			}
			return false;
		}
		if (!jsonPropertyInfo.CanBeNull)
		{
			jsonPropertyInfo.Read(JsonTokenType.Null, ref state, ref reader);
			return false;
		}
		if (state.Current.ReturnValue == null)
		{
			Debug.Assert(state.IsLastFrame);
			return true;
		}
		if (!jsonPropertyInfo.IgnoreNullValues)
		{
			state.Current.JsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, null);
		}
		return false;
	}

	private static void AddNullToCollection(JsonPropertyInfo jsonPropertyInfo, ref Utf8JsonReader reader, ref ReadStack state)
	{
		JsonPropertyInfo elementPropertyInfo = jsonPropertyInfo.ElementClassInfo.PolicyProperty;
		if (elementPropertyInfo != null && !elementPropertyInfo.CanBeNull)
		{
			elementPropertyInfo.ReadEnumerable(JsonTokenType.Null, ref state, ref reader);
		}
		else
		{
			ApplyObjectToEnumerable(null, ref state);
		}
	}

	private static void HandleStartObject(JsonSerializerOptions options, ref ReadStack state)
	{
		Debug.Assert(!state.Current.IsProcessingDictionaryOrIDictionaryConstructible());
		if (state.Current.IsProcessingEnumerable())
		{
			if (!state.Current.CollectionPropertyInitialized)
			{
				ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(state.Current.JsonPropertyInfo.DeclaredPropertyType);
			}
			Type objType = state.Current.GetElementType();
			state.Push();
			state.Current.Initialize(objType, options);
		}
		else if (state.Current.JsonPropertyInfo != null)
		{
			Debug.Assert(state.Current.IsProcessingObject(ClassType.Object));
			Type objType2 = state.Current.JsonPropertyInfo.RuntimePropertyType;
			state.Push();
			state.Current.Initialize(objType2, options);
		}
		JsonClassInfo classInfo = state.Current.JsonClassInfo;
		if (state.Current.IsProcessingObject(ClassType.IDictionaryConstructible))
		{
			state.Current.TempDictionaryValues = (IDictionary)classInfo.CreateConcreteDictionary();
			state.Current.CollectionPropertyInitialized = true;
		}
		else if (state.Current.IsProcessingObject(ClassType.Dictionary))
		{
			if (classInfo.CreateObject == null)
			{
				throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(classInfo.Type, null, null);
			}
			state.Current.ReturnValue = classInfo.CreateObject();
			state.Current.CollectionPropertyInitialized = true;
		}
		else if (state.Current.IsProcessingObject(ClassType.Object))
		{
			if (classInfo.CreateObject == null)
			{
				ThrowHelper.ThrowNotSupportedException_DeserializeCreateObjectDelegateIsNull(classInfo.Type);
			}
			state.Current.ReturnValue = classInfo.CreateObject();
			if (state.Current.IsProcessingDictionary())
			{
				state.Current.CollectionPropertyInitialized = true;
			}
		}
		else
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(classInfo.Type);
		}
	}

	private static void HandleEndObject(ref ReadStack state)
	{
		Debug.Assert((!state.Current.IsProcessingDictionary() || state.Current.JsonClassInfo.DataExtensionProperty == state.Current.JsonPropertyInfo) && !state.Current.IsProcessingIDictionaryConstructible());
		if (state.Current.JsonClassInfo.ClassType == ClassType.Value)
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(state.Current.JsonPropertyInfo.RuntimePropertyType);
		}
		if (state.Current.PropertyRefCache != null)
		{
			state.Current.JsonClassInfo.UpdateSortedPropertyCache(ref state.Current);
		}
		object value = state.Current.ReturnValue;
		if (state.IsLastFrame)
		{
			state.Current.Reset();
			state.Current.ReturnValue = value;
		}
		else
		{
			state.Pop();
			ApplyObjectToEnumerable(value, ref state);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static void HandlePropertyName(JsonSerializerOptions options, ref Utf8JsonReader reader, ref ReadStack state)
	{
		if (state.Current.Drain)
		{
			return;
		}
		Debug.Assert(state.Current.ReturnValue != null || state.Current.TempDictionaryValues != null);
		Debug.Assert(state.Current.JsonClassInfo != null);
		bool isProcessingDictObject = state.Current.IsProcessingDictionaryOrIDictionaryConstructibleObject();
		if ((isProcessingDictObject || state.Current.IsProcessingDictionaryOrIDictionaryConstructibleProperty()) && state.Current.JsonClassInfo.DataExtensionProperty != state.Current.JsonPropertyInfo)
		{
			if (isProcessingDictObject)
			{
				state.Current.JsonPropertyInfo = state.Current.JsonClassInfo.PolicyProperty;
			}
			state.Current.KeyName = reader.GetString();
			return;
		}
		Debug.Assert(state.Current.JsonClassInfo.ClassType == ClassType.Object);
		state.Current.EndProperty();
		ReadOnlySpan<byte> readOnlySpan;
		if (!reader.HasValueSequence)
		{
			readOnlySpan = reader.ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = reader.ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> propertyName = readOnlySpan;
		if (reader._stringHasEscaping)
		{
			int idx = propertyName.IndexOf<byte>(92);
			Debug.Assert(idx != -1);
			propertyName = GetUnescapedString(propertyName, idx);
		}
		JsonPropertyInfo jsonPropertyInfo = state.Current.JsonClassInfo.GetProperty(propertyName, ref state.Current);
		if (jsonPropertyInfo == JsonPropertyInfo.s_missingProperty)
		{
			JsonPropertyInfo dataExtProperty = state.Current.JsonClassInfo.DataExtensionProperty;
			if (dataExtProperty == null)
			{
				state.Current.JsonPropertyInfo = JsonPropertyInfo.s_missingProperty;
			}
			else
			{
				state.Current.JsonPropertyInfo = dataExtProperty;
				state.Current.JsonPropertyName = propertyName.ToArray();
				state.Current.KeyName = JsonHelpers.Utf8GetString(propertyName);
				state.Current.CollectionPropertyInitialized = true;
				CreateDataExtensionProperty(dataExtProperty, ref state);
			}
		}
		else
		{
			Debug.Assert(jsonPropertyInfo.JsonPropertyName == null || options.PropertyNameCaseInsensitive || propertyName.SequenceEqual(jsonPropertyInfo.JsonPropertyName));
			state.Current.JsonPropertyInfo = jsonPropertyInfo;
			if (jsonPropertyInfo.JsonPropertyName == null)
			{
				byte[] propertyNameArray = propertyName.ToArray();
				if (options.PropertyNameCaseInsensitive)
				{
					state.Current.JsonPropertyName = propertyNameArray;
				}
				else
				{
					state.Current.JsonPropertyInfo.JsonPropertyName = propertyNameArray;
				}
			}
		}
		state.Current.PropertyIndex++;
	}

	private static void CreateDataExtensionProperty(JsonPropertyInfo jsonPropertyInfo, ref ReadStack state)
	{
		Debug.Assert(jsonPropertyInfo != null);
		Debug.Assert(state.Current.ReturnValue != null);
		IDictionary extensionData = (IDictionary)jsonPropertyInfo.GetValueAsObject(state.Current.ReturnValue);
		if (extensionData == null)
		{
			Debug.Assert(jsonPropertyInfo.DeclaredPropertyType.IsGenericType);
			Debug.Assert(jsonPropertyInfo.DeclaredPropertyType.GetGenericArguments().Length == 2);
			Debug.Assert(jsonPropertyInfo.DeclaredPropertyType.GetGenericArguments()[0].UnderlyingSystemType == typeof(string));
			Debug.Assert(jsonPropertyInfo.DeclaredPropertyType.GetGenericArguments()[1].UnderlyingSystemType == typeof(object) || jsonPropertyInfo.DeclaredPropertyType.GetGenericArguments()[1].UnderlyingSystemType == typeof(JsonElement));
			extensionData = (IDictionary)jsonPropertyInfo.RuntimeClassInfo.CreateObject();
			jsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, extensionData);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static void HandleValue(JsonTokenType tokenType, JsonSerializerOptions options, ref Utf8JsonReader reader, ref ReadStack state)
	{
		if (!state.Current.SkipProperty)
		{
			JsonPropertyInfo jsonPropertyInfo = state.Current.JsonPropertyInfo;
			if (jsonPropertyInfo == null)
			{
				jsonPropertyInfo = state.Current.JsonClassInfo.CreateRootObject(options);
			}
			else if (state.Current.JsonClassInfo.ClassType == ClassType.Unknown)
			{
				jsonPropertyInfo = state.Current.JsonClassInfo.GetOrAddPolymorphicProperty(jsonPropertyInfo, typeof(object), options);
			}
			jsonPropertyInfo.Read(tokenType, ref state, ref reader);
		}
	}

	private static object ReadCore(Type returnType, JsonSerializerOptions options, ref Utf8JsonReader reader)
	{
		ReadStack state = default(ReadStack);
		state.Current.Initialize(returnType, options);
		ReadCore(options, ref reader, ref state);
		return state.Current.ReturnValue;
	}

	public static TValue Deserialize<TValue>(ReadOnlySpan<byte> utf8Json, JsonSerializerOptions options = null)
	{
		return (TValue)ParseCore(utf8Json, typeof(TValue), options);
	}

	public static object Deserialize(ReadOnlySpan<byte> utf8Json, Type returnType, JsonSerializerOptions options = null)
	{
		if (returnType == null)
		{
			throw new ArgumentNullException("returnType");
		}
		return ParseCore(utf8Json, returnType, options);
	}

	private static object ParseCore(ReadOnlySpan<byte> utf8Json, Type returnType, JsonSerializerOptions options)
	{
		if (options == null)
		{
			options = JsonSerializerOptions.s_defaultOptions;
		}
		Utf8JsonReader reader = new Utf8JsonReader(state: new JsonReaderState(options.GetReaderOptions()), jsonData: utf8Json, isFinalBlock: true);
		object result = ReadCore(returnType, options, ref reader);
		Debug.Assert(reader.BytesConsumed == utf8Json.Length);
		return result;
	}

	public static ValueTask<TValue> DeserializeAsync<TValue>(Stream utf8Json, JsonSerializerOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (utf8Json == null)
		{
			throw new ArgumentNullException("utf8Json");
		}
		return ReadAsync<TValue>(utf8Json, typeof(TValue), options, cancellationToken);
	}

	public static ValueTask<object> DeserializeAsync(Stream utf8Json, Type returnType, JsonSerializerOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (utf8Json == null)
		{
			throw new ArgumentNullException("utf8Json");
		}
		if (returnType == null)
		{
			throw new ArgumentNullException("returnType");
		}
		return ReadAsync<object>(utf8Json, returnType, options, cancellationToken);
	}

	private static async ValueTask<TValue> ReadAsync<TValue>(Stream utf8Json, Type returnType, JsonSerializerOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (options == null)
		{
			options = JsonSerializerOptions.s_defaultOptions;
		}
		ReadStack readStack = default(ReadStack);
		readStack.Current.Initialize(returnType, options);
		JsonReaderState readerState = new JsonReaderState(options.GetReaderOptions());
		int utf8BomLength = JsonConstants.Utf8Bom.Length;
		byte[] buffer = ArrayPool<byte>.Shared.Rent(Math.Max(options.DefaultBufferSize, utf8BomLength));
		int bytesInBuffer = 0;
		long totalBytesRead = 0L;
		int clearMax = 0;
		bool firstIteration = true;
		try
		{
			while (true)
			{
				bool isFinalBlock = false;
				do
				{
					int bytesRead = await TaskTheraotExtensions.ConfigureAwait(StreamTheraotExtensions.ReadAsync(utf8Json, buffer, bytesInBuffer, buffer.Length - bytesInBuffer, cancellationToken), continueOnCapturedContext: false);
					if (bytesRead == 0)
					{
						isFinalBlock = true;
						break;
					}
					totalBytesRead += bytesRead;
					bytesInBuffer += bytesRead;
				}
				while (bytesInBuffer != buffer.Length);
				if (bytesInBuffer > clearMax)
				{
					clearMax = bytesInBuffer;
				}
				int start = 0;
				if (firstIteration)
				{
					firstIteration = false;
					Debug.Assert(buffer.Length >= JsonConstants.Utf8Bom.Length);
					if (buffer.AsSpan().StartsWith(JsonConstants.Utf8Bom))
					{
						start += utf8BomLength;
						bytesInBuffer -= utf8BomLength;
					}
				}
				ReadCore(ref readerState, isFinalBlock, new ReadOnlySpan<byte>(buffer, start, bytesInBuffer), options, ref readStack);
				Debug.Assert(readStack.BytesConsumed <= bytesInBuffer);
				int bytesConsumed = checked((int)readStack.BytesConsumed);
				bytesInBuffer -= bytesConsumed;
				if (isFinalBlock)
				{
					break;
				}
				if ((uint)bytesInBuffer > (uint)buffer.Length / 2u)
				{
					byte[] dest = ArrayPool<byte>.Shared.Rent((buffer.Length < 1073741823) ? (buffer.Length * 2) : int.MaxValue);
					Buffer.BlockCopy(buffer, bytesConsumed + start, dest, 0, bytesInBuffer);
					new Span<byte>(buffer, 0, clearMax).Clear();
					ArrayPool<byte>.Shared.Return(buffer);
					clearMax = bytesInBuffer;
					buffer = dest;
				}
				else if (bytesInBuffer != 0)
				{
					Buffer.BlockCopy(buffer, bytesConsumed + start, buffer, 0, bytesInBuffer);
				}
			}
		}
		finally
		{
			new Span<byte>(buffer, 0, clearMax).Clear();
			ArrayPool<byte>.Shared.Return(buffer);
		}
		Debug.Assert(bytesInBuffer == 0);
		return (TValue)readStack.Current.ReturnValue;
	}

	private static void ReadCore(ref JsonReaderState readerState, bool isFinalBlock, ReadOnlySpan<byte> buffer, JsonSerializerOptions options, ref ReadStack readStack)
	{
		Utf8JsonReader reader = new Utf8JsonReader(buffer, isFinalBlock, readerState);
		readStack.ReadAhead = !isFinalBlock;
		readStack.BytesConsumed = 0L;
		ReadCore(options, ref reader, ref readStack);
		readerState = reader.CurrentState;
	}

	public static TValue Deserialize<TValue>(string json, JsonSerializerOptions options = null)
	{
		return (TValue)Deserialize(json, typeof(TValue), options);
	}

	public static object Deserialize(string json, Type returnType, JsonSerializerOptions options = null)
	{
		if (json == null)
		{
			throw new ArgumentNullException("json");
		}
		if (returnType == null)
		{
			throw new ArgumentNullException("returnType");
		}
		if (options == null)
		{
			options = JsonSerializerOptions.s_defaultOptions;
		}
		byte[] tempArray = null;
		Span<byte> utf8 = (((long)json.Length > 349525L) ? new byte[JsonReaderHelper.GetUtf8ByteCount(json.AsSpan())] : (tempArray = ArrayPool<byte>.Shared.Rent(json.Length * 3)));
		object result;
		try
		{
			int actualByteCount = JsonReaderHelper.GetUtf8FromText(json.AsSpan(), utf8);
			utf8 = utf8.Slice(0, actualByteCount);
			Utf8JsonReader reader = new Utf8JsonReader(state: new JsonReaderState(options.GetReaderOptions()), jsonData: utf8, isFinalBlock: true);
			result = ReadCore(returnType, options, ref reader);
			Debug.Assert(reader.BytesConsumed == actualByteCount);
		}
		finally
		{
			if (tempArray != null)
			{
				utf8.Clear();
				ArrayPool<byte>.Shared.Return(tempArray);
			}
		}
		return result;
	}

	public static TValue Deserialize<TValue>(ref Utf8JsonReader reader, JsonSerializerOptions options = null)
	{
		return (TValue)ReadValueCore(ref reader, typeof(TValue), options);
	}

	public static object Deserialize(ref Utf8JsonReader reader, Type returnType, JsonSerializerOptions options = null)
	{
		if (returnType == null)
		{
			throw new ArgumentNullException("returnType");
		}
		return ReadValueCore(ref reader, returnType, options);
	}

	private static object ReadValueCore(ref Utf8JsonReader reader, Type returnType, JsonSerializerOptions options)
	{
		if (options == null)
		{
			options = JsonSerializerOptions.s_defaultOptions;
		}
		ReadStack readStack = default(ReadStack);
		readStack.Current.Initialize(returnType, options);
		ReadValueCore(options, ref reader, ref readStack);
		return readStack.Current.ReturnValue;
	}

	private static void CheckSupportedOptions(JsonReaderOptions readerOptions, string paramName)
	{
		if (readerOptions.CommentHandling == JsonCommentHandling.Allow)
		{
			throw new ArgumentException("SR.JsonSerializerDoesNotSupportComments", paramName);
		}
	}

	private static void ReadValueCore(JsonSerializerOptions options, ref Utf8JsonReader reader, ref ReadStack readStack)
	{
		JsonReaderState state = reader.CurrentState;
		CheckSupportedOptions(state.Options, "reader");
		Utf8JsonReader restore = reader;
		ReadOnlySpan<byte> valueSpan = default(ReadOnlySpan<byte>);
		ReadOnlySequence<byte> valueSequence = default(ReadOnlySequence<byte>);
		try
		{
			JsonTokenType tokenType = reader.TokenType;
			JsonTokenType jsonTokenType = tokenType;
			if ((jsonTokenType == JsonTokenType.None || jsonTokenType == JsonTokenType.PropertyName) && !reader.Read())
			{
				ThrowHelper.ThrowJsonReaderException(ref reader, ExceptionResource.ExpectedOneCompleteToken, 0);
			}
			switch (reader.TokenType)
			{
			case JsonTokenType.StartObject:
			case JsonTokenType.StartArray:
			{
				long startingOffset = reader.TokenStartIndex;
				if (!reader.TrySkip())
				{
					ThrowHelper.ThrowJsonReaderException(ref reader, ExceptionResource.NotEnoughData, 0);
				}
				long totalLength = reader.BytesConsumed - startingOffset;
				ReadOnlySequence<byte> sequence = reader.OriginalSequence;
				if (sequence.IsEmpty)
				{
					valueSpan = checked(reader.OriginalSpan.Slice((int)startingOffset, (int)totalLength));
				}
				else
				{
					valueSequence = sequence.Slice(startingOffset, totalLength);
				}
				Debug.Assert(reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray);
				break;
			}
			case JsonTokenType.Number:
			case JsonTokenType.True:
			case JsonTokenType.False:
			case JsonTokenType.Null:
				if (reader.HasValueSequence)
				{
					valueSequence = reader.ValueSequence;
				}
				else
				{
					valueSpan = reader.ValueSpan;
				}
				break;
			case JsonTokenType.String:
			{
				ReadOnlySequence<byte> sequence2 = reader.OriginalSequence;
				if (sequence2.IsEmpty)
				{
					int payloadLength = reader.ValueSpan.Length + 2;
					Debug.Assert(payloadLength > 1);
					ReadOnlySpan<byte> readerSpan = reader.OriginalSpan;
					Debug.Assert(readerSpan[(int)reader.TokenStartIndex] == 34, $"Calculated span starts with {readerSpan[(int)reader.TokenStartIndex]}");
					Debug.Assert(readerSpan[(int)reader.TokenStartIndex + payloadLength - 1] == 34, $"Calculated span ends with {readerSpan[(int)reader.TokenStartIndex + payloadLength - 1]}");
					valueSpan = readerSpan.Slice((int)reader.TokenStartIndex, payloadLength);
				}
				else
				{
					long payloadLength2 = 2L;
					payloadLength2 = ((!reader.HasValueSequence) ? (payloadLength2 + reader.ValueSpan.Length) : (payloadLength2 + reader.ValueSequence.Length));
					valueSequence = sequence2.Slice(reader.TokenStartIndex, payloadLength2);
					Debug.Assert(valueSequence.First.Span[0] == 34, $"Calculated sequence starts with {valueSequence.First.Span[0]}");
					Debug.Assert(BuffersExtensions.ToArray(in valueSequence)[payloadLength2 - 1] == 34, $"Calculated sequence ends with {BuffersExtensions.ToArray(in valueSequence)[payloadLength2 - 1]}");
				}
				break;
			}
			default:
			{
				byte displayByte = ((!reader.HasValueSequence) ? reader.ValueSpan[0] : reader.ValueSequence.First.Span[0]);
				ThrowHelper.ThrowJsonReaderException(ref reader, ExceptionResource.ExpectedStartOfValueNotFound, displayByte);
				break;
			}
			}
		}
		catch (JsonReaderException ex)
		{
			reader = restore;
			ThrowHelper.ReThrowWithPath(in readStack, ex);
		}
		int length = (valueSpan.IsEmpty ? checked((int)valueSequence.Length) : valueSpan.Length);
		byte[] rented = ArrayPool<byte>.Shared.Rent(length);
		Span<byte> rentedSpan = rented.AsSpan(0, length);
		try
		{
			if (valueSpan.IsEmpty)
			{
				valueSequence.CopyTo(rentedSpan);
			}
			else
			{
				valueSpan.CopyTo(rentedSpan);
			}
			JsonReaderOptions originalReaderOptions = state.Options;
			Utf8JsonReader newReader = new Utf8JsonReader(rentedSpan, originalReaderOptions);
			ReadCore(options, ref newReader, ref readStack);
			Debug.Assert(newReader.BytesConsumed == length);
		}
		catch (JsonException)
		{
			reader = restore;
			throw;
		}
		finally
		{
			rentedSpan.Clear();
			ArrayPool<byte>.Shared.Return(rented);
		}
	}

	public static byte[] SerializeToUtf8Bytes<TValue>(TValue value, JsonSerializerOptions options = null)
	{
		return WriteCoreBytes(value, typeof(TValue), options);
	}

	public static byte[] SerializeToUtf8Bytes(object value, Type inputType, JsonSerializerOptions options = null)
	{
		VerifyValueAndType(value, inputType);
		return WriteCoreBytes(value, inputType, options);
	}

	private static bool Write(Utf8JsonWriter writer, int originalWriterDepth, int flushThreshold, JsonSerializerOptions options, ref WriteStack state)
	{
		try
		{
			while (true)
			{
				bool finishedSerializing;
				switch (state.Current.JsonClassInfo.ClassType)
				{
				case ClassType.Enumerable:
					finishedSerializing = HandleEnumerable(state.Current.JsonClassInfo.ElementClassInfo, options, writer, ref state);
					break;
				case ClassType.Value:
					Debug.Assert(state.Current.JsonPropertyInfo.ClassType == ClassType.Value);
					state.Current.JsonPropertyInfo.Write(ref state, writer);
					finishedSerializing = true;
					break;
				case ClassType.Dictionary:
				case ClassType.IDictionaryConstructible:
					finishedSerializing = HandleDictionary(state.Current.JsonClassInfo.ElementClassInfo, options, writer, ref state);
					break;
				default:
					Debug.Assert(state.Current.JsonClassInfo.ClassType == ClassType.Object || state.Current.JsonClassInfo.ClassType == ClassType.Unknown);
					finishedSerializing = WriteObject(options, writer, ref state);
					break;
				}
				if (finishedSerializing)
				{
					if (writer.CurrentDepth == originalWriterDepth)
					{
						break;
					}
				}
				else if (writer.CurrentDepth >= options.EffectiveMaxDepth)
				{
					ThrowHelper.ThrowInvalidOperationException_SerializerCycleDetected(options.MaxDepth);
				}
				if (flushThreshold >= 0 && writer.BytesPending > flushThreshold)
				{
					return false;
				}
				bool flag = true;
			}
		}
		catch (InvalidOperationException ex2) when (ex2.Source == "System.Text.Json.Rethrowable")
		{
			ThrowHelper.ReThrowWithPath(in state, ex2);
		}
		catch (JsonException ex)
		{
			ThrowHelper.AddExceptionInformation(in state, ex);
			throw;
		}
		return true;
	}

	private static bool HandleDictionary(JsonClassInfo elementClassInfo, JsonSerializerOptions options, Utf8JsonWriter writer, ref WriteStack state)
	{
		JsonPropertyInfo jsonPropertyInfo = state.Current.JsonPropertyInfo;
		if (state.Current.CollectionEnumerator == null)
		{
			IEnumerable enumerable = (IEnumerable)jsonPropertyInfo.GetValueAsObject(state.Current.CurrentValue);
			if (enumerable == null)
			{
				if ((state.Current.JsonClassInfo.ClassType != ClassType.Object || !state.Current.JsonPropertyInfo.IgnoreNullValues) && state.Current.ExtensionDataStatus != ExtensionDataWriteStatus.Writing)
				{
					state.Current.WriteObjectOrArrayStart(ClassType.Dictionary, writer, options, writeNull: true);
				}
				if (state.Current.PopStackOnEndCollection)
				{
					state.Pop();
				}
				return true;
			}
			state.Current.CollectionEnumerator = enumerable.GetEnumerator();
			if (state.Current.ExtensionDataStatus != ExtensionDataWriteStatus.Writing)
			{
				state.Current.WriteObjectOrArrayStart(ClassType.Dictionary, writer, options);
			}
		}
		if (state.Current.CollectionEnumerator.MoveNext())
		{
			Debug.Assert(state.Current.CollectionEnumerator.Current != null);
			bool obtainedValues = false;
			string key = null;
			object value = null;
			if (elementClassInfo.ClassType == ClassType.Unknown)
			{
				jsonPropertyInfo.GetDictionaryKeyAndValue(ref state.Current, out key, out value);
				GetRuntimeClassInfo(value, ref elementClassInfo, options);
				obtainedValues = true;
			}
			if (elementClassInfo.ClassType == ClassType.Value)
			{
				elementClassInfo.PolicyProperty.WriteDictionary(ref state, writer);
			}
			else
			{
				if (!obtainedValues)
				{
					jsonPropertyInfo.GetDictionaryKeyAndValue(ref state.Current, out key, out value);
				}
				state.Push(elementClassInfo, value);
				state.Current.KeyName = key;
			}
			return false;
		}
		if (state.Current.ExtensionDataStatus == ExtensionDataWriteStatus.Writing)
		{
			state.Current.ExtensionDataStatus = ExtensionDataWriteStatus.Finished;
		}
		else
		{
			writer.WriteEndObject();
		}
		if (state.Current.PopStackOnEndCollection)
		{
			state.Pop();
		}
		else
		{
			state.Current.EndDictionary();
		}
		return true;
	}

	internal static void WriteDictionary<TProperty>(JsonConverter<TProperty> converter, JsonSerializerOptions options, ref WriteStackFrame current, Utf8JsonWriter writer)
	{
		Debug.Assert(converter != null && current.CollectionEnumerator != null);
		string key;
		TProperty value;
		if (current.CollectionEnumerator is IEnumerator<KeyValuePair<string, TProperty>> { Current: var current2 } enumerator)
		{
			key = current2.Key;
			value = enumerator.Current.Value;
		}
		else if (current.CollectionEnumerator is IEnumerator<KeyValuePair<string, object>> { Current: var current3 } polymorphicEnumerator)
		{
			key = current3.Key;
			value = (TProperty)polymorphicEnumerator.Current.Value;
		}
		else
		{
			if (!(current.CollectionEnumerator is IDictionaryEnumerator { Key: string keyAsString } iDictionaryEnumerator))
			{
				throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(current.JsonPropertyInfo.DeclaredPropertyType, current.JsonPropertyInfo.ParentClassType, current.JsonPropertyInfo.PropertyInfo);
			}
			key = keyAsString;
			value = (TProperty)iDictionaryEnumerator.Value;
		}
		Debug.Assert(key != null);
		if (options.DictionaryKeyPolicy != null && current.ExtensionDataStatus != ExtensionDataWriteStatus.Writing)
		{
			key = options.DictionaryKeyPolicy.ConvertName(key);
			if (key == null)
			{
				ThrowHelper.ThrowInvalidOperationException_SerializerDictionaryKeyNull(options.DictionaryKeyPolicy.GetType());
			}
		}
		if (value == null)
		{
			writer.WriteNull(key);
			return;
		}
		writer.WritePropertyName(key);
		converter.Write(writer, value, options);
	}

	private static bool HandleEnumerable(JsonClassInfo elementClassInfo, JsonSerializerOptions options, Utf8JsonWriter writer, ref WriteStack state)
	{
		Debug.Assert(state.Current.JsonPropertyInfo.ClassType == ClassType.Enumerable);
		if (state.Current.CollectionEnumerator == null)
		{
			IEnumerable enumerable = (IEnumerable)state.Current.JsonPropertyInfo.GetValueAsObject(state.Current.CurrentValue);
			if (enumerable == null)
			{
				if (state.Current.JsonClassInfo.ClassType != ClassType.Object || !state.Current.JsonPropertyInfo.IgnoreNullValues)
				{
					state.Current.WriteObjectOrArrayStart(ClassType.Enumerable, writer, options, writeNull: true);
				}
				if (state.Current.PopStackOnEndCollection)
				{
					state.Pop();
				}
				return true;
			}
			state.Current.CollectionEnumerator = enumerable.GetEnumerator();
			state.Current.WriteObjectOrArrayStart(ClassType.Enumerable, writer, options);
		}
		if (state.Current.CollectionEnumerator.MoveNext())
		{
			if (elementClassInfo.ClassType == ClassType.Unknown)
			{
				object currentValue = state.Current.CollectionEnumerator.Current;
				GetRuntimeClassInfo(currentValue, ref elementClassInfo, options);
			}
			if (elementClassInfo.ClassType == ClassType.Value)
			{
				elementClassInfo.PolicyProperty.WriteEnumerable(ref state, writer);
			}
			else if (state.Current.CollectionEnumerator.Current == null)
			{
				writer.WriteNullValue();
			}
			else
			{
				object nextValue = state.Current.CollectionEnumerator.Current;
				state.Push(elementClassInfo, nextValue);
			}
			return false;
		}
		writer.WriteEndArray();
		if (state.Current.PopStackOnEndCollection)
		{
			state.Pop();
		}
		else
		{
			state.Current.EndArray();
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool WriteObject(JsonSerializerOptions options, Utf8JsonWriter writer, ref WriteStack state)
	{
		if (!state.Current.StartObjectWritten)
		{
			if (state.Current.CurrentValue == null)
			{
				state.Current.WriteObjectOrArrayStart(ClassType.Object, writer, options, writeNull: true);
				return WriteEndObject(ref state);
			}
			state.Current.WriteObjectOrArrayStart(ClassType.Object, writer, options);
			state.Current.MoveToNextProperty = true;
		}
		if (state.Current.MoveToNextProperty)
		{
			state.Current.NextProperty();
		}
		if (state.Current.ExtensionDataStatus != ExtensionDataWriteStatus.Finished)
		{
			Debug.Assert(state.Current.JsonClassInfo.ClassType != ClassType.Unknown);
			JsonPropertyInfo jsonPropertyInfo = state.Current.JsonClassInfo.PropertyCacheArray[state.Current.PropertyEnumeratorIndex - 1];
			HandleObject(jsonPropertyInfo, options, writer, ref state);
			return false;
		}
		writer.WriteEndObject();
		return WriteEndObject(ref state);
	}

	private static bool WriteEndObject(ref WriteStack state)
	{
		if (state.Current.PopStackOnEndObject)
		{
			state.Pop();
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static void HandleObject(JsonPropertyInfo jsonPropertyInfo, JsonSerializerOptions options, Utf8JsonWriter writer, ref WriteStack state)
	{
		Debug.Assert(state.Current.JsonClassInfo.ClassType == ClassType.Object || state.Current.JsonClassInfo.ClassType == ClassType.Unknown);
		if (!jsonPropertyInfo.ShouldSerialize)
		{
			state.Current.MoveToNextProperty = true;
			return;
		}
		bool obtainedValue = false;
		object currentValue = null;
		if (jsonPropertyInfo.ClassType == ClassType.Unknown)
		{
			currentValue = jsonPropertyInfo.GetValueAsObject(state.Current.CurrentValue);
			obtainedValue = true;
			GetRuntimePropertyInfo(currentValue, state.Current.JsonClassInfo, ref jsonPropertyInfo, options);
		}
		state.Current.JsonPropertyInfo = jsonPropertyInfo;
		if (jsonPropertyInfo.ClassType == ClassType.Value)
		{
			jsonPropertyInfo.Write(ref state, writer);
			state.Current.MoveToNextProperty = true;
			return;
		}
		if (jsonPropertyInfo.ClassType == ClassType.Enumerable)
		{
			if (HandleEnumerable(jsonPropertyInfo.ElementClassInfo, options, writer, ref state))
			{
				state.Current.MoveToNextProperty = true;
			}
			return;
		}
		if (jsonPropertyInfo.ClassType == ClassType.Dictionary)
		{
			if (HandleDictionary(jsonPropertyInfo.ElementClassInfo, options, writer, ref state))
			{
				state.Current.MoveToNextProperty = true;
			}
			return;
		}
		if (jsonPropertyInfo.ClassType == ClassType.IDictionaryConstructible)
		{
			state.Current.IsIDictionaryConstructibleProperty = true;
			if (HandleDictionary(jsonPropertyInfo.ElementClassInfo, options, writer, ref state))
			{
				state.Current.MoveToNextProperty = true;
			}
			return;
		}
		if (!obtainedValue)
		{
			currentValue = jsonPropertyInfo.GetValueAsObject(state.Current.CurrentValue);
		}
		if (currentValue != null)
		{
			JsonPropertyInfo previousPropertyInfo = state.Current.JsonPropertyInfo;
			state.Current.MoveToNextProperty = true;
			JsonClassInfo nextClassInfo = jsonPropertyInfo.RuntimeClassInfo;
			state.Push(nextClassInfo, currentValue);
			state.Current.JsonPropertyInfo = previousPropertyInfo;
		}
		else
		{
			if (!jsonPropertyInfo.IgnoreNullValues)
			{
				writer.WriteNull(jsonPropertyInfo.EscapedName.Value);
			}
			state.Current.MoveToNextProperty = true;
		}
	}

	private static void GetRuntimeClassInfo(object value, ref JsonClassInfo jsonClassInfo, JsonSerializerOptions options)
	{
		if (value != null)
		{
			Type runtimeType = value.GetType();
			if (runtimeType != typeof(object))
			{
				jsonClassInfo = options.GetOrAddClass(runtimeType);
			}
		}
	}

	private static void GetRuntimePropertyInfo(object value, JsonClassInfo jsonClassInfo, ref JsonPropertyInfo jsonPropertyInfo, JsonSerializerOptions options)
	{
		if (value != null)
		{
			Type runtimeType = value.GetType();
			if (runtimeType != typeof(object))
			{
				jsonPropertyInfo = jsonClassInfo.GetOrAddPolymorphicProperty(jsonPropertyInfo, runtimeType, options);
			}
		}
	}

	private static void VerifyValueAndType(object value, Type type)
	{
		if (type == null)
		{
			if (value != null)
			{
				throw new ArgumentNullException("type");
			}
		}
		else if (value != null && !type.IsAssignableFrom(value.GetType()))
		{
			ThrowHelper.ThrowArgumentException_DeserializeWrongType(type, value);
		}
	}

	private static byte[] WriteCoreBytes(object value, Type type, JsonSerializerOptions options)
	{
		if (options == null)
		{
			options = JsonSerializerOptions.s_defaultOptions;
		}
		using PooledByteBufferWriter output = new PooledByteBufferWriter(options.DefaultBufferSize);
		WriteCore(output, value, type, options);
		return output.WrittenMemory.ToArray();
	}

	private static string WriteCoreString(object value, Type type, JsonSerializerOptions options)
	{
		if (options == null)
		{
			options = JsonSerializerOptions.s_defaultOptions;
		}
		using PooledByteBufferWriter output = new PooledByteBufferWriter(options.DefaultBufferSize);
		WriteCore(output, value, type, options);
		return JsonReaderHelper.TranscodeHelper(output.WrittenMemory.Span);
	}

	private static void WriteValueCore(Utf8JsonWriter writer, object value, Type type, JsonSerializerOptions options)
	{
		if (options == null)
		{
			options = JsonSerializerOptions.s_defaultOptions;
		}
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		WriteCore(writer, value, type, options);
	}

	private static void WriteCore(PooledByteBufferWriter output, object value, Type type, JsonSerializerOptions options)
	{
		using Utf8JsonWriter writer = new Utf8JsonWriter(output, options.GetWriterOptions());
		WriteCore(writer, value, type, options);
	}

	private static void WriteCore(Utf8JsonWriter writer, object value, Type type, JsonSerializerOptions options)
	{
		Debug.Assert(type != null || value == null);
		Debug.Assert(writer != null);
		if (value == null)
		{
			writer.WriteNullValue();
		}
		else
		{
			if (type == typeof(object))
			{
				type = value.GetType();
			}
			WriteStack state = default(WriteStack);
			state.Current.Initialize(type, options);
			state.Current.CurrentValue = value;
			Write(writer, writer.CurrentDepth, -1, options, ref state);
		}
		writer.Flush();
	}

	public static Task SerializeAsync<TValue>(Stream utf8Json, TValue value, JsonSerializerOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		return WriteAsyncCore(utf8Json, value, typeof(TValue), options, cancellationToken);
	}

	public static Task SerializeAsync(Stream utf8Json, object value, Type inputType, JsonSerializerOptions options = null, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (utf8Json == null)
		{
			throw new ArgumentNullException("utf8Json");
		}
		VerifyValueAndType(value, inputType);
		return WriteAsyncCore(utf8Json, value, inputType, options, cancellationToken);
	}

	private static async Task WriteAsyncCore(Stream utf8Json, object value, Type inputType, JsonSerializerOptions options, CancellationToken cancellationToken)
	{
		if (options == null)
		{
			options = JsonSerializerOptions.s_defaultOptions;
		}
		JsonWriterOptions writerOptions = options.GetWriterOptions();
		using PooledByteBufferWriter bufferWriter = new PooledByteBufferWriter(options.DefaultBufferSize);
		using Utf8JsonWriter writer = new Utf8JsonWriter(bufferWriter, writerOptions);
		if (value == null)
		{
			writer.WriteNullValue();
			writer.Flush();
			await TaskTheraotExtensions.ConfigureAwait(bufferWriter.WriteToStreamAsync(utf8Json, cancellationToken), continueOnCapturedContext: false);
			return;
		}
		if (inputType == null)
		{
			inputType = value.GetType();
		}
		WriteStack state = default(WriteStack);
		state.Current.Initialize(inputType, options);
		state.Current.CurrentValue = value;
		bool isFinalBlock;
		do
		{
			int flushThreshold = (int)((double)bufferWriter.Capacity * 0.9);
			isFinalBlock = Write(writer, 0, flushThreshold, options, ref state);
			writer.Flush();
			await TaskTheraotExtensions.ConfigureAwait(bufferWriter.WriteToStreamAsync(utf8Json, cancellationToken), continueOnCapturedContext: false);
			bufferWriter.Clear();
		}
		while (!isFinalBlock);
	}

	public static string Serialize<TValue>(TValue value, JsonSerializerOptions options = null)
	{
		return WriteCoreString(value, typeof(TValue), options);
	}

	public static string Serialize(object value, Type inputType, JsonSerializerOptions options = null)
	{
		VerifyValueAndType(value, inputType);
		return WriteCoreString(value, inputType, options);
	}

	public static void Serialize<TValue>(Utf8JsonWriter writer, TValue value, JsonSerializerOptions options = null)
	{
		WriteValueCore(writer, value, typeof(TValue), options);
	}

	public static void Serialize(Utf8JsonWriter writer, object value, Type inputType, JsonSerializerOptions options = null)
	{
		VerifyValueAndType(value, inputType);
		WriteValueCore(writer, value, inputType, options);
	}
}
