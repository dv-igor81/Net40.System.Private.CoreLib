using System.Collections.Generic;
using System.Reflection;

namespace System.Diagnostics.Tracing;

public class TraceLoggingEventTypes
{
	internal readonly System.Diagnostics.Tracing.TraceLoggingTypeInfo[] typeInfos;

	internal readonly string name;

	internal readonly EventTags tags;

	internal readonly byte level;

	internal readonly byte opcode;

	internal readonly EventKeywords keywords;

	internal readonly byte[] typeMetadata;

	internal readonly int scratchSize;

	internal readonly int dataCount;

	internal readonly int pinCount;

	private System.Diagnostics.Tracing.ConcurrentSet<KeyValuePair<string, EventTags>, System.Diagnostics.Tracing.NameInfo> nameInfos;

	internal string Name => name;

	internal EventLevel Level => (EventLevel)level;

	internal EventOpcode Opcode => (EventOpcode)opcode;

	internal EventKeywords Keywords => keywords;

	internal EventTags Tags => tags;

	internal TraceLoggingEventTypes(string name, EventTags tags, params Type[] types)
		: this(tags, name, MakeArray(types))
	{
	}

	internal TraceLoggingEventTypes(string name, EventTags tags, params System.Diagnostics.Tracing.TraceLoggingTypeInfo[] typeInfos)
		: this(tags, name, MakeArray(typeInfos))
	{
	}

	internal TraceLoggingEventTypes(string name, EventTags tags, ParameterInfo[] paramInfos)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		typeInfos = MakeArray(paramInfos);
		this.name = name;
		this.tags = tags;
		level = 5;
		System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector = new System.Diagnostics.Tracing.TraceLoggingMetadataCollector();
		for (int i = 0; i < typeInfos.Length; i++)
		{
			System.Diagnostics.Tracing.TraceLoggingTypeInfo typeInfo = typeInfos[i];
			level = System.Diagnostics.Tracing.Statics.Combine((int)typeInfo.Level, level);
			opcode = System.Diagnostics.Tracing.Statics.Combine((int)typeInfo.Opcode, opcode);
			keywords |= typeInfo.Keywords;
			string paramName = paramInfos[i].Name;
			if (System.Diagnostics.Tracing.Statics.ShouldOverrideFieldName(paramName))
			{
				paramName = typeInfo.Name;
			}
			typeInfo.WriteMetadata(collector, paramName, EventFieldFormat.Default);
		}
		typeMetadata = collector.GetMetadata();
		scratchSize = collector.ScratchSize;
		dataCount = collector.DataCount;
		pinCount = collector.PinCount;
	}

	private TraceLoggingEventTypes(EventTags tags, string defaultName, System.Diagnostics.Tracing.TraceLoggingTypeInfo[] typeInfos)
	{
		if (defaultName == null)
		{
			throw new ArgumentNullException("defaultName");
		}
		this.typeInfos = typeInfos;
		name = defaultName;
		this.tags = tags;
		level = 5;
		System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector = new System.Diagnostics.Tracing.TraceLoggingMetadataCollector();
		foreach (System.Diagnostics.Tracing.TraceLoggingTypeInfo typeInfo in typeInfos)
		{
			level = System.Diagnostics.Tracing.Statics.Combine((int)typeInfo.Level, level);
			opcode = System.Diagnostics.Tracing.Statics.Combine((int)typeInfo.Opcode, opcode);
			keywords |= typeInfo.Keywords;
			typeInfo.WriteMetadata(collector, null, EventFieldFormat.Default);
		}
		typeMetadata = collector.GetMetadata();
		scratchSize = collector.ScratchSize;
		dataCount = collector.DataCount;
		pinCount = collector.PinCount;
	}

	internal System.Diagnostics.Tracing.NameInfo GetNameInfo(string name, EventTags tags)
	{
		System.Diagnostics.Tracing.NameInfo ret = nameInfos.TryGet(new KeyValuePair<string, EventTags>(name, tags));
		if (ret == null)
		{
			ret = nameInfos.GetOrAdd(new System.Diagnostics.Tracing.NameInfo(name, tags, typeMetadata.Length));
		}
		return ret;
	}

	private System.Diagnostics.Tracing.TraceLoggingTypeInfo[] MakeArray(ParameterInfo[] paramInfos)
	{
		if (paramInfos == null)
		{
			throw new ArgumentNullException("paramInfos");
		}
		List<Type> recursionCheck = new List<Type>(paramInfos.Length);
		System.Diagnostics.Tracing.TraceLoggingTypeInfo[] result = new System.Diagnostics.Tracing.TraceLoggingTypeInfo[paramInfos.Length];
		for (int i = 0; i < paramInfos.Length; i++)
		{
			result[i] = System.Diagnostics.Tracing.TraceLoggingTypeInfo.GetInstance(paramInfos[i].ParameterType, recursionCheck);
		}
		return result;
	}

	private static System.Diagnostics.Tracing.TraceLoggingTypeInfo[] MakeArray(Type[] types)
	{
		if (types == null)
		{
			throw new ArgumentNullException("types");
		}
		List<Type> recursionCheck = new List<Type>(types.Length);
		System.Diagnostics.Tracing.TraceLoggingTypeInfo[] result = new System.Diagnostics.Tracing.TraceLoggingTypeInfo[types.Length];
		for (int i = 0; i < types.Length; i++)
		{
			result[i] = System.Diagnostics.Tracing.TraceLoggingTypeInfo.GetInstance(types[i], recursionCheck);
		}
		return result;
	}

	private static System.Diagnostics.Tracing.TraceLoggingTypeInfo[] MakeArray(System.Diagnostics.Tracing.TraceLoggingTypeInfo[] typeInfos)
	{
		if (typeInfos == null)
		{
			throw new ArgumentNullException("typeInfos");
		}
		return (System.Diagnostics.Tracing.TraceLoggingTypeInfo[])typeInfos.Clone();
	}
}
