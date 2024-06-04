namespace System.Diagnostics.Tracing;

internal sealed class DateTimeTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	public DateTimeTypeInfo()
		: base(typeof(DateTime))
	{
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		collector.AddScalar(name, System.Diagnostics.Tracing.Statics.MakeDataType(System.Diagnostics.Tracing.TraceLoggingDataType.FileTime, format));
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		DateTime dateTime = value.ScalarValue.AsDateTime;
		long dateTimeTicks = 0L;
		if (dateTime.Ticks > 504911232000000000L)
		{
			dateTimeTicks = dateTime.ToFileTimeUtc();
		}
		collector.AddScalar(dateTimeTicks);
	}
}
