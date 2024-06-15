#define DEBUG
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;

namespace System.Text.Json;

internal abstract class JsonPropertyInfoCommon<TClass, TDeclaredProperty, TRuntimeProperty, TConverter> : JsonPropertyInfo
{
	public Func<object, TDeclaredProperty> Get { get; private set; }

	public Action<object, TDeclaredProperty> Set { get; private set; }

	public JsonConverter<TConverter> Converter { get; internal set; }

	public override JsonConverter ConverterBase
	{
		get
		{
			return Converter;
		}
		set
		{
			Debug.Assert(Converter == null);
			Debug.Assert(value is JsonConverter<TConverter>);
			Converter = (JsonConverter<TConverter>)value;
		}
	}

	public override void Initialize(Type parentClassType, Type declaredPropertyType, Type runtimePropertyType, Type implementedPropertyType, PropertyInfo propertyInfo, Type elementType, JsonConverter converter, JsonSerializerOptions options)
	{
		base.Initialize(parentClassType, declaredPropertyType, runtimePropertyType, implementedPropertyType, propertyInfo, elementType, converter, options);
		if (propertyInfo != null && declaredPropertyType == propertyInfo.PropertyType)
		{
			MethodInfo method = propertyInfo.GetMethod();
			if ((object)method != null && method.IsPublic)
			{
				base.HasGetter = true;
				Get = options.MemberAccessorStrategy.CreatePropertyGetter<TClass, TDeclaredProperty>(propertyInfo);
			}
			MethodInfo methodInfo = propertyInfo.SetMethod();
			if ((object)methodInfo != null && methodInfo.IsPublic)
			{
				base.HasSetter = true;
				Set = options.MemberAccessorStrategy.CreatePropertySetter<TClass, TDeclaredProperty>(propertyInfo);
			}
		}
		else
		{
			base.IsPropertyPolicy = true;
			base.HasGetter = true;
			base.HasSetter = true;
		}
		GetPolicies();
	}

	public override object GetValueAsObject(object obj)
	{
		if (base.IsPropertyPolicy)
		{
			return obj;
		}
		Debug.Assert(base.HasGetter);
		return Get(obj);
	}

	public override void SetValueAsObject(object obj, object value)
	{
		Debug.Assert(base.HasSetter);
		TDeclaredProperty typedValue = (TDeclaredProperty)value;
		if (typedValue != null || !base.IgnoreNullValues)
		{
			Set(obj, typedValue);
		}
	}

	public override IList CreateConverterList()
	{
		return new List<TDeclaredProperty>();
	}

	public override Type GetConcreteType(Type parentType)
	{
		if (JsonClassInfo.IsDeserializedByAssigningFromList(parentType))
		{
			return typeof(List<TDeclaredProperty>);
		}
		if (JsonClassInfo.IsSetInterface(parentType))
		{
			return typeof(HashSet<TDeclaredProperty>);
		}
		return parentType;
	}

	public override IEnumerable CreateDerivedEnumerableInstance(ref ReadStack state, JsonPropertyInfo collectionPropertyInfo, IList sourceList)
	{
		if (collectionPropertyInfo.DeclaredTypeClassInfo.CreateObject == null)
		{
			throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(collectionPropertyInfo.DeclaredPropertyType, collectionPropertyInfo.ParentClassType, collectionPropertyInfo.PropertyInfo);
		}
		object instance = collectionPropertyInfo.DeclaredTypeClassInfo.CreateObject();
		if (instance is IList { IsReadOnly: false } instanceOfIList)
		{
			foreach (object item in sourceList)
			{
				instanceOfIList.Add(item);
			}
			return instanceOfIList;
		}
		if (instance is ICollection<TDeclaredProperty> { IsReadOnly: false } instanceOfICollection)
		{
			foreach (TDeclaredProperty item2 in sourceList)
			{
				instanceOfICollection.Add(item2);
			}
			return instanceOfICollection;
		}
		if (instance is Stack<TDeclaredProperty> instanceOfStack)
		{
			foreach (TDeclaredProperty item3 in sourceList)
			{
				instanceOfStack.Push(item3);
			}
			return instanceOfStack;
		}
		if (instance is Queue<TDeclaredProperty> instanceOfQueue)
		{
			foreach (TDeclaredProperty item4 in sourceList)
			{
				instanceOfQueue.Enqueue(item4);
			}
			return instanceOfQueue;
		}
		throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(collectionPropertyInfo.DeclaredPropertyType, collectionPropertyInfo.ParentClassType, collectionPropertyInfo.PropertyInfo);
	}

	public override object CreateDerivedDictionaryInstance(ref ReadStack state, JsonPropertyInfo collectionPropertyInfo, IDictionary sourceDictionary)
	{
		if (collectionPropertyInfo.DeclaredTypeClassInfo.CreateObject == null)
		{
			throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(collectionPropertyInfo.DeclaredPropertyType, collectionPropertyInfo.ParentClassType, collectionPropertyInfo.PropertyInfo);
		}
		object instance = collectionPropertyInfo.DeclaredTypeClassInfo.CreateObject();
		if (instance is IDictionary { IsReadOnly: false } instanceOfIDictionary)
		{
			foreach (DictionaryEntry entry in sourceDictionary)
			{
				instanceOfIDictionary.Add((string)entry.Key, entry.Value);
			}
			return instanceOfIDictionary;
		}
		if (instance is IDictionary<string, TDeclaredProperty> { IsReadOnly: false } instanceOfGenericIDictionary)
		{
			foreach (DictionaryEntry entry2 in sourceDictionary)
			{
				instanceOfGenericIDictionary.Add((string)entry2.Key, (TDeclaredProperty)entry2.Value);
			}
			return instanceOfGenericIDictionary;
		}
		throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(collectionPropertyInfo.DeclaredPropertyType, collectionPropertyInfo.ParentClassType, collectionPropertyInfo.PropertyInfo);
	}

	public override IEnumerable CreateIEnumerableInstance(ref ReadStack state, Type parentType, IList sourceList)
	{
		if (parentType.IsGenericType)
		{
			Type genericTypeDefinition = parentType.GetGenericTypeDefinition();
			IEnumerable<TDeclaredProperty> items = CreateGenericTDeclaredPropertyIEnumerable(sourceList);
			if (genericTypeDefinition == typeof(Stack<>))
			{
				return new Stack<TDeclaredProperty>(items);
			}
			if (genericTypeDefinition == typeof(Queue<>))
			{
				return new Queue<TDeclaredProperty>(items);
			}
			if (genericTypeDefinition == typeof(HashSet<>))
			{
				return new HashSet<TDeclaredProperty>(items);
			}
			if (genericTypeDefinition == typeof(LinkedList<>))
			{
				return new LinkedList<TDeclaredProperty>(items);
			}
			if (genericTypeDefinition == typeof(SortedSet<>))
			{
				return new SortedSet<TDeclaredProperty>(items);
			}
			return (IEnumerable)Activator.CreateInstance(parentType, items);
		}
		if (parentType == typeof(ArrayList))
		{
			return new ArrayList(sourceList);
		}
		return (IEnumerable)Activator.CreateInstance(parentType, sourceList);
	}

	public override IDictionary CreateIDictionaryInstance(ref ReadStack state, Type parentType, IDictionary sourceDictionary)
	{
		if (parentType.FullName == "System.Collections.Hashtable")
		{
			return new Hashtable(sourceDictionary);
		}
		return (IDictionary)Activator.CreateInstance(parentType, sourceDictionary);
	}

	public override IEnumerable CreateImmutableCollectionInstance(ref ReadStack state, Type collectionType, string delegateKey, IList sourceList, JsonSerializerOptions options)
	{
		IEnumerable collection = null;
		if (!options.TryGetCreateRangeDelegate(delegateKey, out var creator) || !creator.CreateImmutableEnumerable(sourceList, out collection))
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(collectionType, state.JsonPath());
		}
		return collection;
	}

	public override IDictionary CreateImmutableDictionaryInstance(ref ReadStack state, Type collectionType, string delegateKey, IDictionary sourceDictionary, JsonSerializerOptions options)
	{
		IDictionary collection = null;
		if (!options.TryGetCreateRangeDelegate(delegateKey, out var creator) || !creator.CreateImmutableDictionary(sourceDictionary, out collection))
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(collectionType, state.JsonPath());
		}
		return collection;
	}

	private IEnumerable<TDeclaredProperty> CreateGenericTDeclaredPropertyIEnumerable(IList sourceList)
	{
		foreach (object item in sourceList)
		{
			yield return (TDeclaredProperty)item;
		}
	}
}
