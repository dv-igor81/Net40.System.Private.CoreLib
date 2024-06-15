namespace System.Text.Json.Serialization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property, AllowMultiple = false)]
public class JsonConverterAttribute : JsonAttribute
{
	public Type ConverterType { get; private set; }

	public JsonConverterAttribute(Type converterType)
	{
		ConverterType = converterType;
	}

	protected JsonConverterAttribute()
	{
	}

	public virtual JsonConverter CreateConverter(Type typeToConvert)
	{
		return null;
	}
}
