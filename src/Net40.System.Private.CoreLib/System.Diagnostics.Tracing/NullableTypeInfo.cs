#define DEBUG
using System.Collections.Generic;
using System.Reflection;

namespace System.Diagnostics.Tracing;

internal sealed class NullableTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	private readonly System.Diagnostics.Tracing.TraceLoggingTypeInfo valueInfo;

	private readonly Func<PropertyValue, PropertyValue> valueGetter;

	public NullableTypeInfo(Type type, List<Type> recursionCheck)
		: base(type)
	{
		Type[] typeArgs = type.GenericTypeArguments();
		Debug.Assert(typeArgs.Length == 1);
		valueInfo = System.Diagnostics.Tracing.TraceLoggingTypeInfo.GetInstance(typeArgs[0], recursionCheck);
		valueGetter = PropertyValue.GetPropertyGetter(IntrospectionExtensions.GetTypeInfo(type).GetDeclaredProperty("Value"));
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		System.Diagnostics.Tracing.TraceLoggingMetadataCollector group = collector.AddGroup(name);
		group.AddScalar("HasValue", System.Diagnostics.Tracing.TraceLoggingDataType.Boolean8);
		valueInfo.WriteMetadata(group, "Value", format);
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		bool hasValue = value.ReferenceValue != null;
		collector.AddScalar(hasValue);
		PropertyValue val = (hasValue ? valueGetter(value) : valueInfo.PropertyValueFactory(Activator.CreateInstance(valueInfo.DataType)));
		valueInfo.WriteData(collector, val);
	}
}
