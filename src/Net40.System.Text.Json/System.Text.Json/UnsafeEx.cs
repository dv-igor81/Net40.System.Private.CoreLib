namespace System.Text.Json;

public static class UnsafeEx
{
	public static ref T Unbox<T>(object box) where T : struct
	{
		throw null;
	}
}
