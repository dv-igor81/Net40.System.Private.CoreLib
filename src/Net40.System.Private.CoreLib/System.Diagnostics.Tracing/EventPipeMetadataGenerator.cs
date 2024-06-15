using System.Reflection;

namespace System.Diagnostics.Tracing;

internal sealed class EventPipeMetadataGenerator
{
    public static EventPipeMetadataGenerator Instance = new EventPipeMetadataGenerator();

    private EventPipeMetadataGenerator()
    {
    }

    public byte[] GenerateEventMetadata(EventSource.EventMetadata eventMetadata)
    {
        ParameterInfo[] parameters = eventMetadata.Parameters;
        EventParameterInfo[] array = new EventParameterInfo[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            array[i].SetInfo(parameters[i].Name, parameters[i].ParameterType);
        }

        return GenerateMetadata(eventMetadata.Descriptor.EventId, eventMetadata.Name, eventMetadata.Descriptor.Keywords,
            eventMetadata.Descriptor.Level, eventMetadata.Descriptor.Version, array);
    }

    public byte[] GenerateEventMetadata(int eventId, string eventName, EventKeywords keywords, EventLevel level,
        uint version, TraceLoggingEventTypes eventTypes)
    {
        TraceLoggingTypeInfo[] typeInfos = eventTypes.typeInfos;
        string[] paramNames = eventTypes.paramNames;
        EventParameterInfo[] array = new EventParameterInfo[typeInfos.Length];
        for (int i = 0; i < typeInfos.Length; i++)
        {
            string name = string.Empty;
            if (paramNames != null)
            {
                name = paramNames[i];
            }

            array[i].SetInfo(name, typeInfos[i].DataType, typeInfos[i]);
        }

        return GenerateMetadata(eventId, eventName, (long)keywords, (uint)level, version, array);
    }

    private unsafe byte[] GenerateMetadata(int eventId, string eventName, long keywords, uint level, uint version,
        EventParameterInfo[] parameters)
    {
        byte[] array = null;
        try
        {
            uint num = (uint)(24 + (eventName.Length + 1) * 2);
            uint num2 = num;
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(EmptyStruct))
            {
                parameters = ArrayEx.Empty<EventParameterInfo>();
            }

            EventParameterInfo[] array2 = parameters;
            foreach (EventParameterInfo eventParameterInfo in array2)
            {
                int metadataLength = eventParameterInfo.GetMetadataLength();
                if (metadataLength < 0)
                {
                    parameters = ArrayEx.Empty<EventParameterInfo>();
                    num = num2;
                    break;
                }

                num += (uint)metadataLength;
            }

            array = new byte[num];
            fixed (byte* ptr = array)
            {
                uint offset = 0u;
                WriteToBuffer(ptr, num, ref offset, (uint)eventId);
                fixed (char* src = eventName)
                {
                    WriteToBuffer(ptr, num, ref offset, (byte*)src, (uint)((eventName.Length + 1) * 2));
                }

                WriteToBuffer(ptr, num, ref offset, keywords);
                WriteToBuffer(ptr, num, ref offset, version);
                WriteToBuffer(ptr, num, ref offset, level);
                WriteToBuffer(ptr, num, ref offset, (uint)parameters.Length);
                EventParameterInfo[] array3 = parameters;
                foreach (EventParameterInfo eventParameterInfo2 in array3)
                {
                    if (!eventParameterInfo2.GenerateMetadata(ptr, ref offset, num))
                    {
                        return GenerateMetadata(eventId, eventName, keywords, level, version,
                            ArrayEx.Empty<EventParameterInfo>());
                    }
                }
            }
        }
        catch
        {
            array = null;
        }

        return array;
    }

    internal static unsafe void WriteToBuffer(byte* buffer, uint bufferLength, ref uint offset, byte* src,
        uint srcLength)
    {
        for (int i = 0; i < srcLength; i++)
        {
            (buffer + offset)[i] = src[i];
        }

        offset += srcLength;
    }

    internal static unsafe void WriteToBuffer(byte* buffer, uint bufferLength, ref uint offset, uint value)
    {
        *(uint*)(buffer + offset) = value;
        offset += 4u;
    }

    internal static unsafe void WriteToBuffer(byte* buffer, uint bufferLength, ref uint offset, long value)
    {
        *(long*)(buffer + offset) = value;
        offset += 8u;
    }

    internal static unsafe void WriteToBuffer(byte* buffer, uint bufferLength, ref uint offset, char value)
    {
        *(char*)(buffer + offset) = value;
        offset += 2u;
    }
}