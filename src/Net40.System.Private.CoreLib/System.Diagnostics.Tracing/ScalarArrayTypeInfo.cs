namespace System.Diagnostics.Tracing;

internal sealed class ScalarArrayTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	private Func<EventFieldFormat, System.Diagnostics.Tracing.TraceLoggingDataType, System.Diagnostics.Tracing.TraceLoggingDataType> formatFunc;

	private System.Diagnostics.Tracing.TraceLoggingDataType nativeFormat;

	private int elementSize;

	private ScalarArrayTypeInfo(Type type, Func<EventFieldFormat, System.Diagnostics.Tracing.TraceLoggingDataType, System.Diagnostics.Tracing.TraceLoggingDataType> formatFunc, System.Diagnostics.Tracing.TraceLoggingDataType nativeFormat, int elementSize)
		: base(type)
	{
		this.formatFunc = formatFunc;
		this.nativeFormat = nativeFormat;
		this.elementSize = elementSize;
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		collector.AddArray(name, formatFunc(format, nativeFormat));
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		collector.AddArray(value, elementSize);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Boolean()
	{
		return new ScalarArrayTypeInfo(typeof(bool[]), System.Diagnostics.Tracing.Statics.Format8, System.Diagnostics.Tracing.TraceLoggingDataType.Boolean8, 1);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Byte()
	{
		return new ScalarArrayTypeInfo(typeof(byte[]), System.Diagnostics.Tracing.Statics.Format8, System.Diagnostics.Tracing.TraceLoggingDataType.UInt8, 1);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo SByte()
	{
		return new ScalarArrayTypeInfo(typeof(sbyte[]), System.Diagnostics.Tracing.Statics.Format8, System.Diagnostics.Tracing.TraceLoggingDataType.Int8, 1);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Char()
	{
		return new ScalarArrayTypeInfo(typeof(char[]), System.Diagnostics.Tracing.Statics.Format16, System.Diagnostics.Tracing.TraceLoggingDataType.Char16, 2);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Int16()
	{
		return new ScalarArrayTypeInfo(typeof(short[]), System.Diagnostics.Tracing.Statics.Format16, System.Diagnostics.Tracing.TraceLoggingDataType.Int16, 2);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo UInt16()
	{
		return new ScalarArrayTypeInfo(typeof(ushort[]), System.Diagnostics.Tracing.Statics.Format16, System.Diagnostics.Tracing.TraceLoggingDataType.UInt16, 2);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Int32()
	{
		return new ScalarArrayTypeInfo(typeof(int[]), System.Diagnostics.Tracing.Statics.Format32, System.Diagnostics.Tracing.TraceLoggingDataType.Int32, 4);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo UInt32()
	{
		return new ScalarArrayTypeInfo(typeof(uint[]), System.Diagnostics.Tracing.Statics.Format32, System.Diagnostics.Tracing.TraceLoggingDataType.UInt32, 4);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Int64()
	{
		return new ScalarArrayTypeInfo(typeof(long[]), System.Diagnostics.Tracing.Statics.Format64, System.Diagnostics.Tracing.TraceLoggingDataType.Int64, 8);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo UInt64()
	{
		return new ScalarArrayTypeInfo(typeof(ulong[]), System.Diagnostics.Tracing.Statics.Format64, System.Diagnostics.Tracing.TraceLoggingDataType.UInt64, 8);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo IntPtr()
	{
		return new ScalarArrayTypeInfo(typeof(IntPtr[]), System.Diagnostics.Tracing.Statics.FormatPtr, System.Diagnostics.Tracing.Statics.IntPtrType, System.IntPtr.Size);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo UIntPtr()
	{
		return new ScalarArrayTypeInfo(typeof(UIntPtr[]), System.Diagnostics.Tracing.Statics.FormatPtr, System.Diagnostics.Tracing.Statics.UIntPtrType, System.IntPtr.Size);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Single()
	{
		return new ScalarArrayTypeInfo(typeof(float[]), System.Diagnostics.Tracing.Statics.Format32, System.Diagnostics.Tracing.TraceLoggingDataType.Float, 4);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo Double()
	{
		return new ScalarArrayTypeInfo(typeof(double[]), System.Diagnostics.Tracing.Statics.Format64, System.Diagnostics.Tracing.TraceLoggingDataType.Double, 8);
	}

	public unsafe static System.Diagnostics.Tracing.TraceLoggingTypeInfo Guid()
	{
		return new ScalarArrayTypeInfo(typeof(Guid), (EventFieldFormat f, System.Diagnostics.Tracing.TraceLoggingDataType t) => System.Diagnostics.Tracing.Statics.MakeDataType(System.Diagnostics.Tracing.TraceLoggingDataType.Guid, f), System.Diagnostics.Tracing.TraceLoggingDataType.Guid, sizeof(Guid));
	}
}
