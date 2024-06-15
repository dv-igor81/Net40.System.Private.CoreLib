namespace System.Text.Json.Serialization;

public abstract class JsonConverter
{
	internal virtual Type TypeToConvert => null;

	internal JsonConverter()
	{
	}

	public abstract bool CanConvert(Type typeToConvert);
}
public abstract class JsonConverter<T> : JsonConverter
{
	internal override Type TypeToConvert => typeof(T);

	protected internal JsonConverter()
	{
	}

	public override bool CanConvert(Type typeToConvert)
	{
		return typeToConvert == typeof(T);
	}

	public abstract T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options);

	public abstract void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options);
}
