using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System;

internal static class SpanHelpers
{
    internal struct ComparerComparable<T, TComparer> : IComparable<T> where TComparer : IComparer<T>
    {
        private readonly T _value;

        private readonly TComparer _comparer;

        public ComparerComparable(T value, TComparer comparer)
        {
            _value = value;
            _comparer = comparer;
        }

        [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
        public int CompareTo(T other)
        {
            return _comparer.Compare(_value, other);
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct Reg64
    {
    }

    [StructLayout(LayoutKind.Sequential, Size = 32)]
    private struct Reg32
    {
    }

    [StructLayout(LayoutKind.Sequential, Size = 16)]
    private struct Reg16
    {
    }

    public static class PerTypeValues<T>
    {
        public static readonly bool IsReferenceOrContainsReferences = IsReferenceOrContainsReferencesCore(typeof(T));

        public static readonly T[] EmptyArray = new T[0];

        public static readonly IntPtr ArrayAdjustment = MeasureArrayAdjustment();

        private static IntPtr MeasureArrayAdjustment()
        {
            T[] array = new T[1];
            return Unsafe.ByteOffset(ref Unsafe.As<Pinnable<T>>(array).Data, ref array[0]);
        }
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    public static int BinarySearch<T, TComparable>(this ReadOnlySpan<T> span, TComparable comparable)
        where TComparable : IComparable<T>
    {
        if (comparable == null)
        {
            ThrowHelper.ThrowArgumentNullException(ExceptionArgument.comparable);
        }

        return BinarySearch(ref MemoryMarshal.GetReference(span), span.Length, comparable);
    }
    
    
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    public static unsafe bool Contains(ref char searchSpace, char value, int length)
    {
        fixed (char* ptr = &searchSpace)
        {
            char* ptr2 = ptr;
            char* ptr3 = ptr2 + length;
            if (Vector.IsHardwareAccelerated && length >= Vector<ushort>.Count * 2)
            {
                int num = ((int)ptr2 & (Unsafe.SizeOf<Vector<ushort>>() - 1)) / 2;
                length = (Vector<ushort>.Count - num) & (Vector<ushort>.Count - 1);
            }
            while (true)
            {
                if (length >= 4)
                {
                    length -= 4;
                    if (value == *ptr2 || value == ptr2[1] || value == ptr2[2] || value == ptr2[3])
                    {
                        break;
                    }
                    ptr2 += 4;
                    continue;
                }
                while (length > 0)
                {
                    length--;
                    if (value == *ptr2)
                    {
                        goto end_IL_0079;
                    }
                    ptr2++;
                }
                if (Vector.IsHardwareAccelerated && ptr2 < ptr3)
                {
                    length = (int)((ptr3 - ptr2) & ~(Vector<ushort>.Count - 1));
                    Vector<ushort> left = new Vector<ushort>(value);
                    while (length > 0)
                    {
                        Vector<ushort> other = Vector.Equals(left, Unsafe.Read<Vector<ushort>>(ptr2));
                        if (!Vector<ushort>.Zero.Equals(other))
                        {
                            goto end_IL_0079;
                        }
                        ptr2 += Vector<ushort>.Count;
                        length -= Vector<ushort>.Count;
                    }
                    if (ptr2 < ptr3)
                    {
                        length = (int)(ptr3 - ptr2);
                        continue;
                    }
                }
                return false;
                continue;
                end_IL_0079:
                break;
            }
            return true;
        }
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    public static unsafe bool Contains(ref byte searchSpace, byte value, int length)
    {
        IntPtr intPtr = (IntPtr)0;
        IntPtr intPtr2 = (IntPtr)length;
        if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        {
            intPtr2 = UnalignedCountVector(ref searchSpace);
        }

        while (true)
        {
            if ((nuint)(void*)intPtr2 >= (nuint)8u)
            {
                intPtr2 -= 8;
                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 0) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 1) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 2) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 3) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 4) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 5) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 6) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 7))
                {
                    break;
                }

                intPtr += 8;
                continue;
            }

            if ((nuint)(void*)intPtr2 >= (nuint)4u)
            {
                intPtr2 -= 4;
                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 0) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 1) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 2) ||
                    value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 3))
                {
                    break;
                }

                intPtr += 4;
            }

            while ((void*)intPtr2 != null)
            {
                intPtr2 -= 1;
                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr))
                {
                    goto end_IL_00e3;
                }

                intPtr += 1;
            }

            if (Vector.IsHardwareAccelerated && (int)(void*)intPtr < length)
            {
                intPtr2 = (IntPtr)((length - (int)(void*)intPtr) & ~(Vector<byte>.Count - 1));
                Vector<byte> left = new Vector<byte>(value);
                for (; (void*)intPtr2 > (void*)intPtr; intPtr += Vector<byte>.Count)
                {
                    Vector<byte> other = Vector.Equals(left, LoadVector(ref searchSpace, intPtr));
                    if (!Vector<byte>.Zero.Equals(other))
                    {
                        goto end_IL_00e3;
                    }
                }

                if ((int)(void*)intPtr < length)
                {
                    intPtr2 = (IntPtr)(length - (int)(void*)intPtr);
                    continue;
                }
            }

            return false;
            continue;
            end_IL_00e3:
            break;
        }

        return true;
    }
    
    public static unsafe bool Contains<T>(ref T searchSpace, T value, int length) where T : IEquatable<T>
    {
        IntPtr intPtr = (IntPtr)0;
        if (default(T) != null || value != null)
        {
            while (length >= 8)
            {
                length -= 8;
                if (!value.Equals(Unsafe.Add(ref searchSpace, intPtr + 0)) && !value.Equals(Unsafe.Add(ref searchSpace, intPtr + 1)) && !value.Equals(Unsafe.Add(ref searchSpace, intPtr + 2)) && !value.Equals(Unsafe.Add(ref searchSpace, intPtr + 3)) && !value.Equals(Unsafe.Add(ref searchSpace, intPtr + 4)) && !value.Equals(Unsafe.Add(ref searchSpace, intPtr + 5)) && !value.Equals(Unsafe.Add(ref searchSpace, intPtr + 6)) && !value.Equals(Unsafe.Add(ref searchSpace, intPtr + 7)))
                {
                    intPtr += 8;
                    continue;
                }
                goto IL_0262;
            }
            if (length >= 4)
            {
                length -= 4;
                if (value.Equals(Unsafe.Add(ref searchSpace, intPtr + 0)) || value.Equals(Unsafe.Add(ref searchSpace, intPtr + 1)) || value.Equals(Unsafe.Add(ref searchSpace, intPtr + 2)) || value.Equals(Unsafe.Add(ref searchSpace, intPtr + 3)))
                {
                    goto IL_0262;
                }
                intPtr += 4;
            }
            while (length > 0)
            {
                length--;
                if (!value.Equals(Unsafe.Add(ref searchSpace, intPtr)))
                {
                    intPtr += 1;
                    continue;
                }
                goto IL_0262;
            }
        }
        else
        {
            byte* ptr = (byte*)length;
            intPtr = (IntPtr)0;
            while (intPtr.ToPointer() < ptr)
            {
                if (Unsafe.Add(ref searchSpace, intPtr) != null)
                {
                    intPtr += 1;
                    continue;
                }
                goto IL_0262;
            }
        }
        return false;
        IL_0262:
        return true;
    }
    

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static Vector<byte> LoadVector(ref byte start, IntPtr offset)
    {
        return Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref start, offset));
    }
    
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static unsafe IntPtr UnalignedCountVector(ref byte searchSpace)
    {
        int num = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
        return (IntPtr)((Vector<byte>.Count - num) & (Vector<byte>.Count - 1));
    }

    
    // ReSharper disable once MemberCanBePrivate.Global
    public static int BinarySearch<T, TComparable>(ref T spanStart, int length, TComparable comparable)
        where TComparable : IComparable<T>
    {
        int num = 0;
        int num2 = length - 1;
        while (num <= num2)
        {
            int num3 = (num2 + num) >>> 1;
            int num4 = comparable.CompareTo(Unsafe.Add(ref spanStart, num3));
            if (num4 == 0)
            {
                return num3;
            }

            if (num4 > 0)
            {
                num = num3 + 1;
            }
            else
            {
                num2 = num3 - 1;
            }
        }

        return ~num;
    }

    public static int IndexOf(ref byte searchSpace, int searchSpaceLength, ref byte value, int valueLength)
    {
        if (valueLength == 0)
        {
            return 0;
        }

        byte value2 = value;
        ref byte second = ref Unsafe.Add(ref value, 1);
        int num = valueLength - 1;
        int num2 = 0;
        while (true)
        {
            int num3 = searchSpaceLength - num2 - num;
            if (num3 <= 0)
            {
                break;
            }

            int num4 = IndexOf(ref Unsafe.Add(ref searchSpace, num2), value2, num3);
            if (num4 == -1)
            {
                break;
            }

            num2 += num4;
            if (SequenceEqual(ref Unsafe.Add(ref searchSpace, num2 + 1), ref second, num))
            {
                return num2;
            }

            num2++;
        }

        return -1;
    }

    public static int IndexOfAny(ref byte searchSpace, int searchSpaceLength, ref byte value, int valueLength)
    {
        if (valueLength == 0)
        {
            return 0;
        }

        int num = -1;
        for (int i = 0; i < valueLength; i++)
        {
            int num2 = IndexOf(ref searchSpace, Unsafe.Add(ref value, i), searchSpaceLength);
            if ((uint)num2 < (uint)num)
            {
                num = num2;
                searchSpaceLength = num2;
                if (num == 0)
                {
                    break;
                }
            }
        }

        return num;
    }

    public static int LastIndexOfAny(ref byte searchSpace, int searchSpaceLength, ref byte value, int valueLength)
    {
        if (valueLength == 0)
        {
            return 0;
        }

        int num = -1;
        for (int i = 0; i < valueLength; i++)
        {
            int num2 = LastIndexOf(ref searchSpace, Unsafe.Add(ref value, i), searchSpaceLength);
            if (num2 > num)
            {
                num = num2;
            }
        }

        return num;
    }

    public static unsafe int IndexOf(ref byte searchSpace, byte value, int length)
    {
        IntPtr intPtr = (IntPtr)0;
        IntPtr intPtr2 = (IntPtr)length;
        if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        {
            int num = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            intPtr2 = (IntPtr)((Vector<byte>.Count - num) & (Vector<byte>.Count - 1));
        }

        while (true)
        {
            if ((ulong)(void*)intPtr2 >= 8uL)
            {
                intPtr2 -= 8;
                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr))
                {
                    goto IL_0242;
                }

                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 1))
                {
                    goto IL_024a;
                }

                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 2))
                {
                    goto IL_0258;
                }

                if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 3))
                {
                    if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 4))
                    {
                        if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 5))
                        {
                            if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 6))
                            {
                                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 7))
                                {
                                    break;
                                }

                                intPtr += 8;
                                continue;
                            }

                            return (int)(void*)(intPtr + 6);
                        }

                        return (int)(void*)(intPtr + 5);
                    }

                    return (int)(void*)(intPtr + 4);
                }

                goto IL_0266;
            }

            if ((ulong)(void*)intPtr2 >= 4uL)
            {
                intPtr2 -= 4;
                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr))
                {
                    goto IL_0242;
                }

                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 1))
                {
                    goto IL_024a;
                }

                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 2))
                {
                    goto IL_0258;
                }

                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 3))
                {
                    goto IL_0266;
                }

                intPtr += 4;
            }

            while ((void*)intPtr2 != null)
            {
                intPtr2 -= 1;
                if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr))
                {
                    intPtr += 1;
                    continue;
                }

                goto IL_0242;
            }

            if (Vector.IsHardwareAccelerated && (int)(void*)intPtr < length)
            {
                intPtr2 = (IntPtr)((length - (int)(void*)intPtr) & ~(Vector<byte>.Count - 1));
                Vector<byte> vector = GetVector(value);
                for (; (void*)intPtr2 > (void*)intPtr; intPtr += Vector<byte>.Count)
                {
                    Vector<byte> vector2 = Vector.Equals(vector,
                        Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref searchSpace, intPtr)));
                    if (!Vector<byte>.Zero.Equals(vector2))
                    {
                        return (int)(void*)intPtr + LocateFirstFoundByte(vector2);
                    }
                }

                if ((int)(void*)intPtr < length)
                {
                    intPtr2 = (IntPtr)(length - (int)(void*)intPtr);
                    continue;
                }
            }

            return -1;
            IL_0266:
            return (int)(void*)(intPtr + 3);
            IL_0242:
            return (int)(void*)intPtr;
            IL_0258:
            return (int)(void*)(intPtr + 2);
            IL_024a:
            return (int)(void*)(intPtr + 1);
        }

        return (int)(void*)(intPtr + 7);
    }

    public static int LastIndexOf(ref byte searchSpace, int searchSpaceLength, ref byte value, int valueLength)
    {
        if (valueLength == 0)
        {
            return 0;
        }

        byte value2 = value;
        ref byte second = ref Unsafe.Add(ref value, 1);
        int num = valueLength - 1;
        int num2 = 0;
        while (true)
        {
            int num3 = searchSpaceLength - num2 - num;
            if (num3 <= 0)
            {
                break;
            }

            int num4 = LastIndexOf(ref searchSpace, value2, num3);
            if (num4 == -1)
            {
                break;
            }

            if (SequenceEqual(ref Unsafe.Add(ref searchSpace, num4 + 1), ref second, num))
            {
                return num4;
            }

            num2 += num3 - num4;
        }

        return -1;
    }

    public static unsafe int LastIndexOf(ref byte searchSpace, byte value, int length)
    {
        IntPtr intPtr = (IntPtr)length;
        IntPtr intPtr2 = (IntPtr)length;
        if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        {
            int num = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            intPtr2 = (IntPtr)(((length & (Vector<byte>.Count - 1)) + num) & (Vector<byte>.Count - 1));
        }

        while (true)
        {
            if ((ulong)(void*)intPtr2 >= 8uL)
            {
                intPtr2 -= 8;
                intPtr -= 8;
                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 7))
                {
                    break;
                }

                if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 6))
                {
                    if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 5))
                    {
                        if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 4))
                        {
                            if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 3))
                            {
                                if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 2))
                                {
                                    if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr + 1))
                                    {
                                        if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr))
                                        {
                                            continue;
                                        }

                                        goto IL_0254;
                                    }

                                    goto IL_025c;
                                }

                                goto IL_026a;
                            }

                            goto IL_0278;
                        }

                        return (int)(void*)(intPtr + 4);
                    }

                    return (int)(void*)(intPtr + 5);
                }

                return (int)(void*)(intPtr + 6);
            }

            if ((ulong)(void*)intPtr2 >= 4uL)
            {
                intPtr2 -= 4;
                intPtr -= 4;
                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 3))
                {
                    goto IL_0278;
                }

                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 2))
                {
                    goto IL_026a;
                }

                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr + 1))
                {
                    goto IL_025c;
                }

                if (value == Unsafe.AddByteOffset(ref searchSpace, intPtr))
                {
                    goto IL_0254;
                }
            }

            while ((void*)intPtr2 != null)
            {
                intPtr2 -= 1;
                intPtr -= 1;
                if (value != Unsafe.AddByteOffset(ref searchSpace, intPtr))
                {
                    continue;
                }

                goto IL_0254;
            }

            if (Vector.IsHardwareAccelerated && (void*)intPtr != null)
            {
                intPtr2 = (IntPtr)((int)(void*)intPtr & ~(Vector<byte>.Count - 1));
                Vector<byte> vector = GetVector(value);
                for (; (ulong)(void*)intPtr2 > (ulong)(Vector<byte>.Count - 1); intPtr2 -= Vector<byte>.Count)
                {
                    Vector<byte> vector2 = Vector.Equals(vector,
                        Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref searchSpace,
                            intPtr - Vector<byte>.Count)));
                    if (Vector<byte>.Zero.Equals(vector2))
                    {
                        intPtr -= Vector<byte>.Count;
                        continue;
                    }

                    return (int)intPtr - Vector<byte>.Count + LocateLastFoundByte(vector2);
                }

                if ((void*)intPtr != null)
                {
                    intPtr2 = intPtr;
                    continue;
                }
            }

            return -1;
            IL_0254:
            return (int)(void*)intPtr;
            IL_026a:
            return (int)(void*)(intPtr + 2);
            IL_0278:
            return (int)(void*)(intPtr + 3);
            IL_025c:
            return (int)(void*)(intPtr + 1);
        }

        return (int)(void*)(intPtr + 7);
    }

    public static unsafe int IndexOfAny(ref byte searchSpace, byte value0, byte value1, int length)
    {
        IntPtr intPtr = (IntPtr)0;
        IntPtr intPtr2 = (IntPtr)length;
        if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        {
            int num = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            intPtr2 = (IntPtr)((Vector<byte>.Count - num) & (Vector<byte>.Count - 1));
        }

        while (true)
        {
            if ((ulong)(void*)intPtr2 >= 8uL)
            {
                intPtr2 -= 8;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_02ff;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 1);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_0307;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 2);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_0315;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 3);
                if (value0 != num2 && value1 != num2)
                {
                    num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 4);
                    if (value0 != num2 && value1 != num2)
                    {
                        num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 5);
                        if (value0 != num2 && value1 != num2)
                        {
                            num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 6);
                            if (value0 != num2 && value1 != num2)
                            {
                                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 7);
                                if (value0 == num2 || value1 == num2)
                                {
                                    break;
                                }

                                intPtr += 8;
                                continue;
                            }

                            return (int)(void*)(intPtr + 6);
                        }

                        return (int)(void*)(intPtr + 5);
                    }

                    return (int)(void*)(intPtr + 4);
                }

                goto IL_0323;
            }

            if ((ulong)(void*)intPtr2 >= 4uL)
            {
                intPtr2 -= 4;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_02ff;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 1);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_0307;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 2);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_0315;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 3);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_0323;
                }

                intPtr += 4;
            }

            while ((void*)intPtr2 != null)
            {
                intPtr2 -= 1;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 != num2 && value1 != num2)
                {
                    intPtr += 1;
                    continue;
                }

                goto IL_02ff;
            }

            if (Vector.IsHardwareAccelerated && (int)(void*)intPtr < length)
            {
                intPtr2 = (IntPtr)((length - (int)(void*)intPtr) & ~(Vector<byte>.Count - 1));
                Vector<byte> vector = GetVector(value0);
                Vector<byte> vector2 = GetVector(value1);
                for (; (void*)intPtr2 > (void*)intPtr; intPtr += Vector<byte>.Count)
                {
                    Vector<byte> left =
                        Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref searchSpace, intPtr));
                    Vector<byte> vector3 = Vector.BitwiseOr(Vector.Equals(left, vector), Vector.Equals(left, vector2));
                    if (!Vector<byte>.Zero.Equals(vector3))
                    {
                        return (int)(void*)intPtr + LocateFirstFoundByte(vector3);
                    }
                }

                if ((int)(void*)intPtr < length)
                {
                    intPtr2 = (IntPtr)(length - (int)(void*)intPtr);
                    continue;
                }
            }

            return -1;
            IL_02ff:
            return (int)(void*)intPtr;
            IL_0315:
            return (int)(void*)(intPtr + 2);
            IL_0307:
            return (int)(void*)(intPtr + 1);
            IL_0323:
            return (int)(void*)(intPtr + 3);
        }

        return (int)(void*)(intPtr + 7);
    }

    public static unsafe int IndexOfAny(ref byte searchSpace, byte value0, byte value1, byte value2, int length)
    {
        IntPtr intPtr = (IntPtr)0;
        IntPtr intPtr2 = (IntPtr)length;
        if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        {
            int num = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            intPtr2 = (IntPtr)((Vector<byte>.Count - num) & (Vector<byte>.Count - 1));
        }

        while (true)
        {
            if ((ulong)(void*)intPtr2 >= 8uL)
            {
                intPtr2 -= 8;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_0393;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 1);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_039b;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 2);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_03a9;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 3);
                if (value0 != num2 && value1 != num2 && value2 != num2)
                {
                    num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 4);
                    if (value0 != num2 && value1 != num2 && value2 != num2)
                    {
                        num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 5);
                        if (value0 != num2 && value1 != num2 && value2 != num2)
                        {
                            num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 6);
                            if (value0 != num2 && value1 != num2 && value2 != num2)
                            {
                                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 7);
                                if (value0 == num2 || value1 == num2 || value2 == num2)
                                {
                                    break;
                                }

                                intPtr += 8;
                                continue;
                            }

                            return (int)(void*)(intPtr + 6);
                        }

                        return (int)(void*)(intPtr + 5);
                    }

                    return (int)(void*)(intPtr + 4);
                }

                goto IL_03b7;
            }

            if ((ulong)(void*)intPtr2 >= 4uL)
            {
                intPtr2 -= 4;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_0393;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 1);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_039b;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 2);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_03a9;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 3);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_03b7;
                }

                intPtr += 4;
            }

            while ((void*)intPtr2 != null)
            {
                intPtr2 -= 1;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 != num2 && value1 != num2 && value2 != num2)
                {
                    intPtr += 1;
                    continue;
                }

                goto IL_0393;
            }

            if (Vector.IsHardwareAccelerated && (int)(void*)intPtr < length)
            {
                intPtr2 = (IntPtr)((length - (int)(void*)intPtr) & ~(Vector<byte>.Count - 1));
                Vector<byte> vector = GetVector(value0);
                Vector<byte> vector2 = GetVector(value1);
                Vector<byte> vector3 = GetVector(value2);
                for (; (void*)intPtr2 > (void*)intPtr; intPtr += Vector<byte>.Count)
                {
                    Vector<byte> left =
                        Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref searchSpace, intPtr));
                    Vector<byte> vector4 =
                        Vector.BitwiseOr(Vector.BitwiseOr(Vector.Equals(left, vector), Vector.Equals(left, vector2)),
                            Vector.Equals(left, vector3));
                    if (!Vector<byte>.Zero.Equals(vector4))
                    {
                        return (int)(void*)intPtr + LocateFirstFoundByte(vector4);
                    }
                }

                if ((int)(void*)intPtr < length)
                {
                    intPtr2 = (IntPtr)(length - (int)(void*)intPtr);
                    continue;
                }
            }

            return -1;
            IL_0393:
            return (int)(void*)intPtr;
            IL_039b:
            return (int)(void*)(intPtr + 1);
            IL_03b7:
            return (int)(void*)(intPtr + 3);
            IL_03a9:
            return (int)(void*)(intPtr + 2);
        }

        return (int)(void*)(intPtr + 7);
    }

    public static unsafe int LastIndexOfAny(ref byte searchSpace, byte value0, byte value1, int length)
    {
        IntPtr intPtr = (IntPtr)length;
        IntPtr intPtr2 = (IntPtr)length;
        if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        {
            int num = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            intPtr2 = (IntPtr)(((length & (Vector<byte>.Count - 1)) + num) & (Vector<byte>.Count - 1));
        }

        while (true)
        {
            if ((ulong)(void*)intPtr2 >= 8uL)
            {
                intPtr2 -= 8;
                intPtr -= 8;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 7);
                if (value0 == num2 || value1 == num2)
                {
                    break;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 6);
                if (value0 != num2 && value1 != num2)
                {
                    num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 5);
                    if (value0 != num2 && value1 != num2)
                    {
                        num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 4);
                        if (value0 != num2 && value1 != num2)
                        {
                            num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 3);
                            if (value0 != num2 && value1 != num2)
                            {
                                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 2);
                                if (value0 != num2 && value1 != num2)
                                {
                                    num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 1);
                                    if (value0 != num2 && value1 != num2)
                                    {
                                        num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                                        if (value0 != num2 && value1 != num2)
                                        {
                                            continue;
                                        }

                                        goto IL_0314;
                                    }

                                    goto IL_031c;
                                }

                                goto IL_032a;
                            }

                            goto IL_0338;
                        }

                        return (int)(void*)(intPtr + 4);
                    }

                    return (int)(void*)(intPtr + 5);
                }

                return (int)(void*)(intPtr + 6);
            }

            if ((ulong)(void*)intPtr2 >= 4uL)
            {
                intPtr2 -= 4;
                intPtr -= 4;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 3);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_0338;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 2);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_032a;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 1);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_031c;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 == num2 || value1 == num2)
                {
                    goto IL_0314;
                }
            }

            while ((void*)intPtr2 != null)
            {
                intPtr2 -= 1;
                intPtr -= 1;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 != num2 && value1 != num2)
                {
                    continue;
                }

                goto IL_0314;
            }

            if (Vector.IsHardwareAccelerated && (void*)intPtr != null)
            {
                intPtr2 = (IntPtr)((int)(void*)intPtr & ~(Vector<byte>.Count - 1));
                Vector<byte> vector = GetVector(value0);
                Vector<byte> vector2 = GetVector(value1);
                for (; (ulong)(void*)intPtr2 > (ulong)(Vector<byte>.Count - 1); intPtr2 -= Vector<byte>.Count)
                {
                    Vector<byte> left =
                        Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref searchSpace,
                            intPtr - Vector<byte>.Count));
                    Vector<byte> vector3 = Vector.BitwiseOr(Vector.Equals(left, vector), Vector.Equals(left, vector2));
                    if (Vector<byte>.Zero.Equals(vector3))
                    {
                        intPtr -= Vector<byte>.Count;
                        continue;
                    }

                    return (int)intPtr - Vector<byte>.Count + LocateLastFoundByte(vector3);
                }

                if ((void*)intPtr != null)
                {
                    intPtr2 = intPtr;
                    continue;
                }
            }

            return -1;
            IL_0314:
            return (int)(void*)intPtr;
            IL_0338:
            return (int)(void*)(intPtr + 3);
            IL_031c:
            return (int)(void*)(intPtr + 1);
            IL_032a:
            return (int)(void*)(intPtr + 2);
        }

        return (int)(void*)(intPtr + 7);
    }

    public static unsafe int LastIndexOfAny(ref byte searchSpace, byte value0, byte value1, byte value2, int length)
    {
        IntPtr intPtr = (IntPtr)length;
        IntPtr intPtr2 = (IntPtr)length;
        if (Vector.IsHardwareAccelerated && length >= Vector<byte>.Count * 2)
        {
            int num = (int)Unsafe.AsPointer(ref searchSpace) & (Vector<byte>.Count - 1);
            intPtr2 = (IntPtr)(((length & (Vector<byte>.Count - 1)) + num) & (Vector<byte>.Count - 1));
        }

        while (true)
        {
            if ((ulong)(void*)intPtr2 >= 8uL)
            {
                intPtr2 -= 8;
                intPtr -= 8;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 7);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    break;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 6);
                if (value0 != num2 && value1 != num2 && value2 != num2)
                {
                    num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 5);
                    if (value0 != num2 && value1 != num2 && value2 != num2)
                    {
                        num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 4);
                        if (value0 != num2 && value1 != num2 && value2 != num2)
                        {
                            num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 3);
                            if (value0 != num2 && value1 != num2 && value2 != num2)
                            {
                                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 2);
                                if (value0 != num2 && value1 != num2 && value2 != num2)
                                {
                                    num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 1);
                                    if (value0 != num2 && value1 != num2 && value2 != num2)
                                    {
                                        num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                                        if (value0 != num2 && value1 != num2 && value2 != num2)
                                        {
                                            continue;
                                        }

                                        goto IL_03ab;
                                    }

                                    goto IL_03b3;
                                }

                                goto IL_03c1;
                            }

                            goto IL_03cf;
                        }

                        return (int)(void*)(intPtr + 4);
                    }

                    return (int)(void*)(intPtr + 5);
                }

                return (int)(void*)(intPtr + 6);
            }

            if ((ulong)(void*)intPtr2 >= 4uL)
            {
                intPtr2 -= 4;
                intPtr -= 4;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 3);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_03cf;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 2);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_03c1;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr + 1);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_03b3;
                }

                num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 == num2 || value1 == num2 || value2 == num2)
                {
                    goto IL_03ab;
                }
            }

            while ((void*)intPtr2 != null)
            {
                intPtr2 -= 1;
                intPtr -= 1;
                uint num2 = Unsafe.AddByteOffset(ref searchSpace, intPtr);
                if (value0 != num2 && value1 != num2 && value2 != num2)
                {
                    continue;
                }

                goto IL_03ab;
            }

            if (Vector.IsHardwareAccelerated && (void*)intPtr != null)
            {
                intPtr2 = (IntPtr)((int)(void*)intPtr & ~(Vector<byte>.Count - 1));
                Vector<byte> vector = GetVector(value0);
                Vector<byte> vector2 = GetVector(value1);
                Vector<byte> vector3 = GetVector(value2);
                for (; (ulong)(void*)intPtr2 > (ulong)(Vector<byte>.Count - 1); intPtr2 -= Vector<byte>.Count)
                {
                    Vector<byte> left =
                        Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref searchSpace,
                            intPtr - Vector<byte>.Count));
                    Vector<byte> vector4 =
                        Vector.BitwiseOr(Vector.BitwiseOr(Vector.Equals(left, vector), Vector.Equals(left, vector2)),
                            Vector.Equals(left, vector3));
                    if (Vector<byte>.Zero.Equals(vector4))
                    {
                        intPtr -= Vector<byte>.Count;
                        continue;
                    }

                    return (int)intPtr - Vector<byte>.Count + LocateLastFoundByte(vector4);
                }

                if ((void*)intPtr != null)
                {
                    intPtr2 = intPtr;
                    continue;
                }
            }

            return -1;
            IL_03ab:
            return (int)(void*)intPtr;
            IL_03cf:
            return (int)(void*)(intPtr + 3);
            IL_03c1:
            return (int)(void*)(intPtr + 2);
            IL_03b3:
            return (int)(void*)(intPtr + 1);
        }

        return (int)(void*)(intPtr + 7);
    }

    public static unsafe bool SequenceEqual(ref byte first, ref byte second, NUInt length)
    {
        if (Unsafe.AreSame(ref first, ref second))
        {
            goto IL_013d;
        }

        IntPtr intPtr = (IntPtr)0;
        IntPtr intPtr2 = (IntPtr)(void*)length;
        if (Vector.IsHardwareAccelerated && (ulong)(void*)intPtr2 >= (ulong)Vector<byte>.Count)
        {
            intPtr2 -= Vector<byte>.Count;
            while (true)
            {
                if ((void*)intPtr2 > (void*)intPtr)
                {
                    if (Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref first, intPtr)) !=
                        Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref second, intPtr)))
                    {
                        break;
                    }

                    intPtr += Vector<byte>.Count;
                    continue;
                }

                return Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref first, intPtr2)) ==
                       Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref second, intPtr2));
            }
        }
        else
        {
            if ((ulong)(void*)intPtr2 < (ulong)sizeof(UIntPtr))
            {
                while ((void*)intPtr2 > (void*)intPtr)
                {
                    if (Unsafe.AddByteOffset(ref first, intPtr) == Unsafe.AddByteOffset(ref second, intPtr))
                    {
                        intPtr += 1;
                        continue;
                    }

                    goto IL_013f;
                }

                goto IL_013d;
            }

            intPtr2 -= sizeof(UIntPtr);
            while (true)
            {
                if ((void*)intPtr2 > (void*)intPtr)
                {
                    if (Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset(ref first, intPtr)) !=
                        Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset(ref second, intPtr)))
                    {
                        break;
                    }

                    intPtr += sizeof(UIntPtr);
                    continue;
                }

                return Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset(ref first, intPtr2)) ==
                       Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset(ref second, intPtr2));
            }
        }

        IL_013f:
        return false;
        IL_013d:
        return true;
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static int LocateFirstFoundByte(Vector<byte> match)
    {
        Vector<ulong> vector = Vector.AsVectorUInt64(match);
        ulong num = 0uL;
        int i;
        for (i = 0; i < Vector<ulong>.Count; i++)
        {
            num = vector[i];
            if (num != 0L)
            {
                break;
            }
        }

        return i * 8 + LocateFirstFoundByte(num);
    }

    public static unsafe int SequenceCompareTo(ref byte first, int firstLength, ref byte second, int secondLength)
    {
        if (!Unsafe.AreSame(ref first, ref second))
        {
            IntPtr intPtr = (IntPtr)((firstLength < secondLength) ? firstLength : secondLength);
            IntPtr intPtr2 = (IntPtr)0;
            IntPtr intPtr3 = (IntPtr)(void*)intPtr;
            if (Vector.IsHardwareAccelerated && (ulong)(void*)intPtr3 > (ulong)Vector<byte>.Count)
            {
                intPtr3 -= Vector<byte>.Count;
                for (;
                     (void*)intPtr3 > (void*)intPtr2 &&
                     !(Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref first, intPtr2)) !=
                       Unsafe.ReadUnaligned<Vector<byte>>(ref Unsafe.AddByteOffset(ref second, intPtr2)));
                     intPtr2 += Vector<byte>.Count)
                {
                }
            }
            else if ((ulong)(void*)intPtr3 > (ulong)sizeof(UIntPtr))
            {
                intPtr3 -= sizeof(UIntPtr);
                for (;
                     (void*)intPtr3 > (void*)intPtr2 &&
                     !(Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset(ref first, intPtr2)) !=
                       Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.AddByteOffset(ref second, intPtr2)));
                     intPtr2 += sizeof(UIntPtr))
                {
                }
            }

            for (; (void*)intPtr > (void*)intPtr2; intPtr2 += 1)
            {
                int num = Unsafe.AddByteOffset(ref first, intPtr2).CompareTo(Unsafe.AddByteOffset(ref second, intPtr2));
                if (num != 0)
                {
                    return num;
                }
            }
        }

        return firstLength - secondLength;
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static int LocateLastFoundByte(Vector<byte> match)
    {
        Vector<ulong> vector = Vector.AsVectorUInt64(match);
        ulong num = 0uL;
        int num2;
        for (num2 = Vector<ulong>.Count - 1; num2 >= 0; num2--)
        {
            num = vector[num2];
            if (num != 0L)
            {
                break;
            }
        }

        return num2 * 8 + LocateLastFoundByte(num);
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static int LocateFirstFoundByte(ulong match)
    {
        ulong num = match ^ (match - 1);
        return (int)(num * 283686952306184L >> 57);
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static int LocateLastFoundByte(ulong match)
    {
        int num = 7;
        while ((long)match > 0L)
        {
            match <<= 8;
            num--;
        }

        return num;
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static Vector<byte> GetVector(byte vectorByte)
    {
        return Vector.AsVectorByte(new Vector<uint>((uint)(vectorByte * 16843009)));
    }

    public static unsafe int SequenceCompareTo(ref char first, int firstLength, ref char second, int secondLength)
    {
        int result = firstLength - secondLength;
        if (!Unsafe.AreSame(ref first, ref second))
        {
            IntPtr intPtr = (IntPtr)((firstLength < secondLength) ? firstLength : secondLength);
            IntPtr intPtr2 = (IntPtr)0;
            if ((ulong)(void*)intPtr >= (ulong)(sizeof(UIntPtr) / 2))
            {
                if (Vector.IsHardwareAccelerated && (ulong)(void*)intPtr >= (ulong)Vector<ushort>.Count)
                {
                    IntPtr intPtr3 = intPtr - Vector<ushort>.Count;
                    while (!(Unsafe.ReadUnaligned<Vector<ushort>>(
                                 ref Unsafe.As<char, byte>(ref Unsafe.Add(ref first, intPtr2))) !=
                             Unsafe.ReadUnaligned<Vector<ushort>>(
                                 ref Unsafe.As<char, byte>(ref Unsafe.Add(ref second, intPtr2)))))
                    {
                        intPtr2 += Vector<ushort>.Count;
                        if ((void*)intPtr3 < (void*)intPtr2)
                        {
                            break;
                        }
                    }
                }

                for (;
                     (void*)intPtr >= (void*)(intPtr2 + sizeof(UIntPtr) / 2) &&
                     !(Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref first, intPtr2))) !=
                       Unsafe.ReadUnaligned<UIntPtr>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref second, intPtr2))));
                     intPtr2 += sizeof(UIntPtr) / 2)
                {
                }
            }

            if (sizeof(UIntPtr) > 4 && (void*)intPtr >= (void*)(intPtr2 + 2) &&
                Unsafe.ReadUnaligned<int>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref first, intPtr2))) ==
                Unsafe.ReadUnaligned<int>(ref Unsafe.As<char, byte>(ref Unsafe.Add(ref second, intPtr2))))
            {
                intPtr2 += 2;
            }

            for (; (void*)intPtr2 < (void*)intPtr; intPtr2 += 1)
            {
                int num = Unsafe.Add(ref first, intPtr2).CompareTo(Unsafe.Add(ref second, intPtr2));
                if (num != 0)
                {
                    return num;
                }
            }
        }

        return result;
    }

    public static unsafe int IndexOf(ref char searchSpace, char value, int length)
    {
        fixed (char* ptr = &searchSpace)
        {
            char* ptr2 = ptr;
            char* ptr3 = ptr2 + length;
            if (Vector.IsHardwareAccelerated && length >= Vector<ushort>.Count * 2)
            {
                int num = ((int)ptr2 & (Unsafe.SizeOf<Vector<ushort>>() - 1)) / 2;
                length = (Vector<ushort>.Count - num) & (Vector<ushort>.Count - 1);
            }

            while (true)
            {
                if (length >= 4)
                {
                    length -= 4;
                    if (*ptr2 == value)
                    {
                        break;
                    }

                    if (ptr2[1] != value)
                    {
                        if (ptr2[2] != value)
                        {
                            if (ptr2[3] != value)
                            {
                                ptr2 += 4;
                                continue;
                            }

                            ptr2++;
                        }

                        ptr2++;
                    }

                    ptr2++;
                    break;
                }

                while (length > 0)
                {
                    length--;
                    if (*ptr2 == value)
                    {
                        goto end_IL_0079;
                    }

                    ptr2++;
                }

                if (Vector.IsHardwareAccelerated && ptr2 < ptr3)
                {
                    length = (int)((ptr3 - ptr2) & ~(Vector<ushort>.Count - 1));
                    Vector<ushort> left = new Vector<ushort>(value);
                    while (length > 0)
                    {
                        Vector<ushort> vector = Vector.Equals(left, Unsafe.Read<Vector<ushort>>(ptr2));
                        if (Vector<ushort>.Zero.Equals(vector))
                        {
                            ptr2 += Vector<ushort>.Count;
                            length -= Vector<ushort>.Count;
                            continue;
                        }

                        return (int)(ptr2 - ptr) + LocateFirstFoundChar(vector);
                    }

                    if (ptr2 < ptr3)
                    {
                        length = (int)(ptr3 - ptr2);
                        continue;
                    }
                }

                return -1;
                end_IL_0079:
                break;
            }

            return (int)(ptr2 - ptr);
        }
    }

    public static unsafe int LastIndexOf(ref char searchSpace, char value, int length)
    {
        fixed (char* ptr = &searchSpace)
        {
            char* ptr2 = ptr + length;
            char* ptr3 = ptr;
            if (Vector.IsHardwareAccelerated && length >= Vector<ushort>.Count * 2)
            {
                length = ((int)ptr2 & (Unsafe.SizeOf<Vector<ushort>>() - 1)) / 2;
            }

            while (true)
            {
                if (length >= 4)
                {
                    length -= 4;
                    ptr2 -= 4;
                    if (ptr2[3] == value)
                    {
                        break;
                    }

                    if (ptr2[2] != value)
                    {
                        if (ptr2[1] != value)
                        {
                            if (*ptr2 != value)
                            {
                                continue;
                            }

                            goto IL_011d;
                        }

                        return (int)(ptr2 - ptr3) + 1;
                    }

                    return (int)(ptr2 - ptr3) + 2;
                }

                while (length > 0)
                {
                    length--;
                    ptr2--;
                    if (*ptr2 != value)
                    {
                        continue;
                    }

                    goto IL_011d;
                }

                if (Vector.IsHardwareAccelerated && ptr2 > ptr3)
                {
                    length = (int)((ptr2 - ptr3) & ~(Vector<ushort>.Count - 1));
                    Vector<ushort> left = new Vector<ushort>(value);
                    while (length > 0)
                    {
                        char* ptr4 = ptr2 - Vector<ushort>.Count;
                        Vector<ushort> vector = Vector.Equals(left, Unsafe.Read<Vector<ushort>>(ptr4));
                        if (Vector<ushort>.Zero.Equals(vector))
                        {
                            ptr2 -= Vector<ushort>.Count;
                            length -= Vector<ushort>.Count;
                            continue;
                        }

                        return (int)(ptr4 - ptr3) + LocateLastFoundChar(vector);
                    }

                    if (ptr2 > ptr3)
                    {
                        length = (int)(ptr2 - ptr3);
                        continue;
                    }
                }

                return -1;
                IL_011d:
                return (int)(ptr2 - ptr3);
            }

            return (int)(ptr2 - ptr3) + 3;
        }
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static int LocateFirstFoundChar(Vector<ushort> match)
    {
        Vector<ulong> vector = Vector.AsVectorUInt64(match);
        ulong num = 0uL;
        int i;
        for (i = 0; i < Vector<ulong>.Count; i++)
        {
            num = vector[i];
            if (num != 0L)
            {
                break;
            }
        }

        return i * 4 + LocateFirstFoundChar(num);
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static int LocateFirstFoundChar(ulong match)
    {
        ulong num = match ^ (match - 1);
        return (int)(num * 4295098372L >> 49);
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static int LocateLastFoundChar(Vector<ushort> match)
    {
        Vector<ulong> vector = Vector.AsVectorUInt64(match);
        ulong num = 0uL;
        int num2;
        for (num2 = Vector<ulong>.Count - 1; num2 >= 0; num2--)
        {
            num = vector[num2];
            if (num != 0L)
            {
                break;
            }
        }

        return num2 * 4 + LocateLastFoundChar(num);
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static int LocateLastFoundChar(ulong match)
    {
        int num = 3;
        while ((long)match > 0L)
        {
            match <<= 16;
            num--;
        }

        return num;
    }

    public static int IndexOf<T>(ref T searchSpace, int searchSpaceLength, ref T value, int valueLength)
        where T : IEquatable<T>
    {
        if (valueLength == 0)
        {
            return 0;
        }

        T value2 = value;
        ref T second = ref Unsafe.Add(ref value, 1);
        int num = valueLength - 1;
        int num2 = 0;
        while (true)
        {
            int num3 = searchSpaceLength - num2 - num;
            if (num3 <= 0)
            {
                break;
            }

            int num4 = IndexOf(ref Unsafe.Add(ref searchSpace, num2), value2, num3);
            if (num4 == -1)
            {
                break;
            }

            num2 += num4;
            if (SequenceEqual(ref Unsafe.Add(ref searchSpace, num2 + 1), ref second, num))
            {
                return num2;
            }

            num2++;
        }

        return -1;
    }

    public static unsafe int IndexOf<T>(ref T searchSpace, T value, int length) where T : IEquatable<T>
    {
        IntPtr intPtr = (IntPtr)0;
        while (true)
        {
            if (length >= 8)
            {
                length -= 8;
                if (!value.Equals(Unsafe.Add(ref searchSpace, intPtr)))
                {
                    if (value.Equals(Unsafe.Add(ref searchSpace, intPtr + 1)))
                    {
                        goto IL_020a;
                    }

                    if (value.Equals(Unsafe.Add(ref searchSpace, intPtr + 2)))
                    {
                        goto IL_0218;
                    }

                    if (!value.Equals(Unsafe.Add(ref searchSpace, intPtr + 3)))
                    {
                        if (!value.Equals(Unsafe.Add(ref searchSpace, intPtr + 4)))
                        {
                            if (!value.Equals(Unsafe.Add(ref searchSpace, intPtr + 5)))
                            {
                                if (!value.Equals(Unsafe.Add(ref searchSpace, intPtr + 6)))
                                {
                                    if (value.Equals(Unsafe.Add(ref searchSpace, intPtr + 7)))
                                    {
                                        break;
                                    }

                                    intPtr += 8;
                                    continue;
                                }

                                return (int)(void*)(intPtr + 6);
                            }

                            return (int)(void*)(intPtr + 5);
                        }

                        return (int)(void*)(intPtr + 4);
                    }

                    goto IL_0226;
                }
            }
            else
            {
                if (length >= 4)
                {
                    length -= 4;
                    if (value.Equals(Unsafe.Add(ref searchSpace, intPtr)))
                    {
                        goto IL_0202;
                    }

                    if (value.Equals(Unsafe.Add(ref searchSpace, intPtr + 1)))
                    {
                        goto IL_020a;
                    }

                    if (value.Equals(Unsafe.Add(ref searchSpace, intPtr + 2)))
                    {
                        goto IL_0218;
                    }

                    if (value.Equals(Unsafe.Add(ref searchSpace, intPtr + 3)))
                    {
                        goto IL_0226;
                    }

                    intPtr += 4;
                }

                while (true)
                {
                    if (length > 0)
                    {
                        if (value.Equals(Unsafe.Add(ref searchSpace, intPtr)))
                        {
                            break;
                        }

                        intPtr += 1;
                        length--;
                        continue;
                    }

                    return -1;
                }
            }

            goto IL_0202;
            IL_0218:
            return (int)(void*)(intPtr + 2);
            IL_0202:
            return (int)(void*)intPtr;
            IL_020a:
            return (int)(void*)(intPtr + 1);
            IL_0226:
            return (int)(void*)(intPtr + 3);
        }

        return (int)(void*)(intPtr + 7);
    }

    public static int IndexOfAny<T>(ref T searchSpace, T value0, T value1, int length) where T : IEquatable<T>
    {
        int num = 0;
        while (true)
        {
            if (length - num >= 8)
            {
                T other = Unsafe.Add(ref searchSpace, num);
                if (!value0.Equals(other) && !value1.Equals(other))
                {
                    other = Unsafe.Add(ref searchSpace, num + 1);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02cb;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 2);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02cf;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 3);
                    if (!value0.Equals(other) && !value1.Equals(other))
                    {
                        other = Unsafe.Add(ref searchSpace, num + 4);
                        if (!value0.Equals(other) && !value1.Equals(other))
                        {
                            other = Unsafe.Add(ref searchSpace, num + 5);
                            if (!value0.Equals(other) && !value1.Equals(other))
                            {
                                other = Unsafe.Add(ref searchSpace, num + 6);
                                if (!value0.Equals(other) && !value1.Equals(other))
                                {
                                    other = Unsafe.Add(ref searchSpace, num + 7);
                                    if (value0.Equals(other) || value1.Equals(other))
                                    {
                                        break;
                                    }

                                    num += 8;
                                    continue;
                                }

                                return num + 6;
                            }

                            return num + 5;
                        }

                        return num + 4;
                    }

                    goto IL_02d3;
                }
            }
            else
            {
                if (length - num >= 4)
                {
                    T other = Unsafe.Add(ref searchSpace, num);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02c9;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 1);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02cb;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 2);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02cf;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 3);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02d3;
                    }

                    num += 4;
                }

                while (true)
                {
                    if (num < length)
                    {
                        T other = Unsafe.Add(ref searchSpace, num);
                        if (value0.Equals(other) || value1.Equals(other))
                        {
                            break;
                        }

                        num++;
                        continue;
                    }

                    return -1;
                }
            }

            goto IL_02c9;
            IL_02cf:
            return num + 2;
            IL_02cb:
            return num + 1;
            IL_02d3:
            return num + 3;
            IL_02c9:
            return num;
        }

        return num + 7;
    }

    public static int IndexOfAny<T>(ref T searchSpace, T value0, T value1, T value2, int length) where T : IEquatable<T>
    {
        int num = 0;
        while (true)
        {
            if (length - num >= 8)
            {
                T other = Unsafe.Add(ref searchSpace, num);
                if (!value0.Equals(other) && !value1.Equals(other) && !value2.Equals(other))
                {
                    other = Unsafe.Add(ref searchSpace, num + 1);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03c2;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 2);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03c6;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 3);
                    if (!value0.Equals(other) && !value1.Equals(other) && !value2.Equals(other))
                    {
                        other = Unsafe.Add(ref searchSpace, num + 4);
                        if (!value0.Equals(other) && !value1.Equals(other) && !value2.Equals(other))
                        {
                            other = Unsafe.Add(ref searchSpace, num + 5);
                            if (!value0.Equals(other) && !value1.Equals(other) && !value2.Equals(other))
                            {
                                other = Unsafe.Add(ref searchSpace, num + 6);
                                if (!value0.Equals(other) && !value1.Equals(other) && !value2.Equals(other))
                                {
                                    other = Unsafe.Add(ref searchSpace, num + 7);
                                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                                    {
                                        break;
                                    }

                                    num += 8;
                                    continue;
                                }

                                return num + 6;
                            }

                            return num + 5;
                        }

                        return num + 4;
                    }

                    goto IL_03ca;
                }
            }
            else
            {
                if (length - num >= 4)
                {
                    T other = Unsafe.Add(ref searchSpace, num);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03c0;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 1);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03c2;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 2);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03c6;
                    }

                    other = Unsafe.Add(ref searchSpace, num + 3);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03ca;
                    }

                    num += 4;
                }

                while (true)
                {
                    if (num < length)
                    {
                        T other = Unsafe.Add(ref searchSpace, num);
                        if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                        {
                            break;
                        }

                        num++;
                        continue;
                    }

                    return -1;
                }
            }

            IL_03c0:
            return num;
            IL_03c6:
            return num + 2;
            IL_03c2:
            return num + 1;
            IL_03ca:
            return num + 3;
        }

        return num + 7;
    }

    public static int IndexOfAny<T>(ref T searchSpace, int searchSpaceLength, ref T value, int valueLength)
        where T : IEquatable<T>
    {
        if (valueLength == 0)
        {
            return 0;
        }

        int num = -1;
        for (int i = 0; i < valueLength; i++)
        {
            int num2 = IndexOf(ref searchSpace, Unsafe.Add(ref value, i), searchSpaceLength);
            if ((uint)num2 < (uint)num)
            {
                num = num2;
                searchSpaceLength = num2;
                if (num == 0)
                {
                    break;
                }
            }
        }

        return num;
    }

    public static int LastIndexOf<T>(ref T searchSpace, int searchSpaceLength, ref T value, int valueLength)
        where T : IEquatable<T>
    {
        if (valueLength == 0)
        {
            return 0;
        }

        T value2 = value;
        ref T second = ref Unsafe.Add(ref value, 1);
        int num = valueLength - 1;
        int num2 = 0;
        while (true)
        {
            int num3 = searchSpaceLength - num2 - num;
            if (num3 <= 0)
            {
                break;
            }

            int num4 = LastIndexOf(ref searchSpace, value2, num3);
            if (num4 == -1)
            {
                break;
            }

            if (SequenceEqual(ref Unsafe.Add(ref searchSpace, num4 + 1), ref second, num))
            {
                return num4;
            }

            num2 += num3 - num4;
        }

        return -1;
    }

    public static int LastIndexOf<T>(ref T searchSpace, T value, int length) where T : IEquatable<T>
    {
        while (true)
        {
            if (length >= 8)
            {
                length -= 8;
                if (value.Equals(Unsafe.Add(ref searchSpace, length + 7)))
                {
                    break;
                }

                if (value.Equals(Unsafe.Add(ref searchSpace, length + 6)))
                {
                    return length + 6;
                }

                if (value.Equals(Unsafe.Add(ref searchSpace, length + 5)))
                {
                    return length + 5;
                }

                if (value.Equals(Unsafe.Add(ref searchSpace, length + 4)))
                {
                    return length + 4;
                }

                if (value.Equals(Unsafe.Add(ref searchSpace, length + 3)))
                {
                    goto IL_01c2;
                }

                if (value.Equals(Unsafe.Add(ref searchSpace, length + 2)))
                {
                    goto IL_01be;
                }

                if (value.Equals(Unsafe.Add(ref searchSpace, length + 1)))
                {
                    goto IL_01ba;
                }

                if (!value.Equals(Unsafe.Add(ref searchSpace, length)))
                {
                    continue;
                }
            }
            else
            {
                if (length >= 4)
                {
                    length -= 4;
                    if (value.Equals(Unsafe.Add(ref searchSpace, length + 3)))
                    {
                        goto IL_01c2;
                    }

                    if (value.Equals(Unsafe.Add(ref searchSpace, length + 2)))
                    {
                        goto IL_01be;
                    }

                    if (value.Equals(Unsafe.Add(ref searchSpace, length + 1)))
                    {
                        goto IL_01ba;
                    }

                    if (value.Equals(Unsafe.Add(ref searchSpace, length)))
                    {
                        goto IL_01b8;
                    }
                }

                do
                {
                    if (length > 0)
                    {
                        length--;
                        continue;
                    }

                    return -1;
                } while (!value.Equals(Unsafe.Add(ref searchSpace, length)));
            }

            goto IL_01b8;
            IL_01be:
            return length + 2;
            IL_01c2:
            return length + 3;
            IL_01ba:
            return length + 1;
            IL_01b8:
            return length;
        }

        return length + 7;
    }

    public static int LastIndexOfAny<T>(ref T searchSpace, T value0, T value1, int length) where T : IEquatable<T>
    {
        while (true)
        {
            if (length >= 8)
            {
                length -= 8;
                T other = Unsafe.Add(ref searchSpace, length + 7);
                if (value0.Equals(other) || value1.Equals(other))
                {
                    break;
                }

                other = Unsafe.Add(ref searchSpace, length + 6);
                if (value0.Equals(other) || value1.Equals(other))
                {
                    return length + 6;
                }

                other = Unsafe.Add(ref searchSpace, length + 5);
                if (value0.Equals(other) || value1.Equals(other))
                {
                    return length + 5;
                }

                other = Unsafe.Add(ref searchSpace, length + 4);
                if (value0.Equals(other) || value1.Equals(other))
                {
                    return length + 4;
                }

                other = Unsafe.Add(ref searchSpace, length + 3);
                if (value0.Equals(other) || value1.Equals(other))
                {
                    goto IL_02cd;
                }

                other = Unsafe.Add(ref searchSpace, length + 2);
                if (value0.Equals(other) || value1.Equals(other))
                {
                    goto IL_02c9;
                }

                other = Unsafe.Add(ref searchSpace, length + 1);
                if (value0.Equals(other) || value1.Equals(other))
                {
                    goto IL_02c5;
                }

                other = Unsafe.Add(ref searchSpace, length);
                if (!value0.Equals(other) && !value1.Equals(other))
                {
                    continue;
                }
            }
            else
            {
                T other;
                if (length >= 4)
                {
                    length -= 4;
                    other = Unsafe.Add(ref searchSpace, length + 3);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02cd;
                    }

                    other = Unsafe.Add(ref searchSpace, length + 2);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02c9;
                    }

                    other = Unsafe.Add(ref searchSpace, length + 1);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02c5;
                    }

                    other = Unsafe.Add(ref searchSpace, length);
                    if (value0.Equals(other) || value1.Equals(other))
                    {
                        goto IL_02c3;
                    }
                }

                do
                {
                    if (length > 0)
                    {
                        length--;
                        other = Unsafe.Add(ref searchSpace, length);
                        continue;
                    }

                    return -1;
                } while (!value0.Equals(other) && !value1.Equals(other));
            }

            goto IL_02c3;
            IL_02c9:
            return length + 2;
            IL_02c5:
            return length + 1;
            IL_02c3:
            return length;
            IL_02cd:
            return length + 3;
        }

        return length + 7;
    }

    public static int LastIndexOfAny<T>(ref T searchSpace, T value0, T value1, T value2, int length)
        where T : IEquatable<T>
    {
        while (true)
        {
            if (length >= 8)
            {
                length -= 8;
                T other = Unsafe.Add(ref searchSpace, length + 7);
                if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                {
                    break;
                }

                other = Unsafe.Add(ref searchSpace, length + 6);
                if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                {
                    return length + 6;
                }

                other = Unsafe.Add(ref searchSpace, length + 5);
                if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                {
                    return length + 5;
                }

                other = Unsafe.Add(ref searchSpace, length + 4);
                if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                {
                    return length + 4;
                }

                other = Unsafe.Add(ref searchSpace, length + 3);
                if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                {
                    goto IL_03da;
                }

                other = Unsafe.Add(ref searchSpace, length + 2);
                if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                {
                    goto IL_03d5;
                }

                other = Unsafe.Add(ref searchSpace, length + 1);
                if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                {
                    goto IL_03d0;
                }

                other = Unsafe.Add(ref searchSpace, length);
                if (!value0.Equals(other) && !value1.Equals(other) && !value2.Equals(other))
                {
                    continue;
                }
            }
            else
            {
                T other;
                if (length >= 4)
                {
                    length -= 4;
                    other = Unsafe.Add(ref searchSpace, length + 3);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03da;
                    }

                    other = Unsafe.Add(ref searchSpace, length + 2);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03d5;
                    }

                    other = Unsafe.Add(ref searchSpace, length + 1);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03d0;
                    }

                    other = Unsafe.Add(ref searchSpace, length);
                    if (value0.Equals(other) || value1.Equals(other) || value2.Equals(other))
                    {
                        goto IL_03cd;
                    }
                }

                do
                {
                    if (length > 0)
                    {
                        length--;
                        other = Unsafe.Add(ref searchSpace, length);
                        continue;
                    }

                    return -1;
                } while (!value0.Equals(other) && !value1.Equals(other) && !value2.Equals(other));
            }

            goto IL_03cd;
            IL_03d0:
            return length + 1;
            IL_03d5:
            return length + 2;
            IL_03da:
            return length + 3;
            IL_03cd:
            return length;
        }

        return length + 7;
    }

    public static int LastIndexOfAny<T>(ref T searchSpace, int searchSpaceLength, ref T value, int valueLength)
        where T : IEquatable<T>
    {
        if (valueLength == 0)
        {
            return 0;
        }

        int num = -1;
        for (int i = 0; i < valueLength; i++)
        {
            int num2 = LastIndexOf(ref searchSpace, Unsafe.Add(ref value, i), searchSpaceLength);
            if (num2 > num)
            {
                num = num2;
            }
        }

        return num;
    }

    public static bool SequenceEqual<T>(ref T first, ref T second, int length) where T : IEquatable<T>
    {
        if (!Unsafe.AreSame(ref first, ref second))
        {
            IntPtr intPtr = (IntPtr)0;
            while (true)
            {
                if (length >= 8)
                {
                    length -= 8;
                    if (Unsafe.Add(ref first, intPtr).Equals(Unsafe.Add(ref second, intPtr)) &&
                        Unsafe.Add(ref first, intPtr + 1).Equals(Unsafe.Add(ref second, intPtr + 1)) &&
                        Unsafe.Add(ref first, intPtr + 2).Equals(Unsafe.Add(ref second, intPtr + 2)) &&
                        Unsafe.Add(ref first, intPtr + 3).Equals(Unsafe.Add(ref second, intPtr + 3)) &&
                        Unsafe.Add(ref first, intPtr + 4).Equals(Unsafe.Add(ref second, intPtr + 4)) &&
                        Unsafe.Add(ref first, intPtr + 5).Equals(Unsafe.Add(ref second, intPtr + 5)) &&
                        Unsafe.Add(ref first, intPtr + 6).Equals(Unsafe.Add(ref second, intPtr + 6)) &&
                        Unsafe.Add(ref first, intPtr + 7).Equals(Unsafe.Add(ref second, intPtr + 7)))
                    {
                        intPtr += 8;
                        continue;
                    }

                    goto IL_028b;
                }

                if (length >= 4)
                {
                    length -= 4;
                    if (!Unsafe.Add(ref first, intPtr).Equals(Unsafe.Add(ref second, intPtr)) ||
                        !Unsafe.Add(ref first, intPtr + 1).Equals(Unsafe.Add(ref second, intPtr + 1)) ||
                        !Unsafe.Add(ref first, intPtr + 2).Equals(Unsafe.Add(ref second, intPtr + 2)) ||
                        !Unsafe.Add(ref first, intPtr + 3).Equals(Unsafe.Add(ref second, intPtr + 3)))
                    {
                        goto IL_028b;
                    }

                    intPtr += 4;
                }

                while (length > 0)
                {
                    if (Unsafe.Add(ref first, intPtr).Equals(Unsafe.Add(ref second, intPtr)))
                    {
                        intPtr += 1;
                        length--;
                        continue;
                    }

                    goto IL_028b;
                }

                break;
                IL_028b:
                return false;
            }
        }

        return true;
    }

    public static int SequenceCompareTo<T>(ref T first, int firstLength, ref T second, int secondLength)
        where T : IComparable<T>
    {
        int num = firstLength;
        if (num > secondLength)
        {
            num = secondLength;
        }

        for (int i = 0; i < num; i++)
        {
            int num2 = Unsafe.Add(ref first, i).CompareTo(Unsafe.Add(ref second, i));
            if (num2 != 0)
            {
                return num2;
            }
        }

        return firstLength.CompareTo(secondLength);
    }

    public static unsafe void CopyTo<T>(ref T dst, int dstLength, ref T src, int srcLength)
    {
        IntPtr intPtr = Unsafe.ByteOffset(ref src, ref Unsafe.Add(ref src, srcLength));
        IntPtr intPtr2 = Unsafe.ByteOffset(ref dst, ref Unsafe.Add(ref dst, dstLength));
        IntPtr intPtr3 = Unsafe.ByteOffset(ref src, ref dst);
        bool num;
        if (sizeof(IntPtr) != 4)
        {
            if ((ulong)(long)intPtr3 >= (ulong)(long)intPtr)
            {
                num = (ulong)(long)intPtr3 > (ulong)(-(long)intPtr2);
                goto IL_006f;
            }
        }
        else if ((uint)(int)intPtr3 >= (uint)(int)intPtr)
        {
            num = (uint)(int)intPtr3 > (uint)(-(int)intPtr2);
            goto IL_006f;
        }

        goto IL_00de;
        IL_006f:
        if (!num && !IsReferenceOrContainsReferences<T>())
        {
            ref byte source = ref Unsafe.As<T, byte>(ref dst);
            ref byte source2 = ref Unsafe.As<T, byte>(ref src);
            ulong num2 = (ulong)(long)intPtr;
            uint num4;
            for (ulong num3 = 0uL; num3 < num2; num3 += num4)
            {
                num4 = (uint)((num2 - num3 > uint.MaxValue) ? uint.MaxValue : (num2 - num3));
                Unsafe.CopyBlock(ref Unsafe.Add(ref source, (IntPtr)(long)num3),
                    ref Unsafe.Add(ref source2, (IntPtr)(long)num3), num4);
            }

            return;
        }

        IL_00de:
        bool flag = ((sizeof(IntPtr) == 4)
            ? ((uint)(int)intPtr3 > (uint)(-(int)intPtr2))
            : ((ulong)(long)intPtr3 > (ulong)(-(long)intPtr2)));
        int num5 = (flag ? 1 : (-1));
        int num6 = ((!flag) ? (srcLength - 1) : 0);
        int i;
        for (i = 0; i < (srcLength & -8); i += 8)
        {
            Unsafe.Add(ref dst, num6) = Unsafe.Add(ref src, num6);
            Unsafe.Add(ref dst, num6 + num5) = Unsafe.Add(ref src, num6 + num5);
            Unsafe.Add(ref dst, num6 + num5 * 2) = Unsafe.Add(ref src, num6 + num5 * 2);
            Unsafe.Add(ref dst, num6 + num5 * 3) = Unsafe.Add(ref src, num6 + num5 * 3);
            Unsafe.Add(ref dst, num6 + num5 * 4) = Unsafe.Add(ref src, num6 + num5 * 4);
            Unsafe.Add(ref dst, num6 + num5 * 5) = Unsafe.Add(ref src, num6 + num5 * 5);
            Unsafe.Add(ref dst, num6 + num5 * 6) = Unsafe.Add(ref src, num6 + num5 * 6);
            Unsafe.Add(ref dst, num6 + num5 * 7) = Unsafe.Add(ref src, num6 + num5 * 7);
            num6 += num5 * 8;
        }

        if (i < (srcLength & -4))
        {
            Unsafe.Add(ref dst, num6) = Unsafe.Add(ref src, num6);
            Unsafe.Add(ref dst, num6 + num5) = Unsafe.Add(ref src, num6 + num5);
            Unsafe.Add(ref dst, num6 + num5 * 2) = Unsafe.Add(ref src, num6 + num5 * 2);
            Unsafe.Add(ref dst, num6 + num5 * 3) = Unsafe.Add(ref src, num6 + num5 * 3);
            num6 += num5 * 4;
            i += 4;
        }

        for (; i < srcLength; i++)
        {
            Unsafe.Add(ref dst, num6) = Unsafe.Add(ref src, num6);
            num6 += num5;
        }
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    public static unsafe IntPtr Add<T>(this IntPtr start, int index)
    {
        if (sizeof(IntPtr) == 4)
        {
            uint num = (uint)(index * Unsafe.SizeOf<T>());
            return (IntPtr)((byte*)(void*)start + num);
        }

        ulong num2 = (ulong)index * (ulong)Unsafe.SizeOf<T>();
        return (IntPtr)((byte*)(void*)start + num2);
    }

    public static bool IsReferenceOrContainsReferences<T>()
    {
        return PerTypeValues<T>.IsReferenceOrContainsReferences;
    }

    private static bool IsReferenceOrContainsReferencesCore(Type type)
    {
        if (type.GetTypeInfo().IsPrimitive)
        {
            return false;
        }

        if (!type.GetTypeInfo().IsValueType)
        {
            return true;
        }

        Type underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            type = underlyingType;
        }

        if (type.GetTypeInfo().IsEnum)
        {
            return false;
        }

        foreach (FieldInfo declaredField in type.GetTypeInfo().DeclaredFields)
        {
            if (!declaredField.IsStatic && IsReferenceOrContainsReferencesCore(declaredField.FieldType))
            {
                return true;
            }
        }

        return false;
    }

    public static unsafe void ClearLessThanPointerSized(byte* ptr, UIntPtr byteLength)
    {
        if (sizeof(UIntPtr) == 4)
        {
            Unsafe.InitBlockUnaligned(ptr, 0, (uint)byteLength);
            return;
        }

        ulong num = (ulong)byteLength;
        uint num2 = (uint)(num & 0xFFFFFFFFu);
        Unsafe.InitBlockUnaligned(ptr, 0, num2);
        num -= num2;
        ptr += num2;
        while (num != 0)
        {
            num2 = (uint)((num >= uint.MaxValue) ? uint.MaxValue : num);
            Unsafe.InitBlockUnaligned(ptr, 0, num2);
            ptr += num2;
            num -= num2;
        }
    }

    public static unsafe void ClearLessThanPointerSized(ref byte b, UIntPtr byteLength)
    {
        if (sizeof(UIntPtr) == 4)
        {
            Unsafe.InitBlockUnaligned(ref b, 0, (uint)byteLength);
            return;
        }

        ulong num = (ulong)byteLength;
        uint num2 = (uint)(num & 0xFFFFFFFFu);
        Unsafe.InitBlockUnaligned(ref b, 0, num2);
        num -= num2;
        long num3 = num2;
        while (num != 0)
        {
            num2 = (uint)((num >= uint.MaxValue) ? uint.MaxValue : num);
            Unsafe.InitBlockUnaligned(ref Unsafe.Add(ref b, (IntPtr)num3), 0, num2);
            num3 += num2;
            num -= num2;
        }
    }

    public static unsafe void ClearPointerSizedWithoutReferences(ref byte b, UIntPtr byteLength)
    {
        IntPtr zero;
        for (zero = IntPtr.Zero; zero.LessThanEqual(byteLength - sizeof(Reg64)); zero += sizeof(Reg64))
        {
            Unsafe.As<byte, Reg64>(ref Unsafe.Add(ref b, zero)) = default(Reg64);
        }

        if (zero.LessThanEqual(byteLength - sizeof(Reg32)))
        {
            Unsafe.As<byte, Reg32>(ref Unsafe.Add(ref b, zero)) = default(Reg32);
            zero += sizeof(Reg32);
        }

        if (zero.LessThanEqual(byteLength - sizeof(Reg16)))
        {
            Unsafe.As<byte, Reg16>(ref Unsafe.Add(ref b, zero)) = default(Reg16);
            zero += sizeof(Reg16);
        }

        if (zero.LessThanEqual(byteLength - 8))
        {
            Unsafe.As<byte, long>(ref Unsafe.Add(ref b, zero)) = 0L;
            zero += 8;
        }

        if (sizeof(IntPtr) == 4 && zero.LessThanEqual(byteLength - 4))
        {
            Unsafe.As<byte, int>(ref Unsafe.Add(ref b, zero)) = 0;
            zero += 4;
        }
    }

    public static void ClearPointerSizedWithReferences(ref IntPtr ip, UIntPtr pointerSizeLength)
    {
        IntPtr intPtr = IntPtr.Zero;
        IntPtr zero = IntPtr.Zero;
        while ((zero = intPtr + 8).LessThanEqual(pointerSizeLength))
        {
            Unsafe.Add(ref ip, intPtr + 0) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 1) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 2) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 3) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 4) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 5) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 6) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 7) = default(IntPtr);
            intPtr = zero;
        }

        if ((zero = intPtr + 4).LessThanEqual(pointerSizeLength))
        {
            Unsafe.Add(ref ip, intPtr + 0) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 1) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 2) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 3) = default(IntPtr);
            intPtr = zero;
        }

        if ((zero = intPtr + 2).LessThanEqual(pointerSizeLength))
        {
            Unsafe.Add(ref ip, intPtr + 0) = default(IntPtr);
            Unsafe.Add(ref ip, intPtr + 1) = default(IntPtr);
            intPtr = zero;
        }

        if ((intPtr + 1).LessThanEqual(pointerSizeLength))
        {
            Unsafe.Add(ref ip, intPtr) = default(IntPtr);
        }
    }

    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    private static unsafe bool LessThanEqual(this IntPtr index, UIntPtr length)
    {
        if (sizeof(UIntPtr) != 4)
        {
            return (long)index <= (long)(ulong)length;
        }

        return (int)index <= (int)(uint)length;
    }
}