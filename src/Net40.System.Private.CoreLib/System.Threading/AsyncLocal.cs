// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace System.Threading;

public sealed class AsyncLocal<T> : IAsyncLocal
{
    private readonly Action<AsyncLocalValueChangedArgs<T>>? m_valueChangedHandler;

    //
    // Constructs an AsyncLocal<T> that does not receive change notifications.
    //
    public AsyncLocal()
    {
    }

    //
    // Constructs an AsyncLocal<T> with a delegate that is called whenever the current value changes
    // on any thread.
    //
    public AsyncLocal(Action<AsyncLocalValueChangedArgs<T>>? valueChangedHandler)
    {
        m_valueChangedHandler = valueChangedHandler;
    }

    [MaybeNull]
    public T Value
    {
        get
        {
            object? obj = ExecutionContextEx.GetLocalValue(this);
            return (obj == null) ? default : (T)obj;
        }
        set => ExecutionContextEx.SetLocalValue(this, value, m_valueChangedHandler != null);
    }

    void IAsyncLocal.OnValueChanged(object? previousValueObj, object? currentValueObj, bool contextChanged)
    {
        Debug.Assert(m_valueChangedHandler != null);
        T previousValue = previousValueObj == null ? default! : (T)previousValueObj;
        T currentValue = currentValueObj == null ? default! : (T)currentValueObj;
        m_valueChangedHandler(new AsyncLocalValueChangedArgs<T>(previousValue, currentValue, contextChanged));
    }
}