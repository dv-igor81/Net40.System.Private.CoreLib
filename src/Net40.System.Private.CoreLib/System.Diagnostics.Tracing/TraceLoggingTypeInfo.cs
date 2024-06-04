using System.Collections.Generic;

namespace System.Diagnostics.Tracing;

internal abstract class TraceLoggingTypeInfo
{
	private readonly string name;

	private readonly EventKeywords keywords;

	private readonly EventLevel level = (EventLevel)(-1);

	private readonly EventOpcode opcode = (EventOpcode)(-1);

	private readonly EventTags tags;

	private readonly Type dataType;

	private readonly Func<object?, PropertyValue> propertyValueFactory;

	[ThreadStatic]
	private static Dictionary<Type, System.Diagnostics.Tracing.TraceLoggingTypeInfo>? threadCache;

	public string Name => name;

	public EventLevel Level => level;

	public EventOpcode Opcode => opcode;

	public EventKeywords Keywords => keywords;

	public EventTags Tags => tags;

	internal Type DataType => dataType;

	internal Func<object?, PropertyValue> PropertyValueFactory => propertyValueFactory;

	internal TraceLoggingTypeInfo(Type dataType)
	{
		if (dataType == null)
		{
			throw new ArgumentNullException("dataType");
		}
		name = dataType.Name;
		this.dataType = dataType;
		propertyValueFactory = PropertyValue.GetFactory(dataType);
	}

	internal TraceLoggingTypeInfo(Type dataType, string name, EventLevel level, EventOpcode opcode, EventKeywords keywords, EventTags tags)
	{
		if (dataType == null)
		{
			throw new ArgumentNullException("dataType");
		}
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		System.Diagnostics.Tracing.Statics.CheckName(name);
		this.name = name;
		this.keywords = keywords;
		this.level = level;
		this.opcode = opcode;
		this.tags = tags;
		this.dataType = dataType;
		propertyValueFactory = PropertyValue.GetFactory(dataType);
	}

	public abstract void WriteMetadata(System.Diagnostics.Tracing.TraceLoggingMetadataCollector collector, string? name, EventFieldFormat format);

	public abstract void WriteData(System.Diagnostics.Tracing.TraceLoggingDataCollector collector, PropertyValue value);

	public virtual object? GetData(object? value)
	{
		return value;
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo GetInstance(Type type, List<Type>? recursionCheck)
	{
		Dictionary<Type, System.Diagnostics.Tracing.TraceLoggingTypeInfo> cache = threadCache ?? (threadCache = new Dictionary<Type, System.Diagnostics.Tracing.TraceLoggingTypeInfo>());
		if (!cache.TryGetValue(type, out var instance))
		{
			if (recursionCheck == null)
			{
				recursionCheck = new List<Type>();
			}
			int recursionCheckCount = recursionCheck.Count;
			instance = (cache[type] = System.Diagnostics.Tracing.Statics.CreateDefaultTypeInfo(type, recursionCheck));
			recursionCheck.RemoveRange(recursionCheckCount, recursionCheck.Count - recursionCheckCount);
		}
		return instance;
	}
}
