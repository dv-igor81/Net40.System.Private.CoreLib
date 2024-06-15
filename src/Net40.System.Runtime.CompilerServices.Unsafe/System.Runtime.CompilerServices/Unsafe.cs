// ReSharper disable once CheckNamespace
// ReSharper disable UnusedTypeParameter

using System.Runtime.Versioning;

// ReSharper disable once CheckNamespace
namespace System.Runtime.CompilerServices
{
    public static class Unsafe
    {

        public static void SkipInit<T>(out T result)
        {
            result = default;
        }
        
        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe T Read<T>(void* source);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe T ReadUnaligned<T>(void* source);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern T ReadUnaligned<T>(ref byte source);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void Write<T>(void* destination, T value);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void WriteUnaligned<T>(void* destination, T value);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern void WriteUnaligned<T>(ref byte destination, T value);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void Copy<T>(void* destination, ref T source);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void Copy<T>(ref T destination, void* source);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void* AsPointer<T>(ref T value);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern int SizeOf<T>();


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void CopyBlock(void* destination, void* source, uint byteCount);

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern void CopyBlock(ref byte destination, ref byte source, uint byteCount);

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void CopyBlockUnaligned(void* destination, void* source, uint byteCount);

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern void CopyBlockUnaligned(ref byte destination, ref byte source, uint byteCount);

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void InitBlock(void* startAddress, byte value, uint byteCount);

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern void InitBlock(ref byte startAddress, byte value, uint byteCount);

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void InitBlockUnaligned(void* startAddress, byte value, uint byteCount);

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern void InitBlockUnaligned(ref byte startAddress, byte value, uint byteCount);

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern T As<T>(object o) where T : class;

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe ref T AsRef<T>(void* source);

        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern ref T AsRef<T>(in T source);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern ref TTo As<TFrom, TTo>(ref TFrom source);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern ref T Unbox<T>(object box) where T : struct;


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern ref T Add<T>(ref T source, int elementOffset);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void* Add<T>(void* source, int elementOffset);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern ref T Add<T>(ref T source, IntPtr elementOffset);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern ref T AddByteOffset<T>(ref T source, IntPtr byteOffset);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern ref T Subtract<T>(ref T source, int elementOffset);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern unsafe void* Subtract<T>(void* source, int elementOffset);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern ref T Subtract<T>(ref T source, IntPtr elementOffset);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern ref T SubtractByteOffset<T>(ref T source, IntPtr byteOffset);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern IntPtr ByteOffset<T>(ref T origin, ref T target);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern bool AreSame<T>(ref T left, ref T right);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern bool IsAddressGreaterThan<T>(ref T left, ref T right);


        [NonVersionable]
        [MethodImpl(MethodImplOptions.ForwardRef)]
        public static extern bool IsAddressLessThan<T>(ref T left, ref T right);
    }
}
