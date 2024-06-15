using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace System;

internal static class Marvin
{
	public static ulong DefaultSeed { get; } = GenerateSeed();


	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public static int ComputeHash32(ReadOnlySpan<byte> data, ulong seed)
	{
			long num = ComputeHash(data, seed);
			return (int)(num >> 32) ^ (int)num;
		}

	public static long ComputeHash(ReadOnlySpan<byte> data, ulong seed)
	{
			uint rp = (uint)seed;
			uint rp2 = (uint)(seed >> 32);
			if (data.Length >= 4)
			{
				ReadOnlySpan<uint> readOnlySpan = MemoryMarshal.Cast<byte, uint>(data);
				for (int i = 0; i < readOnlySpan.Length; i++)
				{
					rp += readOnlySpan[i];
					Block(ref rp, ref rp2);
				}
				int start = data.Length & -4;
				data = data.Slice(start);
			}
			switch (data.Length)
			{
			case 0:
				rp += 128;
				break;
			case 1:
				rp += (uint)(0x8000 | data[0]);
				break;
			case 2:
				rp += (uint)(0x800000 | MemoryMarshal.Cast<byte, ushort>(data)[0]);
				break;
			case 3:
				rp += (uint)(int.MinValue | (data[2] << 16) | MemoryMarshal.Cast<byte, ushort>(data)[0]);
				break;
			}
			Block(ref rp, ref rp2);
			Block(ref rp, ref rp2);
			return (long)(((ulong)rp2 << 32) | rp);
		}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static void Block(ref uint rp0, ref uint rp1)
	{
			uint num = rp0;
			uint num2 = rp1;
			num2 ^= num;
			num = _rotl(num, 20);
			num += num2;
			num2 = _rotl(num2, 9);
			num2 ^= num;
			num = _rotl(num, 27);
			num += num2;
			num2 = _rotl(num2, 19);
			rp0 = num;
			rp1 = num2;
		}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint _rotl(uint value, int shift)
	{
			return (value << shift) | (value >> 32 - shift);
		}

	private static ulong GenerateSeed()
	{
			using RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();
			byte[] array = new byte[8];
			randomNumberGenerator.GetBytes(array);
			return BitConverter.ToUInt64(array, 0);
		}
}