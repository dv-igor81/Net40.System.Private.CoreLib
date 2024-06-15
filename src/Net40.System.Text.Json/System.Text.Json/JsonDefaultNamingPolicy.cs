namespace System.Text.Json;

internal class JsonDefaultNamingPolicy : JsonNamingPolicy
{
	public override string ConvertName(string name)
	{
		return name;
	}
}
