#define DEBUG
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Threading;

namespace System.Text.Json;

[DebuggerDisplay("ClassType.{ClassType}, {Type.Name}")]
internal sealed class JsonClassInfo
{
	public delegate object ConstructorDelegate();

	private const int PropertyNameKeyLength = 7;

	private const int PropertyNameCountCacheThreshold = 64;

	public volatile Dictionary<string, JsonPropertyInfo> PropertyCache;

	public ConcurrentDictionary<(JsonPropertyInfo, Type), JsonPropertyInfo> RuntimePropertyCache;

	public volatile JsonPropertyInfo[] PropertyCacheArray;

	private volatile PropertyRef[] _propertyRefsSorted;

	private JsonClassInfo _elementClassInfo;

	public const string ImmutableNamespaceName = "System.Collections.Immutable";

	private const string EnumerableGenericInterfaceTypeName = "System.Collections.Generic.IEnumerable`1";

	private const string EnumerableInterfaceTypeName = "System.Collections.IEnumerable";

	private const string ListInterfaceTypeName = "System.Collections.IList";

	private const string ListGenericInterfaceTypeName = "System.Collections.Generic.IList`1";

	private const string ListGenericTypeName = "System.Collections.Generic.List`1";

	private const string CollectionGenericInterfaceTypeName = "System.Collections.Generic.ICollection`1";

	private const string CollectionInterfaceTypeName = "System.Collections.ICollection";

	private const string ReadOnlyListGenericInterfaceTypeName = "System.Collections.Generic.IReadOnlyList`1";

	private const string ReadOnlyCollectionGenericInterfaceTypeName = "System.Collections.Generic.IReadOnlyCollection`1";

	public const string HashtableTypeName = "System.Collections.Hashtable";

	public const string SortedListTypeName = "System.Collections.SortedList";

	public const string StackTypeName = "System.Collections.Stack";

	public const string StackGenericTypeName = "System.Collections.Generic.Stack`1";

	public const string QueueTypeName = "System.Collections.Queue";

	public const string QueueGenericTypeName = "System.Collections.Generic.Queue`1";

	public const string SetGenericInterfaceTypeName = "System.Collections.Generic.ISet`1";

	public const string SortedSetGenericTypeName = "System.Collections.Generic.SortedSet`1";

	public const string HashSetGenericTypeName = "System.Collections.Generic.HashSet`1";

	public const string LinkedListGenericTypeName = "System.Collections.Generic.LinkedList`1";

	public const string DictionaryInterfaceTypeName = "System.Collections.IDictionary";

	public const string DictionaryGenericTypeName = "System.Collections.Generic.Dictionary`2";

	public const string DictionaryGenericInterfaceTypeName = "System.Collections.Generic.IDictionary`2";

	public const string ReadOnlyDictionaryGenericInterfaceTypeName = "System.Collections.Generic.IReadOnlyDictionary`2";

	public const string SortedDictionaryGenericTypeName = "System.Collections.Generic.SortedDictionary`2";

	public const string KeyValuePairGenericTypeName = "System.Collections.Generic.KeyValuePair`2";

	public const string ArrayListTypeName = "System.Collections.ArrayList";

	private static readonly Type[] s_genericInterfacesWithAddMethods = new Type[2]
	{
		typeof(IDictionary<, >),
		typeof(ICollection<>)
	};

	private static readonly Type[] s_nonGenericInterfacesWithAddMethods = new Type[2]
	{
		typeof(IDictionary),
		typeof(IList)
	};

	private static readonly Type[] s_genericInterfacesWithoutAddMethods = new Type[4]
	{
		typeof(IReadOnlyDictionary<, >),
		typeof(IReadOnlyCollection<>),
		typeof(IReadOnlyList<>),
		typeof(IEnumerable<>)
	};

	private static readonly HashSet<string> s_nativelySupportedGenericCollections = new HashSet<string>
	{
		"System.Collections.Generic.List`1", "System.Collections.Generic.IEnumerable`1", "System.Collections.Generic.IList`1", "System.Collections.Generic.ICollection`1", "System.Collections.Generic.IReadOnlyList`1", "System.Collections.Generic.IReadOnlyCollection`1", "System.Collections.Generic.ISet`1", "System.Collections.Generic.Stack`1", "System.Collections.Generic.Queue`1", "System.Collections.Generic.HashSet`1",
		"System.Collections.Generic.LinkedList`1", "System.Collections.Generic.SortedSet`1", "System.Collections.IDictionary", "System.Collections.Generic.Dictionary`2", "System.Collections.Generic.IDictionary`2", "System.Collections.Generic.IReadOnlyDictionary`2", "System.Collections.Generic.SortedDictionary`2", "System.Collections.Generic.KeyValuePair`2", "System.Collections.Immutable.ImmutableArray`1", "System.Collections.Immutable.ImmutableList`1",
		"System.Collections.Immutable.IImmutableList`1", "System.Collections.Immutable.ImmutableStack`1", "System.Collections.Immutable.IImmutableStack`1", "System.Collections.Immutable.ImmutableQueue`1", "System.Collections.Immutable.IImmutableQueue`1", "System.Collections.Immutable.ImmutableSortedSet", "System.Collections.Immutable.ImmutableSortedSet`1", "System.Collections.Immutable.ImmutableHashSet`1", "System.Collections.Immutable.IImmutableSet`1", "System.Collections.Immutable.ImmutableDictionary`2",
		"System.Collections.Immutable.IImmutableDictionary`2", "System.Collections.Immutable.ImmutableSortedDictionary`2"
	};

	private static readonly HashSet<string> s_nativelySupportedNonGenericCollections = new HashSet<string> { "System.Collections.IEnumerable", "System.Collections.ICollection", "System.Collections.IList", "System.Collections.IDictionary", "System.Collections.Stack", "System.Collections.Queue", "System.Collections.Hashtable", "System.Collections.ArrayList", "System.Collections.SortedList" };

	public ConstructorDelegate CreateObject { get; private set; }

	public ConstructorDelegate CreateConcreteDictionary { get; private set; }

	public ClassType ClassType { get; private set; }

	public JsonPropertyInfo DataExtensionProperty { get; private set; }

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

	public JsonSerializerOptions Options { get; private set; }

	public Type Type { get; private set; }

	public JsonPropertyInfo PolicyProperty { get; private set; }

	private void AddPolicyProperty(Type propertyType, JsonSerializerOptions options)
	{
		PolicyProperty = AddProperty(propertyType, null, typeof(object), options);
	}

	private JsonPropertyInfo AddProperty(Type propertyType, PropertyInfo propertyInfo, Type classType, JsonSerializerOptions options)
	{
		JsonConverter converter;
		Type implementedType = GetImplementedCollectionType(classType, propertyType, propertyInfo, out converter, options);
		JsonPropertyInfo jsonInfo = ((!(implementedType != propertyType)) ? CreateProperty(propertyType, propertyType, propertyType, propertyInfo, classType, converter, options) : CreateProperty(implementedType, implementedType, implementedType, propertyInfo, typeof(object), converter, options));
		if (IsNativelySupportedCollection(propertyType) && implementedType.IsInterface && jsonInfo.ClassType == ClassType.Dictionary)
		{
			JsonPropertyInfo elementPropertyInfo = options.GetJsonPropertyInfoFromClassInfo(jsonInfo.ElementType, options);
			Type newPropertyType = elementPropertyInfo.GetDictionaryConcreteType();
			jsonInfo = ((!(implementedType != newPropertyType)) ? CreateProperty(propertyType, implementedType, implementedType, propertyInfo, classType, converter, options) : CreateProperty(propertyType, newPropertyType, implementedType, propertyInfo, classType, converter, options));
		}
		else if (jsonInfo.ClassType == ClassType.Enumerable && !implementedType.IsArray && ((IsDeserializedByAssigningFromList(implementedType) && IsNativelySupportedCollection(propertyType)) || IsSetInterface(implementedType)))
		{
			JsonPropertyInfo elementPropertyInfo2 = options.GetJsonPropertyInfoFromClassInfo(jsonInfo.ElementType, options);
			Type newPropertyType2 = elementPropertyInfo2.GetConcreteType(implementedType);
			jsonInfo = ((!(implementedType != newPropertyType2) || !implementedType.IsAssignableFrom(newPropertyType2)) ? CreateProperty(propertyType, implementedType, implementedType, propertyInfo, classType, converter, options) : CreateProperty(propertyType, newPropertyType2, implementedType, propertyInfo, classType, converter, options));
		}
		else if (propertyType != implementedType)
		{
			jsonInfo = CreateProperty(propertyType, implementedType, implementedType, propertyInfo, classType, converter, options);
		}
		return jsonInfo;
	}

	internal static JsonPropertyInfo CreateProperty(Type declaredPropertyType, Type runtimePropertyType, Type implementedPropertyType, PropertyInfo propertyInfo, Type parentClassType, JsonConverter converter, JsonSerializerOptions options)
	{
		if (JsonPropertyInfo.GetAttribute<JsonIgnoreAttribute>(propertyInfo) != null)
		{
			return JsonPropertyInfo.CreateIgnoredPropertyPlaceholder(propertyInfo, options);
		}
		if (converter == null)
		{
			converter = options.DetermineConverterForProperty(parentClassType, runtimePropertyType, propertyInfo);
		}
		Type propertyInfoClassType;
		if (runtimePropertyType.IsGenericType && runtimePropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
		{
			if (converter != null)
			{
				propertyInfoClassType = typeof(JsonPropertyInfoNotNullable<, , , >).MakeGenericType(parentClassType, declaredPropertyType, runtimePropertyType, runtimePropertyType);
			}
			else
			{
				Type typeToConvert2 = Nullable.GetUnderlyingType(runtimePropertyType);
				converter = options.DetermineConverterForProperty(parentClassType, typeToConvert2, propertyInfo);
				propertyInfoClassType = typeof(JsonPropertyInfoNullable<, >).MakeGenericType(parentClassType, typeToConvert2);
			}
		}
		else
		{
			Type typeToConvert = converter?.TypeToConvert;
			if (typeToConvert == null)
			{
				typeToConvert = ((!IsNativelySupportedCollection(declaredPropertyType)) ? declaredPropertyType : implementedPropertyType);
			}
			if (runtimePropertyType.IsAssignableFrom(typeToConvert))
			{
				propertyInfoClassType = typeof(JsonPropertyInfoNotNullable<, , , >).MakeGenericType(parentClassType, declaredPropertyType, runtimePropertyType, typeToConvert);
			}
			else
			{
				Debug.Assert(typeToConvert.IsAssignableFrom(runtimePropertyType));
				propertyInfoClassType = typeof(JsonPropertyInfoNotNullableContravariant<, , , >).MakeGenericType(parentClassType, declaredPropertyType, runtimePropertyType, typeToConvert);
			}
		}
		JsonPropertyInfo jsonPropertyInfo = (JsonPropertyInfo)Activator.CreateInstance(propertyInfoClassType, BindingFlags.Instance | BindingFlags.Public, null, null, null);
		Type collectionElementType = null;
		if (converter == null)
		{
			switch (GetClassType(runtimePropertyType, options))
			{
			case ClassType.Unknown:
			case ClassType.Enumerable:
			case ClassType.Dictionary:
			case ClassType.IDictionaryConstructible:
				collectionElementType = GetElementType(runtimePropertyType, parentClassType, propertyInfo, options);
				break;
			}
		}
		jsonPropertyInfo.Initialize(parentClassType, declaredPropertyType, runtimePropertyType, implementedPropertyType, propertyInfo, collectionElementType, converter, options);
		return jsonPropertyInfo;
	}

	internal JsonPropertyInfo CreateRootObject(JsonSerializerOptions options)
	{
		return CreateProperty(Type, Type, Type, null, Type, null, options);
	}

	internal JsonPropertyInfo GetOrAddPolymorphicProperty(JsonPropertyInfo property, Type runtimePropertyType, JsonSerializerOptions options)
	{
		ConcurrentDictionary<(JsonPropertyInfo, Type), JsonPropertyInfo> cache = LazyInitializer.EnsureInitialized(ref RuntimePropertyCache, () => new ConcurrentDictionary<(JsonPropertyInfo, Type), JsonPropertyInfo>());
		return cache.GetOrAdd((property, runtimePropertyType), ((JsonPropertyInfo, Type) key) => CreateRuntimeProperty(key, (options: options, classType: Type)));
		static JsonPropertyInfo CreateRuntimeProperty((JsonPropertyInfo property, Type runtimePropertyType) key, (JsonSerializerOptions options, Type classType) arg)
		{
			JsonPropertyInfo runtimeProperty = CreateProperty(key.property.DeclaredPropertyType, key.runtimePropertyType, key.property.ImplementedPropertyType, key.property.PropertyInfo, arg.classType, null, arg.options);
			key.property.CopyRuntimeSettingsTo(runtimeProperty);
			return runtimeProperty;
		}
	}

	public void UpdateSortedPropertyCache(ref ReadStackFrame frame)
	{
		Debug.Assert(frame.PropertyRefCache != null);
		List<PropertyRef> listToAppend = frame.PropertyRefCache;
		if (_propertyRefsSorted != null)
		{
			List<PropertyRef> replacementList = new List<PropertyRef>(_propertyRefsSorted);
			Debug.Assert(replacementList.Count <= 64);
			while (replacementList.Count + listToAppend.Count > 64)
			{
				listToAppend.RemoveAt(listToAppend.Count - 1);
			}
			replacementList.AddRange(listToAppend);
			_propertyRefsSorted = replacementList.ToArray();
		}
		else
		{
			_propertyRefsSorted = listToAppend.ToArray();
		}
		frame.PropertyRefCache = null;
	}

	public JsonClassInfo(Type type, JsonSerializerOptions options)
	{
		Type = type;
		Options = options;
		ClassType = GetClassType(type, options);
		CreateObject = options.MemberAccessorStrategy.CreateConstructor(type);
		switch (ClassType)
		{
		case ClassType.Object:
		{
			PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Dictionary<string, JsonPropertyInfo> cache = CreatePropertyCache(properties.Length);
			PropertyInfo[] array = properties;
			foreach (PropertyInfo propertyInfo in array)
			{
				if (propertyInfo.GetIndexParameters().Length != 0)
				{
					continue;
				}
				MethodInfo method = propertyInfo.GetMethod();
				if ((object)method == null || !method.IsPublic)
				{
					MethodInfo methodInfo = propertyInfo.SetMethod();
					if ((object)methodInfo == null || !methodInfo.IsPublic)
					{
						continue;
					}
				}
				JsonPropertyInfo jsonPropertyInfo = AddProperty(propertyInfo.PropertyType, propertyInfo, type, options);
				Debug.Assert(jsonPropertyInfo != null);
				if (!JsonHelpers.TryAdd(cache, jsonPropertyInfo.NameAsString, jsonPropertyInfo))
				{
					JsonPropertyInfo other = cache[jsonPropertyInfo.NameAsString];
					if (!other.ShouldDeserialize && !other.ShouldSerialize)
					{
						cache[jsonPropertyInfo.NameAsString] = jsonPropertyInfo;
					}
					else if (jsonPropertyInfo.ShouldDeserialize || jsonPropertyInfo.ShouldSerialize)
					{
						ThrowHelper.ThrowInvalidOperationException_SerializerPropertyNameConflict(this, jsonPropertyInfo);
					}
				}
			}
			JsonPropertyInfo[] cacheArray;
			if (DetermineExtensionDataProperty(cache))
			{
				cache.Remove(DataExtensionProperty.NameAsString);
				cacheArray = new JsonPropertyInfo[cache.Count + 1];
				cacheArray[cache.Count] = DataExtensionProperty;
			}
			else
			{
				cacheArray = new JsonPropertyInfo[cache.Count];
			}
			PropertyCache = cache;
			cache.Values.CopyTo(cacheArray, 0);
			PropertyCacheArray = cacheArray;
			break;
		}
		case ClassType.Enumerable:
		case ClassType.Dictionary:
		{
			AddPolicyProperty(type, options);
			Type objectType = ((!IsNativelySupportedCollection(type)) ? PolicyProperty.DeclaredPropertyType : PolicyProperty.RuntimePropertyType);
			CreateObject = options.MemberAccessorStrategy.CreateConstructor(objectType);
			ElementType = GetElementType(type, null, null, options);
			break;
		}
		case ClassType.IDictionaryConstructible:
			AddPolicyProperty(type, options);
			ElementType = GetElementType(type, null, null, options);
			CreateConcreteDictionary = options.MemberAccessorStrategy.CreateConstructor(typeof(Dictionary<, >).MakeGenericType(typeof(string), ElementType));
			CreateObject = options.MemberAccessorStrategy.CreateConstructor(PolicyProperty.DeclaredPropertyType);
			break;
		case ClassType.Value:
			AddPolicyProperty(type, options);
			break;
		case ClassType.Unknown:
			AddPolicyProperty(type, options);
			PropertyCache = new Dictionary<string, JsonPropertyInfo>();
			PropertyCacheArray = ArrayEx.Empty<JsonPropertyInfo>();
			break;
		default:
			Debug.Fail($"Unexpected class type: {ClassType}");
			break;
		}
	}

	private bool DetermineExtensionDataProperty(Dictionary<string, JsonPropertyInfo> cache)
	{
		JsonPropertyInfo jsonPropertyInfo = GetPropertyWithUniqueAttribute(typeof(JsonExtensionDataAttribute), cache);
		if (jsonPropertyInfo != null)
		{
			Type declaredPropertyType = jsonPropertyInfo.DeclaredPropertyType;
			if (!typeof(IDictionary<string, JsonElement>).IsAssignableFrom(declaredPropertyType) && !typeof(IDictionary<string, object>).IsAssignableFrom(declaredPropertyType))
			{
				ThrowHelper.ThrowInvalidOperationException_SerializationDataExtensionPropertyInvalid(this, jsonPropertyInfo);
			}
			DataExtensionProperty = jsonPropertyInfo;
			return true;
		}
		return false;
	}

	private JsonPropertyInfo GetPropertyWithUniqueAttribute(Type attributeType, Dictionary<string, JsonPropertyInfo> cache)
	{
		JsonPropertyInfo property = null;
		foreach (JsonPropertyInfo jsonPropertyInfo in cache.Values)
		{
			Attribute attribute = CustomAttributeExtensions.GetCustomAttribute(jsonPropertyInfo.PropertyInfo, attributeType);
			if (attribute != null)
			{
				if (property != null)
				{
					ThrowHelper.ThrowInvalidOperationException_SerializationDuplicateTypeAttribute(Type, attributeType);
				}
				property = jsonPropertyInfo;
			}
		}
		return property;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public JsonPropertyInfo GetProperty(ReadOnlySpan<byte> propertyName, ref ReadStackFrame frame)
	{
		JsonPropertyInfo info = null;
		PropertyRef[] localPropertyRefsSorted = _propertyRefsSorted;
		ulong key = GetKey(propertyName);
		if (localPropertyRefsSorted != null)
		{
			int propertyIndex = frame.PropertyIndex;
			int count = localPropertyRefsSorted.Length;
			int iForward = Math.Min(propertyIndex, count);
			int iBackward = iForward - 1;
			while (true)
			{
				if (iForward < count)
				{
					PropertyRef propertyRef = localPropertyRefsSorted[iForward];
					if (TryIsPropertyRefEqual(in propertyRef, propertyName, key, ref info))
					{
						return info;
					}
					iForward++;
					if (iBackward >= 0)
					{
						propertyRef = localPropertyRefsSorted[iBackward];
						if (TryIsPropertyRefEqual(in propertyRef, propertyName, key, ref info))
						{
							return info;
						}
						iBackward--;
					}
				}
				else
				{
					if (iBackward < 0)
					{
						break;
					}
					PropertyRef propertyRef2 = localPropertyRefsSorted[iBackward];
					if (TryIsPropertyRefEqual(in propertyRef2, propertyName, key, ref info))
					{
						return info;
					}
					iBackward--;
				}
			}
		}
		string stringPropertyName = JsonHelpers.Utf8GetString(propertyName);
		Debug.Assert(PropertyCache != null);
		if (!PropertyCache.TryGetValue(stringPropertyName, out info))
		{
			info = JsonPropertyInfo.s_missingProperty;
		}
		Debug.Assert(info != null);
		Debug.Assert(info == JsonPropertyInfo.s_missingProperty || key == info.PropertyNameKey || Options.PropertyNameCaseInsensitive);
		int cacheCount = 0;
		if (localPropertyRefsSorted != null)
		{
			cacheCount = localPropertyRefsSorted.Length;
		}
		if (cacheCount < 64)
		{
			if (frame.PropertyRefCache != null)
			{
				cacheCount += frame.PropertyRefCache.Count;
			}
			if (cacheCount < 64)
			{
				if (frame.PropertyRefCache == null)
				{
					frame.PropertyRefCache = new List<PropertyRef>();
				}
				PropertyRef propertyRef3 = new PropertyRef(key, info);
				frame.PropertyRefCache.Add(propertyRef3);
			}
		}
		return info;
	}

	private Dictionary<string, JsonPropertyInfo> CreatePropertyCache(int capacity)
	{
		StringComparer comparer = ((!Options.PropertyNameCaseInsensitive) ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
		return new Dictionary<string, JsonPropertyInfo>(capacity, comparer);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool TryIsPropertyRefEqual(in PropertyRef propertyRef, ReadOnlySpan<byte> propertyName, ulong key, ref JsonPropertyInfo info)
	{
		if (key == propertyRef.Key && (propertyName.Length <= 7 || propertyName.SequenceEqual(propertyRef.Info.Name)))
		{
			info = propertyRef.Info;
			return true;
		}
		return false;
	}

	public static ulong GetKey(ReadOnlySpan<byte> propertyName)
	{
		int length = propertyName.Length;
		ulong key;
		if (length > 7)
		{
			key = MemoryMarshal.Read<ulong>(propertyName);
			key |= 0xFF00000000000000uL;
		}
		else if (length > 3)
		{
			key = MemoryMarshal.Read<uint>(propertyName);
			key = length switch
			{
				7 => key | (((ulong)propertyName[6] << 48) | ((ulong)propertyName[5] << 40) | ((ulong)propertyName[4] << 32) | 0x700000000000000uL), 
				6 => key | (((ulong)propertyName[5] << 40) | ((ulong)propertyName[4] << 32) | 0x600000000000000uL), 
				5 => key | (((ulong)propertyName[4] << 32) | 0x500000000000000uL), 
				_ => key | 0x400000000000000uL, 
			};
		}
		else if (length <= 1)
		{
			key = ((length != 1) ? 0 : (propertyName[0] | 0x100000000000000uL));
		}
		else
		{
			key = MemoryMarshal.Read<ushort>(propertyName);
			key = ((length != 3) ? (key | 0x200000000000000uL) : (key | (((ulong)propertyName[2] << 16) | 0x300000000000000uL)));
		}
		Debug.Assert((length < 1 || propertyName[0] == (key & 0xFF)) && (length < 2 || propertyName[1] == (key & 0xFF00) >> 8) && (length < 3 || propertyName[2] == (key & 0xFF0000) >> 16) && (length < 4 || propertyName[3] == (key & 0xFF000000u) >> 24) && (length < 5 || propertyName[4] == (key & 0xFF00000000L) >> 32) && (length < 6 || propertyName[5] == (key & 0xFF0000000000L) >> 40) && (length < 7 || propertyName[6] == (key & 0xFF000000000000L) >> 48));
		return key;
	}

	public static Type GetElementType(Type propertyType, Type parentType, MemberInfo memberInfo, JsonSerializerOptions options)
	{
		JsonConverter converter;
		Type implementedType = GetImplementedCollectionType(parentType, propertyType, null, out converter, options);
		if (!typeof(IEnumerable).IsAssignableFrom(implementedType))
		{
			return null;
		}
		Type elementType = implementedType.GetElementType();
		if (elementType != null)
		{
			return elementType;
		}
		if (implementedType.IsGenericType)
		{
			Type[] args = implementedType.GetGenericArguments();
			ClassType classType = GetClassType(implementedType, options);
			if ((classType == ClassType.Dictionary || classType == ClassType.IDictionaryConstructible) && args.Length >= 2 && args[0].UnderlyingSystemType == typeof(string))
			{
				return args[1];
			}
			if (classType == ClassType.Enumerable && args.Length >= 1)
			{
				return args[0];
			}
		}
		if (implementedType.IsAssignableFrom(typeof(IList)) || implementedType.IsAssignableFrom(typeof(IDictionary)) || IsDeserializedByConstructingWithIList(implementedType) || IsDeserializedByConstructingWithIDictionary(implementedType))
		{
			return typeof(object);
		}
		throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(propertyType, parentType, memberInfo);
	}

	public static ClassType GetClassType(Type type, JsonSerializerOptions options)
	{
		Debug.Assert(type != null);
		JsonConverter converter;
		Type implementedType = GetImplementedCollectionType(typeof(object), type, null, out converter, options);
		if (implementedType.IsGenericType && implementedType.GetGenericTypeDefinition() == typeof(Nullable<>))
		{
			implementedType = Nullable.GetUnderlyingType(implementedType);
		}
		if (implementedType == typeof(object))
		{
			return ClassType.Unknown;
		}
		if (options.HasConverter(implementedType))
		{
			return ClassType.Value;
		}
		if (DefaultImmutableDictionaryConverter.IsImmutableDictionary(implementedType) || IsDeserializedByConstructingWithIDictionary(implementedType))
		{
			return ClassType.IDictionaryConstructible;
		}
		if (typeof(IDictionary).IsAssignableFrom(implementedType) || IsDictionaryClassType(implementedType))
		{
			if (type != implementedType && !IsNativelySupportedCollection(type))
			{
				return ClassType.IDictionaryConstructible;
			}
			return ClassType.Dictionary;
		}
		if (typeof(IEnumerable).IsAssignableFrom(implementedType))
		{
			return ClassType.Enumerable;
		}
		return ClassType.Object;
	}

	public static bool IsDictionaryClassType(Type type)
	{
		return type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(IDictionary<, >) || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<, >));
	}

	public static Type GetImplementedCollectionType(Type parentClassType, Type queryType, PropertyInfo propertyInfo, out JsonConverter converter, JsonSerializerOptions options)
	{
		Debug.Assert(queryType != null);
		if (!typeof(IEnumerable).IsAssignableFrom(queryType) || queryType == typeof(string) || queryType.IsInterface || queryType.IsArray || IsNativelySupportedCollection(queryType))
		{
			converter = null;
			return queryType;
		}
		converter = options.DetermineConverterForProperty(parentClassType, queryType, propertyInfo);
		if (converter != null)
		{
			return queryType;
		}
		Type baseType = IntrospectionExtensions.GetTypeInfo(queryType).BaseType;
		if (IsNativelySupportedCollection(baseType))
		{
			return baseType;
		}
		Type[] array = s_genericInterfacesWithAddMethods;
		foreach (Type candidate in array)
		{
			Type derivedGeneric = ExtractGenericInterface(queryType, candidate);
			if (derivedGeneric != null)
			{
				return derivedGeneric;
			}
		}
		Type[] array2 = s_nonGenericInterfacesWithAddMethods;
		foreach (Type candidate3 in array2)
		{
			if (candidate3.IsAssignableFrom(queryType))
			{
				return candidate3;
			}
		}
		Type[] array3 = s_genericInterfacesWithoutAddMethods;
		foreach (Type candidate2 in array3)
		{
			Type derivedGeneric2 = ExtractGenericInterface(queryType, candidate2);
			if (derivedGeneric2 != null)
			{
				return derivedGeneric2;
			}
		}
		return typeof(IEnumerable);
	}

	public static bool IsDeserializedByAssigningFromList(Type type)
	{
		if (type.IsGenericType)
		{
			switch (type.GetGenericTypeDefinition().FullName)
			{
			case "System.Collections.Generic.IEnumerable`1":
			case "System.Collections.Generic.IList`1":
			case "System.Collections.Generic.ICollection`1":
			case "System.Collections.Generic.IReadOnlyList`1":
			case "System.Collections.Generic.IReadOnlyCollection`1":
				return true;
			default:
				return false;
			}
		}
		switch (type.FullName)
		{
		case "System.Collections.IEnumerable":
		case "System.Collections.IList":
		case "System.Collections.ICollection":
			return true;
		default:
			return false;
		}
	}

	public static bool IsSetInterface(Type type)
	{
		return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ISet<>);
	}

	public static bool HasConstructorThatTakesGenericIEnumerable(Type type, JsonSerializerOptions options)
	{
		Type elementType = GetElementType(type, null, null, options);
		return type.GetConstructor(new Type[1] { typeof(List<>).MakeGenericType(elementType) }) != null;
	}

	public static bool IsDeserializedByConstructingWithIList(Type type)
	{
		switch (type.FullName)
		{
		case "System.Collections.Stack":
		case "System.Collections.Queue":
		case "System.Collections.ArrayList":
			return true;
		default:
			return false;
		}
	}

	public static bool IsDeserializedByConstructingWithIDictionary(Type type)
	{
		string fullName = type.FullName;
		string text = fullName;
		if (text == "System.Collections.Hashtable" || text == "System.Collections.SortedList")
		{
			return true;
		}
		return false;
	}

	public static bool IsNativelySupportedCollection(Type queryType)
	{
		Debug.Assert(queryType != null);
		if (queryType.IsGenericType)
		{
			return s_nativelySupportedGenericCollections.Contains(queryType.GetGenericTypeDefinition().FullName);
		}
		return s_nativelySupportedNonGenericCollections.Contains(queryType.FullName);
	}

	public static Type ExtractGenericInterface(Type queryType, Type interfaceType)
	{
		if (queryType == null)
		{
			throw new ArgumentNullException("queryType");
		}
		if (interfaceType == null)
		{
			throw new ArgumentNullException("interfaceType");
		}
		if (IsGenericInstantiation(queryType, interfaceType))
		{
			return queryType;
		}
		return GetGenericInstantiation(queryType, interfaceType);
	}

	private static bool IsGenericInstantiation(Type candidate, Type interfaceType)
	{
		return IntrospectionExtensions.GetTypeInfo(candidate).IsGenericType && candidate.GetGenericTypeDefinition() == interfaceType;
	}

	private static Type GetGenericInstantiation(Type queryType, Type interfaceType)
	{
		Type bestMatch = null;
		Type[] interfaces = queryType.GetInterfaces();
		Type[] array = interfaces;
		foreach (Type @interface in array)
		{
			if (IsGenericInstantiation(@interface, interfaceType))
			{
				if (bestMatch == null)
				{
					bestMatch = @interface;
				}
				else if (StringComparer.Ordinal.Compare(@interface.FullName, bestMatch.FullName) < 0)
				{
					bestMatch = @interface;
				}
			}
		}
		if (bestMatch != null)
		{
			return bestMatch;
		}
		Type baseType = (((object)queryType != null) ? IntrospectionExtensions.GetTypeInfo(queryType).BaseType : null);
		if (baseType == null)
		{
			return null;
		}
		return GetGenericInstantiation(baseType, interfaceType);
	}
}
