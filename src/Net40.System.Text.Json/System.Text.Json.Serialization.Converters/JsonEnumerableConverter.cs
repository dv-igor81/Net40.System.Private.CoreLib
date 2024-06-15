using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal abstract class JsonEnumerableConverter
{
	public abstract IEnumerable CreateFromList(ref ReadStack state, IList sourceList, JsonSerializerOptions options);
}
