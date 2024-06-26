using System.Collections;

namespace System.Text.Json.Serialization.Converters;

internal sealed class DefaultArrayConverter : JsonEnumerableConverter
{
	public override IEnumerable CreateFromList(ref ReadStack state, IList sourceList, JsonSerializerOptions options)
	{
		Type elementType = state.Current.GetElementType();
		Array array;
		if (sourceList.Count > 0 && sourceList[0] is Array probe)
		{
			array = Array.CreateInstance(probe.GetType(), sourceList.Count);
			int i = 0;
			foreach (IList child in sourceList)
			{
				if (child is Array childArray)
				{
					array.SetValue(childArray, i++);
				}
			}
		}
		else
		{
			array = Array.CreateInstance(elementType, sourceList.Count);
			sourceList.CopyTo(array, 0);
		}
		return array;
	}
}
