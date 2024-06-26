namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterJsonElement : JsonConverter<JsonElement>
{
	public override JsonElement Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		using JsonDocument document = JsonDocument.ParseValue(ref reader);
		return document.RootElement.Clone();
	}

	public override void Write(Utf8JsonWriter writer, JsonElement value, JsonSerializerOptions options)
	{
		value.WriteTo(writer);
	}
}
