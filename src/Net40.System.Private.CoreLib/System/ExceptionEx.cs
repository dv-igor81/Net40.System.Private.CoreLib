namespace System;

public class ExceptionEx : Exception
{
    public int HResult => base.HResult;
}