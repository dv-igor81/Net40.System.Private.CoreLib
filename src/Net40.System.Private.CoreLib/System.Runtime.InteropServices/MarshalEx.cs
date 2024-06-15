namespace System.Runtime.InteropServices;

public static class MarshalEx
{
    public static TDelegate GetDelegateForFunctionPointer<TDelegate>(IntPtr ptr)
    {
        return (TDelegate)(object)Marshal.GetDelegateForFunctionPointer(ptr, typeof(TDelegate));
    }
    
    public static int SizeOf<T>()
    {
        return Marshal.SizeOf(typeof(T));
    }
    
    public static T PtrToStructure<T>(IntPtr ptr)
    {
        return (T)Marshal.PtrToStructure(ptr, typeof(T));
    }

}