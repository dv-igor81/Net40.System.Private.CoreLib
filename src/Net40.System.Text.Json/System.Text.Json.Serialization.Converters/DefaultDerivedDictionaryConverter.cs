using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal sealed class DefaultDerivedDictionaryConverter : JsonDictionaryConverter
{
	public override object CreateFromDictionary(ref ReadStack state, IDictionary sourceDictionary, JsonSerializerOptions options)
	{
		JsonPropertyInfo collectionPropertyInfo = state.Current.JsonPropertyInfo;
		JsonPropertyInfo elementPropertyInfo = options.GetJsonPropertyInfoFromClassInfo(collectionPropertyInfo.ElementType, options);
		return elementPropertyInfo.CreateDerivedDictionaryInstance(ref state, collectionPropertyInfo, sourceDictionary);
	}
}
