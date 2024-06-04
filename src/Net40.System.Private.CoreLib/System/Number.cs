using System.Buffers.Text;
using System.Runtime.CompilerServices;

namespace System;

internal static class Number
{
	private static class DoubleHelper
	{
		public unsafe static uint Exponent(double d)
		{
			return (*(uint*)((byte*)(&d) + 4) >> 20) & 0x7FFu;
		}

		public unsafe static ulong Mantissa(double d)
		{
			return *(uint*)(&d) | ((ulong)(uint)(*(int*)((byte*)(&d) + 4) & 0xFFFFF) << 32);
		}

		public unsafe static bool Sign(double d)
		{
			return *(uint*)((byte*)(&d) + 4) >> 31 != 0;
		}
	}

	internal const int DECIMAL_PRECISION = 29;

	private static readonly ulong[] s_rgval64Power10 = new ulong[30]
	{
		11529215046068469760uL, 14411518807585587200uL, 18014398509481984000uL, 11258999068426240000uL, 14073748835532800000uL, 17592186044416000000uL, 10995116277760000000uL, 13743895347200000000uL, 17179869184000000000uL, 10737418240000000000uL,
		13421772800000000000uL, 16777216000000000000uL, 10485760000000000000uL, 13107200000000000000uL, 16384000000000000000uL, 14757395258967641293uL, 11805916207174113035uL, 9444732965739290428uL, 15111572745182864686uL, 12089258196146291749uL,
		9671406556917033399uL, 15474250491067253438uL, 12379400392853802751uL, 9903520314283042201uL, 15845632502852867522uL, 12676506002282294018uL, 10141204801825835215uL, 16225927682921336344uL, 12980742146337069075uL, 10384593717069655260uL
	};

	private static readonly sbyte[] s_rgexp64Power10 = new sbyte[15]
	{
		4, 7, 10, 14, 17, 20, 24, 27, 30, 34,
		37, 40, 44, 47, 50
	};

	private static readonly ulong[] s_rgval64Power10By16 = new ulong[42]
	{
		10240000000000000000uL, 11368683772161602974uL, 12621774483536188886uL, 14012984643248170708uL, 15557538194652854266uL, 17272337110188889248uL, 9588073174409622172uL, 10644899600020376798uL, 11818212630765741798uL, 13120851772591970216uL,
		14567071740625403792uL, 16172698447808779622uL, 17955302187076837696uL, 9967194951097567532uL, 11065809325636130658uL, 12285516299433008778uL, 13639663065038175358uL, 15143067982934716296uL, 16812182738118149112uL, 9332636185032188787uL,
		10361307573072618722uL, 16615349947311448416uL, 14965776766268445891uL, 13479973333575319909uL, 12141680576410806707uL, 10936253623915059637uL, 9850501549098619819uL, 17745086042373215136uL, 15983352577617880260uL, 14396524142538228461uL,
		12967236152753103031uL, 11679847981112819795uL, 10520271803096747049uL, 9475818434452569218uL, 17070116948172427008uL, 15375394465392026135uL, 13848924157002783096uL, 12474001934591998882uL, 11235582092889474480uL, 10120112665365530972uL,
		18230774251475056952uL, 16420821625123739930uL
	};

	private static readonly short[] s_rgexp64Power10By16 = new short[21]
	{
		54, 107, 160, 213, 266, 319, 373, 426, 479, 532,
		585, 638, 691, 745, 798, 851, 904, 957, 1010, 1064,
		1117
	};

	public static void RoundNumber(ref NumberBuffer number, int pos)
	{
		Span<byte> digits = number.Digits;
		int i;
		for (i = 0; i < pos && digits[i] != 0; i++)
		{
		}
		if (i == pos && digits[i] >= 53)
		{
			while (i > 0 && digits[i - 1] == 57)
			{
				i--;
			}
			if (i > 0)
			{
				digits[i - 1]++;
			}
			else
			{
				number.Scale++;
				digits[0] = 49;
				i = 1;
			}
		}
		else
		{
			while (i > 0 && digits[i - 1] == 48)
			{
				i--;
			}
		}
		if (i == 0)
		{
			number.Scale = 0;
			number.IsNegative = false;
		}
		digits[i] = 0;
	}

	internal static bool NumberBufferToDouble(ref NumberBuffer number, out double value)
	{
		double num = NumberToDouble(ref number);
		uint num2 = DoubleHelper.Exponent(num);
		ulong num3 = DoubleHelper.Mantissa(num);
		switch (num2)
		{
		case 2047u:
			value = 0.0;
			return false;
		case 0u:
			if (num3 == 0)
			{
				num = 0.0;
			}
			break;
		}
		value = num;
		return true;
	}

	public unsafe static bool NumberBufferToDecimal(ref NumberBuffer number, ref decimal value)
	{
		MutableDecimal source = default(MutableDecimal);
		byte* ptr = number.UnsafeDigits;
		int num = number.Scale;
		if (*ptr == 0)
		{
			if (num > 0)
			{
				num = 0;
			}
		}
		else
		{
			if (num > 29)
			{
				return false;
			}
			while ((num > 0 || (*ptr != 0 && num > -28)) && (source.High < 429496729 || (source.High == 429496729 && (source.Mid < 2576980377u || (source.Mid == 2576980377u && (source.Low < 2576980377u || (source.Low == 2576980377u && *ptr <= 53)))))))
			{
				DecimalDecCalc.DecMul10(ref source);
				if (*ptr != 0)
				{
					DecimalDecCalc.DecAddInt32(ref source, (uint)(*(ptr++) - 48));
				}
				num--;
			}
			if (*(ptr++) >= 53)
			{
				bool flag = true;
				if (*(ptr - 1) == 53 && *(ptr - 2) % 2 == 0)
				{
					int num2 = 20;
					while (*ptr == 48 && num2 != 0)
					{
						ptr++;
						num2--;
					}
					if (*ptr == 0 || num2 == 0)
					{
						flag = false;
					}
				}
				if (flag)
				{
					DecimalDecCalc.DecAddInt32(ref source, 1u);
					if ((source.High | source.Mid | source.Low) == 0)
					{
						source.High = 429496729u;
						source.Mid = 2576980377u;
						source.Low = 2576980378u;
						num++;
					}
				}
			}
		}
		if (num > 0)
		{
			return false;
		}
		if (num <= -29)
		{
			source.High = 0u;
			source.Low = 0u;
			source.Mid = 0u;
			source.Scale = 28;
		}
		else
		{
			source.Scale = -num;
		}
		source.IsNegative = number.IsNegative;
		value = Unsafe.As<MutableDecimal, decimal>(ref source);
		return true;
	}

	public static void DecimalToNumber(decimal value, ref NumberBuffer number)
	{
		ref MutableDecimal reference = ref Unsafe.As<decimal, MutableDecimal>(ref value);
		Span<byte> digits = number.Digits;
		number.IsNegative = reference.IsNegative;
		int num = 29;
		while ((reference.Mid != 0) | (reference.High != 0))
		{
			uint num2 = DecimalDecCalc.DecDivMod1E9(ref reference);
			for (int i = 0; i < 9; i++)
			{
				digits[--num] = (byte)(num2 % 10 + 48);
				num2 /= 10;
			}
		}
		for (uint num3 = reference.Low; num3 != 0; num3 /= 10)
		{
			digits[--num] = (byte)(num3 % 10 + 48);
		}
		int num4 = 29 - num;
		number.Scale = num4 - reference.Scale;
		Span<byte> digits2 = number.Digits;
		int index = 0;
		while (--num4 >= 0)
		{
			digits2[index++] = digits[num++];
		}
		digits2[index] = 0;
	}

	private static uint DigitsToInt(ReadOnlySpan<byte> digits, int count)
	{
		uint value;
		int bytesConsumed;
		bool flag = Utf8Parser.TryParse(digits.Slice(0, count), out value, out bytesConsumed, 'D');
		return value;
	}

	private static ulong Mul32x32To64(uint a, uint b)
	{
		return (ulong)a * (ulong)b;
	}

	private static ulong Mul64Lossy(ulong a, ulong b, ref int pexp)
	{
		ulong num = Mul32x32To64((uint)(a >> 32), (uint)(b >> 32)) + (Mul32x32To64((uint)(a >> 32), (uint)b) >> 32) + (Mul32x32To64((uint)a, (uint)(b >> 32)) >> 32);
		if ((num & 0x8000000000000000uL) == 0)
		{
			num <<= 1;
			pexp--;
		}
		return num;
	}

	private static int abs(int value)
	{
		if (value < 0)
		{
			return -value;
		}
		return value;
	}

	private unsafe static double NumberToDouble(ref NumberBuffer number)
	{
		ReadOnlySpan<byte> digits = number.Digits;
		int i = 0;
		int numDigits = number.NumDigits;
		int num = numDigits;
		for (; digits[i] == 48; i++)
		{
			num--;
		}
		if (num == 0)
		{
			return 0.0;
		}
		int num3 = Math.Min(num, 9);
		num -= num3;
		ulong num4 = DigitsToInt(digits, num3);
		if (num > 0)
		{
			num3 = Math.Min(num, 9);
			num -= num3;
			uint b = (uint)(s_rgval64Power10[num3 - 1] >> 64 - s_rgexp64Power10[num3 - 1]);
			num4 = Mul32x32To64((uint)num4, b) + DigitsToInt(digits.Slice(9), num3);
		}
		int num5 = number.Scale - (numDigits - num);
		int num6 = abs(num5);
		if (num6 >= 352)
		{
			ulong num7 = ((num5 > 0) ? 9218868437227405312uL : 0);
			if (number.IsNegative)
			{
				num7 |= 0x8000000000000000uL;
			}
			return *(double*)(&num7);
		}
		int pexp = 64;
		if ((num4 & 0xFFFFFFFF00000000uL) == 0)
		{
			num4 <<= 32;
			pexp -= 32;
		}
		if ((num4 & 0xFFFF000000000000uL) == 0)
		{
			num4 <<= 16;
			pexp -= 16;
		}
		if ((num4 & 0xFF00000000000000uL) == 0)
		{
			num4 <<= 8;
			pexp -= 8;
		}
		if ((num4 & 0xF000000000000000uL) == 0)
		{
			num4 <<= 4;
			pexp -= 4;
		}
		if ((num4 & 0xC000000000000000uL) == 0)
		{
			num4 <<= 2;
			pexp -= 2;
		}
		if ((num4 & 0x8000000000000000uL) == 0)
		{
			num4 <<= 1;
			pexp--;
		}
		int num8 = num6 & 0xF;
		if (num8 != 0)
		{
			int num9 = s_rgexp64Power10[num8 - 1];
			pexp += ((num5 < 0) ? (-num9 + 1) : num9);
			ulong b2 = s_rgval64Power10[num8 + ((num5 < 0) ? 15 : 0) - 1];
			num4 = Mul64Lossy(num4, b2, ref pexp);
		}
		num8 = num6 >> 4;
		if (num8 != 0)
		{
			int num10 = s_rgexp64Power10By16[num8 - 1];
			pexp += ((num5 < 0) ? (-num10 + 1) : num10);
			ulong b3 = s_rgval64Power10By16[num8 + ((num5 < 0) ? 21 : 0) - 1];
			num4 = Mul64Lossy(num4, b3, ref pexp);
		}
		if (((uint)(int)num4 & 0x400u) != 0)
		{
			ulong num2 = num4 + 1023 + (ulong)(((int)num4 >> 11) & 1);
			if (num2 < num4)
			{
				num2 = (num2 >> 1) | 0x8000000000000000uL;
				pexp++;
			}
			num4 = num2;
		}
		pexp += 1022;
		num4 = ((pexp > 0) ? ((pexp < 2047) ? ((ulong)((long)pexp << 52) + ((num4 >> 11) & 0xFFFFFFFFFFFFFL)) : 9218868437227405312uL) : ((pexp == -52 && num4 >= 9223372036854775896uL) ? 1 : ((pexp > -52) ? (num4 >> -pexp + 11 + 1) : 0)));
		if (number.IsNegative)
		{
			num4 |= 0x8000000000000000uL;
		}
		return *(double*)(&num4);
	}
}
