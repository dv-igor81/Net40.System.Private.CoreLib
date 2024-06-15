namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterUInt16 : JsonConverter<ushort>
{
	public override ushort Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetUInt16();
	}

	public override void Write(Utf8JsonWriter writer, ushort value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
