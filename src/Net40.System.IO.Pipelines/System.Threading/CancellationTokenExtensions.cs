namespace System.Threading;

internal static class CancellationTokenExtensions
{
	internal static CancellationTokenRegistration UnsafeRegister(this CancellationToken cancellationToken, Action<object> callback, object state)
	{
		return cancellationToken.Register(callback, state);
	}
}
