using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;

namespace System.Diagnostics.Tracing;

public class EventSource : IDisposable
{
    protected internal struct EventData
    {
        internal ulong m_Ptr;

        internal int m_Size;

        internal int m_Reserved;

        public unsafe IntPtr DataPointer
        {
            get { return (IntPtr)(void*)m_Ptr; }
            set { m_Ptr = (ulong)(void*)value; }
        }

        public int Size
        {
            get { return m_Size; }
            set { m_Size = value; }
        }

        internal int Reserved
        {
            get { return m_Reserved; }
            set { m_Reserved = value; }
        }

        internal unsafe void SetMetadata(byte* pointer, int size, int reserved)
        {
            m_Ptr = (ulong)pointer;
            m_Size = size;
            m_Reserved = reserved;
        }
    }

    private struct Sha1ForNonSecretPurposes
    {
        private long length;

        private uint[] w;

        private int pos;

        public void Start()
        {
            if (w == null)
            {
                w = new uint[85];
            }

            length = 0L;
            pos = 0;
            w[80] = 1732584193u;
            w[81] = 4023233417u;
            w[82] = 2562383102u;
            w[83] = 271733878u;
            w[84] = 3285377520u;
        }

        public void Append(byte input)
        {
            w[pos / 4] = (w[pos / 4] << 8) | input;
            if (64 == ++pos)
            {
                Drain();
            }
        }

        public void Append(byte[] input)
        {
            foreach (byte input2 in input)
            {
                Append(input2);
            }
        }

        public void Finish(byte[] output)
        {
            long num = length + 8 * pos;
            Append(128);
            while (pos != 56)
            {
                Append(0);
            }

            Append((byte)(num >> 56));
            Append((byte)(num >> 48));
            Append((byte)(num >> 40));
            Append((byte)(num >> 32));
            Append((byte)(num >> 24));
            Append((byte)(num >> 16));
            Append((byte)(num >> 8));
            Append((byte)num);
            int num2 = ((output.Length < 20) ? output.Length : 20);
            for (int i = 0; i != num2; i++)
            {
                uint num3 = w[80 + i / 4];
                output[i] = (byte)(num3 >> 24);
                w[80 + i / 4] = num3 << 8;
            }
        }

        private void Drain()
        {
            for (int i = 16; i != 80; i++)
            {
                w[i] = Rol1(w[i - 3] ^ w[i - 8] ^ w[i - 14] ^ w[i - 16]);
            }

            uint num = w[80];
            uint num2 = w[81];
            uint num3 = w[82];
            uint num4 = w[83];
            uint num5 = w[84];
            for (int j = 0; j != 20; j++)
            {
                uint num6 = (num2 & num3) | (~num2 & num4);
                uint num7 = Rol5(num) + num6 + num5 + 1518500249 + w[j];
                num5 = num4;
                num4 = num3;
                num3 = Rol30(num2);
                num2 = num;
                num = num7;
            }

            for (int k = 20; k != 40; k++)
            {
                uint num8 = num2 ^ num3 ^ num4;
                uint num9 = Rol5(num) + num8 + num5 + 1859775393 + w[k];
                num5 = num4;
                num4 = num3;
                num3 = Rol30(num2);
                num2 = num;
                num = num9;
            }

            for (int l = 40; l != 60; l++)
            {
                uint num10 = (num2 & num3) | (num2 & num4) | (num3 & num4);
                uint num11 = (uint)((int)(Rol5(num) + num10 + num5) + -1894007588) + w[l];
                num5 = num4;
                num4 = num3;
                num3 = Rol30(num2);
                num2 = num;
                num = num11;
            }

            for (int m = 60; m != 80; m++)
            {
                uint num12 = num2 ^ num3 ^ num4;
                uint num13 = (uint)((int)(Rol5(num) + num12 + num5) + -899497514) + w[m];
                num5 = num4;
                num4 = num3;
                num3 = Rol30(num2);
                num2 = num;
                num = num13;
            }

            w[80] += num;
            w[81] += num2;
            w[82] += num3;
            w[83] += num4;
            w[84] += num5;
            length += 512L;
            pos = 0;
        }

        private static uint Rol1(uint input)
        {
            return (input << 1) | (input >> 31);
        }

        private static uint Rol5(uint input)
        {
            return (input << 5) | (input >> 27);
        }

        private static uint Rol30(uint input)
        {
            return (input << 30) | (input >> 2);
        }
    }

    private class OverideEventProvider : EventProviderEx
    {
        private EventSource m_eventSource;

        private EventProviderType m_eventProviderType;

        public OverideEventProvider(EventSource eventSource, EventProviderType providerType)
            : base(/*providerType*/new Guid())
        {
            m_eventSource = eventSource;
            m_eventProviderType = providerType;
        }

        // protected override void OnControllerCommand(ControllerCommand command, IDictionary<string, string> arguments, int perEventSourceSessionId, int etwSessionId)
        // {
        // 	EventListener listener = null;
        // 	m_eventSource.SendCommand(listener, m_eventProviderType, perEventSourceSessionId, etwSessionId, (EventCommand)command, IsEnabled(), base.Level, base.MatchAnyKeyword, arguments);
        // }
    }

    internal struct EventMetadata
    {
        public EventDescriptor Descriptor;

        public IntPtr EventHandle;

        public EventTags Tags;

        public bool EnabledForAnyListener;

        public bool EnabledForETW;

        public bool EnabledForEventPipe;

        public bool HasRelatedActivityID;

        public byte TriggersActivityTracking;

        public string Name;

        public string Message;

        public ParameterInfo[] Parameters;

        public TraceLoggingEventTypes TraceLoggingEventTypes;

        public EventActivityOptions ActivityOptions;
    }

    private static readonly bool m_EventSourcePreventRecursion;

    private string m_name;

    internal int m_id;

    private Guid m_guid;

    internal volatile EventMetadata[] m_eventData;

    private volatile byte[] m_rawManifest;

    private EventHandler<EventCommandEventArgs> m_eventCommandExecuted;

    private EventSourceSettings m_config;

    private bool m_eventSourceDisposed;

    private bool m_eventSourceEnabled;

    internal EventLevel m_level;

    internal EventKeywords m_matchAnyKeyword;

    internal volatile EventDispatcher m_Dispatchers;

    private volatile OverideEventProvider m_etwProvider;

    private volatile OverideEventProvider m_eventPipeProvider;

    private bool m_completelyInited;

    private Exception m_constructionException;

    private byte m_outOfBandMessageCount;

    private EventCommandEventArgs m_deferredCommands;

    private string[] m_traits;

    internal static uint s_currentPid;

    [ThreadStatic] private static byte m_EventSourceExceptionRecurenceCount;

    [ThreadStatic] private static bool m_EventSourceInDecodeObject;

    internal volatile ulong[] m_channelData;

    private ActivityTracker m_activityTracker;

    internal const string s_ActivityStartSuffix = "Start";

    internal const string s_ActivityStopSuffix = "Stop";

    private static byte[] namespaceBytes;

    private byte[] providerMetadata;

    private readonly TraceLoggingEventHandleTable m_eventHandleTable = new TraceLoggingEventHandleTable();

    public static Guid CurrentThreadActivityId
    {
        get
        {
            Guid ActivityId = default(Guid);
            //UnsafeNativeMethods.ManifestEtw.EventActivityIdControl(UnsafeNativeMethods.ManifestEtw.ActivityControl.EVENT_ACTIVITY_CTRL_GET_ID, ref ActivityId);
            return ActivityId;
        }
    }

    public string Name => m_name;

    public Guid Guid => m_guid;

    public EventSourceSettings Settings => m_config;

    internal static Guid InternalCurrentThreadActivityId
    {
        get
        {
            Guid guid = CurrentThreadActivityId;
            if (guid == Guid.Empty)
            {
                guid = FallbackActivityId;
            }

            return guid;
        }
    }

    internal static Guid FallbackActivityId
    {
        get
        {
            int currentThreadId = AppDomain.GetCurrentThreadId();
            return new Guid((uint)currentThreadId, (ushort)s_currentPid, (ushort)(s_currentPid >> 16), 148, 27, 135,
                213, 166, 92, 54, 100);
        }
    }

    public Exception ConstructionException => m_constructionException;

    private bool IsDisposed => m_eventSourceDisposed;

    private bool ThrowOnEventWriteErrors
    {
        get { return (m_config & EventSourceSettings.ThrowOnEventWriteErrors) != 0; }
        set
        {
            if (value)
            {
                m_config |= EventSourceSettings.ThrowOnEventWriteErrors;
            }
            else
            {
                m_config &= ~EventSourceSettings.ThrowOnEventWriteErrors;
            }
        }
    }

    private bool SelfDescribingEvents
    {
        get { return (m_config & EventSourceSettings.EtwSelfDescribingEventFormat) != 0; }
        set
        {
            if (!value)
            {
                m_config |= EventSourceSettings.EtwManifestEventFormat;
                m_config &= ~EventSourceSettings.EtwSelfDescribingEventFormat;
            }
            else
            {
                m_config |= EventSourceSettings.EtwSelfDescribingEventFormat;
                m_config &= ~EventSourceSettings.EtwManifestEventFormat;
            }
        }
    }

    public event EventHandler<EventCommandEventArgs> EventCommandExecuted
    {
        add
        {
            m_eventCommandExecuted =
                (EventHandler<EventCommandEventArgs>)Delegate.Combine(m_eventCommandExecuted, value);
            for (EventCommandEventArgs eventCommandEventArgs = m_deferredCommands;
                 eventCommandEventArgs != null;
                 eventCommandEventArgs = eventCommandEventArgs.nextCommand)
            {
                value(this, eventCommandEventArgs);
            }
        }
        remove
        {
            m_eventCommandExecuted =
                (EventHandler<EventCommandEventArgs>)Delegate.Remove(m_eventCommandExecuted, value);
        }
    }

    // public static void SetCurrentThreadActivityId(Guid activityId)
    // {
    // 	if (TplEtwProvider.Log != null)
    // 	{
    // 		TplEtwProvider.Log.SetActivityId(activityId);
    // 	}
    // 	EventPipeInternal.EventActivityIdControl(2u, ref activityId);
    // 	UnsafeNativeMethods.ManifestEtw.EventActivityIdControl(UnsafeNativeMethods.ManifestEtw.ActivityControl.EVENT_ACTIVITY_CTRL_SET_ID, ref activityId);
    // }

    // public static void SetCurrentThreadActivityId(Guid activityId, out Guid oldActivityThatWillContinue)
    // {
    // 	oldActivityThatWillContinue = activityId;
    // 	EventPipeInternal.EventActivityIdControl(2u, ref oldActivityThatWillContinue);
    // 	UnsafeNativeMethods.ManifestEtw.EventActivityIdControl(UnsafeNativeMethods.ManifestEtw.ActivityControl.EVENT_ACTIVITY_CTRL_GET_SET_ID, ref oldActivityThatWillContinue);
    // 	if (TplEtwProvider.Log != null)
    // 	{
    // 		TplEtwProvider.Log.SetActivityId(activityId);
    // 	}
    // }

    private int GetParameterCount(EventMetadata eventData)
    {
        return eventData.Parameters.Length;
    }

    private Type GetDataType(EventMetadata eventData, int parameterId)
    {
        return eventData.Parameters[parameterId].ParameterType;
    }

    private static string GetResourceString(string key, params object[] args)
    {
        return SR.Format(SR.GetResourceString(key), args);
    }

    public bool IsEnabled()
    {
        return m_eventSourceEnabled;
    }

    public bool IsEnabled(EventLevel level, EventKeywords keywords)
    {
        return IsEnabled(level, keywords, EventChannel.None);
    }

    public bool IsEnabled(EventLevel level, EventKeywords keywords, EventChannel channel)
    {
        if (!m_eventSourceEnabled)
        {
            return false;
        }

        if (!IsEnabledCommon(m_eventSourceEnabled, m_level, m_matchAnyKeyword, level, keywords, channel))
        {
            return false;
        }

        return true;
    }

    public static Guid GetGuid(Type eventSourceType)
    {
        if (eventSourceType == null)
        {
            throw new ArgumentNullException("eventSourceType");
        }

        EventSourceAttribute eventSourceAttribute =
            (EventSourceAttribute)GetCustomAttributeHelper(eventSourceType, typeof(EventSourceAttribute));
        string name = eventSourceType.Name;
        if (eventSourceAttribute != null)
        {
            if (eventSourceAttribute.Guid != null)
            {
                Guid result = Guid.Empty;
                if (Guid.TryParse(eventSourceAttribute.Guid, out result))
                {
                    return result;
                }
            }

            if (eventSourceAttribute.Name != null)
            {
                name = eventSourceAttribute.Name;
            }
        }

        if (name == null)
        {
            throw new ArgumentException(SR.Argument_InvalidTypeName, "eventSourceType");
        }

        return GenerateGuidFromName(name.ToUpperInvariant());
    }

    public static string GetName(Type eventSourceType)
    {
        return GetName(eventSourceType, EventManifestOptions.None);
    }

    public static string GenerateManifest(Type eventSourceType, string assemblyPathToIncludeInManifest)
    {
        return GenerateManifest(eventSourceType, assemblyPathToIncludeInManifest, EventManifestOptions.None);
    }

    public static string GenerateManifest(Type eventSourceType, string assemblyPathToIncludeInManifest,
        EventManifestOptions flags)
    {
        if (eventSourceType == null)
        {
            throw new ArgumentNullException("eventSourceType");
        }

        byte[] array = CreateManifestAndDescriptors(eventSourceType, assemblyPathToIncludeInManifest, null, flags);
        if (array != null)
        {
            return Encoding.UTF8.GetString(array, 0, array.Length);
        }

        return null;
    }

    public static IEnumerable<EventSource> GetSources()
    {
        List<EventSource> list = new List<EventSource>();
        lock (EventListener.EventListenersLock)
        {
            foreach (WeakReference s_EventSource in EventListener.s_EventSources)
            {
                if (s_EventSource.Target is EventSource { IsDisposed: false } eventSource)
                {
                    list.Add(eventSource);
                }
            }

            return list;
        }
    }

    public static void SendCommand(EventSource eventSource, EventCommand command,
        IDictionary<string, string> commandArguments)
    {
        if (eventSource == null)
        {
            throw new ArgumentNullException("eventSource");
        }

        if (command <= EventCommand.Update && command != EventCommand.SendManifest)
        {
            throw new ArgumentException(SR.EventSource_InvalidCommand, "command");
        }

        eventSource.SendCommand(null, EventProviderType.ETW, 0, 0, command, enable: true, EventLevel.LogAlways,
            EventKeywords.None, commandArguments);
    }

    public string GetTrait(string key)
    {
        if (m_traits != null)
        {
            for (int i = 0; i < m_traits.Length - 1; i += 2)
            {
                if (m_traits[i] == key)
                {
                    return m_traits[i + 1];
                }
            }
        }

        return null;
    }

    public override string ToString()
    {
        return SR.Format(SR.EventSource_ToString, Name, Guid);
    }

    protected EventSource()
        : this(EventSourceSettings.EtwManifestEventFormat)
    {
    }

    protected EventSource(bool throwOnEventWriteErrors)
        : this(EventSourceSettings.EtwManifestEventFormat | (throwOnEventWriteErrors
            ? EventSourceSettings.ThrowOnEventWriteErrors
            : EventSourceSettings.Default))
    {
    }

    protected EventSource(EventSourceSettings settings)
        : this(settings, (string[])null)
    {
    }

    protected EventSource(EventSourceSettings settings, params string[] traits)
    {
        m_config = ValidateSettings(settings);
        GetMetadata(out var eventSourceGuid, out var eventSourceName, out var _, out var _);
        if (eventSourceGuid.Equals(Guid.Empty) || eventSourceName == null)
        {
            Type type = GetType();
            eventSourceGuid = GetGuid(type);
            eventSourceName = GetName(type);
        }

        Initialize(eventSourceGuid, eventSourceName, traits);
    }

    private unsafe void DefineEventPipeEvents()
    {
        if (SelfDescribingEvents)
        {
            return;
        }

        int num = m_eventData.Length;
        for (int i = 0; i < num; i++)
        {
            uint eventId = (uint)m_eventData[i].Descriptor.EventId;
            if (eventId != 0)
            {
                byte[] array = EventPipeMetadataGenerator.Instance.GenerateEventMetadata(m_eventData[i]);
                uint metadataLength = ((array != null) ? ((uint)array.Length) : 0u);
                string name = m_eventData[i].Name;
                long keywords = m_eventData[i].Descriptor.Keywords;
                uint version = m_eventData[i].Descriptor.Version;
                uint level = m_eventData[i].Descriptor.Level;
                fixed (byte* pMetadata = array)
                {
                    IntPtr eventHandle = m_eventPipeProvider.m_eventProvider.DefineEventHandle(eventId, name, keywords,
                        version, level, pMetadata, metadataLength);
                    m_eventData[i].EventHandle = eventHandle;
                }
            }
        }
    }

    internal virtual void GetMetadata(out Guid eventSourceGuid, out string eventSourceName,
        out EventMetadata[] eventData, out byte[] manifestBytes)
    {
        eventSourceGuid = Guid.Empty;
        eventSourceName = null;
        eventData = null;
        manifestBytes = null;
    }

    protected virtual void OnEventCommand(EventCommandEventArgs command)
    {
    }

    protected unsafe void WriteEvent(int eventId)
    {
        WriteEventCore(eventId, 0, null);
    }

    protected unsafe void WriteEvent(int eventId, int arg1)
    {
        if (m_eventSourceEnabled)
        {
            EventData* ptr = stackalloc EventData[1];
            ptr->DataPointer = (IntPtr)(&arg1);
            ptr->Size = 4;
            ptr->Reserved = 0;
            WriteEventCore(eventId, 1, ptr);
        }
    }

    protected unsafe void WriteEvent(int eventId, int arg1, int arg2)
    {
        if (m_eventSourceEnabled)
        {
            EventData* ptr = stackalloc EventData[2];
            ptr->DataPointer = (IntPtr)(&arg1);
            ptr->Size = 4;
            ptr->Reserved = 0;
            ptr[1].DataPointer = (IntPtr)(&arg2);
            ptr[1].Size = 4;
            ptr[1].Reserved = 0;
            WriteEventCore(eventId, 2, ptr);
        }
    }

    protected unsafe void WriteEvent(int eventId, int arg1, int arg2, int arg3)
    {
        if (m_eventSourceEnabled)
        {
            EventData* ptr = stackalloc EventData[3];
            ptr->DataPointer = (IntPtr)(&arg1);
            ptr->Size = 4;
            ptr->Reserved = 0;
            ptr[1].DataPointer = (IntPtr)(&arg2);
            ptr[1].Size = 4;
            ptr[1].Reserved = 0;
            ptr[2].DataPointer = (IntPtr)(&arg3);
            ptr[2].Size = 4;
            ptr[2].Reserved = 0;
            WriteEventCore(eventId, 3, ptr);
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1)
    {
        if (m_eventSourceEnabled)
        {
            EventData* ptr = stackalloc EventData[1];
            ptr->DataPointer = (IntPtr)(&arg1);
            ptr->Size = 8;
            ptr->Reserved = 0;
            WriteEventCore(eventId, 1, ptr);
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1, long arg2)
    {
        if (m_eventSourceEnabled)
        {
            EventData* ptr = stackalloc EventData[2];
            ptr->DataPointer = (IntPtr)(&arg1);
            ptr->Size = 8;
            ptr->Reserved = 0;
            ptr[1].DataPointer = (IntPtr)(&arg2);
            ptr[1].Size = 8;
            ptr[1].Reserved = 0;
            WriteEventCore(eventId, 2, ptr);
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1, long arg2, long arg3)
    {
        if (m_eventSourceEnabled)
        {
            EventData* ptr = stackalloc EventData[3];
            ptr->DataPointer = (IntPtr)(&arg1);
            ptr->Size = 8;
            ptr->Reserved = 0;
            ptr[1].DataPointer = (IntPtr)(&arg2);
            ptr[1].Size = 8;
            ptr[1].Reserved = 0;
            ptr[2].DataPointer = (IntPtr)(&arg3);
            ptr[2].Size = 8;
            ptr[2].Reserved = 0;
            WriteEventCore(eventId, 3, ptr);
        }
    }

    protected unsafe void WriteEvent(int eventId, string arg1)
    {
        if (m_eventSourceEnabled)
        {
            if (arg1 == null)
            {
                arg1 = "";
            }

            fixed (char* ptr2 = arg1)
            {
                EventData* ptr = stackalloc EventData[1];
                ptr->DataPointer = (IntPtr)ptr2;
                ptr->Size = (arg1.Length + 1) * 2;
                ptr->Reserved = 0;
                WriteEventCore(eventId, 1, ptr);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string arg1, string arg2)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }

        if (arg1 == null)
        {
            arg1 = "";
        }

        if (arg2 == null)
        {
            arg2 = "";
        }

        fixed (char* ptr2 = arg1)
        {
            fixed (char* ptr3 = arg2)
            {
                EventData* ptr = stackalloc EventData[2];
                ptr->DataPointer = (IntPtr)ptr2;
                ptr->Size = (arg1.Length + 1) * 2;
                ptr->Reserved = 0;
                ptr[1].DataPointer = (IntPtr)ptr3;
                ptr[1].Size = (arg2.Length + 1) * 2;
                ptr[1].Reserved = 0;
                WriteEventCore(eventId, 2, ptr);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string arg1, string arg2, string arg3)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }

        if (arg1 == null)
        {
            arg1 = "";
        }

        if (arg2 == null)
        {
            arg2 = "";
        }

        if (arg3 == null)
        {
            arg3 = "";
        }

        fixed (char* ptr2 = arg1)
        {
            fixed (char* ptr3 = arg2)
            {
                fixed (char* ptr4 = arg3)
                {
                    EventData* ptr = stackalloc EventData[3];
                    ptr->DataPointer = (IntPtr)ptr2;
                    ptr->Size = (arg1.Length + 1) * 2;
                    ptr->Reserved = 0;
                    ptr[1].DataPointer = (IntPtr)ptr3;
                    ptr[1].Size = (arg2.Length + 1) * 2;
                    ptr[1].Reserved = 0;
                    ptr[2].DataPointer = (IntPtr)ptr4;
                    ptr[2].Size = (arg3.Length + 1) * 2;
                    ptr[2].Reserved = 0;
                    WriteEventCore(eventId, 3, ptr);
                }
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string arg1, int arg2)
    {
        if (m_eventSourceEnabled)
        {
            if (arg1 == null)
            {
                arg1 = "";
            }

            fixed (char* ptr2 = arg1)
            {
                EventData* ptr = stackalloc EventData[2];
                ptr->DataPointer = (IntPtr)ptr2;
                ptr->Size = (arg1.Length + 1) * 2;
                ptr->Reserved = 0;
                ptr[1].DataPointer = (IntPtr)(&arg2);
                ptr[1].Size = 4;
                ptr[1].Reserved = 0;
                WriteEventCore(eventId, 2, ptr);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string arg1, int arg2, int arg3)
    {
        if (m_eventSourceEnabled)
        {
            if (arg1 == null)
            {
                arg1 = "";
            }

            fixed (char* ptr2 = arg1)
            {
                EventData* ptr = stackalloc EventData[3];
                ptr->DataPointer = (IntPtr)ptr2;
                ptr->Size = (arg1.Length + 1) * 2;
                ptr->Reserved = 0;
                ptr[1].DataPointer = (IntPtr)(&arg2);
                ptr[1].Size = 4;
                ptr[1].Reserved = 0;
                ptr[2].DataPointer = (IntPtr)(&arg3);
                ptr[2].Size = 4;
                ptr[2].Reserved = 0;
                WriteEventCore(eventId, 3, ptr);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string arg1, long arg2)
    {
        if (m_eventSourceEnabled)
        {
            if (arg1 == null)
            {
                arg1 = "";
            }

            fixed (char* ptr2 = arg1)
            {
                EventData* ptr = stackalloc EventData[2];
                ptr->DataPointer = (IntPtr)ptr2;
                ptr->Size = (arg1.Length + 1) * 2;
                ptr->Reserved = 0;
                ptr[1].DataPointer = (IntPtr)(&arg2);
                ptr[1].Size = 8;
                ptr[1].Reserved = 0;
                WriteEventCore(eventId, 2, ptr);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1, string arg2)
    {
        if (m_eventSourceEnabled)
        {
            if (arg2 == null)
            {
                arg2 = "";
            }

            fixed (char* ptr2 = arg2)
            {
                EventData* ptr = stackalloc EventData[2];
                ptr->DataPointer = (IntPtr)(&arg1);
                ptr->Size = 8;
                ptr->Reserved = 0;
                ptr[1].DataPointer = (IntPtr)ptr2;
                ptr[1].Size = (arg2.Length + 1) * 2;
                ptr[1].Reserved = 0;
                WriteEventCore(eventId, 2, ptr);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, int arg1, string arg2)
    {
        if (m_eventSourceEnabled)
        {
            if (arg2 == null)
            {
                arg2 = "";
            }

            fixed (char* ptr2 = arg2)
            {
                EventData* ptr = stackalloc EventData[2];
                ptr->DataPointer = (IntPtr)(&arg1);
                ptr->Size = 4;
                ptr->Reserved = 0;
                ptr[1].DataPointer = (IntPtr)ptr2;
                ptr[1].Size = (arg2.Length + 1) * 2;
                ptr[1].Reserved = 0;
                WriteEventCore(eventId, 2, ptr);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, byte[] arg1)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }

        EventData* ptr = stackalloc EventData[2];
        if (arg1 == null || arg1.Length == 0)
        {
            int num = 0;
            ptr->DataPointer = (IntPtr)(&num);
            ptr->Size = 4;
            ptr->Reserved = 0;
            ptr[1].DataPointer = (IntPtr)(&num);
            ptr[1].Size = 0;
            ptr[1].Reserved = 0;
            WriteEventCore(eventId, 2, ptr);
            return;
        }

        int size = arg1.Length;
        fixed (byte* ptr2 = &arg1[0])
        {
            ptr->DataPointer = (IntPtr)(&size);
            ptr->Size = 4;
            ptr->Reserved = 0;
            ptr[1].DataPointer = (IntPtr)ptr2;
            ptr[1].Size = size;
            ptr[1].Reserved = 0;
            WriteEventCore(eventId, 2, ptr);
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1, byte[] arg2)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }

        EventData* ptr = stackalloc EventData[3];
        ptr->DataPointer = (IntPtr)(&arg1);
        ptr->Size = 8;
        ptr->Reserved = 0;
        if (arg2 == null || arg2.Length == 0)
        {
            int num = 0;
            ptr[1].DataPointer = (IntPtr)(&num);
            ptr[1].Size = 4;
            ptr[1].Reserved = 0;
            ptr[2].DataPointer = (IntPtr)(&num);
            ptr[2].Size = 0;
            ptr[2].Reserved = 0;
            WriteEventCore(eventId, 3, ptr);
            return;
        }

        int size = arg2.Length;
        fixed (byte* ptr2 = &arg2[0])
        {
            ptr[1].DataPointer = (IntPtr)(&size);
            ptr[1].Size = 4;
            ptr[1].Reserved = 0;
            ptr[2].DataPointer = (IntPtr)ptr2;
            ptr[2].Size = size;
            ptr[2].Reserved = 0;
            WriteEventCore(eventId, 3, ptr);
        }
    }

    [CLSCompliant(false)]
    protected unsafe void WriteEventCore(int eventId, int eventDataCount, EventData* data)
    {
        //WriteEventWithRelatedActivityIdCore(eventId, null, eventDataCount, data);
    }

    /*
    [CLSCompliant(false)]
    protected unsafe void WriteEventWithRelatedActivityIdCore(int eventId, Guid* relatedActivityId, int eventDataCount,
        EventData* data)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }

        try
        {
            if (relatedActivityId != null)
            {
                ValidateEventOpcodeForTransfer(ref m_eventData[eventId], m_eventData[eventId].Name);
            }

            EventOpcode opcode = (EventOpcode)m_eventData[eventId].Descriptor.Opcode;
            EventActivityOptions activityOptions = m_eventData[eventId].ActivityOptions;
            Guid* activityID = null;
            Guid activityId = Guid.Empty;
            Guid relatedActivityId2 = Guid.Empty;
            if (opcode != 0 && relatedActivityId == null && (activityOptions & EventActivityOptions.Disable) == 0)
            {
                switch (opcode)
                {
                    case EventOpcode.Start:
                        m_activityTracker.OnStart(m_name, m_eventData[eventId].Name,
                            m_eventData[eventId].Descriptor.Task, ref activityId, ref relatedActivityId2,
                            m_eventData[eventId].ActivityOptions);
                        break;
                    case EventOpcode.Stop:
                        m_activityTracker.OnStop(m_name, m_eventData[eventId].Name,
                            m_eventData[eventId].Descriptor.Task, ref activityId);
                        break;
                }

                if (activityId != Guid.Empty)
                {
                    activityID = &activityId;
                }

                if (relatedActivityId2 != Guid.Empty)
                {
                    relatedActivityId = &relatedActivityId2;
                }
            }

            if (m_eventData[eventId].EnabledForETW || m_eventData[eventId].EnabledForEventPipe)
            {
                if (!SelfDescribingEvents)
                {
                    if (!m_etwProvider.WriteEvent(ref m_eventData[eventId].Descriptor, m_eventData[eventId].EventHandle,
                            activityID, relatedActivityId, eventDataCount, (IntPtr)data))
                    {
                        ThrowEventSourceException(m_eventData[eventId].Name);
                    }

                    if (!m_eventPipeProvider.WriteEvent(ref m_eventData[eventId].Descriptor,
                            m_eventData[eventId].EventHandle, activityID, relatedActivityId, eventDataCount,
                            (IntPtr)data))
                    {
                        ThrowEventSourceException(m_eventData[eventId].Name);
                    }
                }
                else
                {
                    TraceLoggingEventTypes traceLoggingEventTypes = m_eventData[eventId].TraceLoggingEventTypes;
                    if (traceLoggingEventTypes == null)
                    {
                        traceLoggingEventTypes = new TraceLoggingEventTypes(m_eventData[eventId].Name,
                            m_eventData[eventId].Tags, m_eventData[eventId].Parameters);
                        Interlocked.CompareExchange(ref m_eventData[eventId].TraceLoggingEventTypes,
                            traceLoggingEventTypes, null);
                    }

                    EventSourceOptions eventSourceOptions = default(EventSourceOptions);
                    eventSourceOptions.Keywords = (EventKeywords)m_eventData[eventId].Descriptor.Keywords;
                    eventSourceOptions.Level = (EventLevel)m_eventData[eventId].Descriptor.Level;
                    eventSourceOptions.Opcode = (EventOpcode)m_eventData[eventId].Descriptor.Opcode;
                    EventSourceOptions options = eventSourceOptions;
                    WriteMultiMerge(m_eventData[eventId].Name, ref options, traceLoggingEventTypes, activityID,
                        relatedActivityId, data);
                }
            }

            if (m_Dispatchers != null && m_eventData[eventId].EnabledForAnyListener)
            {
                WriteToAllListeners(eventId, activityID, relatedActivityId, eventDataCount, data);
            }
        }
        catch (Exception ex)
        {
            if (ex is EventSourceException)
            {
                throw;
            }

            ThrowEventSourceException(m_eventData[eventId].Name, ex);
        }
    }
    */

    protected unsafe void WriteEvent(int eventId, params object[] args)
    {
        WriteEventVarargs(eventId, null, args);
    }

    protected unsafe void WriteEventWithRelatedActivityId(int eventId, Guid relatedActivityId, params object[] args)
    {
        WriteEventVarargs(eventId, &relatedActivityId, args);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (m_eventSourceEnabled)
            {
                try
                {
                    SendManifest(m_rawManifest);
                }
                catch (Exception)
                {
                }

                m_eventSourceEnabled = false;
            }

            if (m_etwProvider != null)
            {
                m_etwProvider.Dispose();
                m_etwProvider = null;
            }

            if (m_eventPipeProvider != null)
            {
                m_eventPipeProvider.Dispose();
                m_eventPipeProvider = null;
            }
        }

        m_eventSourceEnabled = false;
        m_eventSourceDisposed = true;
    }

    ~EventSource()
    {
        Dispose(disposing: false);
    }

    private unsafe void WriteEventRaw(string eventName, ref EventDescriptor eventDescriptor, IntPtr eventHandle,
        Guid* activityID, Guid* relatedActivityID, int dataCount, IntPtr data)
    {
        /*if (m_etwProvider == null)
        {
            ThrowEventSourceException(eventName);
        }
        else if (!m_etwProvider.WriteEventRaw(ref eventDescriptor, eventHandle, activityID, relatedActivityID,
                     dataCount, data))
        {
            ThrowEventSourceException(eventName);
        }

        if (m_eventPipeProvider == null)
        {
            ThrowEventSourceException(eventName);
        }
        else if (!m_eventPipeProvider.WriteEventRaw(ref eventDescriptor, eventHandle, activityID, relatedActivityID,
                     dataCount, data))
        {
            ThrowEventSourceException(eventName);
        }*/
    }

    internal EventSource(Guid eventSourceGuid, string eventSourceName)
        : this(eventSourceGuid, eventSourceName, EventSourceSettings.EtwManifestEventFormat)
    {
    }

    internal EventSource(Guid eventSourceGuid, string eventSourceName, EventSourceSettings settings,
        string[] traits = null)
    {
        m_config = ValidateSettings(settings);
        Initialize(eventSourceGuid, eventSourceName, traits);
    }

    private void Initialize(Guid eventSourceGuid, string eventSourceName, string[] traits)
    {
        /*try
        {
            m_traits = traits;
            if (m_traits != null && m_traits.Length % 2 != 0)
            {
                throw new ArgumentException(SR.EventSource_TraitEven, "traits");
            }

            if (eventSourceGuid == Guid.Empty)
            {
                throw new ArgumentException(SR.EventSource_NeedGuid);
            }

            if (eventSourceName == null)
            {
                throw new ArgumentException(SR.EventSource_NeedName);
            }

            m_name = eventSourceName;
            m_guid = eventSourceGuid;
            m_activityTracker = ActivityTracker.Instance;
            InitializeProviderMetadata();
            OverideEventProvider overideEventProvider = new OverideEventProvider(this, EventProviderType.ETW);
            overideEventProvider.Register(this);
            OverideEventProvider overideEventProvider2 = new OverideEventProvider(this, EventProviderType.EventPipe);
            overideEventProvider2.Register(this);
            EventListener.AddEventSource(this);
            m_etwProvider = overideEventProvider;
            if (Name != "System.Diagnostics.Eventing.FrameworkEventSource" || Environment.IsWindows8OrAbove)
            {
                GCHandle gCHandle = GCHandle.Alloc(providerMetadata, GCHandleType.Pinned);
                IntPtr data = gCHandle.AddrOfPinnedObject();
                int num = m_etwProvider.SetInformation(UnsafeNativeMethods.ManifestEtw.EVENT_INFO_CLASS.SetTraits, data,
                    (uint)providerMetadata.Length);
                gCHandle.Free();
            }

            m_eventPipeProvider = overideEventProvider2;
            m_completelyInited = true;
        }
        catch (Exception ex)
        {
            if (m_constructionException == null)
            {
                m_constructionException = ex;
            }

            ReportOutOfBandMessage("ERROR: Exception during construction of EventSource " + Name + ": " + ex.Message,
                flush: true);
        }

        lock (EventListener.EventListenersLock)
        {
            for (EventCommandEventArgs eventCommandEventArgs = m_deferredCommands;
                 eventCommandEventArgs != null;
                 eventCommandEventArgs = eventCommandEventArgs.nextCommand)
            {
                DoCommand(eventCommandEventArgs);
            }
        }*/
    }

    private static string GetName(Type eventSourceType, EventManifestOptions flags)
    {
        if (eventSourceType == null)
        {
            throw new ArgumentNullException("eventSourceType");
        }

        EventSourceAttribute eventSourceAttribute =
            (EventSourceAttribute)GetCustomAttributeHelper(eventSourceType, typeof(EventSourceAttribute), flags);
        if (eventSourceAttribute != null && eventSourceAttribute.Name != null)
        {
            return eventSourceAttribute.Name;
        }

        return eventSourceType.Name;
    }

    private static Guid GenerateGuidFromName(string name)
    {
        if (namespaceBytes == null)
        {
            namespaceBytes = new byte[16]
            {
                72, 44, 45, 178, 195, 144, 71, 200, 135, 248,
                26, 21, 191, 193, 48, 251
            };
        }

        byte[] array = Encoding.BigEndianUnicode.GetBytes(name);
        Sha1ForNonSecretPurposes sha1ForNonSecretPurposes = default(Sha1ForNonSecretPurposes);
        sha1ForNonSecretPurposes.Start();
        sha1ForNonSecretPurposes.Append(namespaceBytes);
        sha1ForNonSecretPurposes.Append(array);
        Array.Resize(ref array, 16);
        sha1ForNonSecretPurposes.Finish(array);
        array[7] = (byte)((array[7] & 0xFu) | 0x50u);
        return new Guid(array);
    }

    private unsafe object DecodeObject(int eventId, int parameterId, ref EventData* data)
    {
        IntPtr dataPointer = data->DataPointer;
        data++;
        Type type = GetDataType(m_eventData[eventId], parameterId);
        while (true)
        {
            if (type == typeof(IntPtr))
            {
                return *(IntPtr*)(void*)dataPointer;
            }

            if (type == typeof(int))
            {
                return *(int*)(void*)dataPointer;
            }

            if (type == typeof(uint))
            {
                return *(uint*)(void*)dataPointer;
            }

            if (type == typeof(long))
            {
                return *(long*)(void*)dataPointer;
            }

            if (type == typeof(ulong))
            {
                return *(ulong*)(void*)dataPointer;
            }

            if (type == typeof(byte))
            {
                return *(byte*)(void*)dataPointer;
            }

            if (type == typeof(sbyte))
            {
                return *(sbyte*)(void*)dataPointer;
            }

            if (type == typeof(short))
            {
                return *(short*)(void*)dataPointer;
            }

            if (type == typeof(ushort))
            {
                return *(ushort*)(void*)dataPointer;
            }

            if (type == typeof(float))
            {
                return *(float*)(void*)dataPointer;
            }

            if (type == typeof(double))
            {
                return *(double*)(void*)dataPointer;
            }

            if (type == typeof(decimal))
            {
                return *(decimal*)(void*)dataPointer;
            }

            if (type == typeof(bool))
            {
                if (*(int*)(void*)dataPointer == 1)
                {
                    return true;
                }

                return false;
            }

            if (type == typeof(Guid))
            {
                return *(Guid*)(void*)dataPointer;
            }

            if (type == typeof(char))
            {
                return *(char*)(void*)dataPointer;
            }

            if (type == typeof(DateTime))
            {
                long fileTime = *(long*)(void*)dataPointer;
                return DateTime.FromFileTimeUtc(fileTime);
            }

            if (type == typeof(byte[]))
            {
                int num = *(int*)(void*)dataPointer;
                byte[] array = new byte[num];
                dataPointer = data->DataPointer;
                data++;
                for (int i = 0; i < num; i++)
                {
                    array[i] = *(byte*)(void*)(dataPointer + i);
                }

                return array;
            }

            if (type == typeof(byte*))
            {
                return null;
            }

            if (m_EventSourcePreventRecursion && m_EventSourceInDecodeObject)
            {
                break;
            }

            try
            {
                m_EventSourceInDecodeObject = true;
                if (type.IsEnum())
                {
                    type = Enum.GetUnderlyingType(type);
                    continue;
                }

                if (dataPointer == IntPtr.Zero)
                {
                    return null;
                }

                return new string((char*)(void*)dataPointer);
            }
            finally
            {
                m_EventSourceInDecodeObject = false;
            }
        }

        return null;
    }

    private EventDispatcher GetDispatcher(EventListener listener)
    {
        EventDispatcher eventDispatcher;
        for (eventDispatcher = m_Dispatchers; eventDispatcher != null; eventDispatcher = eventDispatcher.m_Next)
        {
            if (eventDispatcher.m_Listener == listener)
            {
                return eventDispatcher;
            }
        }

        return eventDispatcher;
    }

    private unsafe void WriteEventVarargs(int eventId, Guid* childActivityID, object[] args)
    {
        // DIA
    }

    internal unsafe void WriteToAllListeners(int eventId, uint* osThreadId, DateTime* timeStamp, Guid* activityID,
        Guid* childActivityID, params object[] args)
    {
        EventWrittenEventArgs eventWrittenEventArgs = new EventWrittenEventArgs(this);
        eventWrittenEventArgs.EventId = eventId;
        if (osThreadId != null)
        {
            eventWrittenEventArgs.OSThreadId = (int)(*osThreadId);
        }

        if (timeStamp != null)
        {
            eventWrittenEventArgs.TimeStamp = *timeStamp;
        }

        if (activityID != null)
        {
            eventWrittenEventArgs.ActivityId = *activityID;
        }

        if (childActivityID != null)
        {
            eventWrittenEventArgs.RelatedActivityId = *childActivityID;
        }

        eventWrittenEventArgs.EventName = m_eventData[eventId].Name;
        eventWrittenEventArgs.Message = m_eventData[eventId].Message;
        eventWrittenEventArgs.Payload = new ReadOnlyCollection<object>(args);
        DispatchToAllListeners(eventId, childActivityID, eventWrittenEventArgs);
    }

    private unsafe void DispatchToAllListeners(int eventId, Guid* childActivityID,
        EventWrittenEventArgs eventCallbackArgs)
    {
        Exception ex = null;
        for (EventDispatcher eventDispatcher = m_Dispatchers;
             eventDispatcher != null;
             eventDispatcher = eventDispatcher.m_Next)
        {
            if (eventId == -1 || eventDispatcher.m_EventEnabled[eventId])
            {
                try
                {
                    eventDispatcher.m_Listener.OnEventWritten(eventCallbackArgs);
                }
                catch (Exception ex2)
                {
                    ReportOutOfBandMessage("ERROR: Exception during EventSource.OnEventWritten: " + ex2.Message,
                        flush: false);
                    ex = ex2;
                }
            }
        }

        if (ex != null)
        {
            throw new EventSourceException(ex);
        }
    }

    private unsafe void WriteEventString(EventLevel level, long keywords, string msgString)
    {
        // DIA
    }

    private void WriteStringToAllListeners(string eventName, string msg)
    {
        EventWrittenEventArgs eventWrittenEventArgs = new EventWrittenEventArgs(this);
        eventWrittenEventArgs.EventId = 0;
        eventWrittenEventArgs.Message = msg;
        eventWrittenEventArgs.Payload = new ReadOnlyCollection<object>(new List<object> { msg });
        eventWrittenEventArgs.PayloadNames = new ReadOnlyCollection<string>(new List<string> { "message" });
        eventWrittenEventArgs.EventName = eventName;
        for (EventDispatcher eventDispatcher = m_Dispatchers;
             eventDispatcher != null;
             eventDispatcher = eventDispatcher.m_Next)
        {
            bool flag = false;
            if (eventDispatcher.m_EventEnabled == null)
            {
                flag = true;
            }
            else
            {
                for (int i = 0; i < eventDispatcher.m_EventEnabled.Length; i++)
                {
                    if (eventDispatcher.m_EventEnabled[i])
                    {
                        flag = true;
                        break;
                    }
                }
            }

            try
            {
                if (flag)
                {
                    eventDispatcher.m_Listener.OnEventWritten(eventWrittenEventArgs);
                }
            }
            catch
            {
            }
        }
    }

    private bool IsEnabledByDefault(int eventNum, bool enable, EventLevel currentLevel,
        EventKeywords currentMatchAnyKeyword)
    {
        if (!enable)
        {
            return false;
        }

        EventLevel level = (EventLevel)m_eventData[eventNum].Descriptor.Level;
        EventKeywords eventKeywords =
            (EventKeywords)(m_eventData[eventNum].Descriptor.Keywords & (long)(~SessionMask.All.ToEventKeywords()));
        EventChannel channel = (EventChannel)m_eventData[eventNum].Descriptor.Channel;
        return IsEnabledCommon(enable, currentLevel, currentMatchAnyKeyword, level, eventKeywords, channel);
    }

    private bool IsEnabledCommon(bool enabled, EventLevel currentLevel, EventKeywords currentMatchAnyKeyword,
        EventLevel eventLevel, EventKeywords eventKeywords, EventChannel eventChannel)
    {
        if (!enabled)
        {
            return false;
        }

        if (currentLevel != 0 && currentLevel < eventLevel)
        {
            return false;
        }

        if (currentMatchAnyKeyword != EventKeywords.None && eventKeywords != EventKeywords.None)
        {
            if (eventChannel != 0 && m_channelData != null && m_channelData.Length > (int)eventChannel)
            {
                EventKeywords eventKeywords2 =
                    (EventKeywords)((long)m_channelData[(uint)eventChannel] | (long)eventKeywords);
                if (eventKeywords2 != EventKeywords.None &&
                    (eventKeywords2 & currentMatchAnyKeyword) == EventKeywords.None)
                {
                    return false;
                }
            }
            else if ((eventKeywords & currentMatchAnyKeyword) == EventKeywords.None)
            {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowEventSourceException(string eventName, Exception innerEx = null)
    {
        // DIA
    }

    private void ValidateEventOpcodeForTransfer(ref EventMetadata eventData, string eventName)
    {
        if (eventData.Descriptor.Opcode != 9 && eventData.Descriptor.Opcode != 240 && eventData.Descriptor.Opcode != 1)
        {
            ThrowEventSourceException(eventName);
        }
    }

    internal static EventOpcode GetOpcodeWithDefault(EventOpcode opcode, string eventName)
    {
        if (opcode == EventOpcode.Info && eventName != null)
        {
            if (eventName.EndsWith("Start", StringComparison.Ordinal))
            {
                return EventOpcode.Start;
            }

            if (eventName.EndsWith("Stop", StringComparison.Ordinal))
            {
                return EventOpcode.Stop;
            }
        }

        return opcode;
    }

    internal void SendCommand(EventListener listener, EventProviderType eventProviderType, int perEventSourceSessionId,
        int etwSessionId, EventCommand command, bool enable, EventLevel level, EventKeywords matchAnyKeyword,
        IDictionary<string, string> commandArguments)
    {
        EventCommandEventArgs eventCommandEventArgs = new EventCommandEventArgs(command, commandArguments, this,
            listener, eventProviderType, perEventSourceSessionId, etwSessionId, enable, level, matchAnyKeyword);
        lock (EventListener.EventListenersLock)
        {
            if (m_completelyInited)
            {
                m_deferredCommands = null;
                DoCommand(eventCommandEventArgs);
                return;
            }

            if (m_deferredCommands == null)
            {
                m_deferredCommands = eventCommandEventArgs;
                return;
            }

            EventCommandEventArgs eventCommandEventArgs2 = m_deferredCommands;
            while (eventCommandEventArgs2.nextCommand != null)
            {
                eventCommandEventArgs2 = eventCommandEventArgs2.nextCommand;
            }

            eventCommandEventArgs2.nextCommand = eventCommandEventArgs;
        }
    }

    internal void DoCommand(EventCommandEventArgs commandArgs)
    {
        if (m_etwProvider == null || m_eventPipeProvider == null)
        {
            return;
        }

        m_outOfBandMessageCount = 0;
        bool flag = commandArgs.perEventSourceSessionId > 0 && (long)commandArgs.perEventSourceSessionId <= 4L;
        try
        {
            EnsureDescriptorsInitialized();
            commandArgs.dispatcher = GetDispatcher(commandArgs.listener);
            if (commandArgs.dispatcher == null && commandArgs.listener != null)
            {
                throw new ArgumentException(SR.EventSource_ListenerNotFound);
            }

            if (commandArgs.Arguments == null)
            {
                commandArgs.Arguments = new Dictionary<string, string>();
            }

            if (commandArgs.Command == EventCommand.Update)
            {
                for (int i = 0; i < m_eventData.Length; i++)
                {
                    EnableEventForDispatcher(commandArgs.dispatcher, commandArgs.eventProviderType, i,
                        IsEnabledByDefault(i, commandArgs.enable, commandArgs.level, commandArgs.matchAnyKeyword));
                }

                if (commandArgs.enable)
                {
                    if (!m_eventSourceEnabled)
                    {
                        m_level = commandArgs.level;
                        m_matchAnyKeyword = commandArgs.matchAnyKeyword;
                    }
                    else
                    {
                        if (commandArgs.level > m_level)
                        {
                            m_level = commandArgs.level;
                        }

                        if (commandArgs.matchAnyKeyword == EventKeywords.None)
                        {
                            m_matchAnyKeyword = EventKeywords.None;
                        }
                        else if (m_matchAnyKeyword != EventKeywords.None)
                        {
                            m_matchAnyKeyword |= commandArgs.matchAnyKeyword;
                        }
                    }
                }

                bool flag2 = commandArgs.perEventSourceSessionId >= 0;
                if (commandArgs.perEventSourceSessionId == 0 && !commandArgs.enable)
                {
                    flag2 = false;
                }

                if (commandArgs.listener == null)
                {
                    if (!flag2)
                    {
                        commandArgs.perEventSourceSessionId = -commandArgs.perEventSourceSessionId;
                    }

                    commandArgs.perEventSourceSessionId--;
                }

                commandArgs.Command = (flag2 ? EventCommand.Enable : EventCommand.Disable);
                if (flag2 && commandArgs.dispatcher == null && !SelfDescribingEvents)
                {
                    SendManifest(m_rawManifest);
                }

                if (commandArgs.enable)
                {
                    m_eventSourceEnabled = true;
                }

                OnEventCommand(commandArgs);
                m_eventCommandExecuted?.Invoke(this, commandArgs);
                if (commandArgs.enable)
                {
                    return;
                }

                for (int j = 0; j < m_eventData.Length; j++)
                {
                    bool enabledForAnyListener = false;
                    for (EventDispatcher eventDispatcher = m_Dispatchers;
                         eventDispatcher != null;
                         eventDispatcher = eventDispatcher.m_Next)
                    {
                        if (eventDispatcher.m_EventEnabled[j])
                        {
                            enabledForAnyListener = true;
                            break;
                        }
                    }

                    m_eventData[j].EnabledForAnyListener = enabledForAnyListener;
                }

                if (!AnyEventEnabled())
                {
                    m_level = EventLevel.LogAlways;
                    m_matchAnyKeyword = EventKeywords.None;
                    m_eventSourceEnabled = false;
                }
            }
            else
            {
                OnEventCommand(commandArgs);
                m_eventCommandExecuted?.Invoke(this, commandArgs);
            }
        }
        catch (Exception ex)
        {
            ReportOutOfBandMessage("ERROR: Exception in Command Processing for EventSource " + Name + ": " + ex.Message,
                flush: true);
        }
    }

    internal bool EnableEventForDispatcher(EventDispatcher dispatcher, EventProviderType eventProviderType, int eventId,
        bool value)
    {
        if (dispatcher == null)
        {
            if (eventId >= m_eventData.Length)
            {
                return false;
            }

            if (m_etwProvider != null && eventProviderType == EventProviderType.ETW)
            {
                m_eventData[eventId].EnabledForETW = value;
            }

            if (m_eventPipeProvider != null && eventProviderType == EventProviderType.EventPipe)
            {
                m_eventData[eventId].EnabledForEventPipe = value;
            }
        }
        else
        {
            if (eventId >= dispatcher.m_EventEnabled.Length)
            {
                return false;
            }

            dispatcher.m_EventEnabled[eventId] = value;
            if (value)
            {
                m_eventData[eventId].EnabledForAnyListener = true;
            }
        }

        return true;
    }

    private bool AnyEventEnabled()
    {
        for (int i = 0; i < m_eventData.Length; i++)
        {
            if (m_eventData[i].EnabledForETW || m_eventData[i].EnabledForEventPipe ||
                m_eventData[i].EnabledForAnyListener)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureDescriptorsInitialized()
    {
        // DIA
    }

    private unsafe bool SendManifest(byte[] rawManifest)
    {
        return true;
    }

    internal static Attribute GetCustomAttributeHelper(MemberInfo member, Type attributeType,
        EventManifestOptions flags = EventManifestOptions.None)
    {
        if (!member.Module.Assembly.ReflectionOnly() && (flags & EventManifestOptions.AllowEventSourceOverride) == 0)
        {
            Attribute result = null;
            object[] customAttributes = member.GetCustomAttributes(attributeType, inherit: false);
            int num = 0;
            if (num < customAttributes.Length)
            {
                object obj = customAttributes[num];
                result = (Attribute)obj;
            }

            return result;
        }

        string fullName = attributeType.FullName;
        foreach (CustomAttributeData customAttribute in CustomAttributeData.GetCustomAttributes(member))
        {
            if (!AttributeTypeNamesMatch(attributeType, customAttribute.Constructor.ReflectedType))
            {
                continue;
            }

            Attribute attribute = null;
            if (customAttribute.ConstructorArguments.Count == 1)
            {
                attribute = (Attribute)Activator.CreateInstance(attributeType,
                    customAttribute.ConstructorArguments[0].Value);
            }
            else if (customAttribute.ConstructorArguments.Count == 0)
            {
                attribute = (Attribute)Activator.CreateInstance(attributeType);
            }

            if (attribute == null)
            {
                continue;
            }

            Type type = attribute.GetType();
            foreach (CustomAttributeNamedArgument namedArgument in customAttribute.NamedArguments)
            {
                PropertyInfo property = type.GetProperty(namedArgument.MemberInfo.Name,
                    BindingFlags.Instance | BindingFlags.Public);
                object obj2 = namedArgument.TypedValue.Value;
                if (property.PropertyType.IsEnum)
                {
                    obj2 = Enum.Parse(property.PropertyType, obj2.ToString());
                }

                property.SetValue(attribute, obj2, null);
            }

            return attribute;
        }

        return null;
    }

    private static bool AttributeTypeNamesMatch(Type attributeType, Type reflectedAttributeType)
    {
        if (!(attributeType == reflectedAttributeType) && !string.Equals(attributeType.FullName,
                reflectedAttributeType.FullName, StringComparison.Ordinal))
        {
            if (string.Equals(attributeType.Name, reflectedAttributeType.Name, StringComparison.Ordinal) &&
                attributeType.Namespace.EndsWith("Diagnostics.Tracing", StringComparison.Ordinal))
            {
                return reflectedAttributeType.Namespace.EndsWith("Diagnostics.Tracing", StringComparison.Ordinal);
            }

            return false;
        }

        return true;
    }

    private static Type GetEventSourceBaseType(Type eventSourceType, bool allowEventSourceOverride, bool reflectionOnly)
    {
        if (eventSourceType.BaseType() == null)
        {
            return null;
        }

        do
        {
            eventSourceType = eventSourceType.BaseType();
        } while (eventSourceType != null && eventSourceType.IsAbstract());

        if (eventSourceType != null)
        {
            if (!allowEventSourceOverride)
            {
                if ((reflectionOnly && eventSourceType.FullName != typeof(EventSource).FullName) ||
                    (!reflectionOnly && eventSourceType != typeof(EventSource)))
                {
                    return null;
                }
            }
            else if (eventSourceType.Name != "EventSource")
            {
                return null;
            }
        }

        return eventSourceType;
    }

    private static byte[] CreateManifestAndDescriptors(Type eventSourceType, string eventSourceDllName,
        EventSource source, EventManifestOptions flags = EventManifestOptions.None)
    {
        ManifestBuilder manifestBuilder = null;
        bool flag = source == null || !source.SelfDescribingEvents;
        Exception ex = null;
        byte[] result = null;
        if (eventSourceType.IsAbstract() && (flags & EventManifestOptions.Strict) == 0)
        {
            return null;
        }

        try
        {
            MethodInfo[] methods = eventSourceType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance |
                                                              BindingFlags.Public | BindingFlags.NonPublic);
            int num = 1;
            EventMetadata[] eventData = null;
            Dictionary<string, string> eventsByName = null;
            if (source != null || (flags & EventManifestOptions.Strict) != 0)
            {
                eventData = new EventMetadata[methods.Length + 1];
                eventData[0].Name = "";
            }

            ResourceManager resources = null;
            EventSourceAttribute eventSourceAttribute =
                (EventSourceAttribute)GetCustomAttributeHelper(eventSourceType, typeof(EventSourceAttribute), flags);
            if (eventSourceAttribute != null && eventSourceAttribute.LocalizationResources != null)
            {
                resources = new ResourceManager(eventSourceAttribute.LocalizationResources, eventSourceType.Assembly());
            }

            manifestBuilder = new ManifestBuilder(GetName(eventSourceType, flags), GetGuid(eventSourceType),
                eventSourceDllName, resources, flags);
            manifestBuilder.StartEvent("EventSourceMessage", new EventAttribute(0)
            {
                Level = EventLevel.LogAlways,
                Task = (EventTask)65534
            });
            manifestBuilder.AddEventParameter(typeof(string), "message");
            manifestBuilder.EndEvent();
            if ((flags & EventManifestOptions.Strict) != 0)
            {
                if (!(GetEventSourceBaseType(eventSourceType,
                        (flags & EventManifestOptions.AllowEventSourceOverride) != 0,
                        eventSourceType.Assembly().ReflectionOnly()) != null))
                {
                    manifestBuilder.ManifestError(SR.EventSource_TypeMustDeriveFromEventSource);
                }

                if (!eventSourceType.IsAbstract() && !eventSourceType.IsSealed())
                {
                    manifestBuilder.ManifestError(SR.EventSource_TypeMustBeSealedOrAbstract);
                }
            }

            string[] array = new string[3] { "Keywords", "Tasks", "Opcodes" };
            foreach (string text in array)
            {
                Type nestedType = eventSourceType.GetNestedType(text);
                if (!(nestedType != null))
                {
                    continue;
                }

                if (eventSourceType.IsAbstract())
                {
                    manifestBuilder.ManifestError(SR.Format(SR.EventSource_AbstractMustNotDeclareKTOC,
                        nestedType.Name));
                    continue;
                }

                FieldInfo[] fields = nestedType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Static |
                                                          BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo staticField in fields)
                {
                    AddProviderEnumKind(manifestBuilder, staticField, text);
                }
            }

            manifestBuilder.AddKeyword("Session3", 17592186044416uL);
            manifestBuilder.AddKeyword("Session2", 35184372088832uL);
            manifestBuilder.AddKeyword("Session1", 70368744177664uL);
            manifestBuilder.AddKeyword("Session0", 140737488355328uL);
            if (eventSourceType != typeof(EventSource))
            {
                foreach (MethodInfo methodInfo in methods)
                {
                    ParameterInfo[] args = methodInfo.GetParameters();
                    EventAttribute eventAttribute =
                        (EventAttribute)GetCustomAttributeHelper(methodInfo, typeof(EventAttribute), flags);
                    if (methodInfo.IsStatic)
                    {
                        continue;
                    }

                    if (eventSourceType.IsAbstract())
                    {
                        if (eventAttribute != null)
                        {
                            manifestBuilder.ManifestError(SR.Format(SR.EventSource_AbstractMustNotDeclareEventMethods,
                                methodInfo.Name, eventAttribute.EventId));
                        }

                        continue;
                    }

                    if (eventAttribute == null)
                    {
                        if (methodInfo.ReturnType != typeof(void) || methodInfo.IsVirtual ||
                            GetCustomAttributeHelper(methodInfo, typeof(NonEventAttribute), flags) != null)
                        {
                            continue;
                        }

                        EventAttribute eventAttribute2 = new EventAttribute(num);
                        eventAttribute = eventAttribute2;
                    }
                    else if (eventAttribute.EventId <= 0)
                    {
                        manifestBuilder.ManifestError(SR.Format(SR.EventSource_NeedPositiveId, methodInfo.Name),
                            runtimeCritical: true);
                        continue;
                    }

                    if (methodInfo.Name.LastIndexOf('.') >= 0)
                    {
                        manifestBuilder.ManifestError(SR.Format(SR.EventSource_EventMustNotBeExplicitImplementation,
                            methodInfo.Name, eventAttribute.EventId));
                    }

                    num++;
                    string name = methodInfo.Name;
                    if (eventAttribute.Opcode == EventOpcode.Info)
                    {
                        bool flag2 = eventAttribute.Task == EventTask.None;
                        if (flag2)
                        {
                            eventAttribute.Task = (EventTask)(65534 - eventAttribute.EventId);
                        }

                        if (!eventAttribute.IsOpcodeSet)
                        {
                            eventAttribute.Opcode = GetOpcodeWithDefault(EventOpcode.Info, name);
                        }

                        if (flag2)
                        {
                            if (eventAttribute.Opcode == EventOpcode.Start)
                            {
                                string text2 = name.Substring(0, name.Length - "Start".Length);
                                if (string.Compare(name, 0, text2, 0, text2.Length) == 0 && string.Compare(name,
                                        text2.Length, "Start", 0,
                                        Math.Max(name.Length - text2.Length, "Start".Length)) == 0)
                                {
                                    manifestBuilder.AddTask(text2, (int)eventAttribute.Task);
                                }
                            }
                            else if (eventAttribute.Opcode == EventOpcode.Stop)
                            {
                                int num2 = eventAttribute.EventId - 1;
                                if (eventData != null && num2 < eventData.Length)
                                {
                                    EventMetadata eventMetadata = eventData[num2];
                                    string text3 = name.Substring(0, name.Length - "Stop".Length);
                                    if (eventMetadata.Descriptor.Opcode == 1 &&
                                        string.Compare(eventMetadata.Name, 0, text3, 0, text3.Length) == 0 &&
                                        string.Compare(eventMetadata.Name, text3.Length, "Start", 0,
                                            Math.Max(eventMetadata.Name.Length - text3.Length, "Start".Length)) == 0)
                                    {
                                        eventAttribute.Task = (EventTask)eventMetadata.Descriptor.Task;
                                        flag2 = false;
                                    }
                                }

                                if (flag2 && (flags & EventManifestOptions.Strict) != 0)
                                {
                                    throw new ArgumentException(SR.EventSource_StopsFollowStarts);
                                }
                            }
                        }
                    }

                    bool hasRelatedActivityID = RemoveFirstArgIfRelatedActivityId(ref args);
                    if (source == null || !source.SelfDescribingEvents)
                    {
                        manifestBuilder.StartEvent(name, eventAttribute);
                        for (int l = 0; l < args.Length; l++)
                        {
                            manifestBuilder.AddEventParameter(args[l].ParameterType, args[l].Name);
                        }

                        manifestBuilder.EndEvent();
                    }

                    if (source != null || (flags & EventManifestOptions.Strict) != 0)
                    {
                        DebugCheckEvent(ref eventsByName, eventData, methodInfo, eventAttribute, manifestBuilder,
                            flags);

                        string key = "event_" + name;
                        string localizedMessage =
                            manifestBuilder.GetLocalizedMessage(key, CultureInfo.CurrentUICulture, etwFormat: false);
                        if (localizedMessage != null)
                        {
                            eventAttribute.Message = localizedMessage;
                        }

                        AddEventDescriptor(ref eventData, name, eventAttribute, args, hasRelatedActivityID);
                    }
                }
            }

            NameInfo.ReserveEventIDsBelow(num);
            if (source != null)
            {
                TrimEventDescriptors(ref eventData);
                source.m_eventData = eventData;
            }

            if (!eventSourceType.IsAbstract() && (source == null || !source.SelfDescribingEvents))
            {
                if (!flag && (flags & EventManifestOptions.Strict) == 0)
                {
                    return null;
                }

                result = manifestBuilder.CreateManifest();
            }
        }
        catch (Exception ex2)
        {
            if ((flags & EventManifestOptions.Strict) == 0)
            {
                throw;
            }

            ex = ex2;
        }

        if ((flags & EventManifestOptions.Strict) != 0 && (manifestBuilder.Errors.Count > 0 || ex != null))
        {
            string text4 = string.Empty;
            if (manifestBuilder.Errors.Count > 0)
            {
                bool flag3 = true;
                foreach (string error in manifestBuilder.Errors)
                {
                    if (!flag3)
                    {
                        text4 += Environment.NewLine;
                    }

                    flag3 = false;
                    text4 += error;
                }
            }
            else
            {
                text4 = "Unexpected error: " + ex.Message;
            }

            throw new ArgumentException(text4, ex);
        }

        if (!flag)
        {
            return null;
        }

        return result;
    }

    private static bool RemoveFirstArgIfRelatedActivityId(ref ParameterInfo[] args)
    {
        if (args.Length != 0 && args[0].ParameterType == typeof(Guid) &&
            string.Compare(args[0].Name, "relatedActivityId", StringComparison.OrdinalIgnoreCase) == 0)
        {
            ParameterInfo[] array = new ParameterInfo[args.Length - 1];
            Array.Copy(args, 1, array, 0, args.Length - 1);
            args = array;
            return true;
        }

        return false;
    }

    private static void AddProviderEnumKind(ManifestBuilder manifest, FieldInfo staticField, string providerEnumKind)
    {
        bool flag = staticField.Module.Assembly.ReflectionOnly();
        Type fieldType = staticField.FieldType;
        if ((!flag && fieldType == typeof(EventOpcode)) || AttributeTypeNamesMatch(fieldType, typeof(EventOpcode)))
        {
            if (!(providerEnumKind != "Opcodes"))
            {
                int value = (int)staticField.GetRawConstantValue();
                manifest.AddOpcode(staticField.Name, value);
                return;
            }
        }
        else if ((!flag && fieldType == typeof(EventTask)) || AttributeTypeNamesMatch(fieldType, typeof(EventTask)))
        {
            if (!(providerEnumKind != "Tasks"))
            {
                int value2 = (int)staticField.GetRawConstantValue();
                manifest.AddTask(staticField.Name, value2);
                return;
            }
        }
        else
        {
            if ((flag || !(fieldType == typeof(EventKeywords))) &&
                !AttributeTypeNamesMatch(fieldType, typeof(EventKeywords)))
            {
                return;
            }

            if (!(providerEnumKind != "Keywords"))
            {
                ulong value3 = (ulong)(long)staticField.GetRawConstantValue();
                manifest.AddKeyword(staticField.Name, value3);
                return;
            }
        }

        manifest.ManifestError(SR.Format(SR.EventSource_EnumKindMismatch, staticField.Name, staticField.FieldType.Name,
            providerEnumKind));
    }

    private static void AddEventDescriptor(ref EventMetadata[] eventData, string eventName,
        EventAttribute eventAttribute, ParameterInfo[] eventParameters, bool hasRelatedActivityID)
    {
        if (eventData == null || eventData.Length <= eventAttribute.EventId)
        {
            EventMetadata[] array = new EventMetadata[Math.Max(eventData.Length + 16, eventAttribute.EventId + 1)];
            Array.Copy(eventData, 0, array, 0, eventData.Length);
            eventData = array;
        }
        
        eventData[eventAttribute.EventId].Tags = eventAttribute.Tags;
        eventData[eventAttribute.EventId].Name = eventName;
        eventData[eventAttribute.EventId].Parameters = eventParameters;
        eventData[eventAttribute.EventId].Message = eventAttribute.Message;
        eventData[eventAttribute.EventId].ActivityOptions = eventAttribute.ActivityOptions;
        eventData[eventAttribute.EventId].HasRelatedActivityID = hasRelatedActivityID;
        eventData[eventAttribute.EventId].EventHandle = IntPtr.Zero;
    }

    private static void TrimEventDescriptors(ref EventMetadata[] eventData)
    {
        int num = eventData.Length;
        while (0 < num)
        {
            num--;
            if (eventData[num].Descriptor.EventId != 0)
            {
                break;
            }
        }

        if (eventData.Length - num > 2)
        {
            EventMetadata[] array = new EventMetadata[num + 1];
            Array.Copy(eventData, 0, array, 0, array.Length);
            eventData = array;
        }
    }

    internal void AddListener(EventListener listener)
    {
        lock (EventListener.EventListenersLock)
        {
            bool[] eventEnabled = null;
            if (m_eventData != null)
            {
                eventEnabled = new bool[m_eventData.Length];
            }

            m_Dispatchers = new EventDispatcher(m_Dispatchers, eventEnabled, listener);
            listener.OnEventSourceCreated(this);
        }
    }

    private static void DebugCheckEvent(ref Dictionary<string, string> eventsByName, EventMetadata[] eventData,
        MethodInfo method, EventAttribute eventAttribute, ManifestBuilder manifest, EventManifestOptions options)
    {
        int eventId = eventAttribute.EventId;
        string name = method.Name;
        int helperCallFirstArg = GetHelperCallFirstArg(method);
        if (helperCallFirstArg >= 0 && eventId != helperCallFirstArg)
        {
            manifest.ManifestError(SR.Format(SR.EventSource_MismatchIdToWriteEvent, name, eventId, helperCallFirstArg),
                runtimeCritical: true);
        }

        if (eventId < eventData.Length && eventData[eventId].Descriptor.EventId != 0)
        {
            manifest.ManifestError(SR.Format(SR.EventSource_EventIdReused, name, eventId, eventData[eventId].Name),
                runtimeCritical: true);
        }

        for (int i = 0; i < eventData.Length; i++)
        {
            if (eventData[i].Name != null && eventData[i].Descriptor.Task == (int)eventAttribute.Task &&
                (EventOpcode)eventData[i].Descriptor.Opcode == eventAttribute.Opcode)
            {
                manifest.ManifestError(SR.Format(SR.EventSource_TaskOpcodePairReused, name, eventId, eventData[i].Name,
                    i));
                if ((options & EventManifestOptions.Strict) == 0)
                {
                    break;
                }
            }
        }

        if (eventAttribute.Opcode != 0)
        {
            bool flag = false;
            if (eventAttribute.Task == EventTask.None)
            {
                flag = true;
            }
            else
            {
                EventTask eventTask = (EventTask)(65534 - eventId);
                if (eventAttribute.Opcode != EventOpcode.Start && eventAttribute.Opcode != EventOpcode.Stop &&
                    eventAttribute.Task == eventTask)
                {
                    flag = true;
                }
            }

            if (flag)
            {
                manifest.ManifestError(SR.Format(SR.EventSource_EventMustHaveTaskIfNonDefaultOpcode, name, eventId));
            }
        }

        if (eventsByName == null)
        {
            eventsByName = new Dictionary<string, string>();
        }

        if (eventsByName.ContainsKey(name))
        {
            manifest.ManifestError(SR.Format(SR.EventSource_EventNameReused, name), runtimeCritical: true);
        }

        eventsByName[name] = name;
    }

    private static int GetHelperCallFirstArg(MethodInfo method)
    {
        byte[] iLAsByteArray = method.GetMethodBody().GetILAsByteArray();
        int num = -1;
        for (int i = 0; i < iLAsByteArray.Length; i++)
        {
            switch (iLAsByteArray[i])
            {
                case 14:
                case 16:
                    i++;
                    continue;
                case 21:
                case 22:
                case 23:
                case 24:
                case 25:
                case 26:
                case 27:
                case 28:
                case 29:
                case 30:
                    if (i > 0 && iLAsByteArray[i - 1] == 2)
                    {
                        num = iLAsByteArray[i] - 22;
                    }

                    continue;
                case 31:
                    if (i > 0 && iLAsByteArray[i - 1] == 2)
                    {
                        num = iLAsByteArray[i + 1];
                    }

                    i++;
                    continue;
                case 32:
                    i += 4;
                    continue;
                case 40:
                    i += 4;
                    if (num >= 0)
                    {
                        for (int j = i + 1; j < iLAsByteArray.Length; j++)
                        {
                            if (iLAsByteArray[j] == 42)
                            {
                                return num;
                            }

                            if (iLAsByteArray[j] != 0)
                            {
                                break;
                            }
                        }
                    }

                    num = -1;
                    continue;
                case 44:
                case 45:
                    num = -1;
                    i++;
                    continue;
                case 57:
                case 58:
                    num = -1;
                    i += 4;
                    continue;
                case 140:
                case 141:
                    i += 4;
                    continue;
                case 254:
                    i++;
                    if (i < iLAsByteArray.Length && iLAsByteArray[i] < 6)
                    {
                        continue;
                    }

                    break;
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                case 13:
                case 20:
                case 37:
                case 103:
                case 104:
                case 105:
                case 106:
                case 109:
                case 110:
                case 162:
                    continue;
            }

            return -1;
        }

        return -1;
    }

    internal void ReportOutOfBandMessage(string msg, bool flush)
    {
        try
        {
            Debugger.Log(0, null, $"EventSource Error: {msg}{Environment.NewLine}");
            if (m_outOfBandMessageCount < 15)
            {
                m_outOfBandMessageCount++;
            }
            else
            {
                if (m_outOfBandMessageCount == 16)
                {
                    return;
                }

                m_outOfBandMessageCount = 16;
                msg = "Reached message limit.   End of EventSource error messages.";
            }

            WriteEventString(EventLevel.LogAlways, -1L, msg);
            WriteStringToAllListeners("EventSourceMessage", msg);
        }
        catch (Exception)
        {
        }
    }

    private EventSourceSettings ValidateSettings(EventSourceSettings settings)
    {
        EventSourceSettings eventSourceSettings = EventSourceSettings.EtwManifestEventFormat |
                                                  EventSourceSettings.EtwSelfDescribingEventFormat;
        if ((settings & eventSourceSettings) == eventSourceSettings)
        {
            throw new ArgumentException(SR.EventSource_InvalidEventFormat, "settings");
        }

        if ((settings & eventSourceSettings) == 0)
        {
            settings |= EventSourceSettings.EtwSelfDescribingEventFormat;
        }

        return settings;
    }

    public EventSource(string eventSourceName)
        : this(eventSourceName, EventSourceSettings.EtwSelfDescribingEventFormat)
    {
    }

    public EventSource(string eventSourceName, EventSourceSettings config)
        : this(eventSourceName, config, (string[])null)
    {
    }

    public EventSource(string eventSourceName, EventSourceSettings config, params string[] traits)
        : this((eventSourceName == null) ? default(Guid) : GenerateGuidFromName(eventSourceName.ToUpperInvariant()),
            eventSourceName, config, traits)
    {
        if (eventSourceName == null)
        {
            throw new ArgumentNullException("eventSourceName");
        }
    }

    public unsafe void Write(string eventName)
    {
        if (eventName == null)
        {
            throw new ArgumentNullException("eventName");
        }
    }

    public unsafe void Write(string eventName, EventSourceOptions options)
    {
        if (eventName == null)
        {
            throw new ArgumentNullException("eventName");
        }
    }
    

    public unsafe void Write<T>(string eventName, EventSourceOptions options, T data)
    {

    }

    public unsafe void Write<T>(string eventName, ref EventSourceOptions options, ref T data)
    {

    }
    

    private unsafe void WriteMultiMerge(string eventName, ref EventSourceOptions options,
        TraceLoggingEventTypes eventTypes, Guid* activityID, Guid* childActivityID, params object[] values)
    {
        if (IsEnabled())
        {
            byte level = (((options.valuesSet & 4u) != 0) ? options.level : eventTypes.level);
            EventKeywords keywords = ((((uint)options.valuesSet & (true ? 1u : 0u)) != 0)
                ? options.keywords
                : eventTypes.keywords);
            if (IsEnabled((EventLevel)level, keywords))
            {
                WriteMultiMergeInner(eventName, ref options, eventTypes, activityID, childActivityID, values);
            }
        }
    }

    private unsafe void WriteMultiMergeInner(string eventName, ref EventSourceOptions options,
        TraceLoggingEventTypes eventTypes, Guid* activityID, Guid* childActivityID, params object[] values)
    {
        int num = 0;
        byte level = (((options.valuesSet & 4u) != 0) ? options.level : eventTypes.level);
        byte opcode = (((options.valuesSet & 8u) != 0) ? options.opcode : eventTypes.opcode);
        EventTags tags = (((options.valuesSet & 2u) != 0) ? options.tags : eventTypes.Tags);
        EventKeywords keywords = ((((uint)options.valuesSet & (true ? 1u : 0u)) != 0)
            ? options.keywords
            : eventTypes.keywords);
        NameInfo nameInfo = eventTypes.GetNameInfo(eventName ?? eventTypes.Name, tags);
        if (nameInfo == null)
        {
            return;
        }

        num = nameInfo.identity;

        int pinCount = eventTypes.pinCount;
        byte* scratch = stackalloc byte[(int)(uint)eventTypes.scratchSize];
        EventData* ptr = stackalloc EventData[eventTypes.dataCount + 3];
        for (int i = 0; i < eventTypes.dataCount + 3; i++)
        {
            ptr[i] = default(EventData);
        }

        GCHandle* ptr2 = stackalloc GCHandle[pinCount];
        for (int j = 0; j < pinCount; j++)
        {
            ptr2[j] = default(GCHandle);
        }

        fixed (byte* pointer = providerMetadata)
        {
            fixed (byte* pointer2 = nameInfo.nameMetadata)
            {
                fixed (byte* pointer3 = eventTypes.typeMetadata)
                {
                    ptr->SetMetadata(pointer, providerMetadata.Length, 2);
                    ptr[1].SetMetadata(pointer2, nameInfo.nameMetadata.Length, 1);
                    ptr[2].SetMetadata(pointer3, eventTypes.typeMetadata.Length, 1);
                    RuntimeHelpers.PrepareConstrainedRegions();
                    try
                    {
                        DataCollector.ThreadInstance.Enable(scratch, eventTypes.scratchSize, ptr + 3,
                            eventTypes.dataCount, ptr2, pinCount);
                        for (int k = 0; k < eventTypes.typeInfos.Length; k++)
                        {
                            TraceLoggingTypeInfo traceLoggingTypeInfo = eventTypes.typeInfos[k];
                            traceLoggingTypeInfo.WriteData(TraceLoggingDataCollector.Instance,
                                traceLoggingTypeInfo.PropertyValueFactory(values[k]));
                        }
                        
                    }
                    finally
                    {
                        WriteCleanup(ptr2, pinCount);
                    }
                }
            }
        }
    }

    internal unsafe void WriteMultiMerge(string eventName, ref EventSourceOptions options,
        TraceLoggingEventTypes eventTypes, Guid* activityID, Guid* childActivityID, EventData* data)
    {
        if (!IsEnabled())
        {
            return;
        }

        fixed (EventSourceOptions* ptr2 = &options)
        {
            EventDescriptor descriptor;
            NameInfo nameInfo = UpdateDescriptor(eventName, eventTypes, ref options, out descriptor);
            if (nameInfo == null)
            {
                return;
            }

            int num = eventTypes.dataCount + eventTypes.typeInfos.Length * 2 + 3;
            EventData* ptr = stackalloc EventData[num];
            for (int i = 0; i < num; i++)
            {
                ptr[i] = default(EventData);
            }

            fixed (byte* pointer = providerMetadata)
            {
                fixed (byte* pointer2 = nameInfo.nameMetadata)
                {
                    fixed (byte* pointer3 = eventTypes.typeMetadata)
                    {
                        ptr->SetMetadata(pointer, providerMetadata.Length, 2);
                        ptr[1].SetMetadata(pointer2, nameInfo.nameMetadata.Length, 1);
                        ptr[2].SetMetadata(pointer3, eventTypes.typeMetadata.Length, 1);
                        int num2 = 3;
                        for (int j = 0; j < eventTypes.typeInfos.Length; j++)
                        {
                            ptr[num2].m_Ptr = data[j].m_Ptr;
                            ptr[num2].m_Size = data[j].m_Size;
                            if (data[j].m_Size == 4 && eventTypes.typeInfos[j].DataType == typeof(bool))
                            {
                                ptr[num2].m_Size = 1;
                            }

                            num2++;
                        }
                    }
                }
            }
        }
    }

    private unsafe void WriteImpl(string eventName, ref EventSourceOptions options, object data, Guid* pActivityId,
        Guid* pRelatedActivityId, TraceLoggingEventTypes eventTypes)
    {
        try
        {
            fixed (EventSourceOptions* ptr3 = &options)
            {
                options.Opcode =
                    (options.IsOpcodeSet ? options.Opcode : GetOpcodeWithDefault(options.Opcode, eventName));
                EventDescriptor descriptor;
                NameInfo nameInfo = UpdateDescriptor(eventName, eventTypes, ref options, out descriptor);
                if (nameInfo == null)
                {
                    return;
                }
                int pinCount = eventTypes.pinCount;
                byte* scratch = stackalloc byte[(int)(uint)eventTypes.scratchSize];
                EventData* ptr = stackalloc EventData[eventTypes.dataCount + 3];
                for (int i = 0; i < eventTypes.dataCount + 3; i++)
                {
                    ptr[i] = default(EventData);
                }

                GCHandle* ptr2 = stackalloc GCHandle[pinCount];
                for (int j = 0; j < pinCount; j++)
                {
                    ptr2[j] = default(GCHandle);
                }

                fixed (byte* pointer = providerMetadata)
                {
                    fixed (byte* pointer2 = nameInfo.nameMetadata)
                    {
                        fixed (byte* pointer3 = eventTypes.typeMetadata)
                        {
                            ptr->SetMetadata(pointer, providerMetadata.Length, 2);
                            ptr[1].SetMetadata(pointer2, nameInfo.nameMetadata.Length, 1);
                            ptr[2].SetMetadata(pointer3, eventTypes.typeMetadata.Length, 1);
                            RuntimeHelpers.PrepareConstrainedRegions();
                            EventOpcode opcode = (EventOpcode)descriptor.Opcode;
                            Guid activityId = Guid.Empty;
                            Guid relatedActivityId = Guid.Empty;
                            if (pActivityId == null && pRelatedActivityId == null &&
                                (options.ActivityOptions & EventActivityOptions.Disable) == 0)
                            {
                                switch (opcode)
                                {
                                    case EventOpcode.Start:
                                        m_activityTracker.OnStart(m_name, eventName, 0, ref activityId,
                                            ref relatedActivityId, options.ActivityOptions);
                                        break;
                                    case EventOpcode.Stop:
                                        m_activityTracker.OnStop(m_name, eventName, 0, ref activityId);
                                        break;
                                }

                                if (activityId != Guid.Empty)
                                {
                                    pActivityId = &activityId;
                                }

                                if (relatedActivityId != Guid.Empty)
                                {
                                    pRelatedActivityId = &relatedActivityId;
                                }
                            }

                            try
                            {
                                DataCollector.ThreadInstance.Enable(scratch, eventTypes.scratchSize, ptr + 3,
                                    eventTypes.dataCount, ptr2, pinCount);
                                TraceLoggingTypeInfo traceLoggingTypeInfo = eventTypes.typeInfos[0];
                                traceLoggingTypeInfo.WriteData(TraceLoggingDataCollector.Instance,
                                    traceLoggingTypeInfo.PropertyValueFactory(data));
                                if (m_Dispatchers != null)
                                {
                                    EventPayload payload = (EventPayload)eventTypes.typeInfos[0].GetData(data);
                                    WriteToAllListeners(eventName, ref descriptor, nameInfo.tags, pActivityId,
                                        pRelatedActivityId, payload);
                                }
                            }
                            catch (Exception ex)
                            {
                                if (ex is EventSourceException)
                                {
                                    throw;
                                }

                                ThrowEventSourceException(eventName, ex);
                            }
                            finally
                            {
                                WriteCleanup(ptr2, pinCount);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex2)
        {
            if (ex2 is EventSourceException)
            {
                throw;
            }

            ThrowEventSourceException(eventName, ex2);
        }
    }

    private unsafe void WriteToAllListeners(string eventName, ref EventDescriptor eventDescriptor, EventTags tags,
        Guid* pActivityId, Guid* pChildActivityId, EventPayload payload)
    {
        EventWrittenEventArgs eventWrittenEventArgs = new EventWrittenEventArgs(this);
        eventWrittenEventArgs.EventName = eventName;
        eventWrittenEventArgs.m_level = (EventLevel)eventDescriptor.Level;
        eventWrittenEventArgs.m_keywords = (EventKeywords)eventDescriptor.Keywords;
        eventWrittenEventArgs.m_opcode = (EventOpcode)eventDescriptor.Opcode;
        eventWrittenEventArgs.m_tags = tags;
        eventWrittenEventArgs.EventId = -1;
        if (pActivityId != null)
        {
            eventWrittenEventArgs.ActivityId = *pActivityId;
        }

        if (pChildActivityId != null)
        {
            eventWrittenEventArgs.RelatedActivityId = *pChildActivityId;
        }

        if (payload != null)
        {
            eventWrittenEventArgs.Payload = new ReadOnlyCollection<object>((IList<object>)payload.Values);
            eventWrittenEventArgs.PayloadNames = new ReadOnlyCollection<string>((IList<string>)payload.Keys);
        }

        DispatchToAllListeners(-1, pActivityId, eventWrittenEventArgs);
    }

    [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
    [NonEvent]
    private unsafe void WriteCleanup(GCHandle* pPins, int cPins)
    {
        DataCollector.ThreadInstance.Disable();
        for (int i = 0; i < cPins; i++)
        {
            if (pPins[i].IsAllocated)
            {
                pPins[i].Free();
            }
        }
    }

    private void InitializeProviderMetadata()
    {
        if (m_traits != null)
        {
            List<byte> list = new List<byte>(100);
            for (int i = 0; i < m_traits.Length - 1; i += 2)
            {
                if (!m_traits[i].StartsWith("ETW_", StringComparison.Ordinal))
                {
                    continue;
                }

                string text = m_traits[i].Substring(4);
                if (!byte.TryParse(text, out var result))
                {
                    if (!(text == "GROUP"))
                    {
                        throw new ArgumentException(SR.Format("SR.EventSource_UnknownEtwTrait", text), "traits");
                    }

                    result = 1;
                }

                string value = m_traits[i + 1];
                int count = list.Count;
                list.Add(0);
                list.Add(0);
                list.Add(result);
                int num = AddValueToMetaData(list, value) + 3;
                list[count] = (byte)num;
                list[count + 1] = (byte)(num >> 8);
            }

            providerMetadata = Statics.MetadataForString(Name, 0, list.Count, 0);
            int num2 = providerMetadata.Length - list.Count;
            {
                foreach (byte item in list)
                {
                    providerMetadata[num2++] = item;
                }

                return;
            }
        }

        providerMetadata = Statics.MetadataForString(Name, 0, 0, 0);
    }

    private static int AddValueToMetaData(List<byte> metaData, string value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        int count = metaData.Count;
        char c = value[0];
        switch (c)
        {
            case '@':
                metaData.AddRange(Encoding.UTF8.GetBytes(value.Substring(1)));
                break;
            case '{':
                metaData.AddRange(new Guid(value).ToByteArray());
                break;
            case '#':
            {
                for (int i = 1; i < value.Length; i++)
                {
                    if (value[i] != ' ')
                    {
                        if (i + 1 >= value.Length)
                        {
                            throw new ArgumentException("SR.EventSource_EvenHexDigits", "traits");
                        }

                        metaData.Add((byte)(HexDigit(value[i]) * 16 + HexDigit(value[i + 1])));
                        i++;
                    }
                }

                break;
            }
            default:
                if ('A' <= c || ' ' == c)
                {
                    metaData.AddRange(Encoding.UTF8.GetBytes(value));
                    break;
                }

                throw new ArgumentException(SR.Format("SR.EventSource_IllegalValue", value), "traits");
        }

        return metaData.Count - count;
    }

    private static int HexDigit(char c)
    {
        if ('0' <= c && c <= '9')
        {
            return c - 48;
        }

        if ('a' <= c)
        {
            c = (char)(c - 32);
        }

        if ('A' <= c && c <= 'F')
        {
            return c - 65 + 10;
        }

        throw new ArgumentException(SR.Format("SR.EventSource_BadHexDigit", c), "traits");
    }

    private NameInfo UpdateDescriptor(string name, TraceLoggingEventTypes eventInfo, ref EventSourceOptions options,
        out EventDescriptor descriptor)
    {
        NameInfo nameInfo = null;
        int traceloggingId = 0;
        byte level = (((options.valuesSet & 4u) != 0) ? options.level : eventInfo.level);
        byte opcode = (((options.valuesSet & 8u) != 0) ? options.opcode : eventInfo.opcode);
        EventTags tags = (((options.valuesSet & 2u) != 0) ? options.tags : eventInfo.Tags);
        EventKeywords keywords =
            ((((uint)options.valuesSet & (true ? 1u : 0u)) != 0) ? options.keywords : eventInfo.keywords);
        if (IsEnabled((EventLevel)level, keywords))
        {
            nameInfo = eventInfo.GetNameInfo(name ?? eventInfo.Name, tags);
            traceloggingId = nameInfo.identity;
        }

        descriptor = new EventDescriptor();
        return nameInfo;
    }
}


/*
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Eventing;
using System.Diagnostics.Tests;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace System.Diagnostics.Tracing;

public class EventSource : IDisposable
{
    protected internal struct EventData
    {
        internal ulong m_Ptr;

        internal int m_Size;

        internal int m_Reserved;

        public unsafe IntPtr DataPointer
        {
            get
            {
                return (IntPtr)(void*)m_Ptr;
            }
            set
            {
                m_Ptr = (ulong)(void*)value;
            }
        }

        public int Size
        {
            get
            {
                return m_Size;
            }
            set
            {
                m_Size = value;
            }
        }

        internal int Reserved
        {
            get
            {
                return m_Reserved;
            }
            set
            {
                m_Reserved = value;
            }
        }

        internal unsafe void SetMetadata(byte* pointer, int size, int reserved)
        {
            m_Ptr = (ulong)pointer;
            m_Size = size;
            m_Reserved = reserved;
        }
    }

    private struct Sha1ForNonSecretPurposes
    {
        private long length;

        private uint[] w;

        private int pos;

        public void Start()
        {
            if (w == null)
            {
                w = new uint[85];
            }
            length = 0L;
            pos = 0;
            w[80] = 1732584193u;
            w[81] = 4023233417u;
            w[82] = 2562383102u;
            w[83] = 271733878u;
            w[84] = 3285377520u;
        }

        public void Append(byte input)
        {
            w[pos / 4] = (w[pos / 4] << 8) | input;
            if (64 == ++pos)
            {
                Drain();
            }
        }

        public void Append(byte[] input)
        {
            foreach (byte b in input)
            {
                Append(b);
            }
        }

        public void Finish(byte[] output)
        {
            long j = length + 8 * pos;
            Append(128);
            while (pos != 56)
            {
                Append(0);
            }
            Append((byte)(j >> 56));
            Append((byte)(j >> 48));
            Append((byte)(j >> 40));
            Append((byte)(j >> 32));
            Append((byte)(j >> 24));
            Append((byte)(j >> 16));
            Append((byte)(j >> 8));
            Append((byte)j);
            int end = ((output.Length < 20) ? output.Length : 20);
            for (int i = 0; i != end; i++)
            {
                uint temp = w[80 + i / 4];
                output[i] = (byte)(temp >> 24);
                w[80 + i / 4] = temp << 8;
            }
        }

        private void Drain()
        {
            for (int i = 16; i != 80; i++)
            {
                w[i] = BitOperations.RotateLeft(w[i - 3] ^ w[i - 8] ^ w[i - 14] ^ w[i - 16], 1);
            }
            uint a = w[80];
            uint b = w[81];
            uint c = w[82];
            uint d = w[83];
            uint e = w[84];
            for (int m = 0; m != 20; m++)
            {
                uint f4 = (b & c) | (~b & d);
                uint temp4 = BitOperations.RotateLeft(a, 5) + f4 + e + 1518500249 + w[m];
                e = d;
                d = c;
                c = BitOperations.RotateLeft(b, 30);
                b = a;
                a = temp4;
            }
            for (int l = 20; l != 40; l++)
            {
                uint f3 = b ^ c ^ d;
                uint temp3 = BitOperations.RotateLeft(a, 5) + f3 + e + 1859775393 + w[l];
                e = d;
                d = c;
                c = BitOperations.RotateLeft(b, 30);
                b = a;
                a = temp3;
            }
            for (int k = 40; k != 60; k++)
            {
                uint f2 = (b & c) | (b & d) | (c & d);
                uint temp2 = (uint)((int)(BitOperations.RotateLeft(a, 5) + f2 + e) + -1894007588) + w[k];
                e = d;
                d = c;
                c = BitOperations.RotateLeft(b, 30);
                b = a;
                a = temp2;
            }
            for (int j = 60; j != 80; j++)
            {
                uint f = b ^ c ^ d;
                uint temp = (uint)((int)(BitOperations.RotateLeft(a, 5) + f + e) + -899497514) + w[j];
                e = d;
                d = c;
                c = BitOperations.RotateLeft(b, 30);
                b = a;
                a = temp;
            }
            w[80] += a;
            w[81] += b;
            w[82] += c;
            w[83] += d;
            w[84] += e;
            length += 512L;
            pos = 0;
        }
    }

    internal struct EventMetadata
    {
        public System.Diagnostics.Tracing.EventDescriptor Descriptor;

        public IntPtr EventHandle;

        public EventTags Tags;

        public bool EnabledForAnyListener;

        public bool EnabledForETW;

        public bool HasRelatedActivityID;

        public byte TriggersActivityTracking;

        public string Name;

        public string? Message;

        public ParameterInfo[] Parameters;

        public TraceLoggingEventTypes? TraceLoggingEventTypes;

        public EventActivityOptions ActivityOptions;
    }

    private static readonly bool m_EventSourcePreventRecursion;

    private string m_name = null;

    internal int m_id;

    private Guid m_guid;

    internal volatile EventMetadata[]? m_eventData;

    private volatile byte[]? m_rawManifest;

    private EventHandler<EventCommandEventArgs>? m_eventCommandExecuted;

    private EventSourceSettings m_config;

    private bool m_eventSourceDisposed;

    private bool m_eventSourceEnabled;

    internal EventLevel m_level;

    internal EventKeywords m_matchAnyKeyword;

    internal volatile System.Diagnostics.Tracing.EventDispatcher? m_Dispatchers;

    private bool m_completelyInited;

    private Exception? m_constructionException;

    private byte m_outOfBandMessageCount;

    private EventCommandEventArgs? m_deferredCommands;

    private string[]? m_traits;

    internal static uint s_currentPid;

    [ThreadStatic]
    private static byte m_EventSourceExceptionRecurenceCount;

    [ThreadStatic]
    private static bool m_EventSourceInDecodeObject;

    private System.Diagnostics.Tracing.ActivityTracker m_activityTracker = null;

    internal const string s_ActivityStartSuffix = "Start";

    internal const string s_ActivityStopSuffix = "Stop";

    private static byte[]? namespaceBytes;

    public string Name => m_name;

    public Guid Guid => m_guid;

    public EventSourceSettings Settings => m_config;

    public Exception? ConstructionException => m_constructionException;

    public static Guid CurrentThreadActivityId => default(Guid);

    private bool IsDisposed => m_eventSourceDisposed;

    private bool ThrowOnEventWriteErrors => (m_config & EventSourceSettings.ThrowOnEventWriteErrors) != 0;

    private bool SelfDescribingEvents
    {
        get
        {
            Debug.Assert((m_config & EventSourceSettings.EtwManifestEventFormat) != 0 != ((m_config & EventSourceSettings.EtwSelfDescribingEventFormat) != 0));
            return (m_config & EventSourceSettings.EtwSelfDescribingEventFormat) != 0;
        }
    }

    public event EventHandler<EventCommandEventArgs>? EventCommandExecuted
    {
        add
        {
            if (value != null)
            {
                m_eventCommandExecuted = (EventHandler<EventCommandEventArgs>)Delegate.Combine(m_eventCommandExecuted, value);
                for (EventCommandEventArgs deferredCommands = m_deferredCommands; deferredCommands != null; deferredCommands = deferredCommands.nextCommand)
                {
                    value(this, deferredCommands);
                }
            }
        }
        remove
        {
            m_eventCommandExecuted = (EventHandler<EventCommandEventArgs>)Delegate.Remove(m_eventCommandExecuted, value);
        }
    }

    public bool IsEnabled()
    {
        return m_eventSourceEnabled;
    }

    public bool IsEnabled(EventLevel level, EventKeywords keywords)
    {
        return IsEnabled(level, keywords, EventChannel.None);
    }

    public bool IsEnabled(EventLevel level, EventKeywords keywords, EventChannel channel)
    {
        if (!m_eventSourceEnabled)
        {
            return false;
        }
        if (!IsEnabledCommon(m_eventSourceEnabled, m_level, m_matchAnyKeyword, level, keywords, channel))
        {
            return false;
        }
        return true;
    }

    public static Guid GetGuid(Type eventSourceType)
    {
        if (eventSourceType == null)
        {
            throw new ArgumentNullException("eventSourceType");
        }
        EventSourceAttribute attrib = (EventSourceAttribute)GetCustomAttributeHelper(eventSourceType, typeof(EventSourceAttribute));
        string name = eventSourceType.Name;
        if (attrib != null)
        {
            if (attrib.Guid != null)
            {
                Guid g = Guid.Empty;
                if (Guid.TryParse(attrib.Guid, out g))
                {
                    return g;
                }
            }
            if (attrib.Name != null)
            {
                name = attrib.Name;
            }
        }
        if (name == null)
        {
            throw new ArgumentException(SR.Argument_InvalidTypeName, "eventSourceType");
        }
        return GenerateGuidFromName(name.ToUpperInvariant());
    }

    public static string GetName(Type eventSourceType)
    {
        return GetName(eventSourceType, EventManifestOptions.None);
    }

    public static string? GenerateManifest(Type eventSourceType, string? assemblyPathToIncludeInManifest)
    {
        return GenerateManifest(eventSourceType, assemblyPathToIncludeInManifest, EventManifestOptions.None);
    }

    public static string? GenerateManifest(Type eventSourceType, string? assemblyPathToIncludeInManifest, EventManifestOptions flags)
    {
        if (eventSourceType == null)
        {
            throw new ArgumentNullException("eventSourceType");
        }
        byte[] manifestBytes = CreateManifestAndDescriptors(eventSourceType, assemblyPathToIncludeInManifest, null, flags);
        return (manifestBytes == null) ? null : Encoding.UTF8.GetString(manifestBytes, 0, manifestBytes.Length);
    }

    public static IEnumerable<EventSource> GetSources()
    {
        List<EventSource> ret = new List<EventSource>();
        lock (EventListener.EventListenersLock)
        {
            Debug.Assert(EventListener.s_EventSources != null);
            foreach (WeakReference eventSourceRef in EventListener.s_EventSources)
            {
                if (eventSourceRef.Target is EventSource { IsDisposed: false } eventSource)
                {
                    ret.Add(eventSource);
                }
            }
        }
        return ret;
    }

    public static void SendCommand(EventSource eventSource, EventCommand command, IDictionary<string, string?>? commandArguments)
    {
        if (eventSource == null)
        {
            throw new ArgumentNullException("eventSource");
        }
        if (command <= EventCommand.Update && command != EventCommand.SendManifest)
        {
            throw new ArgumentException(SR.EventSource_InvalidCommand, "command");
        }
        eventSource.SendCommand(null, EventProviderType.ETW, 0, 0, command, enable: true, EventLevel.LogAlways, EventKeywords.None, commandArguments);
    }

    public string? GetTrait(string key)
    {
        if (m_traits != null)
        {
            for (int i = 0; i < m_traits.Length - 1; i += 2)
            {
                if (m_traits[i] == key)
                {
                    return m_traits[i + 1];
                }
            }
        }
        return null;
    }

    public override string ToString()
    {
        return SR.Format(SR.EventSource_ToString, Name, Guid);
    }

    protected EventSource()
        : this(EventSourceSettings.EtwManifestEventFormat)
    {
    }

    protected EventSource(bool throwOnEventWriteErrors)
        : this(EventSourceSettings.EtwManifestEventFormat | (throwOnEventWriteErrors ? EventSourceSettings.ThrowOnEventWriteErrors : EventSourceSettings.Default))
    {
    }

    protected EventSource(EventSourceSettings settings)
        : this(settings, (string[]?)null)
    {
    }

    protected EventSource(EventSourceSettings settings, params string[]? traits)
    {
        m_config = ValidateSettings(settings);
        GetMetadata(out Guid eventSourceGuid, out string eventSourceName, out EventMetadata[] _, out byte[] _);
        if (eventSourceGuid.Equals(Guid.Empty) || eventSourceName == null)
        {
            Type myType = GetType();
            eventSourceGuid = GetGuid(myType);
            eventSourceName = GetName(myType);
        }
        Initialize(eventSourceGuid, eventSourceName, traits);
    }

    internal virtual void GetMetadata(out Guid eventSourceGuid, out string? eventSourceName, out EventMetadata[]? eventData, out byte[]? manifestBytes)
    {
        eventSourceGuid = Guid.Empty;
        eventSourceName = null;
        eventData = null;
        manifestBytes = null;
    }

    protected virtual void OnEventCommand(EventCommandEventArgs command)
    {
    }

    protected unsafe void WriteEvent(int eventId)
    {
        WriteEventCore(eventId, 0, null);
    }

    protected unsafe void WriteEvent(int eventId, int arg1)
    {
        if (m_eventSourceEnabled)
        {
            EventData* descrs = stackalloc EventData[1];
            descrs->DataPointer = (IntPtr)(&arg1);
            descrs->Size = 4;
            descrs->Reserved = 0;
            WriteEventCore(eventId, 1, descrs);
        }
    }

    protected unsafe void WriteEvent(int eventId, int arg1, int arg2)
    {
        if (m_eventSourceEnabled)
        {
            EventData* descrs = stackalloc EventData[2];
            descrs->DataPointer = (IntPtr)(&arg1);
            descrs->Size = 4;
            descrs->Reserved = 0;
            descrs[1].DataPointer = (IntPtr)(&arg2);
            descrs[1].Size = 4;
            descrs[1].Reserved = 0;
            WriteEventCore(eventId, 2, descrs);
        }
    }

    protected unsafe void WriteEvent(int eventId, int arg1, int arg2, int arg3)
    {
        if (m_eventSourceEnabled)
        {
            EventData* descrs = stackalloc EventData[3];
            descrs->DataPointer = (IntPtr)(&arg1);
            descrs->Size = 4;
            descrs->Reserved = 0;
            descrs[1].DataPointer = (IntPtr)(&arg2);
            descrs[1].Size = 4;
            descrs[1].Reserved = 0;
            descrs[2].DataPointer = (IntPtr)(&arg3);
            descrs[2].Size = 4;
            descrs[2].Reserved = 0;
            WriteEventCore(eventId, 3, descrs);
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1)
    {
        if (m_eventSourceEnabled)
        {
            EventData* descrs = stackalloc EventData[1];
            descrs->DataPointer = (IntPtr)(&arg1);
            descrs->Size = 8;
            descrs->Reserved = 0;
            WriteEventCore(eventId, 1, descrs);
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1, long arg2)
    {
        if (m_eventSourceEnabled)
        {
            EventData* descrs = stackalloc EventData[2];
            descrs->DataPointer = (IntPtr)(&arg1);
            descrs->Size = 8;
            descrs->Reserved = 0;
            descrs[1].DataPointer = (IntPtr)(&arg2);
            descrs[1].Size = 8;
            descrs[1].Reserved = 0;
            WriteEventCore(eventId, 2, descrs);
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1, long arg2, long arg3)
    {
        if (m_eventSourceEnabled)
        {
            EventData* descrs = stackalloc EventData[3];
            descrs->DataPointer = (IntPtr)(&arg1);
            descrs->Size = 8;
            descrs->Reserved = 0;
            descrs[1].DataPointer = (IntPtr)(&arg2);
            descrs[1].Size = 8;
            descrs[1].Reserved = 0;
            descrs[2].DataPointer = (IntPtr)(&arg3);
            descrs[2].Size = 8;
            descrs[2].Reserved = 0;
            WriteEventCore(eventId, 3, descrs);
        }
    }

    protected unsafe void WriteEvent(int eventId, string? arg1)
    {
        if (m_eventSourceEnabled)
        {
            if (arg1 == null)
            {
                arg1 = "";
            }
            fixed (char* string1Bytes = arg1)
            {
                EventData* descrs = stackalloc EventData[1];
                descrs->DataPointer = (IntPtr)string1Bytes;
                descrs->Size = (arg1.Length + 1) * 2;
                descrs->Reserved = 0;
                WriteEventCore(eventId, 1, descrs);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string? arg1, string? arg2)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }
        if (arg1 == null)
        {
            arg1 = "";
        }
        if (arg2 == null)
        {
            arg2 = "";
        }
        fixed (char* string1Bytes = arg1)
        {
            fixed (char* string2Bytes = arg2)
            {
                EventData* descrs = stackalloc EventData[2];
                descrs->DataPointer = (IntPtr)string1Bytes;
                descrs->Size = (arg1.Length + 1) * 2;
                descrs->Reserved = 0;
                descrs[1].DataPointer = (IntPtr)string2Bytes;
                descrs[1].Size = (arg2.Length + 1) * 2;
                descrs[1].Reserved = 0;
                WriteEventCore(eventId, 2, descrs);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string? arg1, string? arg2, string? arg3)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }
        if (arg1 == null)
        {
            arg1 = "";
        }
        if (arg2 == null)
        {
            arg2 = "";
        }
        if (arg3 == null)
        {
            arg3 = "";
        }
        fixed (char* string1Bytes = arg1)
        {
            fixed (char* string2Bytes = arg2)
            {
                fixed (char* string3Bytes = arg3)
                {
                    EventData* descrs = stackalloc EventData[3];
                    descrs->DataPointer = (IntPtr)string1Bytes;
                    descrs->Size = (arg1.Length + 1) * 2;
                    descrs->Reserved = 0;
                    descrs[1].DataPointer = (IntPtr)string2Bytes;
                    descrs[1].Size = (arg2.Length + 1) * 2;
                    descrs[1].Reserved = 0;
                    descrs[2].DataPointer = (IntPtr)string3Bytes;
                    descrs[2].Size = (arg3.Length + 1) * 2;
                    descrs[2].Reserved = 0;
                    WriteEventCore(eventId, 3, descrs);
                }
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string? arg1, int arg2)
    {
        if (m_eventSourceEnabled)
        {
            if (arg1 == null)
            {
                arg1 = "";
            }
            fixed (char* string1Bytes = arg1)
            {
                EventData* descrs = stackalloc EventData[2];
                descrs->DataPointer = (IntPtr)string1Bytes;
                descrs->Size = (arg1.Length + 1) * 2;
                descrs->Reserved = 0;
                descrs[1].DataPointer = (IntPtr)(&arg2);
                descrs[1].Size = 4;
                descrs[1].Reserved = 0;
                WriteEventCore(eventId, 2, descrs);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string? arg1, int arg2, int arg3)
    {
        if (m_eventSourceEnabled)
        {
            if (arg1 == null)
            {
                arg1 = "";
            }
            fixed (char* string1Bytes = arg1)
            {
                EventData* descrs = stackalloc EventData[3];
                descrs->DataPointer = (IntPtr)string1Bytes;
                descrs->Size = (arg1.Length + 1) * 2;
                descrs->Reserved = 0;
                descrs[1].DataPointer = (IntPtr)(&arg2);
                descrs[1].Size = 4;
                descrs[1].Reserved = 0;
                descrs[2].DataPointer = (IntPtr)(&arg3);
                descrs[2].Size = 4;
                descrs[2].Reserved = 0;
                WriteEventCore(eventId, 3, descrs);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, string? arg1, long arg2)
    {
        if (m_eventSourceEnabled)
        {
            if (arg1 == null)
            {
                arg1 = "";
            }
            fixed (char* string1Bytes = arg1)
            {
                EventData* descrs = stackalloc EventData[2];
                descrs->DataPointer = (IntPtr)string1Bytes;
                descrs->Size = (arg1.Length + 1) * 2;
                descrs->Reserved = 0;
                descrs[1].DataPointer = (IntPtr)(&arg2);
                descrs[1].Size = 8;
                descrs[1].Reserved = 0;
                WriteEventCore(eventId, 2, descrs);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1, string? arg2)
    {
        if (m_eventSourceEnabled)
        {
            if (arg2 == null)
            {
                arg2 = "";
            }
            fixed (char* string2Bytes = arg2)
            {
                EventData* descrs = stackalloc EventData[2];
                descrs->DataPointer = (IntPtr)(&arg1);
                descrs->Size = 8;
                descrs->Reserved = 0;
                descrs[1].DataPointer = (IntPtr)string2Bytes;
                descrs[1].Size = (arg2.Length + 1) * 2;
                descrs[1].Reserved = 0;
                WriteEventCore(eventId, 2, descrs);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, int arg1, string? arg2)
    {
        if (m_eventSourceEnabled)
        {
            if (arg2 == null)
            {
                arg2 = "";
            }
            fixed (char* string2Bytes = arg2)
            {
                EventData* descrs = stackalloc EventData[2];
                descrs->DataPointer = (IntPtr)(&arg1);
                descrs->Size = 4;
                descrs->Reserved = 0;
                descrs[1].DataPointer = (IntPtr)string2Bytes;
                descrs[1].Size = (arg2.Length + 1) * 2;
                descrs[1].Reserved = 0;
                WriteEventCore(eventId, 2, descrs);
            }
        }
    }

    protected unsafe void WriteEvent(int eventId, byte[]? arg1)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }
        EventData* descrs = stackalloc EventData[2];
        if (arg1 == null || arg1.Length == 0)
        {
            int blobSize2 = 0;
            descrs->DataPointer = (IntPtr)(&blobSize2);
            descrs->Size = 4;
            descrs->Reserved = 0;
            descrs[1].DataPointer = (IntPtr)(&blobSize2);
            descrs[1].Size = 0;
            descrs[1].Reserved = 0;
            WriteEventCore(eventId, 2, descrs);
            return;
        }
        int blobSize = arg1.Length;
        fixed (byte* blob = &arg1[0])
        {
            descrs->DataPointer = (IntPtr)(&blobSize);
            descrs->Size = 4;
            descrs->Reserved = 0;
            descrs[1].DataPointer = (IntPtr)blob;
            descrs[1].Size = blobSize;
            descrs[1].Reserved = 0;
            WriteEventCore(eventId, 2, descrs);
        }
    }

    protected unsafe void WriteEvent(int eventId, long arg1, byte[]? arg2)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }
        EventData* descrs = stackalloc EventData[3];
        descrs->DataPointer = (IntPtr)(&arg1);
        descrs->Size = 8;
        descrs->Reserved = 0;
        if (arg2 == null || arg2.Length == 0)
        {
            int blobSize2 = 0;
            descrs[1].DataPointer = (IntPtr)(&blobSize2);
            descrs[1].Size = 4;
            descrs[1].Reserved = 0;
            descrs[2].DataPointer = (IntPtr)(&blobSize2);
            descrs[2].Size = 0;
            descrs[2].Reserved = 0;
            WriteEventCore(eventId, 3, descrs);
            return;
        }
        int blobSize = arg2.Length;
        fixed (byte* blob = &arg2[0])
        {
            descrs[1].DataPointer = (IntPtr)(&blobSize);
            descrs[1].Size = 4;
            descrs[1].Reserved = 0;
            descrs[2].DataPointer = (IntPtr)blob;
            descrs[2].Size = blobSize;
            descrs[2].Reserved = 0;
            WriteEventCore(eventId, 3, descrs);
        }
    }

    [CLSCompliant(false)]
    protected unsafe void WriteEventCore(int eventId, int eventDataCount, EventData* data)
    {
        WriteEventWithRelatedActivityIdCore(eventId, null, eventDataCount, data);
    }

    [CLSCompliant(false)]
    protected unsafe void WriteEventWithRelatedActivityIdCore(int eventId, Guid* relatedActivityId, int eventDataCount, EventData* data)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }
        Debug.Assert(m_eventData != null);
        try
        {
            if (relatedActivityId != null)
            {
                ValidateEventOpcodeForTransfer(ref m_eventData[eventId], m_eventData[eventId].Name);
            }
            EventOpcode opcode = (EventOpcode)m_eventData[eventId].Descriptor.Opcode;
            EventActivityOptions activityOptions = m_eventData[eventId].ActivityOptions;
            Guid* pActivityId = null;
            Guid activityId = Guid.Empty;
            Guid relActivityId = Guid.Empty;
            if (opcode != 0 && relatedActivityId == null && (activityOptions & EventActivityOptions.Disable) == 0)
            {
                switch (opcode)
                {
                case EventOpcode.Start:
                    m_activityTracker.OnStart(m_name, m_eventData[eventId].Name, m_eventData[eventId].Descriptor.Task, ref activityId, ref relActivityId, m_eventData[eventId].ActivityOptions);
                    break;
                case EventOpcode.Stop:
                    m_activityTracker.OnStop(m_name, m_eventData[eventId].Name, m_eventData[eventId].Descriptor.Task, ref activityId);
                    break;
                }
                if (activityId != Guid.Empty)
                {
                    pActivityId = &activityId;
                }
                if (relActivityId != Guid.Empty)
                {
                    relatedActivityId = &relActivityId;
                }
            }
            if (m_Dispatchers != null && m_eventData[eventId].EnabledForAnyListener)
            {
                WriteToAllListeners(eventId, pActivityId, relatedActivityId, eventDataCount, data);
            }
        }
        catch (Exception ex)
        {
            if (ex is EventSourceException)
            {
                throw;
            }
            ThrowEventSourceException(m_eventData[eventId].Name, ex);
        }
    }

    protected unsafe void WriteEvent(int eventId, params object?[] args)
    {
        WriteEventVarargs(eventId, null, args);
    }

    protected unsafe void WriteEventWithRelatedActivityId(int eventId, Guid relatedActivityId, params object?[] args)
    {
        WriteEventVarargs(eventId, &relatedActivityId, args);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
        m_eventSourceEnabled = false;
        m_eventSourceDisposed = true;
    }

    ~EventSource()
    {
        Dispose(disposing: false);
    }

    private unsafe void WriteEventRaw(string? eventName, ref System.Diagnostics.Tracing.EventDescriptor eventDescriptor, IntPtr eventHandle, Guid* activityID, Guid* relatedActivityID, int dataCount, IntPtr data)
    {
    }

    internal EventSource(Guid eventSourceGuid, string eventSourceName)
        : this(eventSourceGuid, eventSourceName, EventSourceSettings.EtwManifestEventFormat)
    {
    }

    internal EventSource(Guid eventSourceGuid, string eventSourceName, EventSourceSettings settings, string[]? traits = null)
    {
        m_config = ValidateSettings(settings);
        Initialize(eventSourceGuid, eventSourceName, traits);
    }

    private void Initialize(Guid eventSourceGuid, string eventSourceName, string[]? traits)
    {
        try
        {
            m_traits = traits;
            if (m_traits != null && m_traits.Length % 2 != 0)
            {
                throw new ArgumentException(SR.EventSource_TraitEven, "traits");
            }
            if (eventSourceGuid == Guid.Empty)
            {
                throw new ArgumentException(SR.EventSource_NeedGuid);
            }
            if (eventSourceName == null)
            {
                throw new ArgumentException(SR.EventSource_NeedName);
            }
            m_name = eventSourceName;
            m_guid = eventSourceGuid;
            m_activityTracker = System.Diagnostics.Tracing.ActivityTracker.Instance;
            EventListener.AddEventSource(this);
            Debug.Assert(!m_eventSourceEnabled);
            m_completelyInited = true;
        }
        catch (Exception e)
        {
            if (m_constructionException == null)
            {
                m_constructionException = e;
            }
            ReportOutOfBandMessage("ERROR: Exception during construction of EventSource " + Name + ": " + e.Message, flush: true);
        }
        lock (EventListener.EventListenersLock)
        {
            for (EventCommandEventArgs deferredCommands = m_deferredCommands; deferredCommands != null; deferredCommands = deferredCommands.nextCommand)
            {
                DoCommand(deferredCommands);
            }
        }
    }

    private static string GetName(Type eventSourceType, EventManifestOptions flags)
    {
        if (eventSourceType == null)
        {
            throw new ArgumentNullException("eventSourceType");
        }
        EventSourceAttribute attrib = (EventSourceAttribute)GetCustomAttributeHelper(eventSourceType, typeof(EventSourceAttribute), flags);
        if (attrib != null && attrib.Name != null)
        {
            return attrib.Name;
        }
        return eventSourceType.Name;
    }

    private static Guid GenerateGuidFromName(string name)
    {
        if (namespaceBytes == null)
        {
            namespaceBytes = new byte[16]
            {
                72, 44, 45, 178, 195, 144, 71, 200, 135, 248,
                26, 21, 191, 193, 48, 251
            };
        }
        byte[] bytes = Encoding.BigEndianUnicode.GetBytes(name);
        Sha1ForNonSecretPurposes hash = default(Sha1ForNonSecretPurposes);
        hash.Start();
        hash.Append(namespaceBytes);
        hash.Append(bytes);
        Array.Resize(ref bytes, 16);
        hash.Finish(bytes);
        bytes[7] = (byte)((bytes[7] & 0xFu) | 0x50u);
        return new Guid(bytes);
    }

    private unsafe object? DecodeObject(int eventId, int parameterId, ref EventData* data)
    {
        IntPtr dataPointer = data->DataPointer;
        data++;
        Debug.Assert(m_eventData != null);
        Type dataType = GetDataType(m_eventData[eventId], parameterId);
        while (true)
        {
            if (dataType == typeof(IntPtr))
            {
                return *(IntPtr*)(void*)dataPointer;
            }
            if (dataType == typeof(int))
            {
                return *(int*)(void*)dataPointer;
            }
            if (dataType == typeof(uint))
            {
                return *(uint*)(void*)dataPointer;
            }
            if (dataType == typeof(long))
            {
                return *(long*)(void*)dataPointer;
            }
            if (dataType == typeof(ulong))
            {
                return *(ulong*)(void*)dataPointer;
            }
            if (dataType == typeof(byte))
            {
                return *(byte*)(void*)dataPointer;
            }
            if (dataType == typeof(sbyte))
            {
                return *(sbyte*)(void*)dataPointer;
            }
            if (dataType == typeof(short))
            {
                return *(short*)(void*)dataPointer;
            }
            if (dataType == typeof(ushort))
            {
                return *(ushort*)(void*)dataPointer;
            }
            if (dataType == typeof(float))
            {
                return *(float*)(void*)dataPointer;
            }
            if (dataType == typeof(double))
            {
                return *(double*)(void*)dataPointer;
            }
            if (dataType == typeof(decimal))
            {
                return *(decimal*)(void*)dataPointer;
            }
            if (dataType == typeof(bool))
            {
                if (*(int*)(void*)dataPointer == 1)
                {
                    return true;
                }
                return false;
            }
            if (dataType == typeof(Guid))
            {
                return *(Guid*)(void*)dataPointer;
            }
            if (dataType == typeof(char))
            {
                return *(char*)(void*)dataPointer;
            }
            if (dataType == typeof(DateTime))
            {
                long dateTimeTicks = *(long*)(void*)dataPointer;
                return DateTime.FromFileTimeUtc(dateTimeTicks);
            }
            if (dataType == typeof(byte[]))
            {
                int cbSize = *(int*)(void*)dataPointer;
                byte[] blob = new byte[cbSize];
                dataPointer = data->DataPointer;
                data++;
                for (int i = 0; i < cbSize; i++)
                {
                    blob[i] = *(byte*)(void*)(dataPointer + i);
                }
                return blob;
            }
            if (dataType == typeof(byte*))
            {
                return null;
            }
            if (m_EventSourcePreventRecursion && m_EventSourceInDecodeObject)
            {
                break;
            }
            try
            {
                m_EventSourceInDecodeObject = true;
                if (dataType.IsEnum())
                {
                    dataType = Enum.GetUnderlyingType(dataType);
                    int dataTypeSize = Marshal.SizeOf(dataType);
                    if (dataTypeSize < 4)
                    {
                        dataType = typeof(int);
                    }
                    continue;
                }
                if (dataPointer == IntPtr.Zero)
                {
                    return null;
                }
                return new string((char*)(void*)dataPointer);
            }
            finally
            {
                m_EventSourceInDecodeObject = false;
            }
        }
        return null;
    }

    private System.Diagnostics.Tracing.EventDispatcher? GetDispatcher(EventListener? listener)
    {
        System.Diagnostics.Tracing.EventDispatcher dispatcher;
        for (dispatcher = m_Dispatchers; dispatcher != null; dispatcher = dispatcher.m_Next)
        {
            if (dispatcher.m_Listener == listener)
            {
                return dispatcher;
            }
        }
        return dispatcher;
    }

    private unsafe void WriteEventVarargs(int eventId, Guid* childActivityID, object?[] args)
    {
        if (!m_eventSourceEnabled)
        {
            return;
        }
        Debug.Assert(m_eventData != null);
        try
        {
            if (childActivityID != null)
            {
                ValidateEventOpcodeForTransfer(ref m_eventData[eventId], m_eventData[eventId].Name);
                if (!m_eventData[eventId].HasRelatedActivityID)
                {
                    throw new ArgumentException(SR.EventSource_NoRelatedActivityId);
                }
            }
            LogEventArgsMismatches(m_eventData[eventId].Parameters, args);
            Guid* pActivityId = null;
            Guid activityId = Guid.Empty;
            Guid relatedActivityId = Guid.Empty;
            EventOpcode opcode = (EventOpcode)m_eventData[eventId].Descriptor.Opcode;
            EventActivityOptions activityOptions = m_eventData[eventId].ActivityOptions;
            if (childActivityID == null && (activityOptions & EventActivityOptions.Disable) == 0)
            {
                switch (opcode)
                {
                case EventOpcode.Start:
                    m_activityTracker.OnStart(m_name, m_eventData[eventId].Name, m_eventData[eventId].Descriptor.Task, ref activityId, ref relatedActivityId, m_eventData[eventId].ActivityOptions);
                    break;
                case EventOpcode.Stop:
                    m_activityTracker.OnStop(m_name, m_eventData[eventId].Name, m_eventData[eventId].Descriptor.Task, ref activityId);
                    break;
                }
                if (activityId != Guid.Empty)
                {
                    pActivityId = &activityId;
                }
                if (relatedActivityId != Guid.Empty)
                {
                    childActivityID = &relatedActivityId;
                }
            }
            if (m_Dispatchers != null && m_eventData[eventId].EnabledForAnyListener)
            {
                object[] serializedArgs = SerializeEventArgs(eventId, args);
                WriteToAllListeners(eventId, null, null, pActivityId, childActivityID, serializedArgs);
            }
        }
        catch (Exception ex)
        {
            if (ex is EventSourceException)
            {
                throw;
            }
            ThrowEventSourceException(m_eventData[eventId].Name, ex);
        }
    }

    private object?[] SerializeEventArgs(int eventId, object?[] args)
    {
        Debug.Assert(m_eventData != null);
        TraceLoggingEventTypes eventTypes = m_eventData[eventId].TraceLoggingEventTypes;
        if (eventTypes == null)
        {
            eventTypes = new TraceLoggingEventTypes(m_eventData[eventId].Name, EventTags.None, m_eventData[eventId].Parameters);
            Interlocked.CompareExchange(ref m_eventData[eventId].TraceLoggingEventTypes, eventTypes, null);
        }
        object[] eventData = new object[eventTypes.typeInfos.Length];
        for (int i = 0; i < eventTypes.typeInfos.Length; i++)
        {
            eventData[i] = eventTypes.typeInfos[i].GetData(args[i]);
        }
        return eventData;
    }

    private void LogEventArgsMismatches(ParameterInfo[] infos, object?[] args)
    {
        bool typesMatch = args.Length == infos.Length;
        int i = 0;
        while (typesMatch && i < args.Length)
        {
            Type pType = infos[i].ParameterType;
            Type argType = args[i]?.GetType();
            if ((args[i] != null && !pType.IsAssignableFrom(argType)) || (args[i] == null && (!pType.IsGenericType || !(pType.GetGenericTypeDefinition() == typeof(Nullable<>))) && pType.IsValueType))
            {
                typesMatch = false;
                break;
            }
            i++;
        }
        if (!typesMatch)
        {
            Debugger.Log(0, null, SR.EventSource_VarArgsParameterMismatch + "\r\n");
        }
    }

    private unsafe void WriteToAllListeners(int eventId, Guid* activityID, Guid* childActivityID, int eventDataCount, EventData* data)
    {
        Debug.Assert(m_eventData != null);
        int paramCount = GetParameterCount(m_eventData[eventId]);
        int modifiedParamCount = 0;
        for (int j = 0; j < paramCount; j++)
        {
            Type parameterType = GetDataType(m_eventData[eventId], j);
            modifiedParamCount = ((!(parameterType == typeof(byte[]))) ? (modifiedParamCount + 1) : (modifiedParamCount + 2));
        }
        if (eventDataCount != modifiedParamCount)
        {
            ReportOutOfBandMessage(SR.Format(SR.EventSource_EventParametersMismatch, eventId, eventDataCount, paramCount), flush: true);
            paramCount = Math.Min(paramCount, eventDataCount);
        }
        object[] args = new object[paramCount];
        EventData* dataPtr = data;
        for (int i = 0; i < paramCount; i++)
        {
            args[i] = DecodeObject(eventId, i, ref dataPtr);
        }
        WriteToAllListeners(eventId, null, null, activityID, childActivityID, args);
    }

    internal unsafe void WriteToAllListeners(int eventId, uint* osThreadId, DateTime* timeStamp, Guid* activityID, Guid* childActivityID, params object?[] args)
    {
        EventWrittenEventArgs eventCallbackArgs = new EventWrittenEventArgs(this);
        eventCallbackArgs.EventId = eventId;
        if (osThreadId != null)
        {
            eventCallbackArgs.OSThreadId = (int)(*osThreadId);
        }
        if (timeStamp != null)
        {
            eventCallbackArgs.TimeStamp = *timeStamp;
        }
        if (activityID != null)
        {
            eventCallbackArgs.ActivityId = *activityID;
        }
        if (childActivityID != null)
        {
            eventCallbackArgs.RelatedActivityId = *childActivityID;
        }
        Debug.Assert(m_eventData != null);
        eventCallbackArgs.EventName = m_eventData[eventId].Name;
        eventCallbackArgs.Message = m_eventData[eventId].Message;
        eventCallbackArgs.Payload = new ReadOnlyCollection<object>(args);
        DispatchToAllListeners(eventId, childActivityID, eventCallbackArgs);
    }

    private unsafe void DispatchToAllListeners(int eventId, Guid* childActivityID, EventWrittenEventArgs eventCallbackArgs)
    {
        Exception lastThrownException = null;
        for (System.Diagnostics.Tracing.EventDispatcher dispatcher = m_Dispatchers; dispatcher != null; dispatcher = dispatcher.m_Next)
        {
            Debug.Assert(dispatcher.m_EventEnabled != null);
            if (eventId == -1 || dispatcher.m_EventEnabled[eventId])
            {
                try
                {
                    dispatcher.m_Listener.OnEventWritten(eventCallbackArgs);
                }
                catch (Exception e)
                {
                    ReportOutOfBandMessage("ERROR: Exception during EventSource.OnEventWritten: " + e.Message, flush: false);
                    lastThrownException = e;
                }
            }
        }
        if (lastThrownException != null)
        {
            throw new EventSourceException(lastThrownException);
        }
    }

    private void WriteEventString(EventLevel level, long keywords, string msgString)
    {
    }

    private void WriteStringToAllListeners(string eventName, string msg)
    {
        EventWrittenEventArgs eventCallbackArgs = new EventWrittenEventArgs(this);
        eventCallbackArgs.EventId = 0;
        eventCallbackArgs.Message = msg;
        eventCallbackArgs.Payload = new ReadOnlyCollection<object>(new List<object> { msg });
        eventCallbackArgs.PayloadNames = new ReadOnlyCollection<string>(new List<string> { "message" });
        eventCallbackArgs.EventName = eventName;
        for (System.Diagnostics.Tracing.EventDispatcher dispatcher = m_Dispatchers; dispatcher != null; dispatcher = dispatcher.m_Next)
        {
            bool dispatcherEnabled = false;
            if (dispatcher.m_EventEnabled == null)
            {
                dispatcherEnabled = true;
            }
            else
            {
                for (int evtId = 0; evtId < dispatcher.m_EventEnabled.Length; evtId++)
                {
                    if (dispatcher.m_EventEnabled[evtId])
                    {
                        dispatcherEnabled = true;
                        break;
                    }
                }
            }
            try
            {
                if (dispatcherEnabled)
                {
                    dispatcher.m_Listener.OnEventWritten(eventCallbackArgs);
                }
            }
            catch
            {
            }
        }
    }

    private bool IsEnabledByDefault(int eventNum, bool enable, EventLevel currentLevel, EventKeywords currentMatchAnyKeyword)
    {
        if (!enable)
        {
            return false;
        }
        Debug.Assert(m_eventData != null);
        EventLevel eventLevel = (EventLevel)m_eventData[eventNum].Descriptor.Level;
        EventKeywords eventKeywords = (EventKeywords)(m_eventData[eventNum].Descriptor.Keywords & (long)(~System.Diagnostics.Tracing.SessionMask.All.ToEventKeywords()));
        EventChannel channel = EventChannel.None;
        return IsEnabledCommon(enable, currentLevel, currentMatchAnyKeyword, eventLevel, eventKeywords, channel);
    }

    private bool IsEnabledCommon(bool enabled, EventLevel currentLevel, EventKeywords currentMatchAnyKeyword, EventLevel eventLevel, EventKeywords eventKeywords, EventChannel eventChannel)
    {
        if (!enabled)
        {
            return false;
        }
        if (currentLevel != 0 && currentLevel < eventLevel)
        {
            return false;
        }
        if (currentMatchAnyKeyword != EventKeywords.None && eventKeywords != EventKeywords.None && (eventKeywords & currentMatchAnyKeyword) == 0)
        {
            return false;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowEventSourceException(string? eventName, Exception? innerEx = null)
    {
        if (m_EventSourceExceptionRecurenceCount > 0)
        {
            return;
        }
        try
        {
            m_EventSourceExceptionRecurenceCount++;
            string errorPrefix = "EventSourceException";
            if (eventName != null)
            {
                errorPrefix = errorPrefix + " while processing event \"" + eventName + "\"";
            }
            switch (EventProvider.GetLastWriteEventError())
            {
            case EventProvider.WriteEventErrorCode.EventTooBig:
                ReportOutOfBandMessage(errorPrefix + ": " + SR.EventSource_EventTooBig, flush: true);
                if (ThrowOnEventWriteErrors)
                {
                    throw new EventSourceException(SR.EventSource_EventTooBig, innerEx);
                }
                return;
            case EventProvider.WriteEventErrorCode.NoFreeBuffers:
                ReportOutOfBandMessage(errorPrefix + ": " + SR.EventSource_NoFreeBuffers, flush: true);
                if (ThrowOnEventWriteErrors)
                {
                    throw new EventSourceException(SR.EventSource_NoFreeBuffers, innerEx);
                }
                return;
            }
            if (innerEx != null)
            {
                innerEx = innerEx.GetBaseException();
                ReportOutOfBandMessage(errorPrefix + ": " + innerEx.GetType()?.ToString() + ":" + innerEx.Message, flush: true);
            }
            else
            {
                ReportOutOfBandMessage(errorPrefix, flush: true);
            }
            if (ThrowOnEventWriteErrors)
            {
                throw new EventSourceException(innerEx);
            }
        }
        finally
        {
            m_EventSourceExceptionRecurenceCount--;
        }
    }

    private void ValidateEventOpcodeForTransfer(ref EventMetadata eventData, string? eventName)
    {
        if (eventData.Descriptor.Opcode != 9 && eventData.Descriptor.Opcode != 240 && eventData.Descriptor.Opcode != 1)
        {
            ThrowEventSourceException(eventName);
        }
    }

    internal static EventOpcode GetOpcodeWithDefault(EventOpcode opcode, string? eventName)
    {
        if (opcode == EventOpcode.Info && eventName != null)
        {
            if (eventName.EndsWith("Start", StringComparison.Ordinal))
            {
                return EventOpcode.Start;
            }
            if (eventName.EndsWith("Stop", StringComparison.Ordinal))
            {
                return EventOpcode.Stop;
            }
        }
        return opcode;
    }

    private int GetParameterCount(EventMetadata eventData)
    {
        return eventData.Parameters.Length;
    }

    private Type GetDataType(EventMetadata eventData, int parameterId)
    {
        return eventData.Parameters[parameterId].ParameterType;
    }

    internal void SendCommand(EventListener? listener, EventProviderType eventProviderType, int perEventSourceSessionId, int etwSessionId, EventCommand command, bool enable, EventLevel level, EventKeywords matchAnyKeyword, IDictionary<string, string?>? commandArguments)
    {
        EventCommandEventArgs commandArgs = new EventCommandEventArgs(command, commandArguments, this, listener, eventProviderType, perEventSourceSessionId, etwSessionId, enable, level, matchAnyKeyword);
        lock (EventListener.EventListenersLock)
        {
            if (m_completelyInited)
            {
                m_deferredCommands = null;
                DoCommand(commandArgs);
                return;
            }
            if (m_deferredCommands == null)
            {
                m_deferredCommands = commandArgs;
                return;
            }
            EventCommandEventArgs lastCommand = m_deferredCommands;
            while (lastCommand.nextCommand != null)
            {
                lastCommand = lastCommand.nextCommand;
            }
            lastCommand.nextCommand = commandArgs;
        }
    }

    internal void DoCommand(EventCommandEventArgs commandArgs)
    {
        Debug.Assert(m_completelyInited);
        m_outOfBandMessageCount = 0;
        bool shouldReport = commandArgs.perEventSourceSessionId > 0 && (long)commandArgs.perEventSourceSessionId <= 4L;
        try
        {
            EnsureDescriptorsInitialized();
            Debug.Assert(m_eventData != null);
            commandArgs.dispatcher = GetDispatcher(commandArgs.listener);
            if (commandArgs.dispatcher == null && commandArgs.listener != null)
            {
                throw new ArgumentException(SR.EventSource_ListenerNotFound);
            }
            if (commandArgs.Arguments == null)
            {
                commandArgs.Arguments = new Dictionary<string, string>();
            }
            if (commandArgs.Command == EventCommand.Update)
            {
                for (int i = 0; i < m_eventData.Length; i++)
                {
                    EnableEventForDispatcher(commandArgs.dispatcher, commandArgs.eventProviderType, i, IsEnabledByDefault(i, commandArgs.enable, commandArgs.level, commandArgs.matchAnyKeyword));
                }
                if (commandArgs.enable)
                {
                    if (!m_eventSourceEnabled)
                    {
                        m_level = commandArgs.level;
                        m_matchAnyKeyword = commandArgs.matchAnyKeyword;
                    }
                    else
                    {
                        if (commandArgs.level > m_level)
                        {
                            m_level = commandArgs.level;
                        }
                        if (commandArgs.matchAnyKeyword == EventKeywords.None)
                        {
                            m_matchAnyKeyword = EventKeywords.None;
                        }
                        else if (m_matchAnyKeyword != EventKeywords.None)
                        {
                            m_matchAnyKeyword |= commandArgs.matchAnyKeyword;
                        }
                    }
                }
                bool bSessionEnable = commandArgs.perEventSourceSessionId >= 0;
                if (commandArgs.perEventSourceSessionId == 0 && !commandArgs.enable)
                {
                    bSessionEnable = false;
                }
                if (commandArgs.listener == null)
                {
                    if (!bSessionEnable)
                    {
                        commandArgs.perEventSourceSessionId = -commandArgs.perEventSourceSessionId;
                    }
                    commandArgs.perEventSourceSessionId--;
                }
                commandArgs.Command = (bSessionEnable ? EventCommand.Enable : EventCommand.Disable);
                Debug.Assert(commandArgs.perEventSourceSessionId >= -1 && (long)commandArgs.perEventSourceSessionId <= 4L);
                if (bSessionEnable && commandArgs.dispatcher == null && !SelfDescribingEvents)
                {
                    SendManifest(m_rawManifest);
                }
                if (commandArgs.enable)
                {
                    Debug.Assert(m_eventData != null);
                    m_eventSourceEnabled = true;
                }
                OnEventCommand(commandArgs);
                m_eventCommandExecuted?.Invoke(this, commandArgs);
                if (commandArgs.enable)
                {
                    return;
                }
                for (int j = 0; j < m_eventData.Length; j++)
                {
                    bool isEnabledForAnyListener = false;
                    for (System.Diagnostics.Tracing.EventDispatcher dispatcher = m_Dispatchers; dispatcher != null; dispatcher = dispatcher.m_Next)
                    {
                        Debug.Assert(dispatcher.m_EventEnabled != null);
                        if (dispatcher.m_EventEnabled[j])
                        {
                            isEnabledForAnyListener = true;
                            break;
                        }
                    }
                    m_eventData[j].EnabledForAnyListener = isEnabledForAnyListener;
                }
                if (!AnyEventEnabled())
                {
                    m_level = EventLevel.LogAlways;
                    m_matchAnyKeyword = EventKeywords.None;
                    m_eventSourceEnabled = false;
                }
            }
            else
            {
                if (commandArgs.Command == EventCommand.SendManifest && m_rawManifest != null)
                {
                    SendManifest(m_rawManifest);
                }
                OnEventCommand(commandArgs);
                m_eventCommandExecuted?.Invoke(this, commandArgs);
            }
        }
        catch (Exception e)
        {
            ReportOutOfBandMessage("ERROR: Exception in Command Processing for EventSource " + Name + ": " + e.Message, flush: true);
        }
    }

    internal bool EnableEventForDispatcher(System.Diagnostics.Tracing.EventDispatcher? dispatcher, EventProviderType eventProviderType, int eventId, bool value)
    {
        Debug.Assert(m_eventData != null);
        if (dispatcher == null)
        {
            if (eventId >= m_eventData.Length)
            {
                return false;
            }
        }
        else
        {
            Debug.Assert(dispatcher.m_EventEnabled != null);
            if (eventId >= dispatcher.m_EventEnabled.Length)
            {
                return false;
            }
            dispatcher.m_EventEnabled[eventId] = value;
            if (value)
            {
                m_eventData[eventId].EnabledForAnyListener = true;
            }
        }
        return true;
    }

    private bool AnyEventEnabled()
    {
        Debug.Assert(m_eventData != null);
        for (int i = 0; i < m_eventData.Length; i++)
        {
            if (m_eventData[i].EnabledForETW || m_eventData[i].EnabledForAnyListener)
            {
                return true;
            }
        }
        return false;
    }

    private void EnsureDescriptorsInitialized()
    {
        if (m_eventData == null)
        {
            Guid eventSourceGuid = Guid.Empty;
            string eventSourceName = null;
            EventMetadata[] eventData = null;
            byte[] manifest = null;
            GetMetadata(out eventSourceGuid, out eventSourceName, out eventData, out manifest);
            if (eventSourceGuid.Equals(Guid.Empty) || eventSourceName == null || eventData == null || manifest == null)
            {
                Debug.Assert(m_rawManifest == null);
                m_rawManifest = CreateManifestAndDescriptors(GetType(), Name, this);
                Debug.Assert(m_eventData != null);
            }
            else
            {
                m_name = eventSourceName;
                m_guid = eventSourceGuid;
                m_eventData = eventData;
                m_rawManifest = manifest;
            }
            Debug.Assert(EventListener.s_EventSources != null, "should be called within lock on EventListener.EventListenersLock which ensures s_EventSources to be initialized");
            foreach (WeakReference eventSourceRef in EventListener.s_EventSources)
            {
                if (eventSourceRef.Target is EventSource eventSource && eventSource.Guid == m_guid && !eventSource.IsDisposed && eventSource != this)
                {
                    throw new ArgumentException(SR.Format(SR.EventSource_EventSourceGuidInUse, m_guid));
                }
            }
            for (System.Diagnostics.Tracing.EventDispatcher dispatcher = m_Dispatchers; dispatcher != null; dispatcher = dispatcher.m_Next)
            {
                if (dispatcher.m_EventEnabled == null)
                {
                    dispatcher.m_EventEnabled = new bool[m_eventData.Length];
                }
            }
        }
        if (s_currentPid == 0)
        {
            s_currentPid = Convert.ToUInt32(InteropEx.GetCurrentProcessId());
        }
    }

    private bool SendManifest(byte[]? rawManifest)
    {
        bool success = true;
        if (rawManifest == null)
        {
            return false;
        }
        Debug.Assert(!SelfDescribingEvents);
        return success;
    }

    internal static Attribute? GetCustomAttributeHelper(MemberInfo member, Type attributeType, EventManifestOptions flags = EventManifestOptions.None)
    {
        if (!member.Module.Assembly.ReflectionOnly() && (flags & EventManifestOptions.AllowEventSourceOverride) == 0)
        {
            Attribute firstAttribute = null;
            object[] customAttributes = member.GetCustomAttributes(attributeType, inherit: false);
            int num = 0;
            if (num < customAttributes.Length)
            {
                object attribute = customAttributes[num];
                firstAttribute = (Attribute)attribute;
            }
            return firstAttribute;
        }
        string fullTypeNameToFind = attributeType.FullName;
        foreach (CustomAttributeData data in CustomAttributeData.GetCustomAttributes(member))
        {
            if (!AttributeTypeNamesMatch(attributeType, data.Constructor.ReflectedType))
            {
                continue;
            }
            Attribute attr = null;
            Debug.Assert(data.ConstructorArguments.Count <= 1);
            if (data.ConstructorArguments.Count == 1)
            {
                attr = (Attribute)Activator.CreateInstance(attributeType, data.ConstructorArguments[0].Value);
            }
            else if (data.ConstructorArguments.Count == 0)
            {
                attr = (Attribute)Activator.CreateInstance(attributeType);
            }
            if (attr == null)
            {
                continue;
            }
            Type t = attr.GetType();
            foreach (CustomAttributeNamedArgument namedArgument in data.NamedArguments)
            {
                PropertyInfo p = t.GetProperty(namedArgument.MemberInfo.Name, BindingFlags.Instance | BindingFlags.Public);
                object value = namedArgument.TypedValue.Value;
                if (p.PropertyType.IsEnum)
                {
                    string val = value.ToString();
                    value = Enum.Parse(p.PropertyType, val);
                }
                p.SetValue(attr, value, null);
            }
            return attr;
        }
        return null;
    }

    private static bool AttributeTypeNamesMatch(Type attributeType, Type reflectedAttributeType)
    {
        return attributeType == reflectedAttributeType || string.Equals(attributeType.FullName, reflectedAttributeType.FullName, StringComparison.Ordinal) || (string.Equals(attributeType.Name, reflectedAttributeType.Name, StringComparison.Ordinal) && attributeType.Namespace.EndsWith("Diagnostics.Tracing", StringComparison.Ordinal) && reflectedAttributeType.Namespace.EndsWith("Diagnostics.Tracing", StringComparison.Ordinal));
    }

    private static Type? GetEventSourceBaseType(Type eventSourceType, bool allowEventSourceOverride, bool reflectionOnly)
    {
        Type ret = eventSourceType;
        if (ret.BaseType() == null)
        {
            return null;
        }
        do
        {
            ret = ret.BaseType();
        }
        while (ret != null && ret.IsAbstract());
        if (ret != null)
        {
            if (!allowEventSourceOverride)
            {
                if ((reflectionOnly && ret.FullName != typeof(EventSource).FullName) || (!reflectionOnly && ret != typeof(EventSource)))
                {
                    return null;
                }
            }
            else if (ret.Name != "EventSource")
            {
                return null;
            }
        }
        return ret;
    }

    private static byte[]? CreateManifestAndDescriptors(Type eventSourceType, string? eventSourceDllName, EventSource? source, EventManifestOptions flags = EventManifestOptions.None)
    {
        System.Diagnostics.Tracing.ManifestBuilder manifest = null;
        bool bNeedsManifest = source == null || !source.SelfDescribingEvents;
        Exception exception = null;
        byte[] res = null;
        if (eventSourceType.IsAbstract() && (flags & EventManifestOptions.Strict) == 0)
        {
            return null;
        }
        try
        {
            MethodInfo[] methods = eventSourceType.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            int eventId = 1;
            EventMetadata[] eventData = null;
            Dictionary<string, string> eventsByName = null;
            if (source != null || (flags & EventManifestOptions.Strict) != 0)
            {
                eventData = new EventMetadata[methods.Length + 1];
                eventData[0].Name = "";
            }
            ResourceManager resources = null;
            EventSourceAttribute eventSourceAttrib = (EventSourceAttribute)GetCustomAttributeHelper(eventSourceType, typeof(EventSourceAttribute), flags);
            if (eventSourceAttrib != null && eventSourceAttrib.LocalizationResources != null)
            {
                resources = new ResourceManager(eventSourceAttrib.LocalizationResources, eventSourceType.Assembly());
            }
            manifest = new System.Diagnostics.Tracing.ManifestBuilder(GetName(eventSourceType, flags), GetGuid(eventSourceType), eventSourceDllName, resources, flags);
            manifest.StartEvent("EventSourceMessage", new EventAttribute(0)
            {
                Level = EventLevel.LogAlways,
                Task = (EventTask)65534
            });
            manifest.AddEventParameter(typeof(string), "message");
            manifest.EndEvent();
            if ((flags & EventManifestOptions.Strict) != 0)
            {
                if (!(GetEventSourceBaseType(eventSourceType, (flags & EventManifestOptions.AllowEventSourceOverride) != 0, eventSourceType.Assembly().ReflectionOnly()) != null))
                {
                    manifest.ManifestError(SR.EventSource_TypeMustDeriveFromEventSource);
                }
                if (!eventSourceType.IsAbstract() && !eventSourceType.IsSealed())
                {
                    manifest.ManifestError(SR.EventSource_TypeMustBeSealedOrAbstract);
                }
            }
            string[] array = new string[3] { "Keywords", "Tasks", "Opcodes" };
            foreach (string providerEnumKind in array)
            {
                Type nestedType = eventSourceType.GetNestedType(providerEnumKind);
                if (!(nestedType != null))
                {
                    continue;
                }
                if (eventSourceType.IsAbstract())
                {
                    manifest.ManifestError(SR.Format(SR.EventSource_AbstractMustNotDeclareKTOC, nestedType.Name));
                    continue;
                }
                FieldInfo[] fields = nestedType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (FieldInfo staticField in fields)
                {
                    AddProviderEnumKind(manifest, staticField, providerEnumKind);
                }
            }
            manifest.AddKeyword("Session3", 17592186044416uL);
            manifest.AddKeyword("Session2", 35184372088832uL);
            manifest.AddKeyword("Session1", 70368744177664uL);
            manifest.AddKeyword("Session0", 140737488355328uL);
            if (eventSourceType != typeof(EventSource))
            {
                foreach (MethodInfo method in methods)
                {
                    ParameterInfo[] args = method.GetParameters();
                    EventAttribute eventAttribute = (EventAttribute)GetCustomAttributeHelper(method, typeof(EventAttribute), flags);
                    if (method.IsStatic)
                    {
                        continue;
                    }
                    if (eventSourceType.IsAbstract())
                    {
                        if (eventAttribute != null)
                        {
                            manifest.ManifestError(SR.Format(SR.EventSource_AbstractMustNotDeclareEventMethods, method.Name, eventAttribute.EventId));
                        }
                        continue;
                    }
                    if (eventAttribute == null)
                    {
                        if (method.ReturnType != typeof(void) || method.IsVirtual || GetCustomAttributeHelper(method, typeof(NonEventAttribute), flags) != null)
                        {
                            continue;
                        }
                        EventAttribute defaultEventAttribute = new EventAttribute(eventId);
                        eventAttribute = defaultEventAttribute;
                    }
                    else if (eventAttribute.EventId <= 0)
                    {
                        manifest.ManifestError(SR.Format(SR.EventSource_NeedPositiveId, method.Name), runtimeCritical: true);
                        continue;
                    }
                    if (method.Name.LastIndexOf('.') >= 0)
                    {
                        manifest.ManifestError(SR.Format(SR.EventSource_EventMustNotBeExplicitImplementation, method.Name, eventAttribute.EventId));
                    }
                    eventId++;
                    string eventName = method.Name;
                    if (eventAttribute.Opcode == EventOpcode.Info)
                    {
                        bool noTask = eventAttribute.Task == EventTask.None;
                        if (noTask)
                        {
                            eventAttribute.Task = (EventTask)(65534 - eventAttribute.EventId);
                        }
                        if (!eventAttribute.IsOpcodeSet)
                        {
                            eventAttribute.Opcode = GetOpcodeWithDefault(EventOpcode.Info, eventName);
                        }
                        if (noTask)
                        {
                            if (eventAttribute.Opcode == EventOpcode.Start)
                            {
                                string taskName = eventName.Substring(0, eventName.Length - "Start".Length);
                                if (string.Compare(eventName, 0, taskName, 0, taskName.Length) == 0 && string.Compare(eventName, taskName.Length, "Start", 0, Math.Max(eventName.Length - taskName.Length, "Start".Length)) == 0)
                                {
                                    manifest.AddTask(taskName, (int)eventAttribute.Task);
                                }
                            }
                            else if (eventAttribute.Opcode == EventOpcode.Stop)
                            {
                                int startEventId = eventAttribute.EventId - 1;
                                if (eventData != null && startEventId < eventData.Length)
                                {
                                    Debug.Assert(0 <= startEventId);
                                    EventMetadata startEventMetadata = eventData[startEventId];
                                    string taskName2 = eventName.Substring(0, eventName.Length - "Stop".Length);
                                    if (startEventMetadata.Descriptor.Opcode == 1 && string.Compare(startEventMetadata.Name, 0, taskName2, 0, taskName2.Length) == 0 && string.Compare(startEventMetadata.Name, taskName2.Length, "Start", 0, Math.Max(startEventMetadata.Name.Length - taskName2.Length, "Start".Length)) == 0)
                                    {
                                        eventAttribute.Task = (EventTask)startEventMetadata.Descriptor.Task;
                                        noTask = false;
                                    }
                                }
                                if (noTask && (flags & EventManifestOptions.Strict) != 0)
                                {
                                    throw new ArgumentException(SR.EventSource_StopsFollowStarts);
                                }
                            }
                        }
                    }
                    bool hasRelatedActivityID = RemoveFirstArgIfRelatedActivityId(ref args);
                    if (source == null || !source.SelfDescribingEvents)
                    {
                        manifest.StartEvent(eventName, eventAttribute);
                        for (int fieldIdx = 0; fieldIdx < args.Length; fieldIdx++)
                        {
                            manifest.AddEventParameter(args[fieldIdx].ParameterType, args[fieldIdx].Name);
                        }
                        manifest.EndEvent();
                    }
                    if (source != null || (flags & EventManifestOptions.Strict) != 0)
                    {
                        Debug.Assert(eventData != null);
                        DebugCheckEvent(ref eventsByName, eventData, method, eventAttribute, manifest, flags);
                        string eventKey = "event_" + eventName;
                        string msg2 = manifest.GetLocalizedMessage(eventKey, CultureInfo.CurrentUICulture, etwFormat: false);
                        if (msg2 != null)
                        {
                            eventAttribute.Message = msg2;
                        }
                        AddEventDescriptor(ref eventData, eventName, eventAttribute, args, hasRelatedActivityID);
                    }
                }
            }
            System.Diagnostics.Tracing.NameInfo.ReserveEventIDsBelow(eventId);
            if (source != null)
            {
                Debug.Assert(eventData != null);
                TrimEventDescriptors(ref eventData);
                source.m_eventData = eventData;
            }
            if (!eventSourceType.IsAbstract() && (source == null || !source.SelfDescribingEvents))
            {
                bNeedsManifest = (flags & EventManifestOptions.OnlyIfNeededForRegistration) == 0;
                if (!bNeedsManifest && (flags & EventManifestOptions.Strict) == 0)
                {
                    return null;
                }
                res = manifest.CreateManifest();
            }
        }
        catch (Exception e)
        {
            if ((flags & EventManifestOptions.Strict) == 0)
            {
                throw;
            }
            exception = e;
        }
        if ((flags & EventManifestOptions.Strict) != 0 && ((manifest != null && manifest.Errors.Count > 0) || exception != null))
        {
            string msg = string.Empty;
            if (manifest != null && manifest.Errors.Count > 0)
            {
                bool firstError = true;
                foreach (string error in manifest.Errors)
                {
                    if (!firstError)
                    {
                        msg += Environment.NewLine;
                    }
                    firstError = false;
                    msg += error;
                }
            }
            else
            {
                msg = "Unexpected error: " + exception.Message;
            }
            throw new ArgumentException(msg, exception);
        }
        return bNeedsManifest ? res : null;
    }

    private static bool RemoveFirstArgIfRelatedActivityId(ref ParameterInfo[] args)
    {
        if (args.Length != 0 && args[0].ParameterType == typeof(Guid) && string.Equals(args[0].Name, "relatedActivityId", StringComparison.OrdinalIgnoreCase))
        {
            ParameterInfo[] newargs = new ParameterInfo[args.Length - 1];
            Array.Copy(args, 1, newargs, 0, args.Length - 1);
            args = newargs;
            return true;
        }
        return false;
    }

    private static void AddProviderEnumKind(System.Diagnostics.Tracing.ManifestBuilder manifest, FieldInfo staticField, string providerEnumKind)
    {
        bool reflectionOnly = staticField.Module.Assembly.ReflectionOnly();
        Type staticFieldType = staticField.FieldType;
        if ((!reflectionOnly && staticFieldType == typeof(EventOpcode)) || AttributeTypeNamesMatch(staticFieldType, typeof(EventOpcode)))
        {
            if (!(providerEnumKind != "Opcodes"))
            {
                int value = (int)staticField.GetRawConstantValue();
                manifest.AddOpcode(staticField.Name, value);
                return;
            }
        }
        else if ((!reflectionOnly && staticFieldType == typeof(EventTask)) || AttributeTypeNamesMatch(staticFieldType, typeof(EventTask)))
        {
            if (!(providerEnumKind != "Tasks"))
            {
                int value2 = (int)staticField.GetRawConstantValue();
                manifest.AddTask(staticField.Name, value2);
                return;
            }
        }
        else
        {
            if ((reflectionOnly || !(staticFieldType == typeof(EventKeywords))) && !AttributeTypeNamesMatch(staticFieldType, typeof(EventKeywords)))
            {
                return;
            }
            if (!(providerEnumKind != "Keywords"))
            {
                ulong value3 = (ulong)(long)staticField.GetRawConstantValue();
                manifest.AddKeyword(staticField.Name, value3);
                return;
            }
        }
        manifest.ManifestError(SR.Format(SR.EventSource_EnumKindMismatch, staticField.Name, staticField.FieldType.Name, providerEnumKind));
    }

    private static void AddEventDescriptor([NotNull] ref EventMetadata[] eventData, string eventName, EventAttribute eventAttribute, ParameterInfo[] eventParameters, bool hasRelatedActivityID)
    {
        if (eventData.Length <= eventAttribute.EventId)
        {
            EventMetadata[] newValues = new EventMetadata[Math.Max(eventData.Length + 16, eventAttribute.EventId + 1)];
            Array.Copy(eventData, 0, newValues, 0, eventData.Length);
            eventData = newValues;
        }
        eventData[eventAttribute.EventId].Descriptor = new System.Diagnostics.Tracing.EventDescriptor(eventAttribute.EventId, eventAttribute.Version, 0, (byte)eventAttribute.Level, (byte)eventAttribute.Opcode, (int)eventAttribute.Task, (long)eventAttribute.Keywords | (long)System.Diagnostics.Tracing.SessionMask.All.ToEventKeywords());
        eventData[eventAttribute.EventId].Tags = eventAttribute.Tags;
        eventData[eventAttribute.EventId].Name = eventName;
        eventData[eventAttribute.EventId].Parameters = eventParameters;
        eventData[eventAttribute.EventId].Message = eventAttribute.Message;
        eventData[eventAttribute.EventId].ActivityOptions = eventAttribute.ActivityOptions;
        eventData[eventAttribute.EventId].HasRelatedActivityID = hasRelatedActivityID;
        eventData[eventAttribute.EventId].EventHandle = IntPtr.Zero;
    }

    private static void TrimEventDescriptors(ref EventMetadata[] eventData)
    {
        int idx = eventData.Length;
        while (0 < idx)
        {
            idx--;
            if (eventData[idx].Descriptor.EventId != 0)
            {
                break;
            }
        }
        if (eventData.Length - idx > 2)
        {
            EventMetadata[] newValues = new EventMetadata[idx + 1];
            Array.Copy(eventData, 0, newValues, 0, newValues.Length);
            eventData = newValues;
        }
    }

    internal void AddListener(EventListener listener)
    {
        lock (EventListener.EventListenersLock)
        {
            bool[] enabledArray = null;
            if (m_eventData != null)
            {
                enabledArray = new bool[m_eventData.Length];
            }
            m_Dispatchers = new System.Diagnostics.Tracing.EventDispatcher(m_Dispatchers, enabledArray, listener);
            listener.OnEventSourceCreated(this);
        }
    }

    private static void DebugCheckEvent(ref Dictionary<string, string>? eventsByName, EventMetadata[] eventData, MethodInfo method, EventAttribute eventAttribute, System.Diagnostics.Tracing.ManifestBuilder manifest, EventManifestOptions options)
    {
        int evtId = eventAttribute.EventId;
        string evtName = method.Name;
        int eventArg = GetHelperCallFirstArg(method);
        if (eventArg >= 0 && evtId != eventArg)
        {
            manifest.ManifestError(SR.Format(SR.EventSource_MismatchIdToWriteEvent, evtName, evtId, eventArg), runtimeCritical: true);
        }
        if (evtId < eventData.Length && eventData[evtId].Descriptor.EventId != 0)
        {
            manifest.ManifestError(SR.Format(SR.EventSource_EventIdReused, evtName, evtId, eventData[evtId].Name), runtimeCritical: true);
        }
        Debug.Assert(eventAttribute.Task != 0 || eventAttribute.Opcode != EventOpcode.Info);
        for (int idx = 0; idx < eventData.Length; idx++)
        {
            if (eventData[idx].Name != null && eventData[idx].Descriptor.Task == (int)eventAttribute.Task && (EventOpcode)eventData[idx].Descriptor.Opcode == eventAttribute.Opcode)
            {
                manifest.ManifestError(SR.Format(SR.EventSource_TaskOpcodePairReused, evtName, evtId, eventData[idx].Name, idx));
                if ((options & EventManifestOptions.Strict) == 0)
                {
                    break;
                }
            }
        }
        if (eventAttribute.Opcode != 0)
        {
            bool failure = false;
            if (eventAttribute.Task == EventTask.None)
            {
                failure = true;
            }
            else
            {
                EventTask autoAssignedTask = (EventTask)(65534 - evtId);
                if (eventAttribute.Opcode != EventOpcode.Start && eventAttribute.Opcode != EventOpcode.Stop && eventAttribute.Task == autoAssignedTask)
                {
                    failure = true;
                }
            }
            if (failure)
            {
                manifest.ManifestError(SR.Format(SR.EventSource_EventMustHaveTaskIfNonDefaultOpcode, evtName, evtId));
            }
        }
        if (eventsByName == null)
        {
            eventsByName = new Dictionary<string, string>();
        }
        if (eventsByName.ContainsKey(evtName))
        {
            manifest.ManifestError(SR.Format(SR.EventSource_EventNameReused, evtName), runtimeCritical: true);
        }
        eventsByName[evtName] = evtName;
    }

    private static int GetHelperCallFirstArg(MethodInfo method)
    {
        byte[] instrs = method.GetMethodBody().GetILAsByteArray();
        int retVal = -1;
        for (int idx = 0; idx < instrs.Length; idx++)
        {
            switch (instrs[idx])
            {
            case 14:
            case 16:
                idx++;
                continue;
            case 21:
            case 22:
            case 23:
            case 24:
            case 25:
            case 26:
            case 27:
            case 28:
            case 29:
            case 30:
                if (idx > 0 && instrs[idx - 1] == 2)
                {
                    retVal = instrs[idx] - 22;
                }
                continue;
            case 31:
                if (idx > 0 && instrs[idx - 1] == 2)
                {
                    retVal = instrs[idx + 1];
                }
                idx++;
                continue;
            case 32:
                idx += 4;
                continue;
            case 40:
                idx += 4;
                if (retVal >= 0)
                {
                    for (int search = idx + 1; search < instrs.Length; search++)
                    {
                        if (instrs[search] == 42)
                        {
                            return retVal;
                        }
                        if (instrs[search] != 0)
                        {
                            break;
                        }
                    }
                }
                retVal = -1;
                continue;
            case 44:
            case 45:
                retVal = -1;
                idx++;
                continue;
            case 57:
            case 58:
                retVal = -1;
                idx += 4;
                continue;
            case 140:
            case 141:
                idx += 4;
                continue;
            case 254:
                idx++;
                if (idx >= instrs.Length || instrs[idx] >= 6)
                {
                    break;
                }
                continue;
            case 0:
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
            case 7:
            case 8:
            case 9:
            case 10:
            case 11:
            case 12:
            case 13:
            case 20:
            case 37:
            case 103:
            case 104:
            case 105:
            case 106:
            case 109:
            case 110:
            case 162:
                continue;
            }
            return -1;
        }
        return -1;
    }

    internal void ReportOutOfBandMessage(string msg, bool flush)
    {
        try
        {
            Debugger.Log(0, null, $"EventSource Error: {msg}{Environment.NewLine}");
            if (m_outOfBandMessageCount < 15)
            {
                m_outOfBandMessageCount++;
            }
            else
            {
                if (m_outOfBandMessageCount == 16)
                {
                    return;
                }
                m_outOfBandMessageCount = 16;
                msg = "Reached message limit.   End of EventSource error messages.";
            }
            WriteEventString(EventLevel.LogAlways, -1L, msg);
            WriteStringToAllListeners("EventSourceMessage", msg);
        }
        catch (Exception)
        {
        }
    }

    private EventSourceSettings ValidateSettings(EventSourceSettings settings)
    {
        EventSourceSettings evtFormatMask = EventSourceSettings.EtwManifestEventFormat | EventSourceSettings.EtwSelfDescribingEventFormat;
        if ((settings & evtFormatMask) == evtFormatMask)
        {
            throw new ArgumentException(SR.EventSource_InvalidEventFormat, "settings");
        }
        if ((settings & evtFormatMask) == 0)
        {
            settings |= EventSourceSettings.EtwSelfDescribingEventFormat;
        }
        return settings;
    }
}
*/