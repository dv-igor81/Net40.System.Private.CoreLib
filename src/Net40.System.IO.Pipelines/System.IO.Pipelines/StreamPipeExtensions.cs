using System.Threading;
using System.Threading.Tasks;

namespace System.IO.Pipelines;

public static class StreamPipeExtensions
{
	public static Task CopyToAsync(this Stream source, PipeWriter destination, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (source == null)
		{
			throw new ArgumentNullException("source");
		}
		if (destination == null)
		{
			throw new ArgumentNullException("destination");
		}
		if (cancellationToken.IsCancellationRequested)
		{
			return TaskExEx.FromCanceled(cancellationToken);
		}
		return destination.CopyFromAsync(source, cancellationToken);
	}
}
