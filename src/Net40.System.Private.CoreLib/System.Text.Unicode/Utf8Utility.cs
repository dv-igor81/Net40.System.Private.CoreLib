using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace System.Text.Unicode;

internal static class Utf8Utility
{
	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint ExtractCharFromFirstThreeByteSequence(uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			return ((value & 0x3F0000) >> 16) | ((value & 0x3F00) >> 2) | ((value & 0xF) << 12);
		}
		return ((value & 0xF000000) >> 12) | ((value & 0x3F0000) >> 10) | ((value & 0x3F00) >> 8);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint ExtractCharFromFirstTwoByteSequence(uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			uint num = (uint)((byte)value << 6);
			return (byte)(value >> 8) + num - 12288 - 128;
		}
		return (ushort)(((value & 0x1F000000) >> 18) | ((value & 0x3F0000) >> 16));
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint ExtractCharsFromFourByteSequence(uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			uint num = (uint)((byte)value << 8);
			num |= (value & 0x3F00) >> 6;
			num |= (value & 0x300000) >> 20;
			num |= (value & 0x3F000000) >> 8;
			num |= (value & 0xF0000) << 6;
			num -= 64;
			num -= 8192;
			num += 2048;
			return num + 3690987520u;
		}
		uint num2 = value & 0xFF000000u;
		num2 |= (value & 0x3F0000) << 2;
		num2 |= (value & 0x3000) << 4;
		num2 |= (value & 0xF00) >> 2;
		num2 |= value & 0x3Fu;
		num2 -= 536870912;
		num2 -= 4194304;
		num2 += 56320;
		return num2 + 134217728;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint ExtractFourUtf8BytesFromSurrogatePair(uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			value += 64;
			uint value2 = BinaryPrimitives.ReverseEndianness(value & 0x3F0700u);
			value2 = BitOperations.RotateLeft(value2, 16);
			uint num = (value & 0xFC) << 6;
			uint num2 = (value >> 6) & 0xF0000u;
			num2 |= num;
			uint num3 = (value & 3) << 20;
			num3 |= 0x808080F0u;
			return num3 | value2 | num2;
		}
		value -= 3623934976u;
		value += 4194304;
		uint num4 = value & 0x7000000u;
		uint num5 = (value >> 2) & 0x3F0000u;
		num5 |= num4;
		uint num6 = (value << 2) & 0xF00u;
		uint num7 = (value >> 6) & 0x30000u;
		num7 |= num6;
		uint num8 = (value & 0x3F) + 4034953344u;
		return num8 | num5 | num7;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint ExtractTwoCharsPackedFromTwoAdjacentTwoByteSequences(uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			return ((value & 0x3F003F00) >> 8) | ((value & 0x1F001F) << 6);
		}
		return ((value & 0x1F001F00) >> 2) | (value & 0x3F003Fu);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint ExtractTwoUtf8TwoByteSequencesFromTwoPackedUtf16Chars(uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			return ((value >> 6) & 0x1F001F) + ((value << 8) & 0x3F003F00) + 2160099520u;
		}
		return ((value << 2) & 0x1F001F00) + (value & 0x3F003F) + 3229663360u;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint ExtractUtf8TwoByteSequenceFromFirstUtf16Char(uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			uint num = (value << 2) & 0x1F00u;
			value &= 0x3Fu;
			return BinaryPrimitives.ReverseEndianness((ushort)(num + value + 49280));
		}
		uint num2 = (value >> 16) & 0x3Fu;
		value = (value >> 22) & 0x1F00u;
		return value + num2 + 49280;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsFirstCharAscii(uint value)
	{
		if (!BitConverter.IsLittleEndian || (value & 0xFF80u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return value < 8388608;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsFirstCharAtLeastThreeUtf8Bytes(uint value)
	{
		if (!BitConverter.IsLittleEndian || (value & 0xF800) == 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return value >= 134217728;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsFirstCharSurrogate(uint value)
	{
		if (!BitConverter.IsLittleEndian || ((value - 55296) & 0xF800u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (uint)((int)value - -671088640) < 134217728u;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsFirstCharTwoUtf8Bytes(uint value)
	{
		if (!BitConverter.IsLittleEndian || ((value - 128) & 0xFFFF) >= 1920)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return UnicodeUtility.IsInRangeInclusive(value, 8388608u, 134217727u);
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsLowByteUtf8ContinuationByte(uint value)
	{
		return (uint)(byte)(value - 128) <= 63u;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsSecondCharAscii(uint value)
	{
		if (!BitConverter.IsLittleEndian || value >= 8388608)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (value & 0xFF80) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsSecondCharAtLeastThreeUtf8Bytes(uint value)
	{
		if (!BitConverter.IsLittleEndian || (value & 0xF8000000u) == 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (value & 0xF800) != 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsSecondCharSurrogate(uint value)
	{
		if (!BitConverter.IsLittleEndian || (uint)((int)value - -671088640) >= 134217728u)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return ((value - 55296) & 0xF800) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsSecondCharTwoUtf8Bytes(uint value)
	{
		if (!BitConverter.IsLittleEndian || !UnicodeUtility.IsInRangeInclusive(value, 8388608u, 134217727u))
		{
			if (!BitConverter.IsLittleEndian)
			{
				return ((value - 128) & 0xFFFF) < 1920;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsUtf8ContinuationByte(in byte value)
	{
		return (sbyte)value < -64;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool IsWellFormedUtf16SurrogatePair(uint value)
	{
		if (!BitConverter.IsLittleEndian || ((value - 3691042816u) & 0xFC00FC00u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (((int)value - -671032320) & -67044352) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static uint ToLittleEndian(uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			return value;
		}
		return BinaryPrimitives.ReverseEndianness(value);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32BeginsWithOverlongUtf8TwoByteSequence(uint value)
	{
		if (!BitConverter.IsLittleEndian || (uint)(byte)value >= 194u)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return value < 3254779904u;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32BeginsWithUtf8FourByteMask(uint value)
	{
		if (!BitConverter.IsLittleEndian || ((value - 2155905264u) & 0xC0C0C0F8u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (((int)value - -260014080) & -121585472) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32BeginsWithUtf8ThreeByteMask(uint value)
	{
		if (!BitConverter.IsLittleEndian || ((value - 8421600) & 0xC0C0F0u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (((int)value - -528449536) & -255803392) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32BeginsWithUtf8TwoByteMask(uint value)
	{
		if (!BitConverter.IsLittleEndian || ((value - 32960) & 0xC0E0u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (((int)value - -1065353216) & -524288000) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32EndsWithOverlongUtf8TwoByteSequence(uint value)
	{
		if (!BitConverter.IsLittleEndian || (value & 0x1E0000u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (value & 0x1E00) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32EndsWithUtf8TwoByteMask(uint value)
	{
		if (!BitConverter.IsLittleEndian || ((value - 2160066560u) & 0xC0E00000u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return ((value - 49280) & 0xE0C0) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32BeginsWithValidUtf8TwoByteSequenceLittleEndian(uint value)
	{
		if (!BitConverter.IsLittleEndian || !UnicodeUtility.IsInRangeInclusive(value & 0xC0FFu, 32962u, 32991u))
		{
			if (!BitConverter.IsLittleEndian)
			{
				return false;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32EndsWithValidUtf8TwoByteSequenceLittleEndian(uint value)
	{
		if (!BitConverter.IsLittleEndian || !UnicodeUtility.IsInRangeInclusive(value & 0xC0FF0000u, 2160197632u, 2162098176u))
		{
			if (!BitConverter.IsLittleEndian)
			{
				return false;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32FirstByteIsAscii(uint value)
	{
		if (!BitConverter.IsLittleEndian || (value & 0x80u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (int)value >= 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32FourthByteIsAscii(uint value)
	{
		if (!BitConverter.IsLittleEndian || (int)value < 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (value & 0x80) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32SecondByteIsAscii(uint value)
	{
		if (!BitConverter.IsLittleEndian || (value & 0x8000u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (value & 0x800000) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool UInt32ThirdByteIsAscii(uint value)
	{
		if (!BitConverter.IsLittleEndian || (value & 0x800000u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (value & 0x8000) == 0;
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static void WriteTwoUtf16CharsAsTwoUtf8ThreeByteSequences(ref byte outputBuffer, uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			uint num = ((value << 2) & 0x3F00u) | ((value & 0x3F) << 16);
			uint num2 = ((value >> 4) & 0xF000000u) | ((value >> 12) & 0xFu);
			Unsafe.WriteUnaligned(ref outputBuffer, num + num2 + 3766517984u);
			Unsafe.WriteUnaligned(ref Unsafe.Add(ref outputBuffer, 4), (ushort)(((value >> 22) & 0x3F) + ((value >> 8) & 0x3F00) + 32896));
		}
		else
		{
			Unsafe.Add(ref outputBuffer, 5) = (byte)((value & 0x3Fu) | 0x80u);
			Unsafe.Add(ref outputBuffer, 4) = (byte)(((value >>= 6) & 0x3Fu) | 0x80u);
			Unsafe.Add(ref outputBuffer, 3) = (byte)(((value >>= 6) & 0xFu) | 0xE0u);
			Unsafe.Add(ref outputBuffer, 2) = (byte)(((value >>= 4) & 0x3Fu) | 0x80u);
			Unsafe.Add(ref outputBuffer, 1) = (byte)(((value >>= 6) & 0x3Fu) | 0x80u);
			outputBuffer = (byte)((value >>= 6) | 0xE0u);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static void WriteFirstUtf16CharAsUtf8ThreeByteSequence(ref byte outputBuffer, uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			uint num = (value << 2) & 0x3F00u;
			uint num2 = (uint)(ushort)value >> 12;
			Unsafe.WriteUnaligned(ref outputBuffer, (ushort)(num + num2 + 32992));
			Unsafe.Add(ref outputBuffer, 2) = (byte)((value & 0x3Fu) | 0xFFFFFF80u);
		}
		else
		{
			Unsafe.Add(ref outputBuffer, 2) = (byte)(((value >>= 16) & 0x3Fu) | 0x80u);
			Unsafe.Add(ref outputBuffer, 1) = (byte)(((value >>= 6) & 0x3Fu) | 0x80u);
			outputBuffer = (byte)((value >>= 6) | 0xE0u);
		}
	}

	public static unsafe OperationStatus TranscodeToUtf16(byte* pInputBuffer, int inputLength, char* pOutputBuffer, int outputCharsRemaining, out byte* pInputBufferRemaining, out char* pOutputBufferRemaining)
	{
		ulong num = ASCIIUtility.WidenAsciiToUtf16(pInputBuffer, pOutputBuffer, (uint)Math.Min(inputLength, outputCharsRemaining));
		pInputBuffer += num;
		pOutputBuffer += num;
		if ((int)num == inputLength)
		{
			pInputBufferRemaining = pInputBuffer;
			pOutputBufferRemaining = pOutputBuffer;
			return OperationStatus.Done;
		}
		inputLength -= (int)num;
		outputCharsRemaining -= (int)num;
		if (inputLength < 4)
		{
			goto IL_0709;
		}
		byte* ptr = pInputBuffer + (uint)inputLength - 4;
		while (true)
		{
			IL_0048:
			uint num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
			while (true)
			{
				IL_004f:
				if (!ASCIIUtility.AllBytesInUInt32AreAscii(num2))
				{
					goto IL_011e;
				}
				int num4;
				uint num5;
				if (outputCharsRemaining >= 4)
				{
					ASCIIUtility.WidenFourAsciiBytesToUtf16AndWriteToBuffer(ref *pOutputBuffer, num2);
					pInputBuffer += 4;
					pOutputBuffer += 4;
					outputCharsRemaining -= 4;
					uint val = (uint)((int)(void*)Unsafe.ByteOffset(ref *pInputBuffer, ref *ptr) + 4);
					uint num3 = Math.Min(val, (uint)outputCharsRemaining) / 8;
					num4 = 0;
					while ((uint)num4 < num3)
					{
						num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
						num5 = Unsafe.ReadUnaligned<uint>(pInputBuffer + 4);
						if (ASCIIUtility.AllBytesInUInt32AreAscii(num2 | num5))
						{
							pInputBuffer += 8;
							ASCIIUtility.WidenFourAsciiBytesToUtf16AndWriteToBuffer(ref *pOutputBuffer, num2);
							ASCIIUtility.WidenFourAsciiBytesToUtf16AndWriteToBuffer(ref pOutputBuffer[4], num5);
							pOutputBuffer += 8;
							num4++;
							continue;
						}
						goto IL_00f2;
					}
					outputCharsRemaining -= 8 * num4;
					goto IL_0523;
				}
				goto IL_052a;
				IL_0523:
				if (pInputBuffer <= ptr)
				{
					goto IL_0048;
				}
				goto IL_052a;
				IL_011e:
				if (UInt32FirstByteIsAscii(num2))
				{
					if (outputCharsRemaining >= 3)
					{
						uint num6 = ToLittleEndian(num2);
						ulong num7 = 1uL;
						*pOutputBuffer = (char)(byte)num6;
						if (UInt32SecondByteIsAscii(num2))
						{
							num7++;
							num6 >>= 8;
							pOutputBuffer[1] = (char)(byte)num6;
							if (UInt32ThirdByteIsAscii(num2))
							{
								num7++;
								num6 >>= 8;
								pOutputBuffer[2] = (char)(byte)num6;
							}
						}
						pInputBuffer += num7;
						pOutputBuffer += num7;
						outputCharsRemaining -= (int)num7;
					}
					else
					{
						if (outputCharsRemaining == 0)
						{
							break;
						}
						uint num8 = ToLittleEndian(num2);
						pInputBuffer++;
						*(pOutputBuffer++) = (char)(byte)num8;
						outputCharsRemaining--;
						if (UInt32SecondByteIsAscii(num2))
						{
							if (outputCharsRemaining == 0)
							{
								break;
							}
							pInputBuffer++;
							num8 >>= 8;
							*(pOutputBuffer++) = (char)(byte)num8;
							if (UInt32ThirdByteIsAscii(num2))
							{
								break;
							}
							outputCharsRemaining = 0;
						}
					}
					if (pInputBuffer > ptr)
					{
						goto IL_052a;
					}
					num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
				}
				uint num9;
				while (UInt32BeginsWithUtf8TwoByteMask(num2))
				{
					if (!UInt32BeginsWithOverlongUtf8TwoByteSequence(num2))
					{
						while ((BitConverter.IsLittleEndian && UInt32EndsWithValidUtf8TwoByteSequenceLittleEndian(num2)) || (!BitConverter.IsLittleEndian && UInt32EndsWithUtf8TwoByteMask(num2) && !UInt32EndsWithOverlongUtf8TwoByteSequence(num2)))
						{
							if (outputCharsRemaining >= 2)
							{
								Unsafe.WriteUnaligned(pOutputBuffer, ExtractTwoCharsPackedFromTwoAdjacentTwoByteSequences(num2));
								pInputBuffer += 4;
								pOutputBuffer += 2;
								outputCharsRemaining -= 2;
								if (pInputBuffer <= ptr)
								{
									num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
									if (BitConverter.IsLittleEndian)
									{
										if (!UInt32BeginsWithValidUtf8TwoByteSequenceLittleEndian(num2))
										{
											goto IL_004f;
										}
										continue;
									}
									if (!UInt32BeginsWithUtf8TwoByteMask(num2))
									{
										goto IL_004f;
									}
									if (!UInt32BeginsWithOverlongUtf8TwoByteSequence(num2))
									{
										continue;
									}
									goto IL_071c;
								}
							}
							goto IL_052a;
						}
						num9 = ExtractCharFromFirstTwoByteSequence(num2);
						if (UInt32ThirdByteIsAscii(num2))
						{
							if (UInt32FourthByteIsAscii(num2))
							{
								goto IL_02aa;
							}
							if (outputCharsRemaining >= 2)
							{
								*pOutputBuffer = (char)num9;
								pOutputBuffer[1] = (char)(byte)(num2 >> (BitConverter.IsLittleEndian ? 16 : 8));
								pInputBuffer += 3;
								pOutputBuffer += 2;
								outputCharsRemaining -= 2;
								if (ptr >= pInputBuffer)
								{
									num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
									continue;
								}
							}
						}
						else if (outputCharsRemaining != 0)
						{
							*pOutputBuffer = (char)num9;
							pInputBuffer += 2;
							pOutputBuffer++;
							outputCharsRemaining--;
							if (ptr >= pInputBuffer)
							{
								num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
								break;
							}
						}
						goto IL_052a;
					}
					goto IL_071c;
				}
				if (UInt32BeginsWithUtf8ThreeByteMask(num2))
				{
					while (true)
					{
						if (BitConverter.IsLittleEndian)
						{
							if ((num2 & 0x200F) == 0 || ((num2 - 8205) & 0x200F) == 0)
							{
								break;
							}
						}
						else if ((num2 & 0xF200000) == 0 || ((num2 - 220200960) & 0xF200000) == 0)
						{
							break;
						}
						if (outputCharsRemaining == 0)
						{
							goto end_IL_004f;
						}
						if (BitConverter.IsLittleEndian && (((int)num2 - -536870912) & -268435456) == 0 && outputCharsRemaining > 1 && (long)(void*)Unsafe.ByteOffset(ref *pInputBuffer, ref *ptr) >= 3L)
						{
							uint num10 = Unsafe.ReadUnaligned<uint>(pInputBuffer + 3);
							if (UInt32BeginsWithUtf8ThreeByteMask(num10) && (num10 & 0x200Fu) != 0 && ((num10 - 8205) & 0x200Fu) != 0)
							{
								*pOutputBuffer = (char)ExtractCharFromFirstThreeByteSequence(num2);
								pOutputBuffer[1] = (char)ExtractCharFromFirstThreeByteSequence(num10);
								pInputBuffer += 6;
								pOutputBuffer += 2;
								outputCharsRemaining -= 2;
								goto IL_045d;
							}
						}
						*pOutputBuffer = (char)ExtractCharFromFirstThreeByteSequence(num2);
						pInputBuffer += 3;
						pOutputBuffer++;
						outputCharsRemaining--;
						goto IL_045d;
						IL_045d:
						if (UInt32FourthByteIsAscii(num2))
						{
							if (outputCharsRemaining == 0)
							{
								goto end_IL_004f;
							}
							if (BitConverter.IsLittleEndian)
							{
								*pOutputBuffer = (char)(num2 >> 24);
							}
							else
							{
								*pOutputBuffer = (char)(byte)num2;
							}
							pInputBuffer++;
							pOutputBuffer++;
							outputCharsRemaining--;
						}
						if (pInputBuffer <= ptr)
						{
							num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
							if (!UInt32BeginsWithUtf8ThreeByteMask(num2))
							{
								goto IL_004f;
							}
							continue;
						}
						goto IL_052a;
					}
				}
				else if (UInt32BeginsWithUtf8FourByteMask(num2))
				{
					if (BitConverter.IsLittleEndian)
					{
						uint value = num2 & 0xFFFFu;
						value = BitOperations.RotateRight(value, 8);
						if (UnicodeUtility.IsInRangeInclusive(value, 4026531984u, 4093640847u))
						{
							goto IL_04fe;
						}
					}
					else if (UnicodeUtility.IsInRangeInclusive(num2, 4035969024u, 4103077887u))
					{
						goto IL_04fe;
					}
				}
				goto IL_071c;
				IL_04fe:
				if (outputCharsRemaining < 2)
				{
					break;
				}
				Unsafe.WriteUnaligned(pOutputBuffer, ExtractCharsFromFourByteSequence(num2));
				pInputBuffer += 4;
				pOutputBuffer += 2;
				outputCharsRemaining -= 2;
				goto IL_0523;
				IL_02aa:
				if (outputCharsRemaining >= 3)
				{
					*pOutputBuffer = (char)num9;
					if (BitConverter.IsLittleEndian)
					{
						num2 >>= 16;
						pOutputBuffer[1] = (char)(byte)num2;
						num2 >>= 8;
						pOutputBuffer[2] = (char)num2;
					}
					else
					{
						pOutputBuffer[2] = (char)(byte)num2;
						pOutputBuffer[1] = (char)(byte)(num2 >> 8);
					}
					pInputBuffer += 4;
					pOutputBuffer += 3;
					outputCharsRemaining -= 3;
					goto IL_0523;
				}
				goto IL_052a;
				IL_00f2:
				if (ASCIIUtility.AllBytesInUInt32AreAscii(num2))
				{
					ASCIIUtility.WidenFourAsciiBytesToUtf16AndWriteToBuffer(ref *pOutputBuffer, num2);
					num2 = num5;
					pInputBuffer += 4;
					pOutputBuffer += 4;
					outputCharsRemaining -= 4;
				}
				outputCharsRemaining -= 8 * num4;
				goto IL_011e;
				IL_052a:
				inputLength = (int)(void*)Unsafe.ByteOffset(ref *pInputBuffer, ref *ptr) + 4;
				goto IL_0709;
				continue;
				end_IL_004f:
				break;
			}
			break;
		}
		goto IL_0718;
		IL_0718:
		OperationStatus result = OperationStatus.DestinationTooSmall;
		goto IL_071e;
		IL_0709:
		while (true)
		{
			if (inputLength > 0)
			{
				uint num11 = *pInputBuffer;
				if (num11 <= 127)
				{
					if (outputCharsRemaining != 0)
					{
						*pOutputBuffer = (char)num11;
						pInputBuffer++;
						pOutputBuffer++;
						inputLength--;
						outputCharsRemaining--;
						continue;
					}
					goto IL_0718;
				}
				num11 -= 194;
				if ((uint)(byte)num11 <= 29u)
				{
					if (inputLength < 2)
					{
						goto IL_0714;
					}
					uint num12 = pInputBuffer[1];
					if (IsLowByteUtf8ContinuationByte(num12))
					{
						if (outputCharsRemaining != 0)
						{
							uint num13 = (num11 << 6) + num12 + 128 - 128;
							*pOutputBuffer = (char)num13;
							pInputBuffer += 2;
							pOutputBuffer++;
							inputLength -= 2;
							outputCharsRemaining--;
							continue;
						}
						goto IL_0718;
					}
				}
				else if ((uint)(byte)num11 <= 45u)
				{
					if (inputLength >= 3)
					{
						uint num14 = pInputBuffer[1];
						uint num15 = pInputBuffer[2];
						if (IsLowByteUtf8ContinuationByte(num14) && IsLowByteUtf8ContinuationByte(num15))
						{
							uint num16 = (num11 << 12) + (num14 << 6);
							if (num16 >= 133120)
							{
								num16 -= 186368;
								if (num16 >= 2048)
								{
									if (outputCharsRemaining != 0)
									{
										num16 += num15;
										num16 += 55296;
										num16 -= 128;
										*pOutputBuffer = (char)num16;
										pInputBuffer += 3;
										pOutputBuffer++;
										inputLength -= 3;
										outputCharsRemaining--;
										continue;
									}
									goto IL_0718;
								}
							}
						}
					}
					else
					{
						if (inputLength < 2)
						{
							goto IL_0714;
						}
						uint num17 = pInputBuffer[1];
						if (IsLowByteUtf8ContinuationByte(num17))
						{
							uint num18 = (num11 << 6) + num17;
							if (num18 >= 2080 && !UnicodeUtility.IsInRangeInclusive(num18, 2912u, 2943u))
							{
								goto IL_0714;
							}
						}
					}
				}
				else if ((uint)(byte)num11 <= 50u)
				{
					if (inputLength < 2)
					{
						goto IL_0714;
					}
					uint num19 = pInputBuffer[1];
					if (IsLowByteUtf8ContinuationByte(num19))
					{
						uint value2 = (num11 << 6) + num19;
						if (UnicodeUtility.IsInRangeInclusive(value2, 3088u, 3343u))
						{
							if (inputLength < 3)
							{
								goto IL_0714;
							}
							if (IsLowByteUtf8ContinuationByte(pInputBuffer[2]))
							{
								if (inputLength < 4)
								{
									goto IL_0714;
								}
								if (IsLowByteUtf8ContinuationByte(pInputBuffer[3]))
								{
									goto IL_0718;
								}
							}
						}
					}
				}
				goto IL_071c;
			}
			result = OperationStatus.Done;
			break;
			IL_0714:
			result = OperationStatus.NeedMoreData;
			break;
		}
		goto IL_071e;
		IL_071c:
		result = OperationStatus.InvalidData;
		goto IL_071e;
		IL_071e:
		pInputBufferRemaining = pInputBuffer;
		pOutputBufferRemaining = pOutputBuffer;
		return result;
	}

	public static unsafe OperationStatus TranscodeToUtf8(char* pInputBuffer, int inputLength, byte* pOutputBuffer, int outputBytesRemaining, out char* pInputBufferRemaining, out byte* pOutputBufferRemaining)
	{
		ulong num = ASCIIUtility.NarrowUtf16ToAscii(pInputBuffer, pOutputBuffer, (uint)Math.Min(inputLength, outputBytesRemaining));
		pInputBuffer += num;
		pOutputBuffer += num;
		if ((int)num == inputLength)
		{
			pInputBufferRemaining = pInputBuffer;
			pOutputBufferRemaining = pOutputBuffer;
			return OperationStatus.Done;
		}
		inputLength -= (int)num;
		outputBytesRemaining -= (int)num;
		if (inputLength < 2)
		{
			goto IL_04bd;
		}
		char* ptr = pInputBuffer + (uint)inputLength - 2;
		
		// Vector128<short> right = default(Vector128<short>);
		// if (Sse41.X64.IsSupported)
		// {
		// 	right = Vector128.Create((short)(-128));
		// }
		
		uint num2;
		while (true)
		{
			IL_006c:
			num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
			while (true)
			{
				IL_0073:
				if (!Utf16Utility.AllCharsInUInt32AreAscii(num2))
				{
					goto IL_02a8;
				}
				if (outputBytesRemaining < 2)
				{
					break;
				}
				uint num3 = num2 | (num2 >> 8);
				Unsafe.WriteUnaligned(pOutputBuffer, (ushort)num3);
				pInputBuffer += 2;
				pOutputBuffer += 2;
				outputBytesRemaining -= 2;
				uint num4 = (uint)((int)(ptr - pInputBuffer) + 2);
				uint num5 = (uint)Math.Min(num4, outputBytesRemaining);
				int num7;
				ulong num8;
				//Vector128<short> vector;
				int num10;
				uint num11;
				/*if (Sse41.X64.IsSupported)
				{
					uint num6 = num5 / 8;
					num7 = 0;
					while ((uint)num7 < num6)
					{
						vector = Unsafe.ReadUnaligned<Vector128<short>>(pInputBuffer);
						if (Sse41.TestZ(vector, right))
						{
							Sse2.StoreScalar((ulong*)pOutputBuffer, Sse2.PackUnsignedSaturate(vector, vector).AsUInt64());
							pInputBuffer += 8;
							pOutputBuffer += 8;
							num7++;
							continue;
						}
						goto IL_0179;
					}
					outputBytesRemaining -= 8 * num7;
					if ((num5 & 4u) != 0)
					{
						num8 = Unsafe.ReadUnaligned<ulong>(pInputBuffer);
						if (!Utf16Utility.AllCharsInUInt64AreAscii(num8))
						{
							goto IL_01d2;
						}
						vector = Vector128.CreateScalarUnsafe(num8).AsInt16();
						Unsafe.WriteUnaligned(pOutputBuffer, Sse2.ConvertToUInt32(Sse2.PackUnsignedSaturate(vector, vector).AsUInt32()));
						pInputBuffer += 4;
						pOutputBuffer += 4;
						outputBytesRemaining -= 4;
					}
				}*/
				//else
				{
					uint num9 = num5 / 4;
					num10 = 0;
					while ((uint)num10 < num9)
					{
						num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
						num11 = Unsafe.ReadUnaligned<uint>(pInputBuffer + 2);
						if (Utf16Utility.AllCharsInUInt32AreAscii(num2 | num11))
						{
							Unsafe.WriteUnaligned(pOutputBuffer, (ushort)(num2 | (num2 >> 8)));
							Unsafe.WriteUnaligned(pOutputBuffer + 2, (ushort)(num11 | (num11 >> 8)));
							pInputBuffer += 4;
							pOutputBuffer += 4;
							num10++;
							continue;
						}
						goto IL_0277;
					}
					outputBytesRemaining -= 4 * num10;
				}
				goto IL_04ab;
				IL_02a8:
				while (true)
				{
					if (IsFirstCharAscii(num2))
					{
						if (outputBytesRemaining == 0)
						{
							break;
						}
						if (BitConverter.IsLittleEndian)
						{
							*pOutputBuffer = (byte)num2;
						}
						else
						{
							*pOutputBuffer = (byte)(num2 >> 24);
						}
						pInputBuffer++;
						pOutputBuffer++;
						outputBytesRemaining--;
						if (pInputBuffer > ptr)
						{
							goto IL_04b2;
						}
						num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
					}
					if (!IsFirstCharAtLeastThreeUtf8Bytes(num2))
					{
						while (IsSecondCharTwoUtf8Bytes(num2))
						{
							if (outputBytesRemaining < 4)
							{
								goto end_IL_0073;
							}
							Unsafe.WriteUnaligned(pOutputBuffer, ExtractTwoUtf8TwoByteSequencesFromTwoPackedUtf16Chars(num2));
							pInputBuffer += 2;
							pOutputBuffer += 4;
							outputBytesRemaining -= 4;
							if (pInputBuffer <= ptr)
							{
								num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
								if (!IsFirstCharTwoUtf8Bytes(num2))
								{
									goto IL_0073;
								}
								continue;
							}
							goto IL_04b2;
						}
						if (outputBytesRemaining < 2)
						{
							break;
						}
						Unsafe.WriteUnaligned(pOutputBuffer, (ushort)ExtractUtf8TwoByteSequenceFromFirstUtf16Char(num2));
						if (IsSecondCharAscii(num2))
						{
							goto IL_0356;
						}
						pInputBuffer++;
						pOutputBuffer += 2;
						outputBytesRemaining -= 2;
						if (pInputBuffer > ptr)
						{
							goto IL_04b2;
						}
						num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
					}
					while (!IsFirstCharSurrogate(num2))
					{
						if (IsSecondCharAtLeastThreeUtf8Bytes(num2) && !IsSecondCharSurrogate(num2) && outputBytesRemaining >= 6)
						{
							WriteTwoUtf16CharsAsTwoUtf8ThreeByteSequences(ref *pOutputBuffer, num2);
							pInputBuffer += 2;
							pOutputBuffer += 6;
							outputBytesRemaining -= 6;
							if (pInputBuffer <= ptr)
							{
								num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
								if (!IsFirstCharAtLeastThreeUtf8Bytes(num2))
								{
									goto IL_0073;
								}
								continue;
							}
						}
						else
						{
							if (outputBytesRemaining < 3)
							{
								goto end_IL_02a8;
							}
							WriteFirstUtf16CharAsUtf8ThreeByteSequence(ref *pOutputBuffer, num2);
							pInputBuffer++;
							pOutputBuffer += 3;
							outputBytesRemaining -= 3;
							if (!IsSecondCharAscii(num2))
							{
								goto IL_046b;
							}
							if (outputBytesRemaining == 0)
							{
								goto end_IL_02a8;
							}
							if (BitConverter.IsLittleEndian)
							{
								*pOutputBuffer = (byte)(num2 >> 16);
							}
							else
							{
								*pOutputBuffer = (byte)num2;
							}
							pInputBuffer++;
							pOutputBuffer++;
							outputBytesRemaining--;
							if (pInputBuffer <= ptr)
							{
								num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
								if (!IsFirstCharAtLeastThreeUtf8Bytes(num2))
								{
									goto IL_0073;
								}
								continue;
							}
						}
						goto IL_04b2;
					}
					goto IL_047b;
					IL_046b:
					if (pInputBuffer <= ptr)
					{
						num2 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
						continue;
					}
					goto IL_04b2;
					continue;
					end_IL_02a8:
					break;
				}
				goto IL_057a;
				IL_01d2:
				num2 = (uint)num8;
				if (Utf16Utility.AllCharsInUInt32AreAscii(num2))
				{
					Unsafe.WriteUnaligned(pOutputBuffer, (ushort)(num2 | (num2 >> 8)));
					pInputBuffer += 2;
					pOutputBuffer += 2;
					outputBytesRemaining -= 2;
					num2 = (uint)(num8 >> 32);
				}
				goto IL_02a8;
				IL_0356:
				if (outputBytesRemaining >= 3)
				{
					if (BitConverter.IsLittleEndian)
					{
						num2 >>= 16;
					}
					pOutputBuffer[2] = (byte)num2;
					pInputBuffer += 2;
					pOutputBuffer += 3;
					outputBytesRemaining -= 3;
					goto IL_04ab;
				}
				pInputBuffer++;
				pOutputBuffer += 2;
				goto IL_057a;
				IL_047b:
				if (IsWellFormedUtf16SurrogatePair(num2))
				{
					if (outputBytesRemaining >= 4)
					{
						Unsafe.WriteUnaligned(pOutputBuffer, ExtractFourUtf8BytesFromSurrogatePair(num2));
						pInputBuffer += 2;
						pOutputBuffer += 4;
						outputBytesRemaining -= 4;
						goto IL_04ab;
					}
					goto IL_057a;
				}
				goto IL_057f;
				IL_04b2:
				inputLength = (int)(ptr - pInputBuffer) + 2;
				goto IL_04bd;
				IL_0277:
				outputBytesRemaining -= 4 * num10;
				if (Utf16Utility.AllCharsInUInt32AreAscii(num2))
				{
					Unsafe.WriteUnaligned(pOutputBuffer, (ushort)(num2 | (num2 >> 8)));
					pInputBuffer += 2;
					pOutputBuffer += 2;
					outputBytesRemaining -= 2;
					num2 = num11;
				}
				goto IL_02a8;
				IL_04ab:
				if (pInputBuffer <= ptr)
				{
					goto IL_006c;
				}
				goto IL_04b2;
				/*IL_0179:
				outputBytesRemaining -= 8 * num7;
				num8 = Sse2.X64.ConvertToUInt64(vector.AsUInt64());
				if (Utf16Utility.AllCharsInUInt64AreAscii(num8))
				{
					Unsafe.WriteUnaligned(pOutputBuffer, Sse2.ConvertToUInt32(Sse2.PackUnsignedSaturate(vector, vector).AsUInt32()));
					pInputBuffer += 4;
					pOutputBuffer += 4;
					outputBytesRemaining -= 4;
					num8 = vector.AsUInt64().GetElement(1);
				}
				goto IL_01d2;
				continue;*/
				end_IL_0073:
				break;
			}
			break;
		}
		uint num12 = ((!BitConverter.IsLittleEndian) ? (num2 >> 16) : (num2 & 0xFFFFu));
		goto IL_04de;
		IL_0582:
		pInputBufferRemaining = pInputBuffer;
		pOutputBufferRemaining = pOutputBuffer;
		OperationStatus result;
		return result;
		IL_057a:
		result = OperationStatus.DestinationTooSmall;
		goto IL_0582;
		IL_04bd:
		if (inputLength != 0)
		{
			num12 = *pInputBuffer;
			goto IL_04de;
		}
		goto IL_0570;
		IL_0570:
		result = OperationStatus.Done;
		goto IL_0582;
		IL_056c:
		if (inputLength <= 1)
		{
			goto IL_0570;
		}
		goto IL_057a;
		IL_057f:
		result = OperationStatus.InvalidData;
		goto IL_0582;
		IL_04de:
		if (num12 <= 127)
		{
			if (outputBytesRemaining != 0)
			{
				*pOutputBuffer = (byte)num12;
				pInputBuffer++;
				pOutputBuffer++;
				goto IL_056c;
			}
		}
		else if (num12 < 2048)
		{
			if (outputBytesRemaining >= 2)
			{
				pOutputBuffer[1] = (byte)((num12 & 0x3Fu) | 0xFFFFFF80u);
				*pOutputBuffer = (byte)((num12 >> 6) | 0xFFFFFFC0u);
				pInputBuffer++;
				pOutputBuffer += 2;
				goto IL_056c;
			}
		}
		else
		{
			if (UnicodeUtility.IsSurrogateCodePoint(num12))
			{
				if (num12 > 56319)
				{
					goto IL_057f;
				}
				result = OperationStatus.NeedMoreData;
				goto IL_0582;
			}
			if (outputBytesRemaining >= 3)
			{
				pOutputBuffer[2] = (byte)((num12 & 0x3Fu) | 0xFFFFFF80u);
				pOutputBuffer[1] = (byte)(((num12 >> 6) & 0x3Fu) | 0xFFFFFF80u);
				*pOutputBuffer = (byte)((num12 >> 12) | 0xFFFFFFE0u);
				pInputBuffer++;
				pOutputBuffer += 3;
				goto IL_056c;
			}
		}
		goto IL_057a;
	}

	/*public static unsafe byte* GetPointerToFirstInvalidByte(byte* pInputBuffer, int inputLength, out int utf16CodeUnitCountAdjustment, out int scalarCountAdjustment)
	{
		ulong indexOfFirstNonAsciiByte = ASCIIUtility.GetIndexOfFirstNonAsciiByte(pInputBuffer, (uint)inputLength);
		pInputBuffer += indexOfFirstNonAsciiByte;
		inputLength -= (int)indexOfFirstNonAsciiByte;
		if (inputLength == 0)
		{
			utf16CodeUnitCountAdjustment = 0;
			scalarCountAdjustment = 0;
			return pInputBuffer;
		}
		int num = 0;
		int num2 = 0;
		ulong num8;
		if (inputLength >= 4)
		{
			byte* ptr = pInputBuffer + (uint)inputLength - 4;
			while (pInputBuffer <= ptr)
			{
				uint num3 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
				while (true)
				{
					IL_0042:
					if (ASCIIUtility.AllBytesInUInt32AreAscii(num3))
					{
						pInputBuffer += 4;
						if ((long)(void*)Unsafe.ByteOffset(ref *pInputBuffer, ref *ptr) < 16L)
						{
							break;
						}
						num3 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
						if (ASCIIUtility.AllBytesInUInt32AreAscii(num3))
						{
							pInputBuffer = (byte*)((ulong)(pInputBuffer + 4) & 0xFFFFFFFFFFFFFFFCuL);
							byte* ptr2 = ptr - 12;
							uint num4;
							while (true)
							{
								if (Sse2.IsSupported && Bmi1.IsSupported)
								{
									num4 = (uint)Sse2.MoveMask(Sse2.LoadVector128(pInputBuffer));
									if (num4 != 0)
									{
										break;
									}
									goto IL_00d2;
								}
								if (ASCIIUtility.AllBytesInUInt32AreAscii(*(uint*)pInputBuffer | *(uint*)(pInputBuffer + 4)))
								{
									if (ASCIIUtility.AllBytesInUInt32AreAscii(*(uint*)(pInputBuffer + 2L * 4L) | *(uint*)(pInputBuffer + 3L * 4L)))
									{
										goto IL_00d2;
									}
									pInputBuffer += 8;
								}
								num3 = *(uint*)pInputBuffer;
								if (ASCIIUtility.AllBytesInUInt32AreAscii(num3))
								{
									pInputBuffer += 4;
									num3 = *(uint*)pInputBuffer;
								}
								goto IL_011a;
								IL_00d2:
								pInputBuffer += 16;
								if (pInputBuffer > ptr2)
								{
									goto end_IL_0042;
								}
							}
							pInputBuffer += Bmi1.TrailingZeroCount(num4);
							if (pInputBuffer > ptr)
							{
								goto end_IL_04da;
							}
							num3 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
							goto IL_0139;
						}
					}
					goto IL_011a;
					IL_0139:
					while (true)
					{
						num3 -= (uint)(BitConverter.IsLittleEndian ? 32960 : (-1065353216));
						if ((num3 & (BitConverter.IsLittleEndian ? 49376u : 3770679296u)) != 0)
						{
							break;
						}
						if ((!BitConverter.IsLittleEndian || (uint)(byte)num3 >= 2u) && (BitConverter.IsLittleEndian || num3 >= 33554432))
						{
							while ((BitConverter.IsLittleEndian && UInt32EndsWithValidUtf8TwoByteSequenceLittleEndian(num3)) || (!BitConverter.IsLittleEndian && UInt32EndsWithUtf8TwoByteMask(num3) && !UInt32EndsWithOverlongUtf8TwoByteSequence(num3)))
							{
								pInputBuffer += 4;
								num -= 2;
								if (pInputBuffer > ptr)
								{
									goto end_IL_04da;
								}
								num3 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
								if (BitConverter.IsLittleEndian)
								{
									if (!UInt32BeginsWithValidUtf8TwoByteSequenceLittleEndian(num3))
									{
										goto IL_0042;
									}
									continue;
								}
								if (!UInt32BeginsWithUtf8TwoByteMask(num3))
								{
									goto IL_0042;
								}
								if (!UInt32BeginsWithOverlongUtf8TwoByteSequence(num3))
								{
									continue;
								}
								goto IL_05d5;
							}
							num--;
							if (UInt32ThirdByteIsAscii(num3))
							{
								if (UInt32FourthByteIsAscii(num3))
								{
									pInputBuffer += 4;
									goto end_IL_0042;
								}
								pInputBuffer += 3;
								if (pInputBuffer > ptr)
								{
									goto end_IL_0042;
								}
								num3 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
								continue;
							}
							pInputBuffer += 2;
							goto end_IL_0042;
						}
						goto IL_05d5;
					}
					num3 -= (uint)(BitConverter.IsLittleEndian ? 8388640 : 536903680);
					if ((num3 & (uint)(BitConverter.IsLittleEndian ? 12632304 : (-255803392))) == 0)
					{
						while (true)
						{
							if (BitConverter.IsLittleEndian)
							{
								if ((num3 & 0x200F) == 0 || ((num3 - 8205) & 0x200F) == 0)
								{
									break;
								}
							}
							else if ((num3 & 0xF200000) == 0 || ((num3 - 220200960) & 0xF200000) == 0)
							{
								break;
							}
							while (true)
							{
								IL_02be:
								long num5 = ((!BitConverter.IsLittleEndian) ? ((long)(sbyte)num3 >> 7) : ((int)num3 >> 31));
								pInputBuffer += 4;
								pInputBuffer += num5;
								num -= 2;
								ulong num6;
								while (IntPtr.Size >= 8 && BitConverter.IsLittleEndian && ptr - pInputBuffer >= 5)
								{
									num6 = Unsafe.ReadUnaligned<ulong>(pInputBuffer);
									num3 = (uint)num6;
									if ((num6 & 0xC0F0C0C0F0C0C0F0uL) == 9286563722648649952uL && IsUtf8ContinuationByte(in pInputBuffer[8]))
									{
										if (((int)num6 & 0x200F) == 0 || (((int)num6 - 8205) & 0x200F) == 0)
										{
											goto end_IL_0275;
										}
										num6 >>= 24;
										if (((uint)(int)num6 & 0x200Fu) != 0 && ((uint)((int)num6 - 8205) & 0x200Fu) != 0)
										{
											num6 >>= 24;
											if (((uint)(int)num6 & 0x200Fu) != 0 && ((uint)((int)num6 - 8205) & 0x200Fu) != 0)
											{
												pInputBuffer += 9;
												num -= 6;
												continue;
											}
										}
										goto IL_02be;
									}
									goto IL_03c0;
								}
								break;
								IL_03c0:
								if ((num6 & 0xC0C0F0C0C0F0L) == 141291010687200L)
								{
									if (((int)num6 & 0x200F) == 0 || (((int)num6 - 8205) & 0x200F) == 0)
									{
										goto end_IL_0275;
									}
									num6 >>= 24;
									if (((int)num6 & 0x200F) == 0 || (((int)num6 - 8205) & 0x200F) == 0)
									{
										continue;
									}
									goto IL_0422;
								}
								goto IL_0430;
							}
							if (pInputBuffer > ptr)
							{
								goto end_IL_04da;
							}
							num3 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
							if (!UInt32BeginsWithUtf8ThreeByteMask(num3))
							{
								goto IL_0042;
							}
							continue;
							IL_0430:
							if (!UInt32BeginsWithUtf8ThreeByteMask(num3))
							{
								goto IL_0042;
							}
							continue;
							end_IL_0275:
							break;
						}
					}
					else if (BitConverter.IsLittleEndian)
					{
						num3 &= 0xC0C0FFFFu;
						if ((int)num3 <= -2147467265)
						{
							num3 = BitOperations.RotateRight(num3, 8);
							if (UnicodeUtility.IsInRangeInclusive(num3, 276824080u, 343932943u))
							{
								goto IL_04cd;
							}
						}
					}
					else
					{
						num3 -= 128;
						if ((num3 & 0xC0C0C0) == 0 && UnicodeUtility.IsInRangeInclusive(num3, 269484032u, 336592895u))
						{
							goto IL_04cd;
						}
					}
					goto IL_05d5;
					IL_0422:
					pInputBuffer += 6;
					num -= 4;
					break;
					IL_011a:
					uint num7 = ASCIIUtility.CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(num3);
					pInputBuffer += num7;
					if (ptr < pInputBuffer)
					{
						goto end_IL_04da;
					}
					num3 = Unsafe.ReadUnaligned<uint>(pInputBuffer);
					goto IL_0139;
					IL_04cd:
					pInputBuffer += 4;
					num -= 2;
					num2--;
					break;
					continue;
					end_IL_0042:
					break;
				}
				continue;
				end_IL_04da:
				break;
			}
			num8 = (ulong)(void*)Unsafe.ByteOffset(ref *pInputBuffer, ref *ptr) + 4uL;
		}
		else
		{
			num8 = (uint)inputLength;
		}
		while (num8 != 0)
		{
			uint num9 = *pInputBuffer;
			if ((uint)(byte)num9 < 128u)
			{
				pInputBuffer++;
				num8--;
				continue;
			}
			if (num8 < 2)
			{
				break;
			}
			uint value = pInputBuffer[1];
			if ((uint)(byte)num9 < 224u)
			{
				if ((uint)(byte)num9 < 194u || !IsLowByteUtf8ContinuationByte(value))
				{
					break;
				}
				pInputBuffer += 2;
				num--;
				num8 -= 2;
				continue;
			}
			if (num8 < 3 || (uint)(byte)num9 >= 240u)
			{
				break;
			}
			if ((byte)num9 == 224)
			{
				if (!UnicodeUtility.IsInRangeInclusive(value, 160u, 191u))
				{
					break;
				}
			}
			else if ((byte)num9 == 237)
			{
				if (!UnicodeUtility.IsInRangeInclusive(value, 128u, 159u))
				{
					break;
				}
			}
			else if (!IsLowByteUtf8ContinuationByte(value))
			{
				break;
			}
			if (!IsUtf8ContinuationByte(in pInputBuffer[2]))
			{
				break;
			}
			pInputBuffer += 3;
			num -= 2;
			num8 -= 3;
		}
		goto IL_05d5;
		IL_05d5:
		utf16CodeUnitCountAdjustment = num;
		scalarCountAdjustment = num2;
		return pInputBuffer;
	}*/
}
