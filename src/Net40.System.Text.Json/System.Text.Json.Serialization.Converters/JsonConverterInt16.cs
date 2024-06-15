namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterInt16 : JsonConverter<short>
{
	public override short Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetInt16();
	}

	public override void Write(Utf8JsonWriter writer, short value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
