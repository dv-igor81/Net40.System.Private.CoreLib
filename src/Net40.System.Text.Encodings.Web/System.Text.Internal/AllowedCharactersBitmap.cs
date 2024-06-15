#define DEBUG
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Unicode;

namespace System.Text.Internal;

internal readonly struct AllowedCharactersBitmap
{
	private const int ALLOWED_CHARS_BITMAP_LENGTH = 2048;

	private readonly uint[] _allowedCharacters;

	public static AllowedCharactersBitmap CreateNew()
	{
		return new AllowedCharactersBitmap(new uint[2048]);
	}

	private AllowedCharactersBitmap(uint[] allowedCharacters)
	{
		if (allowedCharacters == null)
		{
			throw new ArgumentNullException("allowedCharacters");
		}
		_allowedCharacters = allowedCharacters;
	}

	public void AllowCharacter(char character)
	{
		int index = (int)character >> 5;
		int offset = character & 0x1F;
		_allowedCharacters[index] |= (uint)(1 << offset);
	}

	public void ForbidCharacter(char character)
	{
		int index = (int)character >> 5;
		int offset = character & 0x1F;
		_allowedCharacters[index] &= (uint)(~(1 << offset));
	}

	public void ForbidUndefinedCharacters()
	{
		ReadOnlySpan<uint> definedCharactersBitmap = UnicodeHelpers.GetDefinedCharacterBitmap();
		Debug.Assert(definedCharactersBitmap.Length == _allowedCharacters.Length);
		for (int i = 0; i < _allowedCharacters.Length; i++)
		{
			_allowedCharacters[i] &= definedCharactersBitmap[i];
		}
	}

	public void Clear()
	{
		Array.Clear(_allowedCharacters, 0, _allowedCharacters.Length);
	}

	public AllowedCharactersBitmap Clone()
	{
		return new AllowedCharactersBitmap((uint[])_allowedCharacters.Clone());
	}

	public bool IsCharacterAllowed(char character)
	{
		int index = (int)character >> 5;
		int offset = character & 0x1F;
		return ((_allowedCharacters[index] >> offset) & 1) != 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public bool IsUnicodeScalarAllowed(int unicodeScalar)
	{
		int index = unicodeScalar >> 5;
		int offset = unicodeScalar & 0x1F;
		return ((_allowedCharacters[index] >> offset) & 1) != 0;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	public unsafe int FindFirstCharacterToEncode(char* text, int textLength)
	{
		for (int i = 0; i < textLength; i++)
		{
			if (!IsCharacterAllowed(text[i]))
			{
				return i;
			}
		}
		return -1;
	}
}
