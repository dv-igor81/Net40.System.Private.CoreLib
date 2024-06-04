using System.Collections.Generic;
using System.Threading;

namespace System.Diagnostics.Tracing;

internal sealed class NameInfo : System.Diagnostics.Tracing.ConcurrentSetItem<KeyValuePair<string, EventTags>, System.Diagnostics.Tracing.NameInfo>
{
	private static int lastIdentity = 184549376;

	internal readonly string name;

	internal readonly EventTags tags;

	internal readonly int identity;

	internal readonly byte[] nameMetadata;

	internal static void ReserveEventIDsBelow(int eventId)
	{
		int snapshot;
		int newIdentity;
		do
		{
			snapshot = lastIdentity;
			newIdentity = (lastIdentity & -16777216) + eventId;
			newIdentity = Math.Max(newIdentity, snapshot);
		}
		while (Interlocked.CompareExchange(ref lastIdentity, newIdentity, snapshot) != snapshot);
	}

	public NameInfo(string name, EventTags tags, int typeMetadataSize)
	{
		this.name = name;
		this.tags = tags & (EventTags)268435455;
		identity = Interlocked.Increment(ref lastIdentity);
		int tagsPos = 0;
		System.Diagnostics.Tracing.Statics.EncodeTags((int)this.tags, ref tagsPos, null);
		nameMetadata = System.Diagnostics.Tracing.Statics.MetadataForString(name, tagsPos, 0, typeMetadataSize);
		tagsPos = 2;
		System.Diagnostics.Tracing.Statics.EncodeTags((int)this.tags, ref tagsPos, nameMetadata);
	}

	public override int Compare(System.Diagnostics.Tracing.NameInfo other)
	{
		return Compare(other.name, other.tags);
	}

	public override int Compare(KeyValuePair<string, EventTags> key)
	{
		return Compare(key.Key, key.Value & (EventTags)268435455);
	}

	private int Compare(string otherName, EventTags otherTags)
	{
		int result = StringComparer.Ordinal.Compare(name, otherName);
		if (result == 0 && tags != otherTags)
		{
			result = ((tags >= otherTags) ? 1 : (-1));
		}
		return result;
	}
}
