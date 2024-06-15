namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterSingle : JsonConverter<float>
{
	public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		return reader.GetSingle();
	}

	public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
