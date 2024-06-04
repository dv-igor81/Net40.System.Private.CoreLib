namespace System.Diagnostics.Tracing;

internal sealed class ScalarTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	private Func<EventFieldFormat, System.Diagnostics.Tracing.TraceLoggingDataType, System.Diagnostics.Tracing.TraceLoggingDataType> formatFunc;

	private System.Diagnostics.Tracing.TraceLoggingDataType nativeFormat;

	private ScalarTypeInfo(Type type, Func<EventFieldFormat, System.Diagnostics.Tracing.TraceLoggingDataType, System.Diagnostics.Tracing.TraceLoggingDataType> formatFunc, System.Diagnostics.Tracing.TraceLoggingDataType nativeFormat)
		: base(type)
	{
		this.formatFunc = formatFunc;
		this.nativeFormat = nativeFormat;
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		collector.AddScalar(name, formatFunc(format, nativeFormat));
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		collector.AddScalar(value);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Boolean()
	{
		return new ScalarTypeInfo(typeof(bool), System.Diagnostics.Tracing.Statics.Format8, System.Diagnostics.Tracing.TraceLoggingDataType.Boolean8);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Byte()
	{
		return new ScalarTypeInfo(typeof(byte), System.Diagnostics.Tracing.Statics.Format8, System.Diagnostics.Tracing.TraceLoggingDataType.UInt8);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo SByte()
	{
		return new ScalarTypeInfo(typeof(sbyte), System.Diagnostics.Tracing.Statics.Format8, System.Diagnostics.Tracing.TraceLoggingDataType.Int8);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Char()
	{
		return new ScalarTypeInfo(typeof(char), System.Diagnostics.Tracing.Statics.Format16, System.Diagnostics.Tracing.TraceLoggingDataType.Char16);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Int16()
	{
		return new ScalarTypeInfo(typeof(short), System.Diagnostics.Tracing.Statics.Format16, System.Diagnostics.Tracing.TraceLoggingDataType.Int16);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo UInt16()
	{
		return new ScalarTypeInfo(typeof(ushort), System.Diagnostics.Tracing.Statics.Format16, System.Diagnostics.Tracing.TraceLoggingDataType.UInt16);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Int32()
	{
		return new ScalarTypeInfo(typeof(int), System.Diagnostics.Tracing.Statics.Format32, System.Diagnostics.Tracing.TraceLoggingDataType.Int32);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo UInt32()
	{
		return new ScalarTypeInfo(typeof(uint), System.Diagnostics.Tracing.Statics.Format32, System.Diagnostics.Tracing.TraceLoggingDataType.UInt32);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Int64()
	{
		return new ScalarTypeInfo(typeof(long), System.Diagnostics.Tracing.Statics.Format64, System.Diagnostics.Tracing.TraceLoggingDataType.Int64);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo UInt64()
	{
		return new ScalarTypeInfo(typeof(ulong), System.Diagnostics.Tracing.Statics.Format64, System.Diagnostics.Tracing.TraceLoggingDataType.UInt64);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo IntPtr()
	{
		return new ScalarTypeInfo(typeof(IntPtr), System.Diagnostics.Tracing.Statics.FormatPtr, System.Diagnostics.Tracing.Statics.IntPtrType);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo UIntPtr()
	{
		return new ScalarTypeInfo(typeof(UIntPtr), System.Diagnostics.Tracing.Statics.FormatPtr, System.Diagnostics.Tracing.Statics.UIntPtrType);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Single()
	{
		return new ScalarTypeInfo(typeof(float), System.Diagnostics.Tracing.Statics.Format32, System.Diagnostics.Tracing.TraceLoggingDataType.Float);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Double()
	{
		return new ScalarTypeInfo(typeof(double), System.Diagnostics.Tracing.Statics.Format64, System.Diagnostics.Tracing.TraceLoggingDataType.Double);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Guid()
	{
		return new ScalarTypeInfo(typeof(Guid), (EventFieldFormat f, System.Diagnostics.Tracing.TraceLoggingDataType t) => System.Diagnostics.Tracing.Statics.MakeDataType(System.Diagnostics.Tracing.TraceLoggingDataType.Guid, f), System.Diagnostics.Tracing.TraceLoggingDataType.Guid);
	}
}
