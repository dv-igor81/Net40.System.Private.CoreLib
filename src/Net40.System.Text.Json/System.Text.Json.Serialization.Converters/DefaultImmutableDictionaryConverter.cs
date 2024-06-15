using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal sealed class DefaultImmutableDictionaryConverter : JsonDictionaryConverter
{
	public const string ImmutableDictionaryTypeName = "System.Collections.Immutable.ImmutableDictionary";

	public const string ImmutableDictionaryGenericTypeName = "System.Collections.Immutable.ImmutableDictionary`2";

	public const string ImmutableDictionaryGenericInterfaceTypeName = "System.Collections.Immutable.IImmutableDictionary`2";

	public const string ImmutableSortedDictionaryTypeName = "System.Collections.Immutable.ImmutableSortedDictionary";

	public const string ImmutableSortedDictionaryGenericTypeName = "System.Collections.Immutable.ImmutableSortedDictionary`2";

	public static void RegisterImmutableDictionary(Type immutableCollectionType, Type elementType, JsonSerializerOptions options)
	{
		Type underlyingType;
		string constructingTypeName;
		string delegateKey = DefaultImmutableEnumerableConverter.GetDelegateKey(immutableCollectionType, elementType, out underlyingType, out constructingTypeName);
		if (!options.CreateRangeDelegatesContainsKey(delegateKey))
		{
			Type constructingType = underlyingType.Assembly.GetType(constructingTypeName);
			ImmutableCollectionCreator createRangeDelegate = options.MemberAccessorStrategy.ImmutableDictionaryCreateRange(constructingType, immutableCollectionType, elementType);
			options.TryAddCreateRangeDelegate(delegateKey, createRangeDelegate);
		}
	}

	public static bool IsImmutableDictionary(Type type)
	{
		if (!type.IsGenericType)
		{
			return false;
		}
		switch (type.GetGenericTypeDefinition().FullName)
		{
		case "System.Collections.Immutable.ImmutableDictionary`2":
		case "System.Collections.Immutable.IImmutableDictionary`2":
		case "System.Collections.Immutable.ImmutableSortedDictionary`2":
			return true;
		default:
			return false;
		}
	}

	public override object CreateFromDictionary(ref ReadStack state, IDictionary sourceDictionary, JsonSerializerOptions options)
	{
		Type immutableCollectionType = state.Current.JsonPropertyInfo.RuntimePropertyType;
		Type elementType = state.Current.GetElementType();
		Type underlyingType;
		string constructingTypeName;
		string delegateKey = DefaultImmutableEnumerableConverter.GetDelegateKey(immutableCollectionType, elementType, out underlyingType, out constructingTypeName);
		JsonPropertyInfo propertyInfo = options.GetJsonPropertyInfoFromClassInfo(elementType, options);
		return propertyInfo.CreateImmutableDictionaryInstance(ref state, immutableCollectionType, delegateKey, sourceDictionary, options);
	}
}
