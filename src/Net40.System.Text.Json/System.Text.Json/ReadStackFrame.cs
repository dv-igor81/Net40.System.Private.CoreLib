#define DEBUG
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Json;

[DebuggerDisplay("ClassType.{JsonClassInfo.ClassType}, {JsonClassInfo.Type.Name}")]
internal struct ReadStackFrame
{
	public object ReturnValue;

	public JsonClassInfo JsonClassInfo;

	public string KeyName;

	public byte[] JsonPropertyName;

	public JsonPropertyInfo JsonPropertyInfo;

	public IList TempEnumerableValues;

	public bool CollectionPropertyInitialized;

	public bool Drain;

	public IDictionary TempDictionaryValues;

	public int PropertyIndex;

	public List<PropertyRef> PropertyRefCache;

	public bool SkipProperty => Drain || (JsonPropertyInfo != null && !JsonPropertyInfo.IsPropertyPolicy && !JsonPropertyInfo.ShouldDeserialize);

	public bool IsProcessingCollectionObject()
	{
		return IsProcessingObject((ClassType)56);
	}

	public bool IsProcessingCollectionProperty()
	{
		return IsProcessingProperty((ClassType)56);
	}

	public bool IsProcessingCollection()
	{
		return IsProcessingObject((ClassType)56) || IsProcessingProperty((ClassType)56);
	}

	public bool IsProcessingDictionary()
	{
		return IsProcessingObject(ClassType.Dictionary) || IsProcessingProperty(ClassType.Dictionary);
	}

	public bool IsProcessingIDictionaryConstructible()
	{
		return IsProcessingObject(ClassType.IDictionaryConstructible) || IsProcessingProperty(ClassType.IDictionaryConstructible);
	}

	public bool IsProcessingDictionaryOrIDictionaryConstructibleObject()
	{
		return IsProcessingObject((ClassType)48);
	}

	public bool IsProcessingDictionaryOrIDictionaryConstructibleProperty()
	{
		return IsProcessingProperty((ClassType)48);
	}

	public bool IsProcessingDictionaryOrIDictionaryConstructible()
	{
		return IsProcessingObject((ClassType)48) || IsProcessingProperty((ClassType)48);
	}

	public bool IsProcessingEnumerable()
	{
		return IsProcessingObject(ClassType.Enumerable) || IsProcessingProperty(ClassType.Enumerable);
	}

	public bool IsProcessingObject(ClassType classTypes)
	{
		return (JsonClassInfo.ClassType & classTypes) != 0;
	}

	public bool IsProcessingProperty(ClassType classTypes)
	{
		return JsonPropertyInfo != null && !JsonPropertyInfo.IsPropertyPolicy && (JsonPropertyInfo.ClassType & classTypes) != 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool IsProcessingValue()
	{
		if (SkipProperty)
		{
			return false;
		}
		ClassType classType = (CollectionPropertyInitialized ? JsonPropertyInfo.ElementClassInfo.ClassType : ((JsonPropertyInfo != null) ? JsonPropertyInfo.ClassType : JsonClassInfo.ClassType));
		return (classType & (ClassType)5) != 0;
	}

	public void Initialize(Type type, JsonSerializerOptions options)
	{
		JsonClassInfo = options.GetOrAddClass(type);
		InitializeJsonPropertyInfo();
	}

	public void InitializeJsonPropertyInfo()
	{
		if (IsProcessingObject((ClassType)60))
		{
			JsonPropertyInfo = JsonClassInfo.PolicyProperty;
		}
	}

	public void Reset()
	{
		Drain = false;
		JsonClassInfo = null;
		PropertyRefCache = null;
		ReturnValue = null;
		EndObject();
	}

	public void EndObject()
	{
		PropertyIndex = 0;
		EndProperty();
	}

	public void EndProperty()
	{
		CollectionPropertyInitialized = false;
		JsonPropertyInfo = null;
		TempEnumerableValues = null;
		TempDictionaryValues = null;
		JsonPropertyName = null;
		KeyName = null;
	}

	public static object CreateEnumerableValue(ref Utf8JsonReader reader, ref ReadStack state)
	{
		JsonPropertyInfo jsonPropertyInfo = state.Current.JsonPropertyInfo;
		if (jsonPropertyInfo.EnumerableConverter != null)
		{
			JsonClassInfo elementClassInfo = jsonPropertyInfo.ElementClassInfo;
			IList converterList = ((elementClassInfo.ClassType != ClassType.Value) ? new List<object>() : elementClassInfo.PolicyProperty.CreateConverterList());
			state.Current.TempEnumerableValues = converterList;
			if (!jsonPropertyInfo.IsPropertyPolicy && !state.Current.JsonPropertyInfo.RuntimePropertyType.FullName.StartsWith("System.Collections.Immutable.ImmutableArray`1"))
			{
				jsonPropertyInfo.SetValueAsObject(state.Current.ReturnValue, null);
			}
			return null;
		}
		Type propertyType = jsonPropertyInfo.RuntimePropertyType;
		if (typeof(IList).IsAssignableFrom(propertyType))
		{
			JsonClassInfo collectionClassInfo = ((!(jsonPropertyInfo.DeclaredPropertyType == jsonPropertyInfo.ImplementedPropertyType)) ? jsonPropertyInfo.DeclaredTypeClassInfo : jsonPropertyInfo.RuntimeClassInfo);
			if (collectionClassInfo.CreateObject() is IList collection)
			{
				return collection;
			}
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(jsonPropertyInfo.DeclaredPropertyType);
			return null;
		}
		ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(propertyType);
		return null;
	}

	public Type GetElementType()
	{
		if (IsProcessingCollectionProperty())
		{
			return JsonPropertyInfo.ElementClassInfo.Type;
		}
		if (IsProcessingCollectionObject())
		{
			return JsonClassInfo.ElementClassInfo.Type;
		}
		return JsonPropertyInfo.RuntimePropertyType;
	}

	public static IEnumerable GetEnumerableValue(in ReadStackFrame current)
	{
		if (current.IsProcessingObject(ClassType.Enumerable) && current.ReturnValue != null)
		{
			return (IEnumerable)current.ReturnValue;
		}
		return current.TempEnumerableValues;
	}

	public void SetReturnValue(object value)
	{
		Debug.Assert(ReturnValue == null);
		ReturnValue = value;
	}
}
