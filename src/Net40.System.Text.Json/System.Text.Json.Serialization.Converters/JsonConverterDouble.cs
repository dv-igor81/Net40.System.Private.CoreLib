namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterDouble : JsonConverter<double>
{
	public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetDouble();
	}

	public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
