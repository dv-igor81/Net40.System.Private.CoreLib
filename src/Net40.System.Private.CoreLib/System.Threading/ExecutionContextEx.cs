using System.Diagnostics;
using System.Runtime.Serialization;

namespace System.Threading;

internal static class ThreadEx
{
    internal static ExecutionContextEx ExecutionContext(this Thread thread, IAsyncLocal? local)
    {
        return new ExecutionContextEx(thread.ExecutionContext, local);
    }

    internal static SynchronizationContext SynchronizationContext(this Thread thread)
    {
        return new SynchronizationContext();
    }
}

public sealed class ExecutionContextEx : IDisposable, ISerializable
{
    
    private static readonly ExecutionContextEx Default = new ExecutionContextEx(isDefault: true);

    private static readonly ExecutionContextEx DefaultFlowSuppressed =
        new ExecutionContextEx(AsyncLocalValueMap.Empty, ArrayEx.Empty<IAsyncLocal>(), isFlowSuppressed: true);

    private readonly ExecutionContext ExecContext = ExecutionContext.Capture();

    private readonly IAsyncLocalValueMap? m_localValues;
    private readonly IAsyncLocal[]? m_localChangeNotifications;
    private readonly bool m_isFlowSuppressed;
    private readonly bool m_isDefault;

    internal ExecutionContextEx(ExecutionContext context, IAsyncLocal? local)
    {
        ExecContext = context;
        m_localValues = AsyncLocalValueMap.Empty;
        m_localChangeNotifications = new IAsyncLocal[1];
        if (local != null)
        {
            m_localChangeNotifications[0] = local;
        }
        m_isFlowSuppressed = true;
    }

    private ExecutionContextEx(bool isDefault)
    {
        m_isDefault = isDefault;
    }

    private ExecutionContextEx(
        IAsyncLocalValueMap localValues,
        IAsyncLocal[]? localChangeNotifications,
        bool isFlowSuppressed)
    {
        m_localValues = localValues;
        m_localChangeNotifications = localChangeNotifications;
        m_isFlowSuppressed = isFlowSuppressed;
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        throw new PlatformNotSupportedException();
    }
    
    private bool IsDefault => m_isDefault;

    [Conditional("DEBUG")]
    internal static void CheckThreadPoolAndContextsAreDefault()
    {
        Debug.Assert(Thread.CurrentThread.IsThreadPoolThread);
        Debug.Assert(Thread.CurrentThread.ExecutionContext == null,
            "ThreadPool thread not on Default ExecutionContext.");
        Debug.Assert(Thread.CurrentThread.SynchronizationContext() == null,
            "ThreadPool thread not on Default SynchronizationContext.");
    }
    
    internal static object? GetLocalValue(IAsyncLocal? local)
    {
        ExecutionContextEx current = Thread.CurrentThread.ExecutionContext(local);

        Debug.Assert(!current.IsDefault);
        Debug.Assert(current.m_localValues != null,
            "Only the default context should have null, and we shouldn't be here on the default context");
        current.m_localValues.TryGetValue(local, out object? value);
        return value;
    }

    internal static void SetLocalValue(IAsyncLocal local, object? newValue, bool needChangeNotifications)
    {
        ExecutionContextEx? current = Thread.CurrentThread.ExecutionContext(local);

        object? previousValue = null;
        bool hadPreviousValue = false;
        if (current != null)
        {
            Debug.Assert(!current.IsDefault);
            Debug.Assert(current.m_localValues != null,
                "Only the default context should have null, and we shouldn't be here on the default context");

            hadPreviousValue = current.m_localValues.TryGetValue(local, out previousValue);
        }

        if (previousValue == newValue)
        {
            return;
        }

        // Regarding 'treatNullValueAsNonexistent: !needChangeNotifications' below:
        // - When change notifications are not necessary for this IAsyncLocal, there is no observable difference between
        //   storing a null value and removing the IAsyncLocal from 'm_localValues'
        // - When change notifications are necessary for this IAsyncLocal, the IAsyncLocal's absence in 'm_localValues'
        //   indicates that this is the first value change for the IAsyncLocal and it needs to be registered for change
        //   notifications. So in this case, a null value must be stored in 'm_localValues' to indicate that the IAsyncLocal
        //   is already registered for change notifications.
        IAsyncLocal[]? newChangeNotifications = null;
        IAsyncLocalValueMap newValues;
        bool isFlowSuppressed = false;
        if (current != null)
        {
            Debug.Assert(!current.IsDefault);
            Debug.Assert(current.m_localValues != null,
                "Only the default context should have null, and we shouldn't be here on the default context");

            isFlowSuppressed = current.m_isFlowSuppressed;
            newValues = current.m_localValues.Set(local, newValue,
                treatNullValueAsNonexistent: !needChangeNotifications);
            newChangeNotifications = current.m_localChangeNotifications;
        }
        else
        {
            // First AsyncLocal
            newValues = AsyncLocalValueMap.Create(local, newValue,
                treatNullValueAsNonexistent: !needChangeNotifications);
        }

        //
        // Either copy the change notification array, or create a new one, depending on whether we need to add a new item.
        //
        if (needChangeNotifications)
        {
            if (hadPreviousValue)
            {
                Debug.Assert(newChangeNotifications != null);
                Debug.Assert(Array.IndexOf(newChangeNotifications, local) >= 0);
            }
            else if (newChangeNotifications == null)
            {
                newChangeNotifications = new IAsyncLocal[1] { local };
            }
            else
            {
                int newNotificationIndex = newChangeNotifications.Length;
                Array.Resize(ref newChangeNotifications, newNotificationIndex + 1);
                newChangeNotifications[newNotificationIndex] = local;
            }
        }

        // Thread.CurrentThread._executionContext =
        //     (!isFlowSuppressed && AsyncLocalValueMap.IsEmpty(newValues))
        //         ? null
        //         : // No values, return to Default context
        //         new ExecutionContextEx(newValues, newChangeNotifications, isFlowSuppressed);

        if (needChangeNotifications)
        {
            local.OnValueChanged(previousValue, newValue, contextChanged: false);
        }
    }

    public ExecutionContext CreateCopy()
    {
        return this.ExecContext; // since CoreCLR's ExecutionContext is immutable, we don't need to create copies.
    }

    public void Dispose()
    {
        // For CLR compat only
    }
    
        /*internal static void OnValuesChanged(ExecutionContextEx? previousExecutionCtx, ExecutionContextEx? nextExecutionCtx)
    {
        Debug.Assert(previousExecutionCtx != nextExecutionCtx);

        // Collect Change Notifications 
        IAsyncLocal[]? previousChangeNotifications = previousExecutionCtx?.m_localChangeNotifications;
        IAsyncLocal[]? nextChangeNotifications = nextExecutionCtx?.m_localChangeNotifications;

        // At least one side must have notifications
        Debug.Assert(previousChangeNotifications != null || nextChangeNotifications != null);

        // Fire Change Notifications
        try
        {
            if (previousChangeNotifications != null && nextChangeNotifications != null)
            {
                // Notifications can't exist without values
                Debug.Assert(previousExecutionCtx!.m_localValues != null);
                Debug.Assert(nextExecutionCtx!.m_localValues != null);
                // Both contexts have change notifications, check previousExecutionCtx first
                foreach (IAsyncLocal local in previousChangeNotifications)
                {
                    previousExecutionCtx.m_localValues.TryGetValue(local, out object? previousValue);
                    nextExecutionCtx.m_localValues.TryGetValue(local, out object? currentValue);

                    if (previousValue != currentValue)
                    {
                        local.OnValueChanged(previousValue, currentValue, contextChanged: true);
                    }
                }

                if (nextChangeNotifications != previousChangeNotifications)
                {
                    // Check for additional notifications in nextExecutionCtx
                    foreach (IAsyncLocal local in nextChangeNotifications)
                    {
                        // If the local has a value in the previous context, we already fired the event 
                        // for that local in the code above.
                        if (!previousExecutionCtx.m_localValues.TryGetValue(local, out object? previousValue))
                        {
                            nextExecutionCtx.m_localValues.TryGetValue(local, out object? currentValue);
                            if (previousValue != currentValue)
                            {
                                local.OnValueChanged(previousValue, currentValue, contextChanged: true);
                            }
                        }
                    }
                }
            }
            else if (previousChangeNotifications != null)
            {
                // Notifications can't exist without values
                Debug.Assert(previousExecutionCtx!.m_localValues != null);
                // No current values, so just check previous against null
                foreach (IAsyncLocal local in previousChangeNotifications)
                {
                    previousExecutionCtx.m_localValues.TryGetValue(local, out object? previousValue);
                    if (previousValue != null)
                    {
                        local.OnValueChanged(previousValue, null, contextChanged: true);
                    }
                }
            }
            else // Implied: nextChangeNotifications != null
            {
                // Notifications can't exist without values
                Debug.Assert(nextExecutionCtx!.m_localValues != null);
                // No previous values, so just check current against null
                foreach (IAsyncLocal local in nextChangeNotifications!)
                {
                    nextExecutionCtx.m_localValues.TryGetValue(local, out object? currentValue);
                    if (currentValue != null)
                    {
                        local.OnValueChanged(null, currentValue, contextChanged: true);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Environment.FailFast(
                "SR.ExecutionContext_ExceptionInAsyncLocalNotification",
                ex);
        }
    }*/
}

public struct AsyncFlowControl : IDisposable
{
    private Thread? _thread;

    internal void Initialize(Thread currentThread)
    {
        Debug.Assert(currentThread == Thread.CurrentThread);
        _thread = currentThread;
    }

    private void Undo()
    {
        if (_thread == null)
        {
            throw new InvalidOperationException("SR.InvalidOperation_CannotUseAFCMultiple");
        }

        if (Thread.CurrentThread != _thread)
        {
            throw new InvalidOperationException("SR.InvalidOperation_CannotUseAFCOtherThread");
        }

        // An async flow control cannot be undone when a different execution context is applied. The desktop framework
        // mutates the execution context when its state changes, and only changes the instance when an execution context
        // is applied (for instance, through ExecutionContext.Run). The framework prevents a suppressed-flow execution
        // context from being applied by returning null from ExecutionContext.Capture, so the only type of execution
        // context that can be applied is one whose flow is not suppressed. After suppressing flow and changing an async
        // local's value, the desktop framework verifies that a different execution context has not been applied by
        // checking the execution context instance against the one saved from when flow was suppressed. In .NET Core,
        // since the execution context instance will change after changing the async local's value, it verifies that a
        // different execution context has not been applied, by instead ensuring that the current execution context's
        // flow is suppressed.
        if (!ExecutionContext.IsFlowSuppressed())
        {
            throw new InvalidOperationException("SR.InvalidOperation_AsyncFlowCtrlCtxMismatch");
        }

        _thread = null;
        ExecutionContext.RestoreFlow();
    }

    public void Dispose()
    {
        Undo();
    }

    public override bool Equals(object? obj)
    {
        return obj is AsyncFlowControl && Equals((AsyncFlowControl)obj);
    }

    private bool Equals(AsyncFlowControl obj)
    {
        return _thread == obj._thread;
    }

    public override int GetHashCode()
    {
        return _thread?.GetHashCode() ?? 0;
    }

    public static bool operator ==(AsyncFlowControl a, AsyncFlowControl b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(AsyncFlowControl a, AsyncFlowControl b)
    {
        return !(a == b);
    }
}