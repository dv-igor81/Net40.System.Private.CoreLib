#define DEBUG
namespace System.Diagnostics.Tracing;

internal sealed class ArrayTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	private readonly System.Diagnostics.Tracing.TraceLoggingTypeInfo elementInfo;

	public ArrayTypeInfo(Type type, System.Diagnostics.Tracing.TraceLoggingTypeInfo elementInfo)
		: base(type)
	{
		this.elementInfo = elementInfo;
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		collector.BeginBufferedArray();
		elementInfo.WriteMetadata(collector, name, format);
		collector.EndBufferedArray();
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		int bookmark = collector.BeginBufferedArray();
		int count = 0;
		Array array = (Array)value.ReferenceValue;
		if (array != null)
		{
			count = array.Length;
			for (int i = 0; i < array.Length; i++)
			{
				elementInfo.WriteData(collector, elementInfo.PropertyValueFactory(array.GetValue(i)));
			}
		}
		collector.EndBufferedArray(bookmark, count);
	}

	public override object? GetData(object? value)
	{
		Debug.Assert(value != null, "null accepted only for some overrides");
		Array array = (Array)value;
		object[] serializedArray = new object[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			serializedArray[i] = elementInfo.GetData(array.GetValue(i));
		}
		return serializedArray;
	}
}
