namespace System.Diagnostics.Tracing;

internal sealed class DecimalTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	public DecimalTypeInfo()
		: base(typeof(decimal))
	{
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		collector.AddScalar(name, System.Diagnostics.Tracing.Statics.MakeDataType(System.Diagnostics.Tracing.TraceLoggingDataType.Double, format));
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		collector.AddScalar((double)value.ScalarValue.AsDecimal);
	}
}
