namespace System.Diagnostics.Tracing;

internal interface IEventProvider
{
    public struct EventData
    {
        internal ulong Ptr;

        internal uint Size;

        internal uint Reserved;
    }

    
    internal struct EVENT_FILTER_DESCRIPTOR
    {
        public long Ptr;

        public int Size;

        public int Type;
    }

    internal enum ActivityControl : uint
    {
        EVENT_ACTIVITY_CTRL_GET_ID = 1u,
        EVENT_ACTIVITY_CTRL_SET_ID,
        EVENT_ACTIVITY_CTRL_CREATE_ID,
        EVENT_ACTIVITY_CTRL_GET_SET_ID,
        EVENT_ACTIVITY_CTRL_CREATE_SET_ID
    }

    internal unsafe delegate void EtwEnableCallback(in Guid sourceId, int isEnabled, byte level, long matchAnyKeywords,
        long matchAllKeywords, EVENT_FILTER_DESCRIPTOR* filterData, void* callbackContext);

    unsafe uint EventRegister(EventSource eventSource, EtwEnableCallback enableCallback, void* callbackContext,
        ref long registrationHandle);

    uint EventUnregister(long registrationHandle);

    unsafe int EventWriteTransferWrapper(long registrationHandle, ref EventDescriptor eventDescriptor,
        IntPtr eventHandle, Guid* activityId, Guid* relatedActivityId, int userDataCount,
        EventData* userData);

    int EventActivityIdControl(ActivityControl ControlCode, ref Guid ActivityId);

    unsafe IntPtr DefineEventHandle(uint eventID, string eventName, long keywords, uint eventVersion, uint level,
        byte* pMetadata, uint metadataLength);
}