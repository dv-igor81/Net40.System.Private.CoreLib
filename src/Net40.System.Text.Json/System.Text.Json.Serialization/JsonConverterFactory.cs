#define DEBUG
using System.Diagnostics;

namespace System.Text.Json.Serialization;

public abstract class JsonConverterFactory : JsonConverter
{
	internal JsonConverter GetConverterInternal(Type typeToConvert, JsonSerializerOptions options)
	{
		Debug.Assert(CanConvert(typeToConvert));
		return CreateConverter(typeToConvert, options);
	}

	public abstract JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options);
}
