namespace System.Threading;

internal interface IAsyncLocalValueMap
{
	bool TryGetValue(System.Threading.IAsyncLocal key, out object? value);

	System.Threading.IAsyncLocalValueMap Set(System.Threading.IAsyncLocal key, object? value, bool treatNullValueAsNonexistent);
}
