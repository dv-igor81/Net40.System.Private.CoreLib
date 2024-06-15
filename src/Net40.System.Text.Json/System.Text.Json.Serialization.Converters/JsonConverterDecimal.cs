namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterDecimal : JsonConverter<decimal>
{
	public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetDecimal();
	}

	public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
