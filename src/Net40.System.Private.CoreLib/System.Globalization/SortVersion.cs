using System.Runtime.CompilerServices;

namespace System.Globalization.Net40;

[Serializable]
[TypeForwardedFrom("mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
public sealed class SortVersion : IEquatable<SortVersion>
{
    private int m_NlsVersion;

    private Guid m_SortId;

    public int FullVersion => m_NlsVersion;

    public Guid SortId => m_SortId;

    public SortVersion(int fullVersion, Guid sortId)
    {
        m_SortId = sortId;
        m_NlsVersion = fullVersion;
    }

    internal SortVersion(int nlsVersion, int effectiveId, Guid customVersion)
    {
        m_NlsVersion = nlsVersion;
        if (customVersion == Guid.Empty)
        {
            byte h = (byte)(effectiveId >> 24);
            byte i = (byte)((effectiveId & 0xFF0000) >> 16);
            byte j = (byte)((effectiveId & 0xFF00) >> 8);
            byte k = (byte)((uint)effectiveId & 0xFFu);
            customVersion = new Guid(0, 0, 0, 0, 0, 0, 0, h, i, j, k);
        }
        m_SortId = customVersion;
    }

    public override bool Equals(object? obj)
    {
        if (obj is SortVersion other)
        {
            return Equals(other);
        }
        return false;
    }

    public bool Equals(SortVersion? other)
    {
        if (other == null)
        {
            return false;
        }
        if (m_NlsVersion == other.m_NlsVersion)
        {
            return m_SortId == other.m_SortId;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return (m_NlsVersion * 7) | m_SortId.GetHashCode();
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    public static bool operator ==(SortVersion? left, SortVersion? right)
    {
        if ((object)right == null)
        {
            if ((object)left != null)
            {
                return false;
            }
            return true;
        }
        return right.Equals(left);
    }

    public static bool operator !=(SortVersion? left, SortVersion? right)
    {
        return !(left == right);
    }
}
