namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterUInt32 : JsonConverter<uint>
{
	public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetUInt32();
	}

	public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
