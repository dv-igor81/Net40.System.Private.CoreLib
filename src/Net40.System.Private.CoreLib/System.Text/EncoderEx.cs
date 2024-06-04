using System.Runtime.InteropServices;

namespace System.Text;

public static class EncoderEx
{
	public unsafe static void Convert(this Encoder encoder, ReadOnlySpan<char> chars, Span<byte> bytes, bool flush, out int charsUsed, out int bytesUsed, out bool completed)
	{
		fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
		{
			fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
			{
				encoder.Convert(charsPtr, chars.Length, bytesPtr, bytes.Length, flush, out charsUsed, out bytesUsed, out completed);
			}
		}
	}
}
