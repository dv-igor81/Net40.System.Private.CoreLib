using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal abstract class JsonDictionaryConverter
{
	public abstract object CreateFromDictionary(ref ReadStack state, IDictionary sourceDictionary, JsonSerializerOptions options);
}
