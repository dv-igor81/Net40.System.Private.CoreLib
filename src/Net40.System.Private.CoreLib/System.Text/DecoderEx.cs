using System.Runtime.InteropServices;

namespace System.Text;

public static class DecoderEx
{
	public static unsafe int GetChars(this Encoding encoding, ReadOnlySpan<byte> bytes, Span<char> chars)
	{
		Decoder decoder = encoding.GetDecoder();
		fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
		{
			fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
			{
				try
				{
					return decoder.GetChars(bytesPtr, bytes.Length, charsPtr, chars.Length, flush: false);
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					throw;
				}
			}
		}
	}

	public static unsafe int GetChars(this Decoder decoder, ReadOnlySpan<byte> bytes, Span<char> chars, bool flush)
	{
		fixed (byte* bytesPtr = &MemoryMarshal.GetReference(bytes))
		{
			fixed (char* charsPtr = &MemoryMarshal.GetReference(chars))
			{
				try
				{
					return decoder.GetChars(bytesPtr, bytes.Length, charsPtr, chars.Length, flush);
				}
				catch (Exception e)
				{
					Console.WriteLine(e);
					throw;
				}
			}
		}
	}
}
