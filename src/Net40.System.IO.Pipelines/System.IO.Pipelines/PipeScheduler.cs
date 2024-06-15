namespace System.IO.Pipelines;

public abstract class PipeScheduler
{
	private static readonly ThreadPoolScheduler s_threadPoolScheduler = new ThreadPoolScheduler();

	private static readonly InlineScheduler s_inlineScheduler = new InlineScheduler();

	public static PipeScheduler ThreadPool => s_threadPoolScheduler;

	public static PipeScheduler Inline => s_inlineScheduler;

	public abstract void Schedule(Action<object> action, object state);

	internal virtual void UnsafeSchedule(Action<object> action, object state)
	{
		Schedule(action, state);
	}
}
