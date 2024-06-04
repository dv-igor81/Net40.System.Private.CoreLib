namespace System.Diagnostics.Tracing;

internal sealed class TimeSpanTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	public TimeSpanTypeInfo()
		: base(typeof(TimeSpan))
	{
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		collector.AddScalar(name, System.Diagnostics.Tracing.Statics.MakeDataType(System.Diagnostics.Tracing.TraceLoggingDataType.Int64, format));
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		collector.AddScalar(value.ScalarValue.AsTimeSpan.Ticks);
	}
}
