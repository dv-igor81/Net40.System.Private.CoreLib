namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterUInt64 : JsonConverter<ulong>
{
	public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetUInt64();
	}

	public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
