using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Diagnostics.Tracing;

internal static class Statics
{
	public const byte DefaultLevel = 5;

	public const byte TraceLoggingChannel = 11;

	public const byte InTypeMask = 31;

	public const byte InTypeFixedCountFlag = 32;

	public const byte InTypeVariableCountFlag = 64;

	public const byte InTypeCustomCountFlag = 96;

	public const byte InTypeCountMask = 96;

	public const byte InTypeChainFlag = 128;

	public const byte OutTypeMask = 127;

	public const byte OutTypeChainFlag = 128;

	public const EventTags EventTagsMask = (EventTags)268435455;

	public static readonly System.Diagnostics.Tracing.TraceLoggingDataType IntPtrType = ((IntPtr.Size == 8) ? System.Diagnostics.Tracing.TraceLoggingDataType.Int64 : System.Diagnostics.Tracing.TraceLoggingDataType.Int32);

	public static readonly System.Diagnostics.Tracing.TraceLoggingDataType UIntPtrType = ((IntPtr.Size == 8) ? System.Diagnostics.Tracing.TraceLoggingDataType.UInt64 : System.Diagnostics.Tracing.TraceLoggingDataType.UInt32);

	public static readonly System.Diagnostics.Tracing.TraceLoggingDataType HexIntPtrType = ((IntPtr.Size == 8) ? System.Diagnostics.Tracing.TraceLoggingDataType.HexInt64 : System.Diagnostics.Tracing.TraceLoggingDataType.HexInt32);

	public static byte[] MetadataForString(string name, int prefixSize, int suffixSize, int additionalSize)
	{
		CheckName(name);
		int metadataSize = Encoding.UTF8.GetByteCount(name) + 3 + prefixSize + suffixSize;
		byte[] metadata = new byte[metadataSize];
		ushort totalSize = checked((ushort)(metadataSize + additionalSize));
		metadata[0] = (byte)totalSize;
		metadata[1] = (byte)(totalSize >> 8);
		Encoding.UTF8.GetBytes(name, 0, name.Length, metadata, 2 + prefixSize);
		return metadata;
	}

	public static void EncodeTags(int tags, ref int pos, byte[]? metadata)
	{
		int tagsLeft = tags & 0xFFFFFFF;
		bool more;
		do
		{
			byte current = (byte)((uint)(tagsLeft >> 21) & 0x7Fu);
			more = (tagsLeft & 0x1FFFFF) != 0;
			current |= (byte)(more ? 128u : 0u);
			tagsLeft <<= 7;
			if (metadata != null)
			{
				metadata[pos] = current;
			}
			pos++;
		}
		while (more);
	}

	public static byte Combine(int settingValue, byte defaultValue)
	{
		return ((byte)settingValue == settingValue) ? ((byte)settingValue) : defaultValue;
	}

	public static byte Combine(int settingValue1, int settingValue2, byte defaultValue)
	{
		return ((byte)settingValue1 == settingValue1) ? ((byte)settingValue1) : (((byte)settingValue2 == settingValue2) ? ((byte)settingValue2) : defaultValue);
	}

	public static int Combine(int settingValue1, int settingValue2)
	{
		return ((byte)settingValue1 == settingValue1) ? settingValue1 : settingValue2;
	}

	public static void CheckName(string? name)
	{
		if (name != null && 0 <= name.IndexOf('\0'))
		{
			throw new ArgumentOutOfRangeException("name");
		}
	}

	public static bool ShouldOverrideFieldName(string fieldName)
	{
		return fieldName.Length <= 2 && fieldName[0] == '_';
	}

	public static System.Diagnostics.Tracing.TraceLoggingDataType MakeDataType(System.Diagnostics.Tracing.TraceLoggingDataType baseType, EventFieldFormat format)
	{
		return (System.Diagnostics.Tracing.TraceLoggingDataType)((int)(baseType & (System.Diagnostics.Tracing.TraceLoggingDataType)31) | ((int)format << 8));
	}

	public static System.Diagnostics.Tracing.TraceLoggingDataType Format8(EventFieldFormat format, System.Diagnostics.Tracing.TraceLoggingDataType native)
	{
		return format switch
		{
			EventFieldFormat.Default => native, 
			EventFieldFormat.String => System.Diagnostics.Tracing.TraceLoggingDataType.Char8, 
			EventFieldFormat.Boolean => System.Diagnostics.Tracing.TraceLoggingDataType.Boolean8, 
			EventFieldFormat.Hexadecimal => System.Diagnostics.Tracing.TraceLoggingDataType.HexInt8, 
			_ => MakeDataType(native, format), 
		};
	}

	public static System.Diagnostics.Tracing.TraceLoggingDataType Format16(EventFieldFormat format, System.Diagnostics.Tracing.TraceLoggingDataType native)
	{
		return format switch
		{
			EventFieldFormat.Default => native, 
			EventFieldFormat.String => System.Diagnostics.Tracing.TraceLoggingDataType.Char16, 
			EventFieldFormat.Hexadecimal => System.Diagnostics.Tracing.TraceLoggingDataType.HexInt16, 
			_ => MakeDataType(native, format), 
		};
	}

	public static System.Diagnostics.Tracing.TraceLoggingDataType Format32(EventFieldFormat format, System.Diagnostics.Tracing.TraceLoggingDataType native)
	{
		return format switch
		{
			EventFieldFormat.Default => native, 
			EventFieldFormat.Boolean => System.Diagnostics.Tracing.TraceLoggingDataType.Boolean32, 
			EventFieldFormat.Hexadecimal => System.Diagnostics.Tracing.TraceLoggingDataType.HexInt32, 
			EventFieldFormat.HResult => System.Diagnostics.Tracing.TraceLoggingDataType.HResult, 
			_ => MakeDataType(native, format), 
		};
	}

	public static System.Diagnostics.Tracing.TraceLoggingDataType Format64(EventFieldFormat format, System.Diagnostics.Tracing.TraceLoggingDataType native)
	{
		return format switch
		{
			EventFieldFormat.Default => native, 
			EventFieldFormat.Hexadecimal => System.Diagnostics.Tracing.TraceLoggingDataType.HexInt64, 
			_ => MakeDataType(native, format), 
		};
	}

	public static System.Diagnostics.Tracing.TraceLoggingDataType FormatPtr(EventFieldFormat format, System.Diagnostics.Tracing.TraceLoggingDataType native)
	{
		return format switch
		{
			EventFieldFormat.Default => native, 
			EventFieldFormat.Hexadecimal => HexIntPtrType, 
			_ => MakeDataType(native, format), 
		};
	}

	public static object? CreateInstance(Type type, params object?[]? parameters)
	{
		return Activator.CreateInstance(type, parameters);
	}

	public static bool IsValueType(Type type)
	{
		return type.IsValueType();
	}

	public static bool IsEnum(Type type)
	{
		return type.IsEnum();
	}

	public static IEnumerable<PropertyInfo> GetProperties(Type type)
	{
		return type.GetProperties();
	}

	public static MethodInfo? GetGetMethod(PropertyInfo propInfo)
	{
		return propInfo.GetGetMethod();
	}

	public static MethodInfo? GetDeclaredStaticMethod(Type declaringType, string name)
	{
		return declaringType.GetMethod(name, BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic);
	}

	public static bool HasCustomAttribute(PropertyInfo propInfo, Type attributeType)
	{
		object[] attributes = propInfo.GetCustomAttributes(attributeType, inherit: false);
		return attributes.Length != 0;
	}

	public static AttributeType? GetCustomAttribute<AttributeType>(PropertyInfo propInfo) where AttributeType : Attribute
	{
		AttributeType result = null;
		object[] attributes = propInfo.GetCustomAttributes(typeof(AttributeType), inherit: false);
		if (attributes.Length != 0)
		{
			return (AttributeType)attributes[0];
		}
		return result;
	}

	public static AttributeType? GetCustomAttribute<AttributeType>(Type type) where AttributeType : Attribute
	{
		AttributeType result = null;
		object[] attributes = type.GetCustomAttributes(typeof(AttributeType), inherit: false);
		if (attributes.Length != 0)
		{
			return (AttributeType)attributes[0];
		}
		return result;
	}

	public static Type[] GetGenericArguments(Type type)
	{
		return type.GetGenericArguments();
	}

	public static Type? FindEnumerableElementType(Type type)
	{
		Type elementType = null;
		if (IsGenericMatch(type, typeof(IEnumerable<>)))
		{
			elementType = GetGenericArguments(type)[0];
		}
		else
		{
			Type[] ifaceTypes = type.FindInterfaces(IsGenericMatch, typeof(IEnumerable<>));
			Type[] array = ifaceTypes;
			foreach (Type ifaceType in array)
			{
				if (elementType != null)
				{
					elementType = null;
					break;
				}
				elementType = GetGenericArguments(ifaceType)[0];
			}
		}
		return elementType;
	}

	public static bool IsGenericMatch(Type type, object? openType)
	{
		return type.IsGenericType() && type.GetGenericTypeDefinition() == (Type)openType;
	}

	public static Delegate CreateDelegate(Type delegateType, MethodInfo methodInfo)
	{
		return Delegate.CreateDelegate(delegateType, methodInfo);
	}

	public static System.Diagnostics.Tracing.TraceLoggingTypeInfo CreateDefaultTypeInfo(Type dataType, List<Type> recursionCheck)
	{
		if (recursionCheck.Contains(dataType))
		{
			throw new NotSupportedException("SR.EventSource_RecursiveTypeDefinition");
		}
		recursionCheck.Add(dataType);
		EventDataAttribute eventAttrib = GetCustomAttribute<EventDataAttribute>(dataType);
		if (eventAttrib != null || GetCustomAttribute<CompilerGeneratedAttribute>(dataType) != null || IsGenericMatch(dataType, typeof(KeyValuePair<, >)))
		{
			System.Diagnostics.Tracing.TypeAnalysis analysis = new System.Diagnostics.Tracing.TypeAnalysis(dataType, eventAttrib, recursionCheck);
			return new InvokeTypeInfo(dataType, analysis);
		}
		if (dataType.IsArray)
		{
			Type elementType2 = dataType.GetElementType();
			if (elementType2 == typeof(bool))
			{
				return ScalarArrayTypeInfo.Boolean();
			}
			if (elementType2 == typeof(byte))
			{
				return ScalarArrayTypeInfo.Byte();
			}
			if (elementType2 == typeof(sbyte))
			{
				return ScalarArrayTypeInfo.SByte();
			}
			if (elementType2 == typeof(short))
			{
				return ScalarArrayTypeInfo.Int16();
			}
			if (elementType2 == typeof(ushort))
			{
				return ScalarArrayTypeInfo.UInt16();
			}
			if (elementType2 == typeof(int))
			{
				return ScalarArrayTypeInfo.Int32();
			}
			if (elementType2 == typeof(uint))
			{
				return ScalarArrayTypeInfo.UInt32();
			}
			if (elementType2 == typeof(long))
			{
				return ScalarArrayTypeInfo.Int64();
			}
			if (elementType2 == typeof(ulong))
			{
				return ScalarArrayTypeInfo.UInt64();
			}
			if (elementType2 == typeof(char))
			{
				return ScalarArrayTypeInfo.Char();
			}
			if (elementType2 == typeof(double))
			{
				return ScalarArrayTypeInfo.Double();
			}
			if (elementType2 == typeof(float))
			{
				return ScalarArrayTypeInfo.Single();
			}
			if (elementType2 == typeof(IntPtr))
			{
				return ScalarArrayTypeInfo.IntPtr();
			}
			if (elementType2 == typeof(UIntPtr))
			{
				return ScalarArrayTypeInfo.UIntPtr();
			}
			if (elementType2 == typeof(Guid))
			{
				return ScalarArrayTypeInfo.Guid();
			}
			return new ArrayTypeInfo(dataType, System.Diagnostics.Tracing.TraceLoggingTypeInfo.GetInstance(elementType2, recursionCheck));
		}
		if (IsEnum(dataType))
		{
			dataType = Enum.GetUnderlyingType(dataType);
		}
		if (dataType == typeof(string))
		{
			return new System.Diagnostics.Tracing.StringTypeInfo();
		}
		if (dataType == typeof(bool))
		{
			return ScalarTypeInfo.Boolean();
		}
		if (dataType == typeof(byte))
		{
			return ScalarTypeInfo.Byte();
		}
		if (dataType == typeof(sbyte))
		{
			return ScalarTypeInfo.SByte();
		}
		if (dataType == typeof(short))
		{
			return ScalarTypeInfo.Int16();
		}
		if (dataType == typeof(ushort))
		{
			return ScalarTypeInfo.UInt16();
		}
		if (dataType == typeof(int))
		{
			return ScalarTypeInfo.Int32();
		}
		if (dataType == typeof(uint))
		{
			return ScalarTypeInfo.UInt32();
		}
		if (dataType == typeof(long))
		{
			return ScalarTypeInfo.Int64();
		}
		if (dataType == typeof(ulong))
		{
			return ScalarTypeInfo.UInt64();
		}
		if (dataType == typeof(char))
		{
			return ScalarTypeInfo.Char();
		}
		if (dataType == typeof(double))
		{
			return ScalarTypeInfo.Double();
		}
		if (dataType == typeof(float))
		{
			return ScalarTypeInfo.Single();
		}
		if (dataType == typeof(DateTime))
		{
			return new System.Diagnostics.Tracing.DateTimeTypeInfo();
		}
		if (dataType == typeof(decimal))
		{
			return new System.Diagnostics.Tracing.DecimalTypeInfo();
		}
		if (dataType == typeof(IntPtr))
		{
			return ScalarTypeInfo.IntPtr();
		}
		if (dataType == typeof(UIntPtr))
		{
			return ScalarTypeInfo.UIntPtr();
		}
		if (dataType == typeof(Guid))
		{
			return ScalarTypeInfo.Guid();
		}
		if (dataType == typeof(TimeSpan))
		{
			return new System.Diagnostics.Tracing.TimeSpanTypeInfo();
		}
		if (dataType == typeof(DateTimeOffset))
		{
			return new System.Diagnostics.Tracing.DateTimeOffsetTypeInfo();
		}
		if (dataType == typeof(System.Diagnostics.Tracing.EmptyStruct))
		{
			return new NullTypeInfo();
		}
		if (IsGenericMatch(dataType, typeof(Nullable<>)))
		{
			return new NullableTypeInfo(dataType, recursionCheck);
		}
		Type elementType = FindEnumerableElementType(dataType);
		if (elementType != null)
		{
			return new EnumerableTypeInfo(dataType, System.Diagnostics.Tracing.TraceLoggingTypeInfo.GetInstance(elementType, recursionCheck));
		}
		throw new ArgumentException("SR.Format(SR.EventSource_NonCompliantTypeError, dataType.Name)");
	}
}
