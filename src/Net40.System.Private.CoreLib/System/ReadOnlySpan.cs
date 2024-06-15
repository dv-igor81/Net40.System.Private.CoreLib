using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System;

[DebuggerTypeProxy(typeof(SpanDebugView<>))]
[DebuggerDisplay("{ToString(),raw}")]
[DebuggerTypeProxy(typeof(SpanDebugView<>))]
[DebuggerDisplay("{ToString(),raw}")]
public struct ReadOnlySpan<T>
{
	public struct Enumerator
	{
		private readonly ReadOnlySpan<T> _span;

		private int _index;

		public ref readonly T Current
		{
			[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
			get
			{
				return ref _span[_index];
			}
		}

		[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
		internal Enumerator(ReadOnlySpan<T> span)
		{
			_span = span;
			_index = -1;
		}

		[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
		public bool MoveNext()
		{
			int num = _index + 1;
			if (num < _span.Length)
			{
				_index = num;
				return true;
			}
			return false;
		}
	}

	private readonly Pinnable<T> _pinnable;

	private readonly IntPtr _byteOffset;

	private readonly int _length;

	public int Length => _length;

	public bool IsEmpty => _length == 0;

	public static ReadOnlySpan<T> Empty => default(ReadOnlySpan<T>);

	public unsafe ref readonly T this[int index]
	{
		[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
		get
		{
			if ((uint)index >= (uint)_length)
			{
				ThrowHelper.ThrowIndexOutOfRangeException();
			}
			if (_pinnable == null)
			{
				return ref Unsafe.Add(ref Unsafe.AsRef<T>(_byteOffset.ToPointer()), index);
			}
			return ref Unsafe.Add(ref Unsafe.AddByteOffset(ref _pinnable.Data, _byteOffset), index);
		}
	}

	internal Pinnable<T> Pinnable => _pinnable;

	internal IntPtr ByteOffset => _byteOffset;

	public static bool operator !=(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
	{
		return !(left == right);
	}

	[Obsolete("Equals() on ReadOnlySpan will always throw an exception. Use == instead.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public override bool Equals(object obj)
	{
		throw new NotSupportedException(SR.NotSupported_CannotCallEqualsOnSpan);
	}

	[Obsolete("GetHashCode() on ReadOnlySpan will always throw an exception.")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public override int GetHashCode()
	{
		throw new NotSupportedException(SR.NotSupported_CannotCallGetHashCodeOnSpan);
	}

	public static implicit operator ReadOnlySpan<T>(T[] array)
	{
		return new ReadOnlySpan<T>(array);
	}

	public static implicit operator ReadOnlySpan<T>(ArraySegment<T> segment)
	{
		return new ReadOnlySpan<T>(segment.Array, segment.Offset, segment.Count);
	}

	public Enumerator GetEnumerator()
	{
		return new Enumerator(this);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public ReadOnlySpan(T[] array)
	{
		if (array == null)
		{
			this = default(ReadOnlySpan<T>);
			return;
		}
		_length = array.Length;
		_pinnable = Unsafe.As<Pinnable<T>>(array);
		_byteOffset = SpanHelpers.PerTypeValues<T>.ArrayAdjustment;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public ReadOnlySpan(T[] array, int start, int length)
	{
		if (array == null)
		{
			if (start != 0 || length != 0)
			{
				ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
			}
			this = default(ReadOnlySpan<T>);
			return;
		}
		if ((uint)start > (uint)array.Length || (uint)length > (uint)(array.Length - start))
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
		}
		_length = length;
		_pinnable = Unsafe.As<Pinnable<T>>(array);
		_byteOffset = SpanHelpers.PerTypeValues<T>.ArrayAdjustment.Add<T>(start);
	}
	
	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public unsafe ReadOnlySpan(ref T reference, int length) : this(Unsafe.AsPointer(ref reference), length)
	{
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public unsafe ReadOnlySpan(void* pointer, int length)
	{
		if (SpanHelpers.IsReferenceOrContainsReferences<T>())
		{
			ThrowHelper.ThrowInvalidTypeWithPointersNotSupported(typeof(T));
		}
		if (length < 0)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
		}
		_length = length;
		_pinnable = null;
		_byteOffset = new IntPtr(pointer);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal ReadOnlySpan(Pinnable<T> pinnable, IntPtr byteOffset, int length)
	{
		_length = length;
		_pinnable = pinnable;
		_byteOffset = byteOffset;
	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	public unsafe ref readonly T GetPinnableReference()
	{
		if (_length != 0)
		{
			if (_pinnable == null)
			{
				return ref Unsafe.AsRef<T>(_byteOffset.ToPointer());
			}
			return ref Unsafe.AddByteOffset(ref _pinnable.Data, _byteOffset);
		}
		return ref Unsafe.AsRef<T>(null);
	}

	public void CopyTo(Span<T> destination)
	{
		if (!TryCopyTo(destination))
		{
			ThrowHelper.ThrowArgumentException_DestinationTooShort();
		}
	}

	public bool TryCopyTo(Span<T> destination)
	{
		int length = _length;
		int length2 = destination.Length;
		if (length == 0)
		{
			return true;
		}
		if ((uint)length > (uint)length2)
		{
			return false;
		}
		ref T src = ref DangerousGetPinnableReference();
		SpanHelpers.CopyTo(ref destination.DangerousGetPinnableReference(), length2, ref src, length);
		return true;
	}

	public static bool operator ==(ReadOnlySpan<T> left, ReadOnlySpan<T> right)
	{
		if (left._length == right._length)
		{
			return Unsafe.AreSame(ref left.DangerousGetPinnableReference(), ref right.DangerousGetPinnableReference());
		}
		return false;
	}

	public override unsafe string ToString()
	{
		if (typeof(T) == typeof(char))
		{
			if (_byteOffset == MemoryExtensions.StringAdjustment)
			{
				object obj = Unsafe.As<object>(_pinnable);
				if (obj is string text && _length == text.Length)
				{
					return text;
				}
			}
			fixed (char* value = &Unsafe.As<T, char>(ref DangerousGetPinnableReference()))
			{
				return new string(value, 0, _length);
			}
		}
		return $"System.ReadOnlySpan<{typeof(T).Name}>[{_length}]";
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public ReadOnlySpan<T> Slice(int start)
	{
		if ((uint)start > (uint)_length)
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
		}
		IntPtr byteOffset = _byteOffset.Add<T>(start);
		int length = _length - start;
		return new ReadOnlySpan<T>(_pinnable, byteOffset, length);
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public ReadOnlySpan<T> Slice(int start, int length)
	{
		if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
		{
			ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.start);
		}
		IntPtr byteOffset = _byteOffset.Add<T>(start);
		return new ReadOnlySpan<T>(_pinnable, byteOffset, length);
	}

	public T[] ToArray()
	{
		if (_length == 0)
		{
			return SpanHelpers.PerTypeValues<T>.EmptyArray;
		}
		T[] array = new T[_length];
		CopyTo(array);
		return array;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	[EditorBrowsable(EditorBrowsableState.Never)]
	private unsafe ref T DangerousGetPinnableReference()
	{
		if (_pinnable == null)
		{
			return ref Unsafe.AsRef<T>(_byteOffset.ToPointer());
		}
		return ref Unsafe.AddByteOffset(ref _pinnable.Data, _byteOffset);
	}
}
