using System.Collections.Generic;
using System.Reflection;

namespace System.Diagnostics.Tracing;

internal sealed class TypeAnalysis
{
	internal readonly System.Diagnostics.Tracing.PropertyAnalysis[] properties;

	internal readonly string? name;

	internal readonly EventKeywords keywords;

	internal readonly EventLevel level = (EventLevel)(-1);

	internal readonly EventOpcode opcode = (EventOpcode)(-1);

	internal readonly EventTags tags;

	public TypeAnalysis(Type dataType, EventDataAttribute? eventAttrib, List<Type> recursionCheck)
	{
		IEnumerable<PropertyInfo> propertyInfos = System.Diagnostics.Tracing.Statics.GetProperties(dataType);
		List<System.Diagnostics.Tracing.PropertyAnalysis> propertyList = new List<System.Diagnostics.Tracing.PropertyAnalysis>();
		foreach (PropertyInfo propertyInfo in propertyInfos)
		{
			if (!System.Diagnostics.Tracing.Statics.HasCustomAttribute(propertyInfo, typeof(EventIgnoreAttribute)) && propertyInfo.CanRead && propertyInfo.GetIndexParameters().Length == 0)
			{
				MethodInfo getterInfo = System.Diagnostics.Tracing.Statics.GetGetMethod(propertyInfo);
				if (!(getterInfo == null) && !getterInfo.IsStatic && getterInfo.IsPublic)
				{
					Type propertyType = propertyInfo.PropertyType;
					System.Diagnostics.Tracing.TraceLoggingTypeInfo propertyTypeInfo = System.Diagnostics.Tracing.TraceLoggingTypeInfo.GetInstance(propertyType, recursionCheck);
					EventFieldAttribute fieldAttribute = System.Diagnostics.Tracing.Statics.GetCustomAttribute<EventFieldAttribute>(propertyInfo);
					string propertyName = ((fieldAttribute != null && fieldAttribute.Name != null) ? fieldAttribute.Name : (System.Diagnostics.Tracing.Statics.ShouldOverrideFieldName(propertyInfo.Name) ? propertyTypeInfo.Name : propertyInfo.Name));
					propertyList.Add(new System.Diagnostics.Tracing.PropertyAnalysis(propertyName, propertyInfo, propertyTypeInfo, fieldAttribute));
				}
			}
		}
		properties = propertyList.ToArray();
		System.Diagnostics.Tracing.PropertyAnalysis[] array = properties;
		foreach (System.Diagnostics.Tracing.PropertyAnalysis property in array)
		{
			System.Diagnostics.Tracing.TraceLoggingTypeInfo typeInfo = property.typeInfo;
			level = (EventLevel)System.Diagnostics.Tracing.Statics.Combine((int)typeInfo.Level, (int)level);
			opcode = (EventOpcode)System.Diagnostics.Tracing.Statics.Combine((int)typeInfo.Opcode, (int)opcode);
			keywords |= typeInfo.Keywords;
			tags |= typeInfo.Tags;
		}
		if (eventAttrib != null)
		{
			level = (EventLevel)System.Diagnostics.Tracing.Statics.Combine((int)eventAttrib.Level, (int)level);
			opcode = (EventOpcode)System.Diagnostics.Tracing.Statics.Combine((int)eventAttrib.Opcode, (int)opcode);
			keywords |= eventAttrib.Keywords;
			tags |= eventAttrib.Tags;
			name = eventAttrib.Name;
		}
		if (name == null)
		{
			name = dataType.Name;
		}
	}
}
