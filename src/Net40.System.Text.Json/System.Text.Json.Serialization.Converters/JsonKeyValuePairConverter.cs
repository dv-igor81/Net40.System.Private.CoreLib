using System.Collections.Generic;
using System.Reflection;

namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonKeyValuePairConverter : JsonConverterFactory
{
	public override bool CanConvert(Type typeToConvert)
	{
		if (!typeToConvert.IsGenericType)
		{
			return false;
		}
		Type generic = typeToConvert.GetGenericTypeDefinition();
		return generic == typeof(KeyValuePair<, >);
	}

	public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
	{
		Type keyType = type.GetGenericArguments()[0];
		Type valueType = type.GetGenericArguments()[1];
		return (JsonConverter)Activator.CreateInstance(typeof(JsonKeyValuePairConverter<, >).MakeGenericType(keyType, valueType), BindingFlags.Instance | BindingFlags.Public, null, null, null);
	}
}
internal sealed class JsonKeyValuePairConverter<TKey, TValue> : JsonConverter<KeyValuePair<TKey, TValue>>
{
	private const string KeyName = "Key";

	private const string ValueName = "Value";

	private static readonly JsonEncodedText _keyName = JsonEncodedText.Encode("Key");

	private static readonly JsonEncodedText _valueName = JsonEncodedText.Encode("Value");

	public override KeyValuePair<TKey, TValue> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.StartObject)
		{
			ThrowHelper.ThrowJsonException();
		}
		TKey i = default(TKey);
		bool keySet = false;
		TValue v = default(TValue);
		bool valueSet = false;
		reader.Read();
		if (reader.TokenType != JsonTokenType.PropertyName)
		{
			ThrowHelper.ThrowJsonException();
		}
		string propertyName = reader.GetString();
		if (propertyName == "Key")
		{
			i = ReadProperty<TKey>(ref reader, typeToConvert, options);
			keySet = true;
		}
		else if (propertyName == "Value")
		{
			v = ReadProperty<TValue>(ref reader, typeToConvert, options);
			valueSet = true;
		}
		else
		{
			ThrowHelper.ThrowJsonException();
		}
		reader.Read();
		if (reader.TokenType != JsonTokenType.PropertyName)
		{
			ThrowHelper.ThrowJsonException();
		}
		propertyName = reader.GetString();
		if (propertyName == "Value")
		{
			v = ReadProperty<TValue>(ref reader, typeToConvert, options);
			valueSet = true;
		}
		else if (propertyName == "Key")
		{
			i = ReadProperty<TKey>(ref reader, typeToConvert, options);
			keySet = true;
		}
		else
		{
			ThrowHelper.ThrowJsonException();
		}
		if (!keySet || !valueSet)
		{
			ThrowHelper.ThrowJsonException();
		}
		reader.Read();
		if (reader.TokenType != JsonTokenType.EndObject)
		{
			ThrowHelper.ThrowJsonException();
		}
		return new KeyValuePair<TKey, TValue>(i, v);
	}

	private T ReadProperty<T>(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (typeToConvert != typeof(object) && options?.GetConverter(typeToConvert) is JsonConverter<T> keyConverter)
		{
			reader.Read();
			return keyConverter.Read(ref reader, typeToConvert, options);
		}
		return JsonSerializer.Deserialize<T>(ref reader, options);
	}

	private void WriteProperty<T>(Utf8JsonWriter writer, T value, JsonEncodedText name, JsonSerializerOptions options)
	{
		Type typeToConvert = typeof(T);
		writer.WritePropertyName(name);
		if (typeToConvert != typeof(object) && options?.GetConverter(typeToConvert) is JsonConverter<T> keyConverter)
		{
			keyConverter.Write(writer, value, options);
		}
		else
		{
			JsonSerializer.Serialize(writer, value, options);
		}
	}

	public override void Write(Utf8JsonWriter writer, KeyValuePair<TKey, TValue> value, JsonSerializerOptions options)
	{
		writer.WriteStartObject();
		WriteProperty(writer, value.Key, _keyName, options);
		WriteProperty(writer, value.Value, _valueName, options);
		writer.WriteEndObject();
	}
}
