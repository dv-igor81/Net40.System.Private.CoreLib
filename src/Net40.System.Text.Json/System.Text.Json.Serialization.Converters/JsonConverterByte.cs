namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterByte : JsonConverter<byte>
{
	public override byte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetByte();
	}

	public override void Write(Utf8JsonWriter writer, byte value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
