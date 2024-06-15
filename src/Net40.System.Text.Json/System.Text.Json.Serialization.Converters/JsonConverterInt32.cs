namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterInt32 : JsonConverter<int>
{
	public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetInt32();
	}

	public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
