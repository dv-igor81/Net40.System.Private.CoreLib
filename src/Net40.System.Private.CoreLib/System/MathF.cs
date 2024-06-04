using System.Runtime.CompilerServices;

namespace System;

internal static class MathF
{
	public const float PI = 3.1415927f;

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float Abs(float x)
	{
		return Math.Abs(x);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float Acos(float x)
	{
		return (float)Math.Acos(x);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float Cos(float x)
	{
		return (float)Math.Cos(x);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float IEEERemainder(float x, float y)
	{
		return (float)Math.IEEERemainder(x, y);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float Pow(float x, float y)
	{
		return (float)Math.Pow(x, y);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float Sin(float x)
	{
		return (float)Math.Sin(x);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float Sqrt(float x)
	{
		return (float)Math.Sqrt(x);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static float Tan(float x)
	{
		return (float)Math.Tan(x);
	}
}
