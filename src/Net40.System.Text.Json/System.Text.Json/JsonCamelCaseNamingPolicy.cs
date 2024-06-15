namespace System.Text.Json;

internal sealed class JsonCamelCaseNamingPolicy : JsonNamingPolicy
{
	public override string ConvertName(string name)
	{
		if (string.IsNullOrEmpty(name) || !char.IsUpper(name[0]))
		{
			return name;
		}
		char[] chars = name.ToCharArray();
		FixCasing(chars);
		return new string(chars);
	}

	private static void FixCasing(Span<char> chars)
	{
		for (int i = 0; i < chars.Length && (i != 1 || char.IsUpper(chars[i])); i++)
		{
			bool hasNext = i + 1 < chars.Length;
			if (i > 0 && hasNext && !char.IsUpper(chars[i + 1]))
			{
				if (chars[i + 1] == ' ')
				{
					chars[i] = char.ToLowerInvariant(chars[i]);
				}
				break;
			}
			chars[i] = char.ToLowerInvariant(chars[i]);
		}
	}
}
