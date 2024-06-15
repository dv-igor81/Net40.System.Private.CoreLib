namespace System.IO.Pipelines;

internal sealed class InlineScheduler : PipeScheduler
{
	public override void Schedule(Action<object> action, object state)
	{
		action(state);
	}

	internal override void UnsafeSchedule(Action<object> action, object state)
	{
		action(state);
	}
}
