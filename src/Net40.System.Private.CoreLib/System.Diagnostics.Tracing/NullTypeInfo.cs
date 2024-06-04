namespace System.Diagnostics.Tracing;

internal sealed class NullTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	public NullTypeInfo()
		: base(typeof(System.Diagnostics.Tracing.EmptyStruct))
	{
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		collector.AddGroup(name);
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
	}

	public override object? GetData(object? value)
	{
		return null;
	}
}
