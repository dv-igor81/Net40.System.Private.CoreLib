namespace System.Text.Json;

internal readonly struct PropertyRef
{
	public readonly ulong Key;

	public readonly JsonPropertyInfo Info;

	public PropertyRef(ulong key, JsonPropertyInfo info)
	{
		Key = key;
		Info = info;
	}
}
