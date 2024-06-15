#define DEBUG
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json;

[DebuggerDisplay("PropertyInfo={PropertyInfo}, Element={ElementClassInfo}")]
internal abstract class JsonPropertyInfo
{
	private static readonly JsonEnumerableConverter s_jsonDerivedEnumerableConverter = new DefaultDerivedEnumerableConverter();

	private static readonly JsonEnumerableConverter s_jsonArrayConverter = new DefaultArrayConverter();

	private static readonly JsonEnumerableConverter s_jsonICollectionConverter = new DefaultICollectionConverter();

	private static readonly JsonEnumerableConverter s_jsonImmutableEnumerableConverter = new DefaultImmutableEnumerableConverter();

	private static readonly JsonDictionaryConverter s_jsonDerivedDictionaryConverter = new DefaultDerivedDictionaryConverter();

	private static readonly JsonDictionaryConverter s_jsonIDictionaryConverter = new DefaultIDictionaryConverter();

	private static readonly JsonDictionaryConverter s_jsonImmutableDictionaryConverter = new DefaultImmutableDictionaryConverter();

	public static readonly JsonPropertyInfo s_missingProperty = GetMissingProperty();

	private JsonClassInfo _elementClassInfo;

	private JsonClassInfo _runtimeClassInfo;

	private JsonClassInfo _declaredTypeClassInfo;

	private JsonPropertyInfo _dictionaryValuePropertyPolicy;

	public ClassType ClassType;

	public JsonEncodedText? EscapedName;

	public bool CanBeNull { get; private set; }

	public abstract JsonConverter ConverterBase { get; set; }

	public Type DeclaredPropertyType { get; private set; }

	public Type ImplementedPropertyType { get; private set; }

	public JsonPropertyInfo DictionaryValuePropertyPolicy
	{
		get
		{
			if ((_dictionaryValuePropertyPolicy = ElementClassInfo.PolicyProperty) == null)
			{
				Debug.Assert(ClassType == ClassType.Dictionary || ClassType == ClassType.IDictionaryConstructible);
				Type dictionaryValueType = ElementType;
				Debug.Assert(ElementType != null);
				_dictionaryValuePropertyPolicy = JsonClassInfo.CreateProperty(dictionaryValueType, dictionaryValueType, dictionaryValueType, null, dictionaryValueType, null, Options);
			}
			return _dictionaryValuePropertyPolicy;
		}
	}

	public JsonClassInfo ElementClassInfo
	{
		get
		{
			if (_elementClassInfo == null && ElementType != null)
			{
				Debug.Assert(ClassType == ClassType.Enumerable || ClassType == ClassType.Dictionary || ClassType == ClassType.IDictionaryConstructible);
				_elementClassInfo = Options.GetOrAddClass(ElementType);
			}
			return _elementClassInfo;
		}
	}

	public Type ElementType { get; set; }

	public JsonEnumerableConverter EnumerableConverter { get; private set; }

	public JsonDictionaryConverter DictionaryConverter { get; private set; }

	public bool HasGetter { get; set; }

	public bool HasSetter { get; set; }

	public bool HasInternalConverter { get; private set; }

	public bool IgnoreNullValues { get; private set; }

	public bool IsNullableType { get; private set; }

	public bool IsPropertyPolicy { get; protected set; }

	public byte[] JsonPropertyName { get; set; }

	public byte[] Name { get; private set; }

	public string NameAsString { get; private set; }

	public ulong PropertyNameKey { get; set; }

	protected JsonSerializerOptions Options { get; set; }

	public Type ParentClassType { get; private set; }

	public PropertyInfo PropertyInfo { get; private set; }

	public JsonClassInfo RuntimeClassInfo
	{
		get
		{
			if (_runtimeClassInfo == null)
			{
				_runtimeClassInfo = Options.GetOrAddClass(RuntimePropertyType);
			}
			return _runtimeClassInfo;
		}
	}

	public JsonClassInfo DeclaredTypeClassInfo
	{
		get
		{
			if (_declaredTypeClassInfo == null)
			{
				_declaredTypeClassInfo = Options.GetOrAddClass(DeclaredPropertyType);
			}
			return _declaredTypeClassInfo;
		}
	}

	public Type RuntimePropertyType { get; private set; }

	public bool ShouldSerialize { get; private set; }

	public bool ShouldDeserialize { get; private set; }

	private static JsonPropertyInfo GetMissingProperty()
	{
		JsonPropertyInfo info = new JsonPropertyInfoNotNullable<object, object, object, object>();
		info.IsPropertyPolicy = false;
		info.ShouldDeserialize = false;
		info.ShouldSerialize = false;
		return info;
	}

	public void CopyRuntimeSettingsTo(JsonPropertyInfo other)
	{
		other.EscapedName = EscapedName;
		other.Name = Name;
		other.NameAsString = NameAsString;
		other.PropertyNameKey = PropertyNameKey;
	}

	public abstract IList CreateConverterList();

	public abstract IEnumerable CreateDerivedEnumerableInstance(ref ReadStack state, JsonPropertyInfo collectionPropertyInfo, IList sourceList);

	public abstract object CreateDerivedDictionaryInstance(ref ReadStack state, JsonPropertyInfo collectionPropertyInfo, IDictionary sourceDictionary);

	public abstract IEnumerable CreateIEnumerableInstance(ref ReadStack state, Type parentType, IList sourceList);

	public abstract IDictionary CreateIDictionaryInstance(ref ReadStack state, Type parentType, IDictionary sourceDictionary);

	public abstract IEnumerable CreateImmutableCollectionInstance(ref ReadStack state, Type collectionType, string delegateKey, IList sourceList, JsonSerializerOptions options);

	public abstract IDictionary CreateImmutableDictionaryInstance(ref ReadStack state, Type collectionType, string delegateKey, IDictionary sourceDictionary, JsonSerializerOptions options);

	public static JsonPropertyInfo CreateIgnoredPropertyPlaceholder(PropertyInfo propertyInfo, JsonSerializerOptions options)
	{
		JsonPropertyInfo jsonPropertyInfo = new JsonPropertyInfoNotNullable<sbyte, sbyte, sbyte, sbyte>();
		jsonPropertyInfo.Options = options;
		jsonPropertyInfo.PropertyInfo = propertyInfo;
		jsonPropertyInfo.DeterminePropertyName();
		Debug.Assert(!jsonPropertyInfo.ShouldDeserialize);
		Debug.Assert(!jsonPropertyInfo.ShouldSerialize);
		return jsonPropertyInfo;
	}

	private void DeterminePropertyName()
	{
		if (PropertyInfo == null)
		{
			return;
		}
		JsonPropertyNameAttribute nameAttribute = GetAttribute<JsonPropertyNameAttribute>(PropertyInfo);
		if (nameAttribute != null)
		{
			string name = nameAttribute.Name;
			if (name == null)
			{
				ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameNull(ParentClassType, this);
			}
			NameAsString = name;
		}
		else if (Options.PropertyNamingPolicy != null)
		{
			string name2 = Options.PropertyNamingPolicy.ConvertName(PropertyInfo.Name);
			if (name2 == null)
			{
				ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameNull(ParentClassType, this);
			}
			NameAsString = name2;
		}
		else
		{
			NameAsString = PropertyInfo.Name;
		}
		Debug.Assert(NameAsString != null);
		Name = Encoding.UTF8.GetBytes(NameAsString);
		EscapedName = JsonEncodedText.Encode(Name, Options.Encoder);
		ulong key = JsonClassInfo.GetKey(Name);
		PropertyNameKey = key;
	}

	private void DetermineSerializationCapabilities()
	{
		if (ClassType != ClassType.Enumerable && ClassType != ClassType.Dictionary && ClassType != ClassType.IDictionaryConstructible)
		{
			ShouldSerialize = HasGetter && (HasSetter || !Options.IgnoreReadOnlyProperties);
			ShouldDeserialize = HasSetter;
		}
		else
		{
			if (!HasGetter)
			{
				return;
			}
			ShouldSerialize = true;
			if (!HasSetter)
			{
				return;
			}
			ShouldDeserialize = true;
			if (RuntimePropertyType.IsArray)
			{
				if (RuntimePropertyType.GetArrayRank() > 1)
				{
					throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(RuntimePropertyType, ParentClassType, PropertyInfo);
				}
				EnumerableConverter = s_jsonArrayConverter;
			}
			else if (ClassType == ClassType.IDictionaryConstructible)
			{
				if (DeclaredPropertyType == ImplementedPropertyType)
				{
					if (RuntimePropertyType.FullName.StartsWith("System.Collections.Immutable"))
					{
						DefaultImmutableDictionaryConverter.RegisterImmutableDictionary(RuntimePropertyType, JsonClassInfo.GetElementType(RuntimePropertyType, ParentClassType, PropertyInfo, Options), Options);
						DictionaryConverter = s_jsonImmutableDictionaryConverter;
					}
					else if (JsonClassInfo.IsDeserializedByConstructingWithIDictionary(RuntimePropertyType))
					{
						DictionaryConverter = s_jsonIDictionaryConverter;
					}
				}
				else
				{
					DictionaryConverter = s_jsonDerivedDictionaryConverter;
				}
			}
			else if (ClassType == ClassType.Enumerable)
			{
				if (DeclaredPropertyType != ImplementedPropertyType && (!typeof(IList).IsAssignableFrom(RuntimePropertyType) || ImplementedPropertyType == typeof(ArrayList) || ImplementedPropertyType == typeof(IList)))
				{
					EnumerableConverter = s_jsonDerivedEnumerableConverter;
				}
				else if (JsonClassInfo.IsDeserializedByConstructingWithIList(RuntimePropertyType) || (!typeof(IList).IsAssignableFrom(RuntimePropertyType) && JsonClassInfo.HasConstructorThatTakesGenericIEnumerable(RuntimePropertyType, Options)))
				{
					EnumerableConverter = s_jsonICollectionConverter;
				}
				else if (RuntimePropertyType.IsGenericType && RuntimePropertyType.FullName.StartsWith("System.Collections.Immutable") && RuntimePropertyType.GetGenericArguments().Length == 1)
				{
					DefaultImmutableEnumerableConverter.RegisterImmutableCollection(RuntimePropertyType, JsonClassInfo.GetElementType(RuntimePropertyType, ParentClassType, PropertyInfo, Options), Options);
					EnumerableConverter = s_jsonImmutableEnumerableConverter;
				}
			}
		}
	}

	public static TAttribute GetAttribute<TAttribute>(PropertyInfo propertyInfo) where TAttribute : Attribute
	{
		return (TAttribute)(((object)propertyInfo != null) ? CustomAttributeExtensions.GetCustomAttribute(propertyInfo, typeof(TAttribute), inherit: false) : null);
	}

	public abstract Type GetConcreteType(Type type);

	public abstract Type GetDictionaryConcreteType();

	public void GetDictionaryKeyAndValue(ref WriteStackFrame writeStackFrame, out string key, out object value)
	{
		Debug.Assert(ClassType == ClassType.Dictionary || ClassType == ClassType.IDictionaryConstructible);
		if (writeStackFrame.CollectionEnumerator is IDictionaryEnumerator iDictionaryEnumerator)
		{
			if (!(iDictionaryEnumerator.Key is string keyAsString))
			{
				throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(writeStackFrame.JsonPropertyInfo.DeclaredPropertyType, writeStackFrame.JsonPropertyInfo.ParentClassType, writeStackFrame.JsonPropertyInfo.PropertyInfo);
			}
			key = keyAsString;
			value = iDictionaryEnumerator.Value;
		}
		else
		{
			DictionaryValuePropertyPolicy.GetDictionaryKeyAndValueFromGenericDictionary(ref writeStackFrame, out key, out value);
		}
	}

	public abstract void GetDictionaryKeyAndValueFromGenericDictionary(ref WriteStackFrame writeStackFrame, out string key, out object value);

	public virtual void GetPolicies()
	{
		DetermineSerializationCapabilities();
		DeterminePropertyName();
		IgnoreNullValues = Options.IgnoreNullValues;
	}

	public abstract object GetValueAsObject(object obj);

	public virtual void Initialize(Type parentClassType, Type declaredPropertyType, Type runtimePropertyType, Type implementedPropertyType, PropertyInfo propertyInfo, Type elementType, JsonConverter converter, JsonSerializerOptions options)
	{
		ParentClassType = parentClassType;
		DeclaredPropertyType = declaredPropertyType;
		RuntimePropertyType = runtimePropertyType;
		ImplementedPropertyType = implementedPropertyType;
		PropertyInfo = propertyInfo;
		ElementType = elementType;
		Options = options;
		IsNullableType = runtimePropertyType.IsGenericType && runtimePropertyType.GetGenericTypeDefinition() == typeof(Nullable<>);
		CanBeNull = IsNullableType || !runtimePropertyType.IsValueType;
		if (converter != null)
		{
			ConverterBase = converter;
			HasInternalConverter = converter.GetType().Assembly == GetType().Assembly;
			if (runtimePropertyType == typeof(object))
			{
				ClassType = ClassType.Unknown;
			}
			else
			{
				ClassType = ClassType.Value;
			}
		}
		else if (declaredPropertyType != implementedPropertyType && !JsonClassInfo.IsNativelySupportedCollection(declaredPropertyType))
		{
			ClassType = JsonClassInfo.GetClassType(declaredPropertyType, options);
		}
		else
		{
			ClassType = JsonClassInfo.GetClassType(runtimePropertyType, options);
		}
	}

	protected abstract void OnRead(ref ReadStack state, ref Utf8JsonReader reader);

	protected abstract void OnReadEnumerable(ref ReadStack state, ref Utf8JsonReader reader);

	protected abstract void OnWrite(ref WriteStackFrame current, Utf8JsonWriter writer);

	protected virtual void OnWriteDictionary(ref WriteStackFrame current, Utf8JsonWriter writer)
	{
	}

	protected abstract void OnWriteEnumerable(ref WriteStackFrame current, Utf8JsonWriter writer);

	public void Read(JsonTokenType tokenType, ref ReadStack state, ref Utf8JsonReader reader)
	{
		Debug.Assert(ShouldDeserialize);
		JsonClassInfo elementClassInfo = ElementClassInfo;
		JsonPropertyInfo propertyInfo;
		if (elementClassInfo != null && (propertyInfo = elementClassInfo.PolicyProperty) != null)
		{
			if (!state.Current.CollectionPropertyInitialized)
			{
				ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(propertyInfo.RuntimePropertyType);
			}
			propertyInfo.ReadEnumerable(tokenType, ref state, ref reader);
		}
		else if (HasInternalConverter)
		{
			JsonTokenType originalTokenType = reader.TokenType;
			int originalDepth = reader.CurrentDepth;
			long originalBytesConsumed = reader.BytesConsumed;
			OnRead(ref state, ref reader);
			VerifyRead(originalTokenType, originalDepth, originalBytesConsumed, ref reader);
		}
		else
		{
			JsonTokenType originalTokenType2 = reader.TokenType;
			int originalDepth2 = reader.CurrentDepth;
			long originalBytesConsumed2 = reader.BytesConsumed;
			OnRead(ref state, ref reader);
			VerifyRead(originalTokenType2, originalDepth2, originalBytesConsumed2, ref reader);
		}
	}

	public void ReadEnumerable(JsonTokenType tokenType, ref ReadStack state, ref Utf8JsonReader reader)
	{
		Debug.Assert(ShouldDeserialize);
		if (HasInternalConverter)
		{
			JsonTokenType originalTokenType = reader.TokenType;
			int originalDepth = reader.CurrentDepth;
			long originalBytesConsumed = reader.BytesConsumed;
			OnReadEnumerable(ref state, ref reader);
			VerifyRead(originalTokenType, originalDepth, originalBytesConsumed, ref reader);
		}
		else
		{
			JsonTokenType originalTokenType2 = reader.TokenType;
			int originalDepth2 = reader.CurrentDepth;
			long originalBytesConsumed2 = reader.BytesConsumed;
			OnReadEnumerable(ref state, ref reader);
			VerifyRead(originalTokenType2, originalDepth2, originalBytesConsumed2, ref reader);
		}
	}

	public abstract void SetValueAsObject(object obj, object value);

	private void VerifyRead(JsonTokenType tokenType, int depth, long bytesConsumed, ref Utf8JsonReader reader)
	{
		switch (tokenType)
		{
		case JsonTokenType.StartArray:
			if (reader.TokenType != JsonTokenType.EndArray)
			{
				ThrowHelper.ThrowJsonException_SerializationConverterRead(ConverterBase);
			}
			else if (depth != reader.CurrentDepth)
			{
				ThrowHelper.ThrowJsonException_SerializationConverterRead(ConverterBase);
			}
			Debug.Assert(bytesConsumed < reader.BytesConsumed);
			break;
		case JsonTokenType.StartObject:
			if (reader.TokenType != JsonTokenType.EndObject)
			{
				ThrowHelper.ThrowJsonException_SerializationConverterRead(ConverterBase);
			}
			else if (depth != reader.CurrentDepth)
			{
				ThrowHelper.ThrowJsonException_SerializationConverterRead(ConverterBase);
			}
			Debug.Assert(bytesConsumed < reader.BytesConsumed);
			break;
		default:
			if (reader.BytesConsumed != bytesConsumed)
			{
				ThrowHelper.ThrowJsonException_SerializationConverterRead(ConverterBase);
			}
			Debug.Assert(reader.TokenType == tokenType);
			break;
		}
	}

	public void Write(ref WriteStack state, Utf8JsonWriter writer)
	{
		Debug.Assert(ShouldSerialize);
		if (state.Current.CollectionEnumerator != null)
		{
			JsonPropertyInfo propertyInfo = ElementClassInfo.PolicyProperty;
			propertyInfo.WriteEnumerable(ref state, writer);
		}
		else if (HasInternalConverter)
		{
			int originalDepth = writer.CurrentDepth;
			OnWrite(ref state.Current, writer);
			VerifyWrite(originalDepth, writer);
		}
		else
		{
			int originalDepth2 = writer.CurrentDepth;
			OnWrite(ref state.Current, writer);
			VerifyWrite(originalDepth2, writer);
		}
	}

	public void WriteDictionary(ref WriteStack state, Utf8JsonWriter writer)
	{
		Debug.Assert(ShouldSerialize);
		if (HasInternalConverter)
		{
			int originalDepth = writer.CurrentDepth;
			OnWriteDictionary(ref state.Current, writer);
			VerifyWrite(originalDepth, writer);
		}
		else
		{
			int originalDepth2 = writer.CurrentDepth;
			OnWriteDictionary(ref state.Current, writer);
			VerifyWrite(originalDepth2, writer);
		}
	}

	public void WriteEnumerable(ref WriteStack state, Utf8JsonWriter writer)
	{
		Debug.Assert(ShouldSerialize);
		if (HasInternalConverter)
		{
			int originalDepth = writer.CurrentDepth;
			OnWriteEnumerable(ref state.Current, writer);
			VerifyWrite(originalDepth, writer);
		}
		else
		{
			int originalDepth2 = writer.CurrentDepth;
			OnWriteEnumerable(ref state.Current, writer);
			VerifyWrite(originalDepth2, writer);
		}
	}

	private void VerifyWrite(int originalDepth, Utf8JsonWriter writer)
	{
		if (originalDepth != writer.CurrentDepth)
		{
			ThrowHelper.ThrowJsonException_SerializationConverterWrite(ConverterBase);
		}
	}
}
