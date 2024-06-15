using System.Threading;

namespace System.IO.Pipelines;

internal sealed class ThreadPoolScheduler : PipeScheduler
{
	public override void Schedule(Action<object> action, object state)
	{
		System.Threading.ThreadPool.QueueUserWorkItem(delegate(object s)
		{
			Tuple<Action<object>, object> tuple = (Tuple<Action<object>, object>)s;
			tuple.Item1(tuple.Item2);
		}, Tuple.Create(action, state));
	}
}
