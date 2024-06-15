namespace System.Text.Json.Serialization;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public sealed class JsonPropertyNameAttribute : JsonAttribute
{
	public string Name { get; }

	public JsonPropertyNameAttribute(string name)
	{
		Name = name;
	}
}
