#define DEBUG
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.Json;

internal struct BitStack
{
	private const int AllocationFreeMaxDepth = 64;

	private const int DefaultInitialArraySize = 2;

	private int[] _array;

	private ulong _allocationFreeContainer;

	private int _currentDepth;

	public int CurrentDepth => _currentDepth;

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void PushTrue()
	{
		if (_currentDepth < 64)
		{
			_allocationFreeContainer = (_allocationFreeContainer << 1) | 1;
		}
		else
		{
			PushToArray(value: true);
		}
		_currentDepth++;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public void PushFalse()
	{
		if (_currentDepth < 64)
		{
			_allocationFreeContainer <<= 1;
		}
		else
		{
			PushToArray(value: false);
		}
		_currentDepth++;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void PushToArray(bool value)
	{
		if (_array == null)
		{
			_array = new int[2];
		}
		int index = _currentDepth - 64;
		Debug.Assert(index >= 0, $"Set - Negative - index: {index}, arrayLength: {_array.Length}");
		Debug.Assert(_array.Length <= 67108864, $"index: {index}, arrayLength: {_array.Length}");
		int extraBits;
		int elementIndex = Div32Rem(index, out extraBits);
		if (elementIndex >= _array.Length)
		{
			Debug.Assert(index >= 0 && index > _array.Length * 32 - 1, $"Only grow when necessary - index: {index}, arrayLength: {_array.Length}");
			DoubleArray(elementIndex);
		}
		Debug.Assert(elementIndex < _array.Length, $"Set - index: {index}, elementIndex: {elementIndex}, arrayLength: {_array.Length}, extraBits: {extraBits}");
		int newValue = _array[elementIndex];
		newValue = ((!value) ? (newValue & ~(1 << extraBits)) : (newValue | (1 << extraBits)));
		_array[elementIndex] = newValue;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool Pop()
	{
		_currentDepth--;
		bool inObject = false;
		if (_currentDepth < 64)
		{
			_allocationFreeContainer >>= 1;
			return (_allocationFreeContainer & 1) != 0;
		}
		if (_currentDepth == 64)
		{
			return (_allocationFreeContainer & 1) != 0;
		}
		return PopFromArray();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool PopFromArray()
	{
		int index = _currentDepth - 64 - 1;
		Debug.Assert(_array != null);
		Debug.Assert(index >= 0, $"Get - Negative - index: {index}, arrayLength: {_array.Length}");
		int extraBits;
		int elementIndex = Div32Rem(index, out extraBits);
		Debug.Assert(elementIndex < _array.Length, $"Get - index: {index}, elementIndex: {elementIndex}, arrayLength: {_array.Length}, extraBits: {extraBits}");
		return (_array[elementIndex] & (1 << extraBits)) != 0;
	}

	private void DoubleArray(int minSize)
	{
		Debug.Assert(_array.Length < 1073741823, $"Array too large - arrayLength: {_array.Length}");
		Debug.Assert(minSize >= 0 && minSize >= _array.Length);
		int nextDouble = Math.Max(minSize + 1, _array.Length * 2);
		Debug.Assert(nextDouble > minSize);
		Array.Resize(ref _array, nextDouble);
	}

	public void SetFirstBit()
	{
		Debug.Assert(_currentDepth == 0, "Only call SetFirstBit when depth is 0");
		_currentDepth++;
		_allocationFreeContainer = 1uL;
	}

	public void ResetFirstBit()
	{
		Debug.Assert(_currentDepth == 0, "Only call ResetFirstBit when depth is 0");
		_currentDepth++;
		_allocationFreeContainer = 0uL;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private static int Div32Rem(int number, out int remainder)
	{
		uint quotient = (uint)number / 32u;
		remainder = number & 0x1F;
		return (int)quotient;
	}
}
