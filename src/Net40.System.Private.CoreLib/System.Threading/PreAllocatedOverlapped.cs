﻿namespace System.Threading;

public sealed class PreAllocatedOverlapped : IDisposable, IDeferredDisposable
{
    internal readonly ThreadPoolBoundHandleOverlapped _overlapped;

    private DeferredDisposableLifetime<PreAllocatedOverlapped> _lifetime;

    [CLSCompliant(false)]
    public PreAllocatedOverlapped(IOCompletionCallback callback, object? state, object? pinData)
    {
        if (callback == null)
        {
            throw new ArgumentNullException("callback");
        }
        _overlapped = new ThreadPoolBoundHandleOverlapped(callback, state, pinData, this);
    }

    internal bool AddRef()
    {
        return _lifetime.AddRef(this);
    }

    internal void Release()
    {
        _lifetime.Release(this);
    }

    public void Dispose()
    {
        _lifetime.Dispose(this);
        GC.SuppressFinalize(this);
    }

    ~PreAllocatedOverlapped()
    {
        Dispose();
    }

    unsafe void IDeferredDisposable.OnFinalRelease(bool disposed)
    {
        if (_overlapped != null)
        {
            if (disposed)
            {
                Overlapped.Free(_overlapped._nativeOverlapped);
                return;
            }
            _overlapped._boundHandle = null;
            _overlapped._completed = false;
            *_overlapped._nativeOverlapped = default(NativeOverlapped);
        }
    }
}