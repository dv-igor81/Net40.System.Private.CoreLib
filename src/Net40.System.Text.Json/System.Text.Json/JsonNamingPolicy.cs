namespace System.Text.Json;

public abstract class JsonNamingPolicy
{
	public static JsonNamingPolicy CamelCase { get; } = new JsonCamelCaseNamingPolicy();


	internal static JsonNamingPolicy Default { get; } = new JsonDefaultNamingPolicy();


	public abstract string ConvertName(string name);
}
