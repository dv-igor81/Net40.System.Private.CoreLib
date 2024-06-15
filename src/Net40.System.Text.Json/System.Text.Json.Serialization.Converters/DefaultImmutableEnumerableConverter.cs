using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal sealed class DefaultImmutableEnumerableConverter : JsonEnumerableConverter
{
	public const string ImmutableArrayTypeName = "System.Collections.Immutable.ImmutableArray";

	public const string ImmutableArrayGenericTypeName = "System.Collections.Immutable.ImmutableArray`1";

	private const string ImmutableListTypeName = "System.Collections.Immutable.ImmutableList";

	public const string ImmutableListGenericTypeName = "System.Collections.Immutable.ImmutableList`1";

	public const string ImmutableListGenericInterfaceTypeName = "System.Collections.Immutable.IImmutableList`1";

	private const string ImmutableStackTypeName = "System.Collections.Immutable.ImmutableStack";

	public const string ImmutableStackGenericTypeName = "System.Collections.Immutable.ImmutableStack`1";

	public const string ImmutableStackGenericInterfaceTypeName = "System.Collections.Immutable.IImmutableStack`1";

	private const string ImmutableQueueTypeName = "System.Collections.Immutable.ImmutableQueue";

	public const string ImmutableQueueGenericTypeName = "System.Collections.Immutable.ImmutableQueue`1";

	public const string ImmutableQueueGenericInterfaceTypeName = "System.Collections.Immutable.IImmutableQueue`1";

	public const string ImmutableSortedSetTypeName = "System.Collections.Immutable.ImmutableSortedSet";

	public const string ImmutableSortedSetGenericTypeName = "System.Collections.Immutable.ImmutableSortedSet`1";

	private const string ImmutableHashSetTypeName = "System.Collections.Immutable.ImmutableHashSet";

	public const string ImmutableHashSetGenericTypeName = "System.Collections.Immutable.ImmutableHashSet`1";

	public const string ImmutableSetGenericInterfaceTypeName = "System.Collections.Immutable.IImmutableSet`1";

	public static string GetDelegateKey(Type immutableCollectionType, Type elementType, out Type underlyingType, out string constructingTypeName)
	{
		underlyingType = immutableCollectionType.GetGenericTypeDefinition();
		switch (underlyingType.FullName)
		{
		case "System.Collections.Immutable.ImmutableArray`1":
			constructingTypeName = "System.Collections.Immutable.ImmutableArray";
			break;
		case "System.Collections.Immutable.IImmutableList`1":
		case "System.Collections.Immutable.ImmutableList`1":
			constructingTypeName = "System.Collections.Immutable.ImmutableList";
			break;
		case "System.Collections.Immutable.ImmutableStack`1":
		case "System.Collections.Immutable.IImmutableStack`1":
			constructingTypeName = "System.Collections.Immutable.ImmutableStack";
			break;
		case "System.Collections.Immutable.ImmutableQueue`1":
		case "System.Collections.Immutable.IImmutableQueue`1":
			constructingTypeName = "System.Collections.Immutable.ImmutableQueue";
			break;
		case "System.Collections.Immutable.ImmutableSortedSet`1":
			constructingTypeName = "System.Collections.Immutable.ImmutableSortedSet";
			break;
		case "System.Collections.Immutable.IImmutableSet`1":
		case "System.Collections.Immutable.ImmutableHashSet`1":
			constructingTypeName = "System.Collections.Immutable.ImmutableHashSet";
			break;
		case "System.Collections.Immutable.ImmutableDictionary`2":
		case "System.Collections.Immutable.IImmutableDictionary`2":
			constructingTypeName = "System.Collections.Immutable.ImmutableDictionary";
			break;
		case "System.Collections.Immutable.ImmutableSortedDictionary`2":
			constructingTypeName = "System.Collections.Immutable.ImmutableSortedDictionary";
			break;
		default:
			throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(immutableCollectionType, null, null);
		}
		return constructingTypeName + ":" + elementType.FullName;
	}

	public static void RegisterImmutableCollection(Type immutableCollectionType, Type elementType, JsonSerializerOptions options)
	{
		Type underlyingType;
		string constructingTypeName;
		string delegateKey = GetDelegateKey(immutableCollectionType, elementType, out underlyingType, out constructingTypeName);
		if (!options.CreateRangeDelegatesContainsKey(delegateKey))
		{
			Type constructingType = underlyingType.Assembly.GetType(constructingTypeName);
			ImmutableCollectionCreator createRangeDelegate = options.MemberAccessorStrategy.ImmutableCollectionCreateRange(constructingType, immutableCollectionType, elementType);
			options.TryAddCreateRangeDelegate(delegateKey, createRangeDelegate);
		}
	}

	public override IEnumerable CreateFromList(ref ReadStack state, IList sourceList, JsonSerializerOptions options)
	{
		Type immutableCollectionType = state.Current.JsonPropertyInfo.RuntimePropertyType;
		Type elementType = state.Current.GetElementType();
		Type underlyingType;
		string constructingTypeName;
		string delegateKey = GetDelegateKey(immutableCollectionType, elementType, out underlyingType, out constructingTypeName);
		JsonPropertyInfo propertyInfo = options.GetJsonPropertyInfoFromClassInfo(elementType, options);
		return propertyInfo.CreateImmutableCollectionInstance(ref state, immutableCollectionType, delegateKey, sourceList, options);
	}
}
