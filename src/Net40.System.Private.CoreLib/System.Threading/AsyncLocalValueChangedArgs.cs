using System.Diagnostics.CodeAnalysis;

namespace System.Threading;

public readonly struct AsyncLocalValueChangedArgs<T>
{
	public T PreviousValue
	{
		[return: MaybeNull]
		get;
	}

	public T CurrentValue
	{
		[return: MaybeNull]
		get;
	}

	public bool ThreadContextChanged { get; }

	internal AsyncLocalValueChangedArgs([AllowNull] T previousValue, [AllowNull] T currentValue, bool contextChanged)
	{
		PreviousValue = previousValue;
		CurrentValue = currentValue;
		ThreadContextChanged = contextChanged;
	}
}
