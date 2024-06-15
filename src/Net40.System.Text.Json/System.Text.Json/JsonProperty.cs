using System.Diagnostics;

namespace System.Text.Json;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public readonly struct JsonProperty
{
	public JsonElement Value { get; }

	public string Name => Value.GetPropertyName();

	private string DebuggerDisplay => (Value.ValueKind == JsonValueKind.Undefined) ? "<Undefined>" : ("\"" + ToString() + "\"");

	internal JsonProperty(JsonElement value)
	{
		Value = value;
	}

	public bool NameEquals(string text)
	{
		return NameEquals(text.AsSpan());
	}

	public bool NameEquals(ReadOnlySpan<byte> utf8Text)
	{
		return Value.TextEqualsHelper(utf8Text, isPropertyName: true);
	}

	public bool NameEquals(ReadOnlySpan<char> text)
	{
		return Value.TextEqualsHelper(text, isPropertyName: true);
	}

	public void WriteTo(Utf8JsonWriter writer)
	{
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		writer.WritePropertyName(Name);
		Value.WriteTo(writer);
	}

	public override string ToString()
	{
		return Value.GetPropertyRawText();
	}
}
