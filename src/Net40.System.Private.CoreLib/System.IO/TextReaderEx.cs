using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO;

public static class TextReaderEx
{
    public static Task<int> ReadAsync(this TextReader reader, char[] buffer, int index, int count)
    {
        if (buffer == null)
        {
            throw new ArgumentNullException("buffer", SR.ArgumentNull_Buffer);
        }
        if (index < 0 || count < 0)
        {
            throw new ArgumentOutOfRangeException((index < 0) ? "index" : "count", SR.ArgumentOutOfRange_NeedNonNegNum);
        }
        if (buffer.Length - index < count)
        {
            throw new ArgumentException(SR.Argument_InvalidOffLen);
        }
        return reader.ReadAsyncInternal(new Memory<char>(buffer, index, count), default(CancellationToken)).AsTask();
    }

    private static ValueTask<int> ReadAsyncInternal(this TextReader reader, Memory<char> buffer, CancellationToken cancellationToken)
    {
        Tuple<TextReader, Memory<char>> state2 = new Tuple<TextReader, Memory<char>>(reader, buffer);
        return new ValueTask<int>(Task<int>.Factory.StartNew(delegate(object state)
        {
            Tuple<TextReader, Memory<char>> tuple = (Tuple<TextReader, Memory<char>>)state;
            return tuple.Item1.Read(tuple.Item2.Span);
        }, state2, cancellationToken, 
            //TaskCreationOptions.DenyChildAttach,
            TaskCreationOptions.None, // DIA-Замена
            TaskScheduler.Default));
    }

    private static int Read(this TextReader reader, Span<char> buffer)
    {
        char[] array = ArrayPool<char>.Shared.Rent(buffer.Length);
        try
        {
            int num = reader.Read(array, 0, buffer.Length);
            if ((uint)num > (uint)buffer.Length)
            {
                throw new IOException("SR.IO_InvalidReadLength");
            }
            new Span<char>(array, 0, num).CopyTo(buffer);
            return num;
        }
        finally
        {
            ArrayPool<char>.Shared.Return(array);
        }
    }

}