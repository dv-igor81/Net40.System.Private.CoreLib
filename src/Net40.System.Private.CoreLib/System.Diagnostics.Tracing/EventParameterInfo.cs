namespace System.Diagnostics.Tracing;

internal struct EventParameterInfo
{
	internal string ParameterName;

	internal Type ParameterType;

	internal TraceLoggingTypeInfo TypeInfo;

	internal void SetInfo(string name, Type type, TraceLoggingTypeInfo typeInfo = null)
	{
		ParameterName = name;
		ParameterType = type;
		TypeInfo = typeInfo;
	}

	internal unsafe bool GenerateMetadata(byte* pMetadataBlob, ref uint offset, uint blobSize)
	{
		TypeCode typeCodeExtended = GetTypeCodeExtended(ParameterType);
		if (typeCodeExtended == TypeCode.Object)
		{
			EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, 1u);
			if (!(TypeInfo is InvokeTypeInfo { properties: var properties }))
			{
				return false;
			}
			if (properties != null)
			{
				EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, (uint)properties.Length);
				PropertyAnalysis[] array = properties;
				foreach (PropertyAnalysis property in array)
				{
					if (!GenerateMetadataForProperty(property, pMetadataBlob, ref offset, blobSize))
					{
						return false;
					}
				}
			}
			else
			{
				EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, 0u);
			}
			EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, '\0');
		}
		else
		{
			EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, (uint)typeCodeExtended);
			fixed (char* src = ParameterName)
			{
				EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, (byte*)src, (uint)((ParameterName.Length + 1) * 2));
			}
		}
		return true;
	}

	private static unsafe bool GenerateMetadataForProperty(PropertyAnalysis property, byte* pMetadataBlob, ref uint offset, uint blobSize)
	{
		if (property.typeInfo is InvokeTypeInfo invokeTypeInfo)
		{
			EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, 1u);
			PropertyAnalysis[] properties = invokeTypeInfo.properties;
			if (properties != null)
			{
				EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, (uint)properties.Length);
				PropertyAnalysis[] array = properties;
				foreach (PropertyAnalysis property2 in array)
				{
					if (!GenerateMetadataForProperty(property2, pMetadataBlob, ref offset, blobSize))
					{
						return false;
					}
				}
			}
			else
			{
				EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, 0u);
			}
			fixed (char* src = property.name)
			{
				EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, (byte*)src, (uint)((property.name.Length + 1) * 2));
			}
		}
		else
		{
			TypeCode typeCodeExtended = GetTypeCodeExtended(property.typeInfo.DataType);
			if (typeCodeExtended == TypeCode.Object)
			{
				return false;
			}
			EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, (uint)typeCodeExtended);
			fixed (char* src2 = property.name)
			{
				EventPipeMetadataGenerator.WriteToBuffer(pMetadataBlob, blobSize, ref offset, (byte*)src2, (uint)((property.name.Length + 1) * 2));
			}
		}
		return true;
	}

	internal int GetMetadataLength()
	{
		int num = 0;
		TypeCode typeCodeExtended = GetTypeCodeExtended(ParameterType);
		if (typeCodeExtended == TypeCode.Object)
		{
			if (!(TypeInfo is InvokeTypeInfo invokeTypeInfo))
			{
				return -1;
			}
			num += 8;
			PropertyAnalysis[] properties = invokeTypeInfo.properties;
			if (properties != null)
			{
				PropertyAnalysis[] array = properties;
				foreach (PropertyAnalysis property in array)
				{
					num += (int)GetMetadataLengthForProperty(property);
				}
			}
			return num + 2;
		}
		return num + (4 + (ParameterName.Length + 1) * 2);
	}

	private static uint GetMetadataLengthForProperty(PropertyAnalysis property)
	{
		uint num = 0u;
		if (property.typeInfo is InvokeTypeInfo invokeTypeInfo)
		{
			num += 8;
			PropertyAnalysis[] properties = invokeTypeInfo.properties;
			if (properties != null)
			{
				PropertyAnalysis[] array = properties;
				foreach (PropertyAnalysis property2 in array)
				{
					num += GetMetadataLengthForProperty(property2);
				}
			}
			return num + (uint)((property.name.Length + 1) * 2);
		}
		return num + (uint)(4 + (property.name.Length + 1) * 2);
	}

	private static TypeCode GetTypeCodeExtended(Type parameterType)
	{
		if (parameterType == typeof(Guid))
		{
			return (TypeCode)17;
		}
		if (parameterType == typeof(IntPtr))
		{
			if (IntPtr.Size != 4)
			{
				return TypeCode.Int64;
			}
			return TypeCode.Int32;
		}
		if (parameterType == typeof(UIntPtr))
		{
			if (UIntPtr.Size != 4)
			{
				return TypeCode.UInt64;
			}
			return TypeCode.UInt32;
		}
		return Type.GetTypeCode(parameterType);
	}
}