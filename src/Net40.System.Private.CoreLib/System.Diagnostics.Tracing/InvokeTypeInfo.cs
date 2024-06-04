using System.Collections.Generic;
using System.Reflection;

namespace System.Diagnostics.Tracing;

internal sealed class InvokeTypeInfo : System.Diagnostics.Tracing.TraceLoggingTypeInfo
{
	internal readonly System.Diagnostics.Tracing.PropertyAnalysis[]? properties;

	public InvokeTypeInfo(Type type, System.Diagnostics.Tracing.TypeAnalysis typeAnalysis)
		: base(type, typeAnalysis.name, typeAnalysis.level, typeAnalysis.opcode, typeAnalysis.keywords, typeAnalysis.tags)
	{
		if (typeAnalysis.properties.Length != 0)
		{
			properties = typeAnalysis.properties;
		}
	}

	public override void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format)
	{
		System.Diagnostics.Tracing.TraceLoggingMetadataCollector groupCollector = collector.AddGroup(name);
		if (properties == null)
		{
			return;
		}
		System.Diagnostics.Tracing.PropertyAnalysis[] array = properties;
		foreach (System.Diagnostics.Tracing.PropertyAnalysis property in array)
		{
			EventFieldFormat propertyFormat = EventFieldFormat.Default;
			EventFieldAttribute propertyAttribute = property.fieldAttribute;
			if (propertyAttribute != null)
			{
				groupCollector.Tags = propertyAttribute.Tags;
				propertyFormat = propertyAttribute.Format;
			}
			property.typeInfo.WriteMetadata(groupCollector, property.name, propertyFormat);
		}
	}

	public override void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value)
	{
		if (properties != null)
		{
			System.Diagnostics.Tracing.PropertyAnalysis[] array = properties;
			foreach (System.Diagnostics.Tracing.PropertyAnalysis property in array)
			{
				property.typeInfo.WriteData(collector, property.getter(value));
			}
		}
	}

	public override object? GetData(object? value)
	{
		if (properties != null)
		{
			List<string> membersNames = new List<string>();
			List<object> memebersValues = new List<object>();
			for (int i = 0; i < properties.Length; i++)
			{
				object propertyValue = PropertyInfoTheraotExtensions.GetValue(properties[i].propertyInfo, value);
				membersNames.Add(properties[i].name);
				memebersValues.Add(properties[i].typeInfo.GetData(propertyValue));
			}
			return new System.Diagnostics.Tracing.EventPayload(membersNames, memebersValues);
		}
		return null;
	}
}
