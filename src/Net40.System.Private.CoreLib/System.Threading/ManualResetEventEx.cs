using Microsoft.Win32.SafeHandles;

namespace System.Threading;

public static class ManualResetEventEx
{
    public static SafeWaitHandle GetSafeWaitHandle(this WaitHandle waitHandle)
    {
        if (waitHandle == null)
        {
            throw new ArgumentNullException("waitHandle");
        }
        return waitHandle.SafeWaitHandle;
    }
}