namespace System.Diagnostics.Tracing;

internal sealed class StringTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	public StringTypeInfo()
		: base(typeof(string))
	{
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		collector.AddNullTerminatedString(name, System.Diagnostics.Tracing.Statics.MakeDataType(System.Diagnostics.Tracing.TraceLoggingDataType.Utf16String, format));
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		collector.AddNullTerminatedString((string)value.ReferenceValue);
	}

	public override object GetData(object? value)
	{
		if (value == null)
		{
			return "";
		}
		return value;
	}
}
