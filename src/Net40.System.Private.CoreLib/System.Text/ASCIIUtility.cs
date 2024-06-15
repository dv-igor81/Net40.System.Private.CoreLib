using System.Numerics;
using System.Runtime.CompilerServices;

namespace System.Text;

internal static class ASCIIUtility
{
	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool AllBytesInUInt64AreAscii(ulong value)
	{
		return (value & 0x8080808080808080uL) == 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool AllCharsInUInt32AreAscii(uint value)
	{
		return (value & 0xFF80FF80u) == 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static bool AllCharsInUInt64AreAscii(ulong value)
	{
		return (value & 0xFF80FF80FF80FF80uL) == 0;
	}

	private static bool FirstCharInUInt32IsAscii(uint value)
	{
		if (!BitConverter.IsLittleEndian || (value & 0xFF80u) != 0)
		{
			if (!BitConverter.IsLittleEndian)
			{
				return (value & 0xFF800000u) == 0;
			}
			return false;
		}
		return true;
	}

	// [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	// public static unsafe ulong GetIndexOfFirstNonAsciiByte(byte* pBuffer, ulong bufferLength)
	// {
	// 	// if (!Sse2.IsSupported)
	// 	// {
	// 	// 	return GetIndexOfFirstNonAsciiByte_Default(pBuffer, bufferLength);
	// 	// }
	// 	return GetIndexOfFirstNonAsciiByte_Sse2(pBuffer, bufferLength);
	// }

	private static unsafe ulong GetIndexOfFirstNonAsciiByte_Default(byte* pBuffer, ulong bufferLength)
	{
		byte* ptr = pBuffer;
		if (Vector.IsHardwareAccelerated && bufferLength >= (uint)(2 * Vector<sbyte>.Count))
		{
			uint count = (uint)Vector<sbyte>.Count;
			if (Vector.GreaterThanOrEqualAll(Unsafe.ReadUnaligned<Vector<sbyte>>(pBuffer), Vector<sbyte>.Zero))
			{
				byte* ptr2 = pBuffer + bufferLength - count;
				pBuffer = (byte*)(((ulong)pBuffer + (ulong)count) & ~(ulong)(count - 1));
				while (!Vector.LessThanAny(Unsafe.Read<Vector<sbyte>>(pBuffer), Vector<sbyte>.Zero))
				{
					pBuffer += count;
					if (pBuffer > ptr2)
					{
						break;
					}
				}
				bufferLength -= (ulong)pBuffer;
				bufferLength += (ulong)ptr;
			}
		}
		while (true)
		{
			uint num;
			if (bufferLength >= 8)
			{
				num = Unsafe.ReadUnaligned<uint>(pBuffer);
				uint num2 = Unsafe.ReadUnaligned<uint>(pBuffer + 4);
				if (!AllBytesInUInt32AreAscii(num | num2))
				{
					if (AllBytesInUInt32AreAscii(num))
					{
						num = num2;
						pBuffer += 4;
					}
					goto IL_00fe;
				}
				pBuffer += 8;
				bufferLength -= 8;
				continue;
			}
			if ((bufferLength & 4) != 0L)
			{
				num = Unsafe.ReadUnaligned<uint>(pBuffer);
				if (!AllBytesInUInt32AreAscii(num))
				{
					goto IL_00fe;
				}
				pBuffer += 4;
			}
			if ((bufferLength & 2) != 0L)
			{
				num = Unsafe.ReadUnaligned<ushort>(pBuffer);
				if (!AllBytesInUInt32AreAscii(num))
				{
					goto IL_00fe;
				}
				pBuffer += 2;
			}
			if ((bufferLength & 1) != 0L && *pBuffer >= 0)
			{
				pBuffer++;
			}
			break;
			IL_00fe:
			pBuffer += CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(num);
			break;
		}
		return (ulong)pBuffer - (ulong)ptr;
	}

	/*private static unsafe ulong GetIndexOfFirstNonAsciiByte_Sse2(byte* pBuffer, ulong bufferLength)
	{
		uint num = (uint)Unsafe.SizeOf<Vector128<byte>>();
		ulong num2 = num - 1;
		byte* ptr = pBuffer;
		uint num3;
		if (bufferLength >= num)
		{
			num3 = (uint)Sse2.MoveMask(Sse2.LoadVector128(pBuffer));
			if (num3 == 0)
			{
				if (bufferLength < 2 * num)
				{
					goto IL_00a4;
				}
				pBuffer = (byte*)(((ulong)pBuffer + (ulong)num) & ~num2);
				bufferLength += (ulong)ptr;
				bufferLength -= (ulong)pBuffer;
				if (bufferLength < 2 * num)
				{
					goto IL_008f;
				}
				byte* ptr2 = (byte*)((long)pBuffer + (long)bufferLength - 2 * num);
				uint num4;
				while (true)
				{
					Vector128<byte> value = Sse2.LoadAlignedVector128(pBuffer);
					Vector128<byte> value2 = Sse2.LoadAlignedVector128(pBuffer + num);
					num3 = (uint)Sse2.MoveMask(value);
					num4 = (uint)Sse2.MoveMask(value2);
					if ((num3 | num4) != 0)
					{
						break;
					}
					pBuffer += 2 * num;
					if (pBuffer <= ptr2)
					{
						continue;
					}
					goto IL_008f;
				}
				if (num3 == 0)
				{
					pBuffer += num;
					num3 = num4;
				}
			}
			goto IL_00e3;
		}
		uint num6;
		if ((bufferLength & 8) != 0L)
		{
			if (Bmi1.X64.IsSupported)
			{
				ulong num5 = Unsafe.ReadUnaligned<ulong>(pBuffer);
				if (!AllBytesInUInt64AreAscii(num5))
				{
					num5 &= 0x8080808080808080uL;
					pBuffer += Bmi1.X64.TrailingZeroCount(num5) / 8;
					goto IL_00d1;
				}
			}
			else
			{
				num6 = Unsafe.ReadUnaligned<uint>(pBuffer);
				uint num7 = Unsafe.ReadUnaligned<uint>(pBuffer + 4);
				if (!AllBytesInUInt32AreAscii(num6 | num7))
				{
					if (AllBytesInUInt32AreAscii(num6))
					{
						num6 = num7;
						pBuffer += 4;
					}
					goto IL_00f0;
				}
			}
			pBuffer += 8;
		}
		if ((bufferLength & 4) != 0L)
		{
			num6 = Unsafe.ReadUnaligned<uint>(pBuffer);
			if (!AllBytesInUInt32AreAscii(num6))
			{
				goto IL_00f0;
			}
			pBuffer += 4;
		}
		if ((bufferLength & 2) != 0L)
		{
			num6 = Unsafe.ReadUnaligned<ushort>(pBuffer);
			if (!AllBytesInUInt32AreAscii(num6))
			{
				pBuffer += ((long)(sbyte)num6 >> 7) + 1;
				goto IL_00d1;
			}
			pBuffer += 2;
		}
		if ((bufferLength & 1) != 0L && *pBuffer >= 0)
		{
			pBuffer++;
		}
		goto IL_00d1;
		IL_00aa:
		if (((byte)bufferLength & num2) != 0L)
		{
			pBuffer += (bufferLength & num2) - num;
			num3 = (uint)Sse2.MoveMask(Sse2.LoadVector128(pBuffer));
			if (num3 != 0)
			{
				goto IL_00e3;
			}
			pBuffer += num;
		}
		goto IL_00d1;
		IL_008f:
		if ((bufferLength & num) == 0L)
		{
			goto IL_00aa;
		}
		num3 = (uint)Sse2.MoveMask(Sse2.LoadAlignedVector128(pBuffer));
		if (num3 == 0)
		{
			goto IL_00a4;
		}
		goto IL_00e3;
		IL_00f0:
		pBuffer += CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(num6);
		goto IL_00d1;
		IL_00e3:
		pBuffer += (uint)BitOperations.TrailingZeroCount(num3);
		goto IL_00d1;
		IL_00a4:
		pBuffer += num;
		goto IL_00aa;
		IL_00d1:
		return (ulong)pBuffer - (ulong)ptr;
	}*/

	// [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	// public static unsafe ulong GetIndexOfFirstNonAsciiChar(char* pBuffer, ulong bufferLength)
	// {
	// 	if (!Sse2.IsSupported)
	// 	{
	// 		return GetIndexOfFirstNonAsciiChar_Default(pBuffer, bufferLength);
	// 	}
	// 	return GetIndexOfFirstNonAsciiChar_Sse2(pBuffer, bufferLength);
	// }

	private static unsafe ulong GetIndexOfFirstNonAsciiChar_Default(char* pBuffer, ulong bufferLength)
	{
		char* ptr = pBuffer;
		if (Vector.IsHardwareAccelerated && bufferLength >= (uint)(2 * Vector<ushort>.Count))
		{
			uint count = (uint)Vector<ushort>.Count;
			uint count2 = (uint)Vector<byte>.Count;
			Vector<ushort> right = new Vector<ushort>(127);
			if (Vector.LessThanOrEqualAll(Unsafe.ReadUnaligned<Vector<ushort>>(pBuffer), right))
			{
				char* ptr2 = pBuffer + bufferLength - count;
				pBuffer = (char*)(((ulong)pBuffer + (ulong)count2) & ~(ulong)(count2 - 1));
				while (!Vector.GreaterThanAny(Unsafe.Read<Vector<ushort>>(pBuffer), right))
				{
					pBuffer += count;
					if (pBuffer > ptr2)
					{
						break;
					}
				}
				bufferLength -= (ulong)((long)pBuffer - (long)ptr) / 2uL;
			}
		}
		while (true)
		{
			uint num;
			if (bufferLength >= 4)
			{
				num = Unsafe.ReadUnaligned<uint>(pBuffer);
				uint num2 = Unsafe.ReadUnaligned<uint>(pBuffer + 2);
				if (!AllCharsInUInt32AreAscii(num | num2))
				{
					if (AllCharsInUInt32AreAscii(num))
					{
						num = num2;
						pBuffer += 2;
					}
					goto IL_010e;
				}
				pBuffer += 4;
				bufferLength -= 4;
				continue;
			}
			if ((bufferLength & 2) != 0L)
			{
				num = Unsafe.ReadUnaligned<uint>(pBuffer);
				if (!AllCharsInUInt32AreAscii(num))
				{
					goto IL_010e;
				}
				pBuffer += 2;
			}
			if ((bufferLength & 1) != 0L && *pBuffer <= '\u007f')
			{
				pBuffer++;
			}
			break;
			IL_010e:
			if (FirstCharInUInt32IsAscii(num))
			{
				pBuffer++;
			}
			break;
		}
		ulong num3 = (ulong)pBuffer - (ulong)ptr;
		return num3 / 2;
	}


	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static void NarrowFourUtf16CharsToAsciiAndWriteToBuffer(ref byte outputBuffer, ulong value)
	{
		/*if (Sse2.X64.IsSupported)
		{
			Vector128<short> vector = Sse2.X64.ConvertScalarToVector128UInt64(value).AsInt16();
			Vector128<uint> value2 = Sse2.PackUnsignedSaturate(vector, vector).AsUInt32();
			Unsafe.WriteUnaligned(ref outputBuffer, Sse2.ConvertToUInt32(value2));
		}
		else */
		if (BitConverter.IsLittleEndian)
		{
			outputBuffer = (byte)value;
			value >>= 16;
			Unsafe.Add(ref outputBuffer, 1) = (byte)value;
			value >>= 16;
			Unsafe.Add(ref outputBuffer, 2) = (byte)value;
			value >>= 16;
			Unsafe.Add(ref outputBuffer, 3) = (byte)value;
		}
		else
		{
			Unsafe.Add(ref outputBuffer, 3) = (byte)value;
			value >>= 16;
			Unsafe.Add(ref outputBuffer, 2) = (byte)value;
			value >>= 16;
			Unsafe.Add(ref outputBuffer, 1) = (byte)value;
			value >>= 16;
			outputBuffer = (byte)value;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static void NarrowTwoUtf16CharsToAsciiAndWriteToBuffer(ref byte outputBuffer, uint value)
	{
		if (BitConverter.IsLittleEndian)
		{
			outputBuffer = (byte)value;
			Unsafe.Add(ref outputBuffer, 1) = (byte)(value >> 16);
		}
		else
		{
			Unsafe.Add(ref outputBuffer, 1) = (byte)value;
			outputBuffer = (byte)(value >> 16);
		}
	}

	public static unsafe ulong NarrowUtf16ToAscii(char* pUtf16Buffer, byte* pAsciiBuffer, ulong elementCount)
	{
		ulong num = 0uL;
		uint num2 = 0u;
		uint num3 = 0u;
		ulong num4 = 0uL;
		uint num5;
		/*if (Sse2.IsSupported)
		{
			if (elementCount >= (uint)(2 * Unsafe.SizeOf<Vector128<byte>>()))
			{
				if (IntPtr.Size >= 8)
				{
					num4 = Unsafe.ReadUnaligned<ulong>(pUtf16Buffer);
					if (AllCharsInUInt64AreAscii(num4))
					{
						goto IL_005b;
					}
				}
				else
				{
					num2 = Unsafe.ReadUnaligned<uint>(pUtf16Buffer);
					num3 = Unsafe.ReadUnaligned<uint>(pUtf16Buffer + 2);
					if (AllCharsInUInt32AreAscii(num2 | num3))
					{
						goto IL_005b;
					}
				}
				goto IL_0206;
			}
		}
		else */
		if (Vector.IsHardwareAccelerated)
		{
			num5 = (uint)Unsafe.SizeOf<Vector<byte>>();
			if (elementCount >= 2 * num5)
			{
				if (IntPtr.Size >= 8)
				{
					num4 = Unsafe.ReadUnaligned<ulong>(pUtf16Buffer);
					if (AllCharsInUInt64AreAscii(num4))
					{
						goto IL_00c1;
					}
				}
				else
				{
					num2 = Unsafe.ReadUnaligned<uint>(pUtf16Buffer);
					num3 = Unsafe.ReadUnaligned<uint>(pUtf16Buffer + 2);
					if (AllCharsInUInt32AreAscii(num2 | num3))
					{
						goto IL_00c1;
					}
				}
				goto IL_0206;
			}
		}
		goto IL_012b;
		IL_0206:
		if (IntPtr.Size >= 8)
		{
			num2 = (uint)((!BitConverter.IsLittleEndian) ? (num4 >> 32) : num4);
			if (AllCharsInUInt32AreAscii(num2))
			{
				NarrowTwoUtf16CharsToAsciiAndWriteToBuffer(ref pAsciiBuffer[num], num2);
				num2 = (uint)((!BitConverter.IsLittleEndian) ? num4 : (num4 >> 32));
				num += 2;
			}
		}
		else if (AllCharsInUInt32AreAscii(num2))
		{
			NarrowTwoUtf16CharsToAsciiAndWriteToBuffer(ref pAsciiBuffer[num], num2);
			num2 = num3;
			num += 2;
		}
		goto IL_0264;
		IL_012b:
		ulong num6 = elementCount - num;
		if (num6 < 4)
		{
			goto IL_01b5;
		}
		ulong num7 = num + num6 - 4;
		while (true)
		{
			if (IntPtr.Size >= 8)
			{
				num4 = Unsafe.ReadUnaligned<ulong>(pUtf16Buffer + num);
				if (!AllCharsInUInt64AreAscii(num4))
				{
					break;
				}
				NarrowFourUtf16CharsToAsciiAndWriteToBuffer(ref pAsciiBuffer[num], num4);
			}
			else
			{
				num2 = Unsafe.ReadUnaligned<uint>(pUtf16Buffer + num);
				num3 = Unsafe.ReadUnaligned<uint>(pUtf16Buffer + num + 2);
				if (!AllCharsInUInt32AreAscii(num2 | num3))
				{
					break;
				}
				NarrowTwoUtf16CharsToAsciiAndWriteToBuffer(ref pAsciiBuffer[num], num2);
				NarrowTwoUtf16CharsToAsciiAndWriteToBuffer(ref pAsciiBuffer[num + 2], num3);
			}
			num += 4;
			if (num <= num7)
			{
				continue;
			}
			goto IL_01b5;
		}
		goto IL_0206;
		IL_0204:
		return num;
		IL_01b5:
		if (((uint)(int)num6 & 2u) != 0)
		{
			num2 = Unsafe.ReadUnaligned<uint>(pUtf16Buffer + num);
			if (!AllCharsInUInt32AreAscii(num2))
			{
				goto IL_0264;
			}
			NarrowTwoUtf16CharsToAsciiAndWriteToBuffer(ref pAsciiBuffer[num], num2);
			num += 2;
		}
		if (((uint)(int)num6 & (true ? 1u : 0u)) != 0)
		{
			num2 = pUtf16Buffer[num];
			if (num2 <= 127)
			{
				pAsciiBuffer[num] = (byte)num2;
				num++;
			}
		}
		goto IL_0204;
		IL_00c1:
		Vector<ushort> right = new Vector<ushort>(127);
		ulong num8 = elementCount - 2 * num5;
		do
		{
			Vector<ushort> vector = Unsafe.ReadUnaligned<Vector<ushort>>(pUtf16Buffer + num);
			Vector<ushort> vector2 = Unsafe.ReadUnaligned<Vector<ushort>>(pUtf16Buffer + num + Vector<ushort>.Count);
			if (Vector.GreaterThanAny(Vector.BitwiseOr(vector, vector2), right))
			{
				break;
			}
			Vector<byte> value = Vector.Narrow(vector, vector2);
			Unsafe.WriteUnaligned(pAsciiBuffer + num, value);
			num += num5;
		}
		while (num <= num8);
		goto IL_012b;
		IL_0264:
		if (FirstCharInUInt32IsAscii(num2))
		{
			if (!BitConverter.IsLittleEndian)
			{
				num2 >>= 16;
			}
			pAsciiBuffer[num] = (byte)num2;
			num++;
		}
		goto IL_0204;
	}

	/*private static unsafe ulong NarrowUtf16ToAscii_Sse2(char* pUtf16Buffer, byte* pAsciiBuffer, ulong elementCount)
	{
		uint num = (uint)Unsafe.SizeOf<Vector128<byte>>();
		ulong num2 = num - 1;
		Vector128<short> right = Vector128.Create((short)(-128));
		Vector128<short> right2 = Vector128.Create(short.MinValue);
		Vector128<short> right3 = Vector128.Create((short)(-32641));
		Vector128<short> vector = Sse2.LoadVector128((short*)pUtf16Buffer);
		if (Sse41.IsSupported)
		{
			if (!Sse41.TestZ(vector, right))
			{
				return 0uL;
			}
		}
		else if (Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(vector, right2), right3).AsByte()) != 0)
		{
			return 0uL;
		}
		Vector128<byte> vector2 = Sse2.PackUnsignedSaturate(vector, vector);
		Sse2.StoreScalar((ulong*)pAsciiBuffer, vector2.AsUInt64());
		ulong num3 = num / 2;
		if (((uint)(int)pAsciiBuffer & (num / 2)) != 0)
		{
			goto IL_00e9;
		}
		vector = Sse2.LoadVector128((short*)(pUtf16Buffer + num3));
		if (Sse41.IsSupported)
		{
			if (Sse41.TestZ(vector, right))
			{
				goto IL_00cd;
			}
		}
		else if (Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(vector, right2), right3).AsByte()) == 0)
		{
			goto IL_00cd;
		}
		goto IL_017f;
		IL_017f:
		return num3;
		IL_00cd:
		vector2 = Sse2.PackUnsignedSaturate(vector, vector);
		Sse2.StoreScalar((ulong*)(pAsciiBuffer + num3), vector2.AsUInt64());
		goto IL_00e9;
		IL_00e9:
		num3 = num - ((ulong)pAsciiBuffer & num2);
		ulong num4 = elementCount - num;
		do
		{
			vector = Sse2.LoadVector128((short*)(pUtf16Buffer + num3));
			Vector128<short> right4 = Sse2.LoadVector128((short*)(pUtf16Buffer + num3 + num / 2));
			Vector128<short> left = Sse2.Or(vector, right4);
			if (Sse41.IsSupported)
			{
				if (Sse41.TestZ(left, right))
				{
					goto IL_0158;
				}
			}
			else if (Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(left, right2), right3).AsByte()) == 0)
			{
				goto IL_0158;
			}
			if (Sse41.IsSupported)
			{
				if (!Sse41.TestZ(vector, right))
				{
					break;
				}
			}
			else if (Sse2.MoveMask(Sse2.CompareGreaterThan(Sse2.Xor(vector, right2), right3).AsByte()) != 0)
			{
				break;
			}
			vector2 = Sse2.PackUnsignedSaturate(vector, vector);
			Sse2.StoreScalar((ulong*)(pAsciiBuffer + num3), vector2.AsUInt64());
			num3 += num / 2;
			break;
			IL_0158:
			vector2 = Sse2.PackUnsignedSaturate(vector, right4);
			Sse2.StoreAligned(pAsciiBuffer + num3, vector2);
			num3 += num;
		}
		while (num3 <= num4);
		goto IL_017f;
	}*/

	public static unsafe ulong WidenAsciiToUtf16(byte* pAsciiBuffer, char* pUtf16Buffer, ulong elementCount)
	{
		ulong num = 0uL;
		/*if (Sse2.IsSupported)
		{
			if (elementCount >= (uint)(2 * Unsafe.SizeOf<Vector128<byte>>()))
			{
				num = WidenAsciiToUtf16_Sse2(pAsciiBuffer, pUtf16Buffer, elementCount);
			}
		}
		else*/ 
		if (Vector.IsHardwareAccelerated)
		{
			uint num2 = (uint)Unsafe.SizeOf<Vector<byte>>();
			if (elementCount >= num2)
			{
				ulong num3 = elementCount - num2;
				do
				{
					Vector<sbyte> vector = Unsafe.ReadUnaligned<Vector<sbyte>>(pAsciiBuffer + num);
					if (Vector.LessThanAny(vector, Vector<sbyte>.Zero))
					{
						break;
					}
					Vector.Widen(Vector.AsVectorByte(vector), out var low, out var high);
					Unsafe.WriteUnaligned(pUtf16Buffer + num, low);
					Unsafe.WriteUnaligned(pUtf16Buffer + num + Vector<ushort>.Count, high);
					num += num2;
				}
				while (num <= num3);
			}
		}
		ulong num4 = elementCount - num;
		if (num4 < 4)
		{
			goto IL_00cd;
		}
		ulong num5 = num + num4 - 4;
		uint num6;
		while (true)
		{
			num6 = Unsafe.ReadUnaligned<uint>(pAsciiBuffer + num);
			if (!AllBytesInUInt32AreAscii(num6))
			{
				break;
			}
			WidenFourAsciiBytesToUtf16AndWriteToBuffer(ref pUtf16Buffer[num], num6);
			num += 4;
			if (num <= num5)
			{
				continue;
			}
			goto IL_00cd;
		}
		goto IL_015f;
		IL_014a:
		return num;
		IL_00cd:
		if (((uint)(int)num4 & 2u) != 0)
		{
			num6 = Unsafe.ReadUnaligned<ushort>(pAsciiBuffer + num);
			if (!AllBytesInUInt32AreAscii(num6))
			{
				goto IL_015f;
			}
			if (BitConverter.IsLittleEndian)
			{
				pUtf16Buffer[num] = (char)(byte)num6;
				pUtf16Buffer[num + 1] = (char)(num6 >> 8);
			}
			else
			{
				pUtf16Buffer[num + 1] = (char)(byte)num6;
				pUtf16Buffer[num] = (char)(num6 >> 8);
			}
			num += 2;
		}
		if (((uint)(int)num4 & (true ? 1u : 0u)) != 0)
		{
			num6 = pAsciiBuffer[num];
			if (((byte)num6 & 0x80) == 0)
			{
				pUtf16Buffer[num] = (char)num6;
				num++;
			}
		}
		goto IL_014a;
		IL_015f:
		while (((byte)num6 & 0x80) == 0)
		{
			pUtf16Buffer[num] = (char)(byte)num6;
			num++;
			num6 >>= 8;
		}
		goto IL_014a;
	}


	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static void WidenFourAsciiBytesToUtf16AndWriteToBuffer(ref char outputBuffer, uint value)
	{
		/*if (Sse2.X64.IsSupported)
		{
			Vector128<byte> left = Sse2.ConvertScalarToVector128UInt32(value).AsByte();
			Vector128<ulong> value2 = Sse2.UnpackLow(left, Vector128<byte>.Zero).AsUInt64();
			Unsafe.WriteUnaligned(ref Unsafe.As<char, byte>(ref outputBuffer), Sse2.X64.ConvertToUInt64(value2));
		}
		else*/ 
		if (BitConverter.IsLittleEndian)
		{
			outputBuffer = (char)(byte)value;
			value >>= 8;
			Unsafe.Add(ref outputBuffer, 1) = (char)(byte)value;
			value >>= 8;
			Unsafe.Add(ref outputBuffer, 2) = (char)(byte)value;
			value >>= 8;
			Unsafe.Add(ref outputBuffer, 3) = (char)value;
		}
		else
		{
			Unsafe.Add(ref outputBuffer, 3) = (char)(byte)value;
			value >>= 8;
			Unsafe.Add(ref outputBuffer, 2) = (char)(byte)value;
			value >>= 8;
			Unsafe.Add(ref outputBuffer, 1) = (char)(byte)value;
			value >>= 8;
			outputBuffer = (char)value;
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static bool AllBytesInUInt32AreAscii(uint value)
	{
		return (value & 0x80808080u) == 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal static uint CountNumberOfLeadingAsciiBytesFromUInt32WithSomeNonAsciiData(uint value)
	{
		// if (Bmi1.IsSupported)
		// {
		// 	return Bmi1.TrailingZeroCount(value & 0x80808080u) >> 3;
		// }
		value = ~value;
		if (BitConverter.IsLittleEndian)
		{
			value >>= 7;
			uint num = value & 1u;
			uint num2 = num;
			value >>= 8;
			num &= value;
			num2 += num;
			value >>= 8;
			num &= value;
			return num2 + num;
		}
		value = BitOperations.RotateLeft(value, 1);
		uint num3 = value & 1u;
		uint num4 = num3;
		value = BitOperations.RotateLeft(value, 8);
		num3 &= value;
		num4 += num3;
		value = BitOperations.RotateLeft(value, 8);
		num3 &= value;
		return num4 + num3;
	}
}
