using System.Runtime.InteropServices;

namespace System.Diagnostics.Tracing;

[StructLayout(LayoutKind.Explicit, Size = 16)]
internal struct EventDescriptor
{
	[FieldOffset(0)]
	private int m_traceloggingId;

	[FieldOffset(0)]
	private ushort m_id;

	[FieldOffset(2)]
	private byte m_version;

	[FieldOffset(3)]
	private byte m_channel;

	[FieldOffset(4)]
	private byte m_level;

	[FieldOffset(5)]
	private byte m_opcode;

	[FieldOffset(6)]
	private ushort m_task;

	[FieldOffset(8)]
	private long m_keywords;

	public int EventId => m_id;

	public byte Version => m_version;

	public byte Channel => m_channel;

	public byte Level => m_level;

	public byte Opcode => m_opcode;

	public int Task => m_task;

	public long Keywords => m_keywords;

	internal int TraceLoggingId => m_traceloggingId;

	public EventDescriptor(int traceloggingId, byte level, byte opcode, long keywords)
	{
		m_id = 0;
		m_version = 0;
		m_channel = 0;
		m_traceloggingId = traceloggingId;
		m_level = level;
		m_opcode = opcode;
		m_task = 0;
		m_keywords = keywords;
	}

	public EventDescriptor(int id, byte version, byte channel, byte level, byte opcode, int task, long keywords)
	{
		if (id < 0)
		{
			throw new ArgumentOutOfRangeException("id", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (id > 65535)
		{
			throw new ArgumentOutOfRangeException("id", SR.Format(SR.ArgumentOutOfRange_NeedValidId, 1, ushort.MaxValue));
		}
		m_traceloggingId = 0;
		m_id = (ushort)id;
		m_version = version;
		m_channel = channel;
		m_level = level;
		m_opcode = opcode;
		m_keywords = keywords;
		if (task < 0)
		{
			throw new ArgumentOutOfRangeException("task", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (task > 65535)
		{
			throw new ArgumentOutOfRangeException("task", SR.Format(SR.ArgumentOutOfRange_NeedValidId, 1, ushort.MaxValue));
		}
		m_task = (ushort)task;
	}

	public override bool Equals(object? obj)
	{
		if (!(obj is System.Diagnostics.Tracing.EventDescriptor))
		{
			return false;
		}
		return Equals((System.Diagnostics.Tracing.EventDescriptor)obj);
	}

	public override int GetHashCode()
	{
		return m_id ^ m_version ^ m_channel ^ m_level ^ m_opcode ^ m_task ^ (int)m_keywords;
	}

	public bool Equals(System.Diagnostics.Tracing.EventDescriptor other)
	{
		if (m_id != other.m_id || m_version != other.m_version || m_channel != other.m_channel || m_level != other.m_level || m_opcode != other.m_opcode || m_task != other.m_task || m_keywords != other.m_keywords)
		{
			return false;
		}
		return true;
	}

	public static bool operator ==(System.Diagnostics.Tracing.EventDescriptor event1, System.Diagnostics.Tracing.EventDescriptor event2)
	{
		return event1.Equals(event2);
	}

	public static bool operator !=(System.Diagnostics.Tracing.EventDescriptor event1, System.Diagnostics.Tracing.EventDescriptor event2)
	{
		return !event1.Equals(event2);
	}
}
