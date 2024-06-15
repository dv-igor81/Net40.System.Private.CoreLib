using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal sealed class DefaultDerivedEnumerableConverter : JsonEnumerableConverter
{
	public override IEnumerable CreateFromList(ref ReadStack state, IList sourceList, JsonSerializerOptions options)
	{
		JsonPropertyInfo collectionPropertyInfo = state.Current.JsonPropertyInfo;
		JsonPropertyInfo elementPropertyInfo = options.GetJsonPropertyInfoFromClassInfo(collectionPropertyInfo.ElementType, options);
		return elementPropertyInfo.CreateDerivedEnumerableInstance(ref state, collectionPropertyInfo, sourceList);
	}
}
