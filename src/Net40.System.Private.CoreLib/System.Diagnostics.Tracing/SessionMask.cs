#define DEBUG
namespace System.Diagnostics.Tracing;

internal struct SessionMask
{
	private uint m_mask;

	internal const int SHIFT_SESSION_TO_KEYWORD = 44;

	internal const uint MASK = 15u;

	internal const uint MAX = 4u;

	public static System.Diagnostics.Tracing.SessionMask All => new System.Diagnostics.Tracing.SessionMask(15u);

	public bool this[int perEventSourceSessionId]
	{
		get
		{
			Debug.Assert((long)perEventSourceSessionId < 4L);
			return (m_mask & (1 << perEventSourceSessionId)) != 0;
		}
		set
		{
			Debug.Assert((long)perEventSourceSessionId < 4L);
			if (value)
			{
				m_mask |= (uint)(1 << perEventSourceSessionId);
			}
			else
			{
				m_mask &= (uint)(~(1 << perEventSourceSessionId));
			}
		}
	}

	public SessionMask(System.Diagnostics.Tracing.SessionMask m)
	{
		m_mask = m.m_mask;
	}

	public SessionMask(uint mask = 0u)
	{
		m_mask = mask & 0xFu;
	}

	public bool IsEqualOrSupersetOf(System.Diagnostics.Tracing.SessionMask m)
	{
		return (m_mask | m.m_mask) == m_mask;
	}

	public static System.Diagnostics.Tracing.SessionMask FromId(int perEventSourceSessionId)
	{
		Debug.Assert((long)perEventSourceSessionId < 4L);
		return new System.Diagnostics.Tracing.SessionMask((uint)(1 << perEventSourceSessionId));
	}

	public ulong ToEventKeywords()
	{
		return (ulong)m_mask << 44;
	}

	public static System.Diagnostics.Tracing.SessionMask FromEventKeywords(ulong m)
	{
		return new System.Diagnostics.Tracing.SessionMask((uint)(m >> 44));
	}

	public static System.Diagnostics.Tracing.SessionMask operator |(System.Diagnostics.Tracing.SessionMask m1, System.Diagnostics.Tracing.SessionMask m2)
	{
		return new System.Diagnostics.Tracing.SessionMask(m1.m_mask | m2.m_mask);
	}

	public static System.Diagnostics.Tracing.SessionMask operator &(System.Diagnostics.Tracing.SessionMask m1, System.Diagnostics.Tracing.SessionMask m2)
	{
		return new System.Diagnostics.Tracing.SessionMask(m1.m_mask & m2.m_mask);
	}

	public static System.Diagnostics.Tracing.SessionMask operator ^(System.Diagnostics.Tracing.SessionMask m1, System.Diagnostics.Tracing.SessionMask m2)
	{
		return new System.Diagnostics.Tracing.SessionMask(m1.m_mask ^ m2.m_mask);
	}

	public static System.Diagnostics.Tracing.SessionMask operator ~(System.Diagnostics.Tracing.SessionMask m)
	{
		return new System.Diagnostics.Tracing.SessionMask(0xFu & ~m.m_mask);
	}

	public static explicit operator ulong(System.Diagnostics.Tracing.SessionMask m)
	{
		return m.m_mask;
	}

	public static explicit operator uint(System.Diagnostics.Tracing.SessionMask m)
	{
		return m.m_mask;
	}
}
