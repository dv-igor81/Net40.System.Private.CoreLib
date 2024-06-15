namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterChar : JsonConverter<char>
{
	public override char Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string str = reader.GetString();
		if (string.IsNullOrEmpty(str))
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedChar(reader.TokenType);
		}
		return str[0];
	}

	public override void Write(Utf8JsonWriter writer, char value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString());
	}
}
