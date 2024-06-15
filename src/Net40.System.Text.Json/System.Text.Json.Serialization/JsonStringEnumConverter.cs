using System.Reflection;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json.Serialization;

public sealed class JsonStringEnumConverter : JsonConverterFactory
{
	private readonly JsonNamingPolicy _namingPolicy;

	private readonly EnumConverterOptions _converterOptions;

	public JsonStringEnumConverter()
		: this(null, allowIntegerValues: true)
	{
	}

	public JsonStringEnumConverter(JsonNamingPolicy namingPolicy = null, bool allowIntegerValues = true)
	{
		_namingPolicy = namingPolicy;
		_converterOptions = ((!allowIntegerValues) ? EnumConverterOptions.AllowStrings : (EnumConverterOptions.AllowStrings | EnumConverterOptions.AllowNumbers));
	}

	public override bool CanConvert(Type typeToConvert)
	{
		return typeToConvert.IsEnum;
	}

	public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
	{
		return (JsonConverter)Activator.CreateInstance(typeof(JsonConverterEnum<>).MakeGenericType(typeToConvert), BindingFlags.Instance | BindingFlags.Public, null, new object[2] { _converterOptions, _namingPolicy }, null);
	}
}
