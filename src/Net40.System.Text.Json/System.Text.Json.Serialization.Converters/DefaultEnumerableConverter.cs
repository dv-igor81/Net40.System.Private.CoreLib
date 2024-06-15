using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal sealed class DefaultEnumerableConverter : JsonEnumerableConverter
{
	public override IEnumerable CreateFromList(ref ReadStack state, IList sourceList, JsonSerializerOptions options)
	{
		Type elementType = state.Current.GetElementType();
		Type t = typeof(JsonEnumerableT<>).MakeGenericType(elementType);
		return (IEnumerable)Activator.CreateInstance(t, sourceList);
	}
}
