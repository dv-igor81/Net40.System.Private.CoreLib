using System.Collections.Generic;

namespace System.Diagnostics.Tracing;

internal class TraceLoggingMetadataCollector
{
	private class Impl
	{
		internal readonly List<System.Diagnostics.Tracing.FieldMetadata> fields = new List<System.Diagnostics.Tracing.FieldMetadata>();

		internal short scratchSize;

		internal sbyte dataCount;

		internal sbyte pinCount;

		private int bufferNesting;

		private bool scalar;

		public void AddScalar(int size)
		{
			checked
			{
				if (bufferNesting == 0)
				{
					if (!scalar)
					{
						dataCount++;
					}
					scalar = true;
					scratchSize = (short)(scratchSize + size);
				}
			}
		}

		public void AddNonscalar()
		{
			checked
			{
				if (bufferNesting == 0)
				{
					scalar = false;
					pinCount++;
					dataCount++;
				}
			}
		}

		public void BeginBuffered()
		{
			if (bufferNesting == 0)
			{
				AddNonscalar();
			}
			bufferNesting++;
		}

		public void EndBuffered()
		{
			bufferNesting--;
		}

		public int Encode(byte[]? metadata)
		{
			int size = 0;
			foreach (System.Diagnostics.Tracing.FieldMetadata field in fields)
			{
				field.Encode(ref size, metadata);
			}
			return size;
		}
	}

	private readonly Impl impl;

	private readonly System.Diagnostics.Tracing.FieldMetadata? currentGroup;

	private int bufferedArrayFieldCount = int.MinValue;

	internal EventFieldTags Tags { get; set; }

	internal int ScratchSize => impl.scratchSize;

	internal int DataCount => impl.dataCount;

	internal int PinCount => impl.pinCount;

	private bool BeginningBufferedArray => bufferedArrayFieldCount == 0;

	internal TraceLoggingMetadataCollector()
	{
		impl = new Impl();
	}

	private TraceLoggingMetadataCollector(System.Diagnostics.Tracing.TraceLoggingMetadataCollector other, System.Diagnostics.Tracing.FieldMetadata group)
	{
		impl = other.impl;
		currentGroup = group;
	}

	public System.Diagnostics.Tracing.TraceLoggingMetadataCollector AddGroup(string? name)
	{
		System.Diagnostics.Tracing.TraceLoggingMetadataCollector result = this;
		if (name != null || BeginningBufferedArray)
		{
			System.Diagnostics.Tracing.FieldMetadata newGroup = new System.Diagnostics.Tracing.FieldMetadata(name, System.Diagnostics.Tracing.TraceLoggingDataType.Struct, Tags, BeginningBufferedArray);
			AddField(newGroup);
			result = new System.Diagnostics.Tracing.TraceLoggingMetadataCollector(this, newGroup);
		}
		return result;
	}

	public void AddScalar(string name, System.Diagnostics.Tracing.TraceLoggingDataType type)
	{
		int size;
		switch (type & (System.Diagnostics.Tracing.TraceLoggingDataType)31)
		{
		case System.Diagnostics.Tracing.TraceLoggingDataType.Int8:
		case System.Diagnostics.Tracing.TraceLoggingDataType.UInt8:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Char8:
			size = 1;
			break;
		case System.Diagnostics.Tracing.TraceLoggingDataType.Int16:
		case System.Diagnostics.Tracing.TraceLoggingDataType.UInt16:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Char16:
			size = 2;
			break;
		case System.Diagnostics.Tracing.TraceLoggingDataType.Int32:
		case System.Diagnostics.Tracing.TraceLoggingDataType.UInt32:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Float:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Boolean32:
		case System.Diagnostics.Tracing.TraceLoggingDataType.HexInt32:
			size = 4;
			break;
		case System.Diagnostics.Tracing.TraceLoggingDataType.Int64:
		case System.Diagnostics.Tracing.TraceLoggingDataType.UInt64:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Double:
		case System.Diagnostics.Tracing.TraceLoggingDataType.FileTime:
		case System.Diagnostics.Tracing.TraceLoggingDataType.HexInt64:
			size = 8;
			break;
		case System.Diagnostics.Tracing.TraceLoggingDataType.Guid:
		case System.Diagnostics.Tracing.TraceLoggingDataType.SystemTime:
			size = 16;
			break;
		default:
			throw new ArgumentOutOfRangeException("type");
		}
		impl.AddScalar(size);
		AddField(new System.Diagnostics.Tracing.FieldMetadata(name, type, Tags, BeginningBufferedArray));
	}

	public void AddBinary(string name, System.Diagnostics.Tracing.TraceLoggingDataType type)
	{
		System.Diagnostics.Tracing.TraceLoggingDataType traceLoggingDataType = type & (System.Diagnostics.Tracing.TraceLoggingDataType)31;
		System.Diagnostics.Tracing.TraceLoggingDataType traceLoggingDataType2 = traceLoggingDataType;
		if (traceLoggingDataType2 != System.Diagnostics.Tracing.TraceLoggingDataType.Binary && (uint)(traceLoggingDataType2 - 22) > 1u)
		{
			throw new ArgumentOutOfRangeException("type");
		}
		impl.AddScalar(2);
		impl.AddNonscalar();
		AddField(new System.Diagnostics.Tracing.FieldMetadata(name, type, Tags, BeginningBufferedArray));
	}

	public void AddNullTerminatedString(string name, System.Diagnostics.Tracing.TraceLoggingDataType type)
	{
		System.Diagnostics.Tracing.TraceLoggingDataType traceLoggingDataType = type & (System.Diagnostics.Tracing.TraceLoggingDataType)31;
		System.Diagnostics.Tracing.TraceLoggingDataType traceLoggingDataType2 = traceLoggingDataType;
		if (traceLoggingDataType2 != System.Diagnostics.Tracing.TraceLoggingDataType.Utf16String)
		{
			throw new ArgumentOutOfRangeException("type");
		}
		impl.AddNonscalar();
		AddField(new System.Diagnostics.Tracing.FieldMetadata(name, type, Tags, BeginningBufferedArray));
	}

	public void AddArray(string name, System.Diagnostics.Tracing.TraceLoggingDataType type)
	{
		switch (type & (System.Diagnostics.Tracing.TraceLoggingDataType)31)
		{
		default:
			throw new ArgumentOutOfRangeException("type");
		case System.Diagnostics.Tracing.TraceLoggingDataType.Int8:
		case System.Diagnostics.Tracing.TraceLoggingDataType.UInt8:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Int16:
		case System.Diagnostics.Tracing.TraceLoggingDataType.UInt16:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Int32:
		case System.Diagnostics.Tracing.TraceLoggingDataType.UInt32:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Int64:
		case System.Diagnostics.Tracing.TraceLoggingDataType.UInt64:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Float:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Double:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Boolean32:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Guid:
		case System.Diagnostics.Tracing.TraceLoggingDataType.FileTime:
		case System.Diagnostics.Tracing.TraceLoggingDataType.HexInt32:
		case System.Diagnostics.Tracing.TraceLoggingDataType.HexInt64:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Char8:
		case System.Diagnostics.Tracing.TraceLoggingDataType.Char16:
			if (BeginningBufferedArray)
			{
				throw new NotSupportedException("SR.EventSource_NotSupportedNestedArraysEnums");
			}
			impl.AddScalar(2);
			impl.AddNonscalar();
			AddField(new System.Diagnostics.Tracing.FieldMetadata(name, type, Tags, variableCount: true));
			break;
		}
	}

	public void BeginBufferedArray()
	{
		if (bufferedArrayFieldCount >= 0)
		{
			throw new NotSupportedException("SR.EventSource_NotSupportedNestedArraysEnums");
		}
		bufferedArrayFieldCount = 0;
		impl.BeginBuffered();
	}

	public void EndBufferedArray()
	{
		if (bufferedArrayFieldCount != 1)
		{
			throw new InvalidOperationException("SR.EventSource_IncorrentlyAuthoredTypeInfo");
		}
		bufferedArrayFieldCount = int.MinValue;
		impl.EndBuffered();
	}

	public void AddCustom(string name, System.Diagnostics.Tracing.TraceLoggingDataType type, byte[] metadata)
	{
		if (BeginningBufferedArray)
		{
			throw new NotSupportedException("SR.EventSource_NotSupportedCustomSerializedData");
		}
		impl.AddScalar(2);
		impl.AddNonscalar();
		AddField(new System.Diagnostics.Tracing.FieldMetadata(name, type, Tags, metadata));
	}

	internal byte[] GetMetadata()
	{
		int size = impl.Encode(null);
		byte[] metadata = new byte[size];
		impl.Encode(metadata);
		return metadata;
	}

	private void AddField(System.Diagnostics.Tracing.FieldMetadata fieldMetadata)
	{
		Tags = EventFieldTags.None;
		bufferedArrayFieldCount++;
		impl.fields.Add(fieldMetadata);
		if (currentGroup != null)
		{
			currentGroup.IncrementStructFieldCount();
		}
	}
}
