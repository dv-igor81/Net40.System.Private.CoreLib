namespace System.Diagnostics.Tracing;

internal sealed class DateTimeOffsetTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	public DateTimeOffsetTypeInfo()
		: base(typeof(DateTimeOffset))
	{
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		System.Diagnostics.Tracing.TraceLoggingMetadataCollector group = collector.AddGroup(name);
		group.AddScalar("Ticks", System.Diagnostics.Tracing.Statics.MakeDataType(System.Diagnostics.Tracing.TraceLoggingDataType.FileTime, format));
		group.AddScalar("Offset", System.Diagnostics.Tracing.TraceLoggingDataType.Int64);
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		DateTimeOffset dateTimeOffset = value.ScalarValue.AsDateTimeOffset;
		long ticks = dateTimeOffset.Ticks;
		collector.AddScalar((ticks < 504911232000000000L) ? 0 : (ticks - 504911232000000000L));
		collector.AddScalar(dateTimeOffset.Offset.Ticks);
	}
}
