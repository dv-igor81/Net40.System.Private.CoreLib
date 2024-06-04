namespace System.Reflection;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
internal sealed class AssemblyMetadataAttribute : Attribute
{
	private string m_key;

	private string m_value;

	public string Key => m_key;

	public string Value => m_value;

	public AssemblyMetadataAttribute(string key, string value)
	{
		m_key = key;
		m_value = value;
	}
}
