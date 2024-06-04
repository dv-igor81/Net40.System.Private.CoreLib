using System.Runtime.Versioning;

namespace System.Runtime.CompilerServices;

public static class Unsafe
{
    
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    [NonVersionable]
    [Intrinsic]
    private static unsafe ref T AddByteOffset<T>(ref T source, nuint byteOffset)
    {
        return ref AddByteOffset(ref source, (IntPtr)(void*)byteOffset);
    }
    
    [MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
    [NonVersionable]
    [Intrinsic]
    internal static unsafe ref T AddByteOffset<T>(ref T source, int byteOffset)
    {
        return ref AddByteOffset(ref source, (IntPtr)(void*)byteOffset);
    }
    
    public static ref T AddByteOffset<T>(ref T source, IntPtr byteOffset)
    {
        unsafe
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            fixed (void* pointer = &source)
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            {
               byte* resultPtr = ((byte*)pointer) + byteOffset.ToInt32();
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
               return ref *(T*)resultPtr;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            }
        }
    }
    

    public static unsafe void* Add<T>(void* source, int elementOffset)
    {
        return (byte*)source + elementOffset * (nint)SizeOf<T>();
    }

    public static ref T Add<T>(ref T source, int elementOffset)
    {
        return ref AddByteOffset(ref source, elementOffset * (nint)SizeOf<T>());
    }

    public static ref T Add<T>(ref T source, IntPtr elementOffset)
    {
        return ref AddByteOffset(ref source, elementOffset * (nint)SizeOf<T>());
    }

    public static bool AreSame<T>(ref T left, ref T right)
    {
        unsafe
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            fixed (void* leftPtr = &left, rightPtr = &right)
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            {
                return leftPtr == rightPtr;
            }
        }
    }

    public static unsafe void* AsPointer<T>(ref T value)
    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        fixed (void* pointer = & value)
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        {
            return pointer;
        }
    }

    public static unsafe ref T AsRef<T>(void* source)
    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        return ref *(T*)source;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }
    
    public static ref T AsRef<T>(in T source)
    {
        unsafe 
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            fixed (void* pointer = &source)
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                return ref *(T*)pointer;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            }
        }
    }

    public static T As<T>(object source) where T : class
    {
        unsafe 
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            void* pointer = &source;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                return *(T*)pointer;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            }
        }
    }

    public static ref TTo As<TFrom, TTo>(ref TFrom source) // where TFrom : unmanaged where TTo : unmanaged
    {
        unsafe 
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            fixed (void* pointer = &source)
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                return ref *(TTo*)pointer;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            }
        }
    }

    public static IntPtr ByteOffset<T>(ref T origin, ref T target) // where T : unmanaged
    {
        unsafe
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            fixed (void* originPtr = &origin, targetPtr = &target)
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            {
                var offset = ((byte*)targetPtr) - ((byte*)originPtr);
                return new IntPtr(offset);
            }
        }
    }

    public static void CopyBlock(ref byte destination, ref byte source, uint byteCount)
    {
        unsafe
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            fixed (byte* destPtr = &destination, srcPtr = &source)
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            {
                BufferEx.Memmove(destPtr, srcPtr, byteCount);
            }
        }
    }

    public static unsafe void CopyBlock(void* destination, void* source, uint byteCount)
    {
        BufferEx.Memmove((byte*)destination, (byte*)source, byteCount);
    }

    public static void CopyBlockUnaligned(ref byte destination, ref byte source, uint byteCount)
    {
    }

    public static unsafe void CopyBlockUnaligned(void* destination, void* source, uint byteCount)
    {
        BufferEx.Memmove((byte*)destination, (byte*)source, byteCount);
    }

    public static unsafe void Copy<T>(void* destination, ref T source)
    {
        Write(destination, source);
    }

    public static unsafe void Copy<T>(ref T destination, void* source)
    {
        destination = Read<T>(source);
    }

    public static void InitBlock(ref byte startAddress, byte value, uint byteCount)
    {
        unsafe
        {
            fixed (byte* currentAddress = &startAddress)
            {
                for (uint i = 0; i < byteCount; i++)
                {
                    currentAddress[i] = value;
                }
            }
        }
    }

    public static unsafe void InitBlock(void* startAddress, byte value, uint byteCount)
    {
        byte* currentAddress = (byte*)startAddress;
        for (uint i = 0; i < byteCount; i++)
        {
            *currentAddress = value;
            currentAddress += 1;
        }
    }

    public static void InitBlockUnaligned(ref byte startAddress, byte value, uint byteCount)
    {
        for (uint i = 0; i < byteCount; i++)
        {
            AddByteOffset(ref startAddress, i) = value;
        }
    }

    public static unsafe void InitBlockUnaligned(void* startAddress, byte value, uint byteCount)
    {
        byte* currentAddress = (byte*)startAddress;
        for (uint i = 0; i < byteCount; i++)
        {
            *currentAddress = value;
            currentAddress += 1;
        }
    }

    public static bool IsAddressGreaterThan<T>(ref T left, ref T right)
    {
        throw new NotImplementedException();
    }

    public static bool IsAddressLessThan<T>(ref T left, ref T right)
    {
        throw new NotImplementedException();
    }

    public static T ReadUnaligned<T>(ref byte source)
    {
        unsafe
        {
            fixed (byte* srcPtr = &source)
            {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                return *(T*)srcPtr;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            }
        }
    }

    public static unsafe T ReadUnaligned<T>(void* source)
    {
        return Read<T>(source);
    }

    public static unsafe T Read<T>(void* source)
    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        return *(T*)source;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }

    public static int SizeOf<T>()
    {
        unsafe
        {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            int size = sizeof(T);
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            return size;
        }
    }

    public static ref T SubtractByteOffset<T>(ref T source, System.IntPtr byteOffset)
    {
        throw new NotImplementedException();
    }

    public static unsafe void* Subtract<T>(void* source, int elementOffset)
    {
        throw new NotImplementedException();
    }

    public static ref T Subtract<T>(ref T source, int elementOffset)
    {
        throw new NotImplementedException();
    }

    public static ref T Subtract<T>(ref T source, System.IntPtr elementOffset)
    {
        throw new NotImplementedException();
    }

    public static ref T Unbox<T>(object box) where T : struct
    {
        throw new NotImplementedException();
    }

    public static void WriteUnaligned<T>(ref byte destination, T value)
    {
        unsafe
        {
            fixed (byte* destPtr = &destination)
            {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
                *(T*)destPtr = value;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
            }
        }
    }

    public static unsafe void WriteUnaligned<T>(void* destination, T value)
    {
       Write<T>(destination, value);
    }

    private static unsafe void Write<T>(void* destination, T source)
    {
#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
        *(T*)destination = source;
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
    }
}