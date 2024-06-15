using System;
using System.Text;

namespace Microsoft.IO;

public static class StringBuilderExEx
{
    public static void CopyTo(this StringBuilder self, int sourceIndex, Span<char> destination, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException("count", "SR.Arg_NegativeArgCount");
        }
        if (sourceIndex != 0)
        {
            throw new NotImplementedException();
        }
        if ((uint)sourceIndex > (uint)self.Length)
        {
            throw new ArgumentOutOfRangeException("sourceIndex", SR.ArgumentOutOfRange_Index);
        }
        if (sourceIndex > self.Length - count)
        {
            throw new ArgumentException("SR.Arg_LongerThanSrcString");
        }
        StringBuilder stringBuilder = self;
        destination.CopyFrom(stringBuilder.ToString().ToCharArray());
    }
}