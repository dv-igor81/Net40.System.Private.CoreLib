#define DEBUG
using System.Text;

namespace System.Diagnostics.Tracing;

internal class FieldMetadata
{
	private readonly string name;

	private readonly int nameSize;

	private readonly EventFieldTags tags;

	private readonly byte[]? custom;

	private readonly ushort fixedCount;

	private byte inType;

	private byte outType;

	public FieldMetadata(string name, System.Diagnostics.Tracing.TraceLoggingDataType type, EventFieldTags tags, bool variableCount)
		: this(name, type, tags, (byte)(variableCount ? 64 : 0), 0)
	{
	}

	public FieldMetadata(string name, System.Diagnostics.Tracing.TraceLoggingDataType type, EventFieldTags tags, ushort fixedCount)
		: this(name, type, tags, 32, fixedCount)
	{
	}

	public FieldMetadata(string name, System.Diagnostics.Tracing.TraceLoggingDataType type, EventFieldTags tags, byte[]? custom)
		: this(name, type, tags, 96, checked((ushort)((custom != null) ? custom.Length : 0)), custom)
	{
	}

	private FieldMetadata(string name, System.Diagnostics.Tracing.TraceLoggingDataType dataType, EventFieldTags tags, byte countFlags, ushort fixedCount = 0, byte[]? custom = null)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name", "This usually means that the object passed to Write is of a type that does not support being used as the top-level object in an event, e.g. a primitive or built-in type.");
		}
		System.Diagnostics.Tracing.Statics.CheckName(name);
		int coreType = (int)(dataType & (System.Diagnostics.Tracing.TraceLoggingDataType)31);
		this.name = name;
		nameSize = Encoding.UTF8.GetByteCount(this.name) + 1;
		inType = (byte)(coreType | countFlags);
		outType = (byte)((uint)((int)dataType >> 8) & 0x7Fu);
		this.tags = tags;
		this.fixedCount = fixedCount;
		this.custom = custom;
		if (countFlags != 0)
		{
			if (coreType == 0)
			{
				throw new NotSupportedException("SR.EventSource_NotSupportedArrayOfNil");
			}
			if (coreType == 14)
			{
				throw new NotSupportedException("SR.EventSource_NotSupportedArrayOfBinary");
			}
			if (coreType == 1 || coreType == 2)
			{
				throw new NotSupportedException("SR.EventSource_NotSupportedArrayOfNullTerminatedString");
			}
		}
		if ((this.tags & (EventFieldTags)268435455) != 0)
		{
			outType |= 128;
		}
		if (outType != 0)
		{
			inType |= 128;
		}
	}

	public void IncrementStructFieldCount()
	{
		inType |= 128;
		outType++;
		if ((outType & 0x7F) == 0)
		{
			throw new NotSupportedException("SR.EventSource_TooManyFields");
		}
	}

	public void Encode(ref int pos, byte[]? metadata)
	{
		if (metadata != null)
		{
			Encoding.UTF8.GetBytes(name, 0, name.Length, metadata, pos);
		}
		pos += nameSize;
		if (metadata != null)
		{
			metadata[pos] = inType;
		}
		pos++;
		if ((inType & 0x80u) != 0)
		{
			if (metadata != null)
			{
				metadata[pos] = outType;
			}
			pos++;
			if ((outType & 0x80u) != 0)
			{
				System.Diagnostics.Tracing.Statics.EncodeTags((int)tags, ref pos, metadata);
			}
		}
		if ((inType & 0x20) == 0)
		{
			return;
		}
		if (metadata != null)
		{
			metadata[pos] = (byte)fixedCount;
			metadata[pos + 1] = (byte)(fixedCount >> 8);
		}
		pos += 2;
		if (96 == (inType & 0x60) && fixedCount != 0)
		{
			if (metadata != null)
			{
				Debug.Assert(custom != null);
				Buffer.BlockCopy(custom, 0, metadata, pos, fixedCount);
			}
			pos += fixedCount;
		}
	}
}
