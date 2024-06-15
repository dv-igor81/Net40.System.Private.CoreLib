using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal sealed class DefaultIDictionaryConverter : JsonDictionaryConverter
{
	public override object CreateFromDictionary(ref ReadStack state, IDictionary sourceDictionary, JsonSerializerOptions options)
	{
		Type dictionaryType = state.Current.JsonPropertyInfo.RuntimePropertyType;
		Type elementType = state.Current.JsonPropertyInfo.ElementType;
		JsonPropertyInfo propertyInfo = options.GetJsonPropertyInfoFromClassInfo(elementType, options);
		return propertyInfo.CreateIDictionaryInstance(ref state, dictionaryType, sourceDictionary);
	}
}
