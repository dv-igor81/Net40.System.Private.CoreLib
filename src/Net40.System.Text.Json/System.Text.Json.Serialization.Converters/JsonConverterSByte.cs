namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterSByte : JsonConverter<sbyte>
{
	public override sbyte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetSByte();
	}

	public override void Write(Utf8JsonWriter writer, sbyte value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
