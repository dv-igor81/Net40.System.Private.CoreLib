using System.Runtime.CompilerServices;

namespace System;

internal struct NUInt
{
	private readonly unsafe void* _value;

	private unsafe NUInt(uint value)
	{
		_value = (void*)value;
	}

	private unsafe NUInt(ulong value)
	{
		_value = (void*)value;
	}

	public static implicit operator NUInt(uint value)
	{
		return new NUInt(value);
	}

	public static unsafe implicit operator IntPtr(NUInt value)
	{
		return (IntPtr)value._value;
	}

	public static explicit operator NUInt(int value)
	{
		return new NUInt((uint)value);
	}

	public static unsafe explicit operator void*(NUInt value)
	{
		return value._value;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static unsafe NUInt operator *(NUInt left, NUInt right)
	{
		if (sizeof(IntPtr) != 4)
		{
			return new NUInt((ulong)left._value * (ulong)right._value);
		}
		return new NUInt((uint)((int)left._value * (int)right._value));
	}
}
