using System.Collections;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.IO;

namespace System.Net;

using Socket = System.Net.Sockets.Net40.Socket;


[EventSource(Name = "Microsoft-System-Net-Sockets", Guid = "e03c0352-f9c9-56ff-0ea7-b94ba8cabc6b",
    LocalizationResources = "FxResources.System.Net.Sockets.SR")]
internal sealed class NetEventSource : EventSource
{
    public class Keywords
    {
        public const EventKeywords Default = (EventKeywords)1L;

        public const EventKeywords Debug = (EventKeywords)2L;

        public const EventKeywords EnterExit = (EventKeywords)4L;
    }

    public static readonly NetEventSource Log = new NetEventSource();

    public new static bool IsEnabled => Log.IsEnabled();

    [NonEvent]
    public static void Accepted(Socket socket, object remoteEp, object localEp)
    {
        if (IsEnabled)
        {
            Log.Accepted(IdOf(remoteEp), IdOf(localEp), GetHashCode(socket));
        }
    }

    [Event(17, Keywords = (EventKeywords)1L, Level = EventLevel.Informational)]
    private void Accepted(string remoteEp, string localEp, int socketHash)
    {
        WriteEvent(17, remoteEp, localEp, socketHash);
    }

    [NonEvent]
    public static void Connected(Socket socket, object localEp, object remoteEp)
    {
        if (IsEnabled)
        {
            Log.Connected(IdOf(localEp), IdOf(remoteEp), GetHashCode(socket));
        }
    }

    [Event(18, Keywords = (EventKeywords)1L, Level = EventLevel.Informational)]
    private void Connected(string localEp, string remoteEp, int socketHash)
    {
        WriteEvent(18, localEp, remoteEp, socketHash);
    }

    [NonEvent]
    public static void ConnectedAsyncDns(Socket socket)
    {
        if (IsEnabled)
        {
            Log.ConnectedAsyncDns(GetHashCode(socket));
        }
    }

    [Event(19, Keywords = (EventKeywords)1L, Level = EventLevel.Informational)]
    private void ConnectedAsyncDns(int socketHash)
    {
        WriteEvent(19, socketHash);
    }

    [NonEvent]
    public static void DumpBuffer(object thisOrContextObject, Memory<byte> buffer, int offset, int count,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            if (offset < 0 || offset > buffer.Length - count)
            {
                Fail(thisOrContextObject,
                    FormattableStringFactory.Create("Invalid {0} Args. Length={1}, Offset={2}, Count={3}",
                        "DumpBuffer", buffer.Length, offset, count), memberName);
            }
            else
            {
                buffer = buffer.Slice(offset, Math.Min(count, 1024));
                ArraySegment<byte> segment;
                byte[] buffer2 =
                    ((MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out segment) && segment.Offset == 0 &&
                      segment.Count == buffer.Length)
                        ? segment.Array
                        : buffer.ToArray());
                Log.DumpBuffer(IdOf(thisOrContextObject), memberName, buffer2);
            }
        }
    }

    [NonEvent]
    public static void Enter(object thisOrContextObject, FormattableString formattableString = null,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.Enter(IdOf(thisOrContextObject), memberName,
                (formattableString != null) ? Format(formattableString) : "");
        }
    }

    [NonEvent]
    public static void Enter(object thisOrContextObject, object arg0, [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.Enter(IdOf(thisOrContextObject), memberName, $"({Format(arg0)})");
        }
    }

    [NonEvent]
    public static void Enter(object thisOrContextObject, object arg0, object arg1,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.Enter(IdOf(thisOrContextObject), memberName, $"({Format(arg0)}, {Format(arg1)})");
        }
    }

    [NonEvent]
    public static void Enter(object thisOrContextObject, object arg0, object arg1, object arg2,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.Enter(IdOf(thisOrContextObject), memberName, $"({Format(arg0)}, {Format(arg1)}, {Format(arg2)})");
        }
    }

    [Event(1, Level = EventLevel.Informational, Keywords = (EventKeywords)4L)]
    private void Enter(string thisOrContextObject, string memberName, string parameters)
    {
        WriteEvent(1, thisOrContextObject, memberName ?? "(?)", parameters);
    }

    [NonEvent]
    public static void Exit(object thisOrContextObject, FormattableString formattableString = null,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.Exit(IdOf(thisOrContextObject), memberName,
                (formattableString != null) ? Format(formattableString) : "");
        }
    }

    [NonEvent]
    public static void Exit(object thisOrContextObject, object arg0, [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.Exit(IdOf(thisOrContextObject), memberName, Format(arg0).ToString());
        }
    }

    [Event(2, Level = EventLevel.Informational, Keywords = (EventKeywords)4L)]
    private void Exit(string thisOrContextObject, string memberName, string result)
    {
        WriteEvent(2, thisOrContextObject, memberName ?? "(?)", result);
    }

    [NonEvent]
    public static void Info(object thisOrContextObject, FormattableString formattableString = null,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.Info(IdOf(thisOrContextObject), memberName,
                (formattableString != null) ? Format(formattableString) : "");
        }
    }

    [NonEvent]
    public static void Info(object thisOrContextObject, object message, [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.Info(IdOf(thisOrContextObject), memberName, Format(message).ToString());
        }
    }

    [Event(4, Level = EventLevel.Informational, Keywords = (EventKeywords)1L)]
    private void Info(string thisOrContextObject, string memberName, string message)
    {
        WriteEvent(4, thisOrContextObject, memberName ?? "(?)", message);
    }

    [NonEvent]
    public static void Error(object thisOrContextObject, FormattableString formattableString,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.ErrorMessage(IdOf(thisOrContextObject), memberName, Format(formattableString));
        }
    }

    [NonEvent]
    public static void Error(object thisOrContextObject, object message,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.ErrorMessage(IdOf(thisOrContextObject), memberName, Format(message).ToString());
        }
    }

    [Event(5, Level = EventLevel.Warning, Keywords = (EventKeywords)1L)]
    private void ErrorMessage(string thisOrContextObject, string memberName, string message)
    {
        WriteEvent(5, thisOrContextObject, memberName ?? "(?)", message);
    }

    [NonEvent]
    public static void Fail(object thisOrContextObject, FormattableString formattableString,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.CriticalFailure(IdOf(thisOrContextObject), memberName, Format(formattableString));
        }
    }

    [NonEvent]
    public static void Fail(object thisOrContextObject, object message, [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            Log.CriticalFailure(IdOf(thisOrContextObject), memberName, Format(message).ToString());
        }
    }

    [Event(6, Level = EventLevel.Critical, Keywords = (EventKeywords)2L)]
    private void CriticalFailure(string thisOrContextObject, string memberName, string message)
    {
        WriteEvent(6, thisOrContextObject, memberName ?? "(?)", message);
    }

    [NonEvent]
    public static void DumpBuffer(object thisOrContextObject, byte[] buffer,
        [CallerMemberName] string memberName = null)
    {
        DumpBuffer(thisOrContextObject, buffer, 0, buffer.Length, memberName);
    }

    [NonEvent]
    public static void DumpBuffer(object thisOrContextObject, byte[] buffer, int offset, int count,
        [CallerMemberName] string memberName = null)
    {
        if (!IsEnabled)
        {
            return;
        }

        if (offset < 0 || offset > buffer.Length - count)
        {
            Fail(thisOrContextObject,
                FormattableStringFactory.Create("Invalid {0} Args. Length={1}, Offset={2}, Count={3}", "DumpBuffer",
                    buffer.Length, offset, count), memberName);
            return;
        }

        count = Math.Min(count, 1024);
        byte[] array = buffer;
        if (offset != 0 || count != buffer.Length)
        {
            array = new byte[count];
            Buffer.BlockCopy(buffer, offset, array, 0, count);
        }

        Log.DumpBuffer(IdOf(thisOrContextObject), memberName, array);
    }

    [NonEvent]
    public static unsafe void DumpBuffer(object thisOrContextObject, IntPtr bufferPtr, int count,
        [CallerMemberName] string memberName = null)
    {
        if (IsEnabled)
        {
            byte[] array = new byte[Math.Min(count, 1024)];
            fixed (byte* destination = array)
            {
                BufferEx.MemoryCopy((void*)bufferPtr, destination, array.Length, array.Length);
            }

            Log.DumpBuffer(IdOf(thisOrContextObject), memberName, array);
        }
    }

    [Event(7, Level = EventLevel.Verbose, Keywords = (EventKeywords)2L)]
    private void DumpBuffer(string thisOrContextObject, string memberName, byte[] buffer)
    {
        WriteEvent(7, thisOrContextObject, memberName ?? "(?)", buffer);
    }

    [NonEvent]
    public static string IdOf(object value)
    {
        if (value == null)
        {
            return "(null)";
        }

        return value.GetType().Name + "#" + GetHashCode(value);
    }

    [NonEvent]
    public static int GetHashCode(object value)
    {
        return value?.GetHashCode() ?? 0;
    }

    [NonEvent]
    public static object Format(object value)
    {
        if (value == null)
        {
            return "(null)";
        }

        string text = null;
        if (text != null)
        {
            return text;
        }

        if (value is Array array)
        {
            return $"{array.GetType().GetElementType()}[{((Array)value).Length}]";
        }

        if (value is ICollection collection)
        {
            return $"{collection.GetType().Name}({collection.Count})";
        }

        if (value is SafeHandle safeHandle)
        {
            return $"{safeHandle.GetType().Name}:{safeHandle.GetHashCode()}(0x{safeHandle.DangerousGetHandle():X})";
        }

        if (value is IntPtr)
        {
            return $"0x{value:X}";
        }

        string text2 = value.ToString();
        if (text2 == null || text2 == value.GetType().FullName)
        {
            return IdOf(value);
        }

        return value;
    }

    [NonEvent]
    private static string Format(FormattableString s)
    {
        switch (s.ArgumentCount)
        {
            case 0:
                return s.Format;
            case 1:
                return string.Format(s.Format, Format(s.GetArgument(0)));
            case 2:
                return string.Format(s.Format, Format(s.GetArgument(0)), Format(s.GetArgument(1)));
            case 3:
                return string.Format(s.Format, Format(s.GetArgument(0)), Format(s.GetArgument(1)),
                    Format(s.GetArgument(2)));
            default:
            {
                object[] arguments = s.GetArguments();
                object[] array = new object[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                {
                    array[i] = Format(arguments[i]);
                }

                return string.Format(s.Format, array);
            }
        }
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, string arg1, string arg2, byte[] arg3)
    {
        //The blocks IL_004b, IL_004f, IL_0051, IL_006a, IL_0151 are reachable both inside and outside the pinned region starting at IL_0048. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
        if (!IsEnabled())
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
            arg3 = ArrayEx.Empty<byte>();
        }

        fixed (char* ptr5 = arg1)
        {
            char* intPtr;
            byte[] array;
            int size;
            EventData* intPtr2;
            IntPtr intPtr3;
            IntPtr intPtr4;
            IntPtr intPtr5;
            if (arg2 == null)
            {
                char* ptr;
                intPtr = (ptr = null);
                array = arg3;
                fixed (byte* ptr2 = array)
                {
                    byte* ptr3 = ptr2;
                    size = arg3.Length;
                    EventData* ptr4 = stackalloc EventData[4];
                    intPtr2 = ptr4;
                    *intPtr2 = new EventData
                    {
                        DataPointer = (IntPtr)ptr5,
                        Size = (arg1.Length + 1) * 2
                    };
                    intPtr3 = (IntPtr)(ptr4 + 1);
                    *(EventData*)intPtr3 = new EventData
                    {
                        DataPointer = (IntPtr)ptr,
                        Size = (arg2.Length + 1) * 2
                    };
                    intPtr4 = (IntPtr)(ptr4 + 2);
                    *(EventData*)intPtr4 = new EventData
                    {
                        DataPointer = (IntPtr)(&size),
                        Size = 4
                    };
                    intPtr5 = (IntPtr)(ptr4 + 3);
                    *(EventData*)intPtr5 = new EventData
                    {
                        DataPointer = (IntPtr)ptr3,
                        Size = size
                    };
                    WriteEventCore(eventId, 4, ptr4);
                }

                return;
            }

            fixed (char* ptr6 = &arg2.GetPinnableReference())
            {
                char* ptr;
                intPtr = (ptr = ptr6);
                array = arg3;
                fixed (byte* ptr2 = array)
                {
                    byte* ptr3 = ptr2;
                    size = arg3.Length;
                    EventData* ptr4 = stackalloc EventData[4];
                    intPtr2 = ptr4;
                    *intPtr2 = new EventData
                    {
                        DataPointer = (IntPtr)ptr5,
                        Size = (arg1.Length + 1) * 2
                    };
                    intPtr3 = (IntPtr)(ptr4 + 1);
                    *(EventData*)intPtr3 = new EventData
                    {
                        DataPointer = (IntPtr)ptr,
                        Size = (arg2.Length + 1) * 2
                    };
                    intPtr4 = (IntPtr)(ptr4 + 2);
                    *(EventData*)intPtr4 = new EventData
                    {
                        DataPointer = (IntPtr)(&size),
                        Size = 4
                    };
                    intPtr5 = (IntPtr)(ptr4 + 3);
                    *(EventData*)intPtr5 = new EventData
                    {
                        DataPointer = (IntPtr)ptr3,
                        Size = size
                    };
                    WriteEventCore(eventId, 4, ptr4);
                }
            }
        }
    }

    [NonEvent]
    private unsafe void WriteEvent(int eventId, string arg1, string arg2, int arg3)
    {
        //The blocks IL_0040 are reachable both inside and outside the pinned region starting at IL_003d. ILSpy has duplicated these blocks in order to place them both within and outside the `fixed` statement.
        if (!IsEnabled())
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

        fixed (char* ptr3 = arg1)
        {
            char* intPtr;
            EventData* intPtr2;
            IntPtr intPtr3;
            IntPtr intPtr4;
            if (arg2 == null)
            {
                char* ptr;
                intPtr = (ptr = null);
                EventData* ptr2 = stackalloc EventData[3];
                intPtr2 = ptr2;
                *intPtr2 = new EventData
                {
                    DataPointer = (IntPtr)ptr3,
                    Size = (arg1.Length + 1) * 2
                };
                intPtr3 = (IntPtr)(ptr2 + 1);
                *(EventData*)intPtr3 = new EventData
                {
                    DataPointer = (IntPtr)ptr,
                    Size = (arg2.Length + 1) * 2
                };
                intPtr4 = (IntPtr)(ptr2 + 2);
                *(EventData*)intPtr4 = new EventData
                {
                    DataPointer = (IntPtr)(&arg3),
                    Size = 4
                };
                WriteEventCore(eventId, 3, ptr2);
                return;
            }

            fixed (char* ptr4 = &arg2.GetPinnableReference())
            {
                char* ptr;
                intPtr = (ptr = ptr4);
                EventData* ptr2 = stackalloc EventData[3];
                intPtr2 = ptr2;
                *intPtr2 = new EventData
                {
                    DataPointer = (IntPtr)ptr3,
                    Size = (arg1.Length + 1) * 2
                };
                intPtr3 = (IntPtr)(ptr2 + 1);
                *(EventData*)intPtr3 = new EventData
                {
                    DataPointer = (IntPtr)ptr,
                    Size = (arg2.Length + 1) * 2
                };
                intPtr4 = (IntPtr)(ptr2 + 2);
                *(EventData*)intPtr4 = new EventData
                {
                    DataPointer = (IntPtr)(&arg3),
                    Size = 4
                };
                WriteEventCore(eventId, 3, ptr2);
            }
        }
    }
}