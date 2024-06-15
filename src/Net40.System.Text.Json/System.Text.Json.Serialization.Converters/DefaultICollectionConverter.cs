using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal sealed class DefaultICollectionConverter : JsonEnumerableConverter
{
	public override IEnumerable CreateFromList(ref ReadStack state, IList sourceList, JsonSerializerOptions options)
	{
		Type enumerableType = state.Current.JsonPropertyInfo.RuntimePropertyType;
		Type elementType = state.Current.JsonPropertyInfo.ElementType;
		JsonPropertyInfo propertyInfo = options.GetJsonPropertyInfoFromClassInfo(elementType, options);
		return propertyInfo.CreateIEnumerableInstance(ref state, enumerableType, sourceList);
	}
}
