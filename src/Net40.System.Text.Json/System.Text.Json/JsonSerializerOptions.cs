#define DEBUG
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json;

public sealed class JsonSerializerOptions
{
	private static readonly Dictionary<Type, JsonConverter> s_defaultSimpleConverters = GetDefaultSimpleConverters();

	private static readonly List<JsonConverter> s_defaultFactoryConverters = GetDefaultConverters();

	private readonly ConcurrentDictionary<Type, JsonConverter> _converters = new ConcurrentDictionary<Type, JsonConverter>();

	private const int NumberOfSimpleConverters = 21;

	internal const int BufferSizeDefault = 16384;

	internal static readonly JsonSerializerOptions s_defaultOptions = new JsonSerializerOptions();

	private readonly ConcurrentDictionary<Type, JsonClassInfo> _classes = new ConcurrentDictionary<Type, JsonClassInfo>();

	private readonly ConcurrentDictionary<Type, JsonPropertyInfo> _objectJsonProperties = new ConcurrentDictionary<Type, JsonPropertyInfo>();

	private static ConcurrentDictionary<string, ImmutableCollectionCreator> s_createRangeDelegates = new ConcurrentDictionary<string, ImmutableCollectionCreator>();

	private MemberAccessor _memberAccessorStrategy;

	private JsonNamingPolicy _dictionayKeyPolicy;

	private JsonNamingPolicy _jsonPropertyNamingPolicy;

	private JsonCommentHandling _readCommentHandling;

	private JavaScriptEncoder _encoder;

	private int _defaultBufferSize = 16384;

	private int _maxDepth;

	private bool _allowTrailingCommas;

	private bool _haveTypesBeenCreated;

	private bool _ignoreNullValues;

	private bool _ignoreReadOnlyProperties;

	private bool _propertyNameCaseInsensitive;

	private bool _writeIndented;

	public IList<JsonConverter> Converters { get; }

	private static IEnumerable<JsonConverter> DefaultSimpleConverters
	{
		get
		{
			yield return new JsonConverterBoolean();
			yield return new JsonConverterByte();
			yield return new JsonConverterByteArray();
			yield return new JsonConverterChar();
			yield return new JsonConverterDateTime();
			yield return new JsonConverterDateTimeOffset();
			yield return new JsonConverterDouble();
			yield return new JsonConverterDecimal();
			yield return new JsonConverterGuid();
			yield return new JsonConverterInt16();
			yield return new JsonConverterInt32();
			yield return new JsonConverterInt64();
			yield return new JsonConverterJsonElement();
			yield return new JsonConverterObject();
			yield return new JsonConverterSByte();
			yield return new JsonConverterSingle();
			yield return new JsonConverterString();
			yield return new JsonConverterUInt16();
			yield return new JsonConverterUInt32();
			yield return new JsonConverterUInt64();
			yield return new JsonConverterUri();
		}
	}

	public bool AllowTrailingCommas
	{
		get
		{
			return _allowTrailingCommas;
		}
		set
		{
			VerifyMutable();
			_allowTrailingCommas = value;
		}
	}

	public int DefaultBufferSize
	{
		get
		{
			return _defaultBufferSize;
		}
		set
		{
			VerifyMutable();
			if (value < 1)
			{
				throw new ArgumentException("SR.SerializationInvalidBufferSize");
			}
			_defaultBufferSize = value;
		}
	}

	public JavaScriptEncoder Encoder
	{
		get
		{
			return _encoder;
		}
		set
		{
			VerifyMutable();
			_encoder = value;
		}
	}

	public JsonNamingPolicy DictionaryKeyPolicy
	{
		get
		{
			return _dictionayKeyPolicy;
		}
		set
		{
			VerifyMutable();
			_dictionayKeyPolicy = value;
		}
	}

	public bool IgnoreNullValues
	{
		get
		{
			return _ignoreNullValues;
		}
		set
		{
			VerifyMutable();
			_ignoreNullValues = value;
		}
	}

	public bool IgnoreReadOnlyProperties
	{
		get
		{
			return _ignoreReadOnlyProperties;
		}
		set
		{
			VerifyMutable();
			_ignoreReadOnlyProperties = value;
		}
	}

	public int MaxDepth
	{
		get
		{
			return _maxDepth;
		}
		set
		{
			VerifyMutable();
			if (value < 0)
			{
				throw ThrowHelper.GetArgumentOutOfRangeException_MaxDepthMustBePositive("value");
			}
			_maxDepth = value;
			EffectiveMaxDepth = ((value == 0) ? 64 : value);
		}
	}

	internal int EffectiveMaxDepth { get; private set; } = 64;


	public JsonNamingPolicy PropertyNamingPolicy
	{
		get
		{
			return _jsonPropertyNamingPolicy;
		}
		set
		{
			VerifyMutable();
			_jsonPropertyNamingPolicy = value;
		}
	}

	public bool PropertyNameCaseInsensitive
	{
		get
		{
			return _propertyNameCaseInsensitive;
		}
		set
		{
			VerifyMutable();
			_propertyNameCaseInsensitive = value;
		}
	}

	public JsonCommentHandling ReadCommentHandling
	{
		get
		{
			return _readCommentHandling;
		}
		set
		{
			VerifyMutable();
			Debug.Assert((int)value >= 0);
			if ((int)value > 1)
			{
				throw new ArgumentOutOfRangeException("value", "SR.JsonSerializerDoesNotSupportComments");
			}
			_readCommentHandling = value;
		}
	}

	public bool WriteIndented
	{
		get
		{
			return _writeIndented;
		}
		set
		{
			VerifyMutable();
			_writeIndented = value;
		}
	}

	internal MemberAccessor MemberAccessorStrategy
	{
		get
		{
			if (_memberAccessorStrategy == null)
			{
				_memberAccessorStrategy = new ReflectionMemberAccessor();
			}
			return _memberAccessorStrategy;
		}
	}

	private static Dictionary<Type, JsonConverter> GetDefaultSimpleConverters()
	{
		Dictionary<Type, JsonConverter> converters = new Dictionary<Type, JsonConverter>(21);
		foreach (JsonConverter converter in DefaultSimpleConverters)
		{
			converters.Add(converter.TypeToConvert, converter);
		}
		Debug.Assert(21 == converters.Count);
		return converters;
	}

	private static List<JsonConverter> GetDefaultConverters()
	{
		List<JsonConverter> converters = new List<JsonConverter>(2);
		converters.Add(new JsonConverterEnum());
		converters.Add(new JsonKeyValuePairConverter());
		Debug.Assert(2 == converters.Count);
		return converters;
	}

	internal JsonConverter DetermineConverterForProperty(Type parentClassType, Type runtimePropertyType, PropertyInfo propertyInfo)
	{
		JsonConverter converter = null;
		if (propertyInfo != null)
		{
			JsonConverterAttribute converterAttribute = (JsonConverterAttribute)GetAttributeThatCanHaveMultiple(parentClassType, typeof(JsonConverterAttribute), propertyInfo);
			if (converterAttribute != null)
			{
				converter = GetConverterFromAttribute(converterAttribute, runtimePropertyType, parentClassType, propertyInfo);
			}
		}
		if (converter == null)
		{
			converter = GetConverter(runtimePropertyType);
		}
		if (converter is JsonConverterFactory factory)
		{
			converter = factory.GetConverterInternal(runtimePropertyType, this);
		}
		return converter;
	}

	public JsonConverter GetConverter(Type typeToConvert)
	{
		if (_converters.TryGetValue(typeToConvert, out var converter))
		{
			return converter;
		}
		foreach (JsonConverter item in Converters)
		{
			if (item.CanConvert(typeToConvert))
			{
				converter = item;
				break;
			}
		}
		if (converter == null)
		{
			JsonConverterAttribute converterAttribute = (JsonConverterAttribute)GetAttributeThatCanHaveMultiple(typeToConvert, typeof(JsonConverterAttribute));
			if (converterAttribute != null)
			{
				converter = GetConverterFromAttribute(converterAttribute, typeToConvert, typeToConvert, null);
			}
		}
		if (converter == null)
		{
			if (s_defaultSimpleConverters.TryGetValue(typeToConvert, out var foundConverter))
			{
				converter = foundConverter;
			}
			else
			{
				foreach (JsonConverter item2 in s_defaultFactoryConverters)
				{
					if (item2.CanConvert(typeToConvert))
					{
						converter = item2;
						break;
					}
				}
			}
		}
		if (converter is JsonConverterFactory factory)
		{
			converter = factory.GetConverterInternal(typeToConvert, this);
			if (converter == null || converter.TypeToConvert == null)
			{
				throw new ArgumentNullException("typeToConvert");
			}
		}
		if (converter != null)
		{
			Type converterTypeToConvert = converter.TypeToConvert;
			if (!converterTypeToConvert.IsAssignableFrom(typeToConvert) && !typeToConvert.IsAssignableFrom(converterTypeToConvert))
			{
				ThrowHelper.ThrowInvalidOperationException_SerializationConverterNotCompatible(converter.GetType(), typeToConvert);
			}
		}
		if (_haveTypesBeenCreated)
		{
			_converters.TryAdd(typeToConvert, converter);
		}
		return converter;
	}

	internal bool HasConverter(Type typeToConvert)
	{
		return GetConverter(typeToConvert) != null;
	}

	private JsonConverter GetConverterFromAttribute(JsonConverterAttribute converterAttribute, Type typeToConvert, Type classTypeAttributeIsOn, PropertyInfo propertyInfo)
	{
		Type type = converterAttribute.ConverterType;
		JsonConverter converter;
		if (type == null)
		{
			converter = converterAttribute.CreateConverter(typeToConvert);
			if (converter == null)
			{
				ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(classTypeAttributeIsOn, propertyInfo, typeToConvert);
			}
		}
		else
		{
			ConstructorInfo ctor = type.GetConstructor(Type.EmptyTypes);
			if (!typeof(JsonConverter).IsAssignableFrom(type) || !ctor.IsPublic)
			{
				ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeInvalid(classTypeAttributeIsOn, propertyInfo);
			}
			converter = (JsonConverter)Activator.CreateInstance(type);
		}
		if (!converter.CanConvert(typeToConvert))
		{
			ThrowHelper.ThrowInvalidOperationException_SerializationConverterOnAttributeNotCompatible(classTypeAttributeIsOn, propertyInfo, typeToConvert);
		}
		return converter;
	}

	private static Attribute GetAttributeThatCanHaveMultiple(Type classType, Type attributeType, PropertyInfo propertyInfo)
	{
		object[] attributes = propertyInfo?.GetCustomAttributes(attributeType, inherit: false);
		return GetAttributeThatCanHaveMultiple(attributeType, classType, propertyInfo, attributes);
	}

	private static Attribute GetAttributeThatCanHaveMultiple(Type classType, Type attributeType)
	{
		object[] attributes = classType.GetCustomAttributes(attributeType, inherit: false);
		return GetAttributeThatCanHaveMultiple(attributeType, classType, null, attributes);
	}

	private static Attribute GetAttributeThatCanHaveMultiple(Type attributeType, Type classType, PropertyInfo propertyInfo, object[] attributes)
	{
		if (attributes.Length == 0)
		{
			return null;
		}
		if (attributes.Length == 1)
		{
			return (Attribute)attributes[0];
		}
		ThrowHelper.ThrowInvalidOperationException_SerializationDuplicateAttribute(attributeType, classType, propertyInfo);
		return null;
	}

	public JsonSerializerOptions()
	{
		Converters = new ConverterList(this);
	}

	internal JsonClassInfo GetOrAddClass(Type classType)
	{
		_haveTypesBeenCreated = true;
		if (!_classes.TryGetValue(classType, out var result))
		{
			return _classes.GetOrAdd(classType, new JsonClassInfo(classType, this));
		}
		return result;
	}

	internal JsonReaderOptions GetReaderOptions()
	{
		JsonReaderOptions result = default(JsonReaderOptions);
		result.AllowTrailingCommas = AllowTrailingCommas;
		result.CommentHandling = ReadCommentHandling;
		result.MaxDepth = MaxDepth;
		return result;
	}

	internal JsonWriterOptions GetWriterOptions()
	{
		JsonWriterOptions result = default(JsonWriterOptions);
		result.Encoder = Encoder;
		result.Indented = WriteIndented;
		return result;
	}

	internal JsonPropertyInfo GetJsonPropertyInfoFromClassInfo(Type objectType, JsonSerializerOptions options)
	{
		if (!_objectJsonProperties.TryGetValue(objectType, out var propertyInfo))
		{
			propertyInfo = JsonClassInfo.CreateProperty(objectType, objectType, objectType, null, typeof(object), null, options);
			_objectJsonProperties[objectType] = propertyInfo;
		}
		return propertyInfo;
	}

	internal bool CreateRangeDelegatesContainsKey(string key)
	{
		return s_createRangeDelegates.ContainsKey(key);
	}

	internal bool TryGetCreateRangeDelegate(string delegateKey, out ImmutableCollectionCreator createRangeDelegate)
	{
		return s_createRangeDelegates.TryGetValue(delegateKey, out createRangeDelegate) && createRangeDelegate != null;
	}

	internal bool TryAddCreateRangeDelegate(string key, ImmutableCollectionCreator createRangeDelegate)
	{
		return s_createRangeDelegates.TryAdd(key, createRangeDelegate);
	}

	internal void VerifyMutable()
	{
		Debug.Assert(this != s_defaultOptions);
		if (_haveTypesBeenCreated)
		{
			ThrowHelper.ThrowInvalidOperationException_SerializerOptionsImmutable();
		}
	}
}
