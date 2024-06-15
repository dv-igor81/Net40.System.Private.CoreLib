using System.Collections.Generic;
using System.Diagnostics.Eventing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace System.Diagnostics.Tracing;

public class EventProviderEx : EventProvider, IDisposable
{
    private struct EventData
    {
        internal ulong Ptr;

        internal uint Size;

        internal uint Reserved;
    }

    public struct SessionInfo
    {
        internal int sessionIdBit;

        internal int etwSessionId;

        internal SessionInfo(int sessionIdBit_, int etwSessionId_)
        {
            sessionIdBit = sessionIdBit_;
            etwSessionId = etwSessionId_;
        }
    }

    public enum WriteEventErrorCode
    {
        NoError,
        NoFreeBuffers,
        EventTooBig,
        NullInput,
        TooManyArgs,
        Other
    }

    private delegate void SessionInfoCallback(int etwSessionId, long matchAllKeywords,
        ref List<SessionInfo> sessionList);

    private static bool m_setInformationMissing;

    internal IEventProvider m_eventProvider;

    //private UnsafeNativeMethods.ManifestEtw.EtwEnableCallback m_etwCallback;

    private long m_regHandle;

    private byte m_level;

    private long m_anyKeywordMask;

    private long m_allKeywordMask;

    private List<SessionInfo> m_liveSessions;

    private bool m_enabled;

    private string m_providerName;

    private Guid m_providerId;

    internal bool m_disposed;

    [ThreadStatic] private static WriteEventErrorCode s_returnCode;

    private static int[] nibblebits = new int[16]
    {
        0, 1, 1, 2, 1, 2, 2, 3, 1, 2,
        2, 3, 2, 3, 3, 4
    };

    /*public EventProviderEx(Guid providerGuid, IEventProvider mEventProvider, long mRegHandle, byte mLevel,
        long mAnyKeywordMask, long mAllKeywordMask, List<SessionInfo> mLiveSessions, bool mEnabled,
        string mProviderName, Guid mProviderId, bool mDisposed) : base(providerGuid)
    {
        m_eventProvider = mEventProvider;
        m_regHandle = mRegHandle;
        m_level = mLevel;
        m_anyKeywordMask = mAnyKeywordMask;
        m_allKeywordMask = mAllKeywordMask;
        m_liveSessions = mLiveSessions;
        m_enabled = mEnabled;
        m_providerName = mProviderName;
        m_providerId = mProviderId;
        m_disposed = mDisposed;
    }*/

    protected EventLevel Level
    {
        get { return (EventLevel)m_level; }
        set { m_level = (byte)value; }
    }

    protected EventKeywords MatchAnyKeyword
    {
        get { return (EventKeywords)m_anyKeywordMask; }
        set { m_anyKeywordMask = (long)value; }
    }

    protected EventKeywords MatchAllKeyword
    {
        get { return (EventKeywords)m_allKeywordMask; }
        set { m_allKeywordMask = (long)value; }
    }

    /*internal unsafe int SetInformation(UnsafeNativeMethods.ManifestEtw.EVENT_INFO_CLASS eventInfoClass, IntPtr data, uint dataSize)
    {
        int result = 50;
        if (!m_setInformationMissing)
        {
            try
            {
                result = UnsafeNativeMethods.ManifestEtw.EventSetInformation(m_regHandle, eventInfoClass, (void*)data, (int)dataSize);
            }
            catch (TypeLoadException)
            {
                m_setInformationMissing = true;
            }
        }
        return result;
    }*/

    /*internal EventProviderEx(EventProviderType providerType)
    {
        switch (providerType)
        {
        case EventProviderType.ETW:
            m_eventProvider = new EtwEventProvider();
            break;
        case EventProviderType.EventPipe:
            m_eventProvider = new EventPipeEventProvider();
            break;
        default:
            m_eventProvider = new NoOpEventProvider();
            break;
        }
    }*/

    // internal unsafe void Register(EventSource eventSource)
    // {
    // 	m_etwCallback = EtwEnableCallBack;
    // 	uint num = EventRegister(eventSource, m_etwCallback);
    // 	if (num != 0)
    // 	{
    // 		throw new ArgumentException(Interop.Kernel32.GetMessage((int)num));
    // 	}
    // }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (m_disposed)
        {
            return;
        }

        m_enabled = false;
        long num = 0L;
        lock (EventListener.EventListenersLock)
        {
            if (m_disposed)
            {
                return;
            }

            num = m_regHandle;
            m_regHandle = 0L;
            m_disposed = true;
        }

        if (num != 0L)
        {
            EventUnregister(num);
        }
    }

    public virtual void Close()
    {
        Dispose();
    }

    ~EventProviderEx()
    {
        Dispose(disposing: false);
    }

    

    private static void GetSessionInfoCallback(int etwSessionId, long matchAllKeywords,
        ref List<SessionInfo> sessionList)
    {
        uint n = (uint)SessionMask.FromEventKeywords((ulong)matchAllKeywords);
        if (bitcount(n) <= 1)
        {
            if (sessionList == null)
            {
                sessionList = new List<SessionInfo>(8);
            }

            if (bitcount(n) == 1)
            {
                sessionList.Add(new SessionInfo(bitindex(n) + 1, etwSessionId));
            }
            else
            {
                sessionList.Add(new SessionInfo(bitcount((uint)SessionMask.All) + 1, etwSessionId));
            }
        }
    }


    private static int IndexOfSessionInList(List<SessionInfo> sessions, int etwSessionId)
    {
        if (sessions == null)
        {
            return -1;
        }

        for (int i = 0; i < sessions.Count; i++)
        {
            if (sessions[i].etwSessionId == etwSessionId)
            {
                return i;
            }
        }

        return -1;
    }


    public bool IsEnabled()
    {
        return m_enabled;
    }

    public bool IsEnabled(byte level, long keywords)
    {
        if (!m_enabled)
        {
            return false;
        }

        if ((level <= m_level || m_level == 0) && (keywords == 0L ||
                                                   ((keywords & m_anyKeywordMask) != 0L &&
                                                    (keywords & m_allKeywordMask) == m_allKeywordMask)))
        {
            return true;
        }

        return false;
    }

    public static WriteEventErrorCode GetLastWriteEventError()
    {
        return s_returnCode;
    }

    private static void SetLastError(int error)
    {
        switch (error)
        {
            case 234:
            case 534:
                s_returnCode = WriteEventErrorCode.EventTooBig;
                break;
            case 8:
                s_returnCode = WriteEventErrorCode.NoFreeBuffers;
                break;
        }
    }

    private static unsafe object EncodeObject(ref object data, ref EventData* dataDescriptor, ref byte* dataBuffer,
        ref uint totalEventSize)
    {
        string text;
        byte[] array;
        while (true)
        {
            dataDescriptor->Reserved = 0u;
            text = data as string;
            array = null;
            if (text != null)
            {
                dataDescriptor->Size = (uint)((text.Length + 1) * 2);
                break;
            }

            if ((array = data as byte[]) != null)
            {
                *(int*)dataBuffer = array.Length;
                dataDescriptor->Ptr = (ulong)dataBuffer;
                dataDescriptor->Size = 4u;
                totalEventSize += dataDescriptor->Size;
                dataDescriptor++;
                dataBuffer += 16;
                dataDescriptor->Size = (uint)array.Length;
                break;
            }

            if (data is IntPtr)
            {
                dataDescriptor->Size = (uint)sizeof(IntPtr);
                IntPtr* ptr = (IntPtr*)dataBuffer;
                *ptr = (IntPtr)data;
                dataDescriptor->Ptr = (ulong)ptr;
                break;
            }

            if (data is int)
            {
                dataDescriptor->Size = 4u;
                int* ptr2 = (int*)dataBuffer;
                *ptr2 = (int)data;
                dataDescriptor->Ptr = (ulong)ptr2;
                break;
            }

            if (data is long)
            {
                dataDescriptor->Size = 8u;
                long* ptr3 = (long*)dataBuffer;
                *ptr3 = (long)data;
                dataDescriptor->Ptr = (ulong)ptr3;
                break;
            }

            if (data is uint)
            {
                dataDescriptor->Size = 4u;
                uint* ptr4 = (uint*)dataBuffer;
                *ptr4 = (uint)data;
                dataDescriptor->Ptr = (ulong)ptr4;
                break;
            }

            if (data is ulong)
            {
                dataDescriptor->Size = 8u;
                ulong* ptr5 = (ulong*)dataBuffer;
                *ptr5 = (ulong)data;
                dataDescriptor->Ptr = (ulong)ptr5;
                break;
            }

            if (data is char)
            {
                dataDescriptor->Size = 2u;
                char* ptr6 = (char*)dataBuffer;
                *ptr6 = (char)data;
                dataDescriptor->Ptr = (ulong)ptr6;
                break;
            }

            if (data is byte)
            {
                dataDescriptor->Size = 1u;
                byte* ptr7 = dataBuffer;
                *ptr7 = (byte)data;
                dataDescriptor->Ptr = (ulong)ptr7;
                break;
            }

            if (data is short)
            {
                dataDescriptor->Size = 2u;
                short* ptr8 = (short*)dataBuffer;
                *ptr8 = (short)data;
                dataDescriptor->Ptr = (ulong)ptr8;
                break;
            }

            if (data is sbyte)
            {
                dataDescriptor->Size = 1u;
                sbyte* ptr9 = (sbyte*)dataBuffer;
                *ptr9 = (sbyte)data;
                dataDescriptor->Ptr = (ulong)ptr9;
                break;
            }

            if (data is ushort)
            {
                dataDescriptor->Size = 2u;
                ushort* ptr10 = (ushort*)dataBuffer;
                *ptr10 = (ushort)data;
                dataDescriptor->Ptr = (ulong)ptr10;
                break;
            }

            if (data is float)
            {
                dataDescriptor->Size = 4u;
                float* ptr11 = (float*)dataBuffer;
                *ptr11 = (float)data;
                dataDescriptor->Ptr = (ulong)ptr11;
                break;
            }

            if (data is double)
            {
                dataDescriptor->Size = 8u;
                double* ptr12 = (double*)dataBuffer;
                *ptr12 = (double)data;
                dataDescriptor->Ptr = (ulong)ptr12;
                break;
            }

            if (data is bool)
            {
                dataDescriptor->Size = 4u;
                int* ptr13 = (int*)dataBuffer;
                if ((bool)data)
                {
                    *ptr13 = 1;
                }
                else
                {
                    *ptr13 = 0;
                }

                dataDescriptor->Ptr = (ulong)ptr13;
                break;
            }

            if (data is Guid)
            {
                dataDescriptor->Size = (uint)sizeof(Guid);
                Guid* ptr14 = (Guid*)dataBuffer;
                *ptr14 = (Guid)data;
                dataDescriptor->Ptr = (ulong)ptr14;
                break;
            }

            if (data is decimal)
            {
                dataDescriptor->Size = 16u;
                decimal* ptr15 = (decimal*)dataBuffer;
                *ptr15 = (decimal)data;
                dataDescriptor->Ptr = (ulong)ptr15;
                break;
            }

            if (data is DateTime)
            {
                long num = 0L;
                if (((DateTime)data).Ticks > 504911232000000000L)
                {
                    num = ((DateTime)data).ToFileTimeUtc();
                }

                dataDescriptor->Size = 8u;
                long* ptr16 = (long*)dataBuffer;
                *ptr16 = num;
                dataDescriptor->Ptr = (ulong)ptr16;
                break;
            }

            if (data is Enum)
            {
                Type underlyingType = Enum.GetUnderlyingType(data.GetType());
                if (underlyingType == typeof(int))
                {
                    data = ((IConvertible)data).ToInt32(null);
                    continue;
                }

                if (underlyingType == typeof(long))
                {
                    data = ((IConvertible)data).ToInt64(null);
                    continue;
                }
            }

            text = ((data != null) ? data.ToString() : "");
            dataDescriptor->Size = (uint)((text.Length + 1) * 2);
            break;
        }

        totalEventSize += dataDescriptor->Size;
        dataDescriptor++;
        dataBuffer += 16;
        return ((object)text) ?? ((object)array);
    }

    internal unsafe bool WriteEvent(ref EventDescriptor eventDescriptor, IntPtr eventHandle, Guid* activityID,
        Guid* childActivityID, params object[] eventPayload)
    {
        int num = 0;
        if (IsEnabled(eventDescriptor.Level, eventDescriptor.Keywords))
        {
            int num2 = 0;
            num2 = eventPayload.Length;
            if (num2 > 128)
            {
                s_returnCode = WriteEventErrorCode.TooManyArgs;
                return false;
            }

            uint totalEventSize = 0u;
            int i = 0;
            List<int> list = new List<int>(8);
            List<object> list2 = new List<object>(8);
            EventData* ptr = stackalloc EventData[2 * num2];
            for (int j = 0; j < 2 * num2; j++)
            {
                ptr[j] = default(EventData);
            }

            EventData* dataDescriptor = ptr;
            byte* ptr2 = stackalloc byte[(int)(uint)(32 * num2)];
            byte* dataBuffer = ptr2;
            bool flag = false;
            for (int k = 0; k < eventPayload.Length; k++)
            {
                if (eventPayload[k] != null)
                {
                    object obj = EncodeObject(ref eventPayload[k], ref dataDescriptor, ref dataBuffer,
                        ref totalEventSize);
                    if (obj == null)
                    {
                        continue;
                    }

                    int num3 = (int)(dataDescriptor - ptr - 1);
                    if (!(obj is string))
                    {
                        if (eventPayload.Length + num3 + 1 - k > 128)
                        {
                            s_returnCode = WriteEventErrorCode.TooManyArgs;
                            return false;
                        }

                        flag = true;
                    }

                    list2.Add(obj);
                    list.Add(num3);
                    i++;
                    continue;
                }

                s_returnCode = WriteEventErrorCode.NullInput;
                return false;
            }

            num2 = (int)(dataDescriptor - ptr);
            if (totalEventSize > 65482)
            {
                s_returnCode = WriteEventErrorCode.EventTooBig;
                return false;
            }

            if (!flag && i < 8)
            {
                for (; i < 8; i++)
                {
                    list2.Add(null);
                }

                fixed (char* ptr3 = (string)list2[0])
                {
                    fixed (char* ptr4 = (string)list2[1])
                    {
                        fixed (char* ptr5 = (string)list2[2])
                        {
                            fixed (char* ptr6 = (string)list2[3])
                            {
                                fixed (char* ptr7 = (string)list2[4])
                                {
                                    fixed (char* ptr8 = (string)list2[5])
                                    {
                                        fixed (char* ptr9 = (string)list2[6])
                                        {
                                            fixed (char* ptr10 = (string)list2[7])
                                            {
                                                dataDescriptor = ptr;
                                                if (list2[0] != null)
                                                {
                                                    dataDescriptor[list[0]].Ptr = (ulong)ptr3;
                                                }

                                                if (list2[1] != null)
                                                {
                                                    dataDescriptor[list[1]].Ptr = (ulong)ptr4;
                                                }

                                                if (list2[2] != null)
                                                {
                                                    dataDescriptor[list[2]].Ptr = (ulong)ptr5;
                                                }

                                                if (list2[3] != null)
                                                {
                                                    dataDescriptor[list[3]].Ptr = (ulong)ptr6;
                                                }

                                                if (list2[4] != null)
                                                {
                                                    dataDescriptor[list[4]].Ptr = (ulong)ptr7;
                                                }

                                                if (list2[5] != null)
                                                {
                                                    dataDescriptor[list[5]].Ptr = (ulong)ptr8;
                                                }

                                                if (list2[6] != null)
                                                {
                                                    dataDescriptor[list[6]].Ptr = (ulong)ptr9;
                                                }

                                                if (list2[7] != null)
                                                {
                                                    dataDescriptor[list[7]].Ptr = (ulong)ptr10;
                                                }

                                                // num = m_eventProvider.EventWriteTransferWrapper(m_regHandle,
                                                //     ref eventDescriptor, eventHandle, activityID, childActivityID, num2,
                                                //     ptr);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                dataDescriptor = ptr;
                GCHandle[] array = new GCHandle[i];
                for (int l = 0; l < i; l++)
                {
                    array[l] = GCHandle.Alloc(list2[l], GCHandleType.Pinned);
                    if (list2[l] is string)
                    {
                        fixed (char* ptr11 = (string)list2[l])
                        {
                            dataDescriptor[list[l]].Ptr = (ulong)ptr11;
                        }
                    }
                    else
                    {
                        fixed (byte* ptr12 = (byte[])list2[l])
                        {
                            dataDescriptor[list[l]].Ptr = (ulong)ptr12;
                        }
                    }
                }

                // num = m_eventProvider.EventWriteTransferWrapper(m_regHandle, ref eventDescriptor, eventHandle,
                //     activityID, childActivityID, num2, ptr);
                for (int m = 0; m < i; m++)
                {
                    array[m].Free();
                }
            }
        }

        if (num != 0)
        {
            SetLastError(num);
            return false;
        }

        return true;
    }
    

    private uint EventUnregister(long registrationHandle)
    {
        return m_eventProvider.EventUnregister(registrationHandle);
    }

    private static int bitcount(uint n)
    {
        int num = 0;
        while (n != 0)
        {
            num += nibblebits[n & 0xF];
            n >>= 4;
        }

        return num;
    }

    private static int bitindex(uint n)
    {
        int i;
        for (i = 0; (n & (1 << i)) == 0L; i++)
        {
        }

        return i;
    }

    public EventProviderEx(Guid providerGuid) : base(providerGuid)
    {
    }
}