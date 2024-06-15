using System.Threading.Tasks;

namespace System.IO;

public static class StreamReaderEx
{
    public static Task<string> ReadToEndAsync(this StreamReader reader)
    {
        // if (reader.GetType() != typeof(StreamReader))
        // {
        //     //return base.ReadToEndAsync();
        // }
        //ThrowIfDisposed();
        //CheckAsyncTaskInProgress();
        //return (Task<string>)(_asyncReadTask = ReadToEndAsyncInternal());
        return TaskEx.Run(reader.ReadToEnd);
    }
}