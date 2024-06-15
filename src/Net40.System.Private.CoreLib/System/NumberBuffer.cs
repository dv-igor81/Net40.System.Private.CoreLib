
using System;
using System.Runtime.CompilerServices;
using System.Text;

internal enum NumberBufferKind : byte
{
	Unknown,
	Integer,
	Decimal,
	FloatingPoint
}


internal ref struct NumberBuffer
{
	public int DigitsCount;

	public int Scale;

	public bool IsNegative;

	public bool HasNonZeroTail;

	public NumberBufferKind Kind;

	public Span<byte> Digits;

	public unsafe NumberBuffer(NumberBufferKind kind, byte* digits, int digitsLength)
	{
		DigitsCount = 0;
		Scale = 0;
		IsNegative = false;
		HasNonZeroTail = false;
		Kind = kind;
		Digits = new Span<byte>(digits, digitsLength);
		Digits[0] = 0;
	}

	public unsafe byte* GetDigitsPointer()
	{
		return (byte*)Unsafe.AsPointer(ref Digits[0]);
	}
	
	public int NumDigits => Digits.IndexOf<byte>(0);

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append('[');
		stringBuilder.Append('"');
		for (int i = 0; i < Digits.Length; i++)
		{
			byte b = Digits[i];
			if (b == 0)
			{
				break;
			}
			stringBuilder.Append((char)b);
		}
		stringBuilder.Append('"');
		stringBuilder.Append(", Length = " + DigitsCount);
		stringBuilder.Append(", Scale = " + Scale);
		stringBuilder.Append(", IsNegative = " + IsNegative);
		stringBuilder.Append(", HasNonZeroTail = " + HasNonZeroTail);
		stringBuilder.Append(", Kind = " + Kind);
		stringBuilder.Append(']');
		return stringBuilder.ToString();
	}
}
