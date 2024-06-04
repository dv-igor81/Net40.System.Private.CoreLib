namespace System.Diagnostics.Tracing;

internal class TraceLoggingDataCollector
{
	internal static readonly System.Diagnostics.Tracing.TraceLoggingDataCollector Instance = new System.Diagnostics.Tracing.TraceLoggingDataCollector();

	private TraceLoggingDataCollector()
	{
	}

	public int BeginBufferedArray()
	{
		return System.Diagnostics.Tracing.DataCollector.ThreadInstance.BeginBufferedArray();
	}

	public void EndBufferedArray(int bookmark, int count)
	{
		System.Diagnostics.Tracing.DataCollector.ThreadInstance.EndBufferedArray(bookmark, count);
	}

	public System.Diagnostics.Tracing.TraceLoggingDataCollector AddGroup()
	{
		return this;
	}

	public unsafe void AddScalar(PropertyValue value)
	{
		PropertyValue.Scalar scalar = value.ScalarValue;
		System.Diagnostics.Tracing.DataCollector.ThreadInstance.AddScalar(&scalar, value.ScalarLength);
	}

	public unsafe void AddScalar(long value)
	{
		System.Diagnostics.Tracing.DataCollector.ThreadInstance.AddScalar(&value, 8);
	}

	public unsafe void AddScalar(double value)
	{
		System.Diagnostics.Tracing.DataCollector.ThreadInstance.AddScalar(&value, 8);
	}

	public unsafe void AddScalar(bool value)
	{
		System.Diagnostics.Tracing.DataCollector.ThreadInstance.AddScalar(&value, 1);
	}

	public void AddNullTerminatedString(string? value)
	{
		System.Diagnostics.Tracing.DataCollector.ThreadInstance.AddNullTerminatedString(value);
	}

	public void AddBinary(string? value)
	{
		System.Diagnostics.Tracing.DataCollector.ThreadInstance.AddBinary(value, (value != null) ? (value.Length * 2) : 0);
	}

	public void AddArray(PropertyValue value, int elementSize)
	{
		Array array = (Array)value.ReferenceValue;
		System.Diagnostics.Tracing.DataCollector.ThreadInstance.AddArray(array, array?.Length ?? 0, elementSize);
	}
}
