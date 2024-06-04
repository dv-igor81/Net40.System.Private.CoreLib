using System.Reflection;

namespace System.Diagnostics.Tracing;

internal sealed class PropertyAnalysis
{
	internal readonly string name;

	internal readonly PropertyInfo propertyInfo;

	internal readonly Func<PropertyValue, PropertyValue> getter;

	internal readonly System.Diagnostics.Tracing.TraceLoggingTypeInfo typeInfo;

	internal readonly EventFieldAttribute? fieldAttribute;

	public PropertyAnalysis(string name, PropertyInfo propertyInfo, System.Diagnostics.Tracing.TraceLoggingTypeInfo typeInfo, EventFieldAttribute? fieldAttribute)
	{
		this.name = name;
		this.propertyInfo = propertyInfo;
		getter = PropertyValue.GetPropertyGetter(propertyInfo);
		this.typeInfo = typeInfo;
		this.fieldAttribute = fieldAttribute;
	}
}
