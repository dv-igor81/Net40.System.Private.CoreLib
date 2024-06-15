#define DEBUG
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System;

public static class BufferEx
{
	private const uint MemmoveNativeThreshold = 32768u;

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[CLSCompliant(false)]
	public static unsafe void MemoryCopy(void* source, void* destination, long destinationSizeInBytes, long sourceBytesToCopy)
	{
		if (sourceBytesToCopy > destinationSizeInBytes)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.sourceBytesToCopy);
		}
		Memmove((byte*)destination, (byte*)source, checked((uint)sourceBytesToCopy));
	}

	public static unsafe void Memmove(byte* dest, byte* src, uint len)
	{
		byte* ptr = src + len;
		byte* ptr2 = dest + len;
		if (len > 16)
		{
			if (len > 64)
			{
				Debug.Assert(len > 64 && len <= 32768);
				uint num = len >> 6;
				do
				{
					*(int*)dest = *(int*)src;
					*(int*)(dest + 4) = *(int*)(src + 4);
					*(int*)(dest + 8) = *(int*)(src + 8);
					*(int*)(dest + 12) = *(int*)(src + 12);
					*(int*)(dest + 16) = *(int*)(src + 16);
					*(int*)(dest + 20) = *(int*)(src + 20);
					*(int*)(dest + 24) = *(int*)(src + 24);
					*(int*)(dest + 28) = *(int*)(src + 28);
					*(int*)(dest + 32) = *(int*)(src + 32);
					*(int*)(dest + 36) = *(int*)(src + 36);
					*(int*)(dest + 40) = *(int*)(src + 40);
					*(int*)(dest + 44) = *(int*)(src + 44);
					*(int*)(dest + 48) = *(int*)(src + 48);
					*(int*)(dest + 52) = *(int*)(src + 52);
					*(int*)(dest + 56) = *(int*)(src + 56);
					*(int*)(dest + 60) = *(int*)(src + 60);
					dest += 64;
					src += 64;
					num--;
				}
				while (num != 0);
				len %= 64;
				if (len <= 16)
				{
					*(int*)(ptr2 - 16) = *(int*)(ptr - 16);
					*(int*)(ptr2 - 12) = *(int*)(ptr - 12);
					*(int*)(ptr2 - 8) = *(int*)(ptr - 8);
					*(int*)(ptr2 - 4) = *(int*)(ptr - 4);
					return;
				}
			}
			Debug.Assert(len > 16 && len <= 64);
			*(int*)dest = *(int*)src;
			*(int*)(dest + 4) = *(int*)(src + 4);
			*(int*)(dest + 8) = *(int*)(src + 8);
			*(int*)(dest + 12) = *(int*)(src + 12);
			if (len > 32)
			{
				*(int*)(dest + 16) = *(int*)(src + 16);
				*(int*)(dest + 20) = *(int*)(src + 20);
				*(int*)(dest + 24) = *(int*)(src + 24);
				*(int*)(dest + 28) = *(int*)(src + 28);
				if (len > 48)
				{
					*(int*)(dest + 32) = *(int*)(src + 32);
					*(int*)(dest + 36) = *(int*)(src + 36);
					*(int*)(dest + 40) = *(int*)(src + 40);
					*(int*)(dest + 44) = *(int*)(src + 44);
				}
			}
			Debug.Assert(len > 16 && len <= 64);
			*(int*)(ptr2 - 16) = *(int*)(ptr - 16);
			*(int*)(ptr2 - 12) = *(int*)(ptr - 12);
			*(int*)(ptr2 - 8) = *(int*)(ptr - 8);
			*(int*)(ptr2 - 4) = *(int*)(ptr - 4);
			return;
		}
		if ((len & 0x18u) != 0)
		{
			Debug.Assert(len >= 8 && len <= 16);
			*(int*)dest = *(int*)src;
			*(int*)(dest + 4) = *(int*)(src + 4);
			*(int*)(ptr2 - 8) = *(int*)(ptr - 8);
			*(int*)(ptr2 - 4) = *(int*)(ptr - 4);
			return;
		}
		if ((len & 4u) != 0)
		{
			Debug.Assert(len >= 4 && len < 8);
			*(int*)dest = *(int*)src;
			*(int*)(ptr2 - 4) = *(int*)(ptr - 4);
			return;
		}
		Debug.Assert(len < 4);
		if (len != 0)
		{
			*dest = *src;
			if ((len & 2u) != 0)
			{
				*(short*)(ptr2 - 2) = *(short*)(ptr - 2);
			}
		}
	}
}
