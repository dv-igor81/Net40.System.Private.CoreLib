namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterGuid : JsonConverter<Guid>
{
	public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetGuid();
	}

	public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value);
	}
}
