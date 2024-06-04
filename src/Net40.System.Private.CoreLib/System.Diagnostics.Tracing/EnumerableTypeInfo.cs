#define DEBUG
using System.Collections;
using System.Collections.Generic;

namespace System.Diagnostics.Tracing;

internal sealed class EnumerableTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	private readonly System.Diagnostics.Tracing.TraceLoggingTypeInfo elementInfo;

	public EnumerableTypeInfo(Type type, System.Diagnostics.Tracing.TraceLoggingTypeInfo elementInfo)
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
		IEnumerable enumerable = (IEnumerable)value.ReferenceValue;
		if (enumerable != null)
		{
			foreach (object element in enumerable)
			{
				elementInfo.WriteData(collector, elementInfo.PropertyValueFactory(element));
				count++;
			}
		}
		collector.EndBufferedArray(bookmark, count);
	}

	public override object? GetData(object? value)
	{
		Debug.Assert(value != null, "null accepted only for some overrides");
		IEnumerable iterType = (IEnumerable)value;
		List<object> serializedEnumerable = new List<object>();
		foreach (object element in iterType)
		{
			serializedEnumerable.Add(elementInfo.GetData(element));
		}
		return serializedEnumerable.ToArray();
	}
}
