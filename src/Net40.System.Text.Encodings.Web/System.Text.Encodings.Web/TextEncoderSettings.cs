using System.Collections.Generic;
using System.Text.Internal;
using System.Text.Unicode;

namespace System.Text.Encodings.Web;

public class TextEncoderSettings
{
	private AllowedCharactersBitmap _allowedCharactersBitmap;

	public TextEncoderSettings()
	{
		_allowedCharactersBitmap = AllowedCharactersBitmap.CreateNew();
	}

	public TextEncoderSettings(TextEncoderSettings other)
	{
		if (other == null)
		{
			throw new ArgumentNullException("other");
		}
		_allowedCharactersBitmap = AllowedCharactersBitmap.CreateNew();
		AllowCodePoints(other.GetAllowedCodePoints());
	}

	public TextEncoderSettings(params UnicodeRange[] allowedRanges)
	{
		if (allowedRanges == null)
		{
			throw new ArgumentNullException("allowedRanges");
		}
		_allowedCharactersBitmap = AllowedCharactersBitmap.CreateNew();
		AllowRanges(allowedRanges);
	}

	public virtual void AllowCharacter(char character)
	{
		_allowedCharactersBitmap.AllowCharacter(character);
	}

	public virtual void AllowCharacters(params char[] characters)
	{
		if (characters == null)
		{
			throw new ArgumentNullException("characters");
		}
		for (int i = 0; i < characters.Length; i++)
		{
			_allowedCharactersBitmap.AllowCharacter(characters[i]);
		}
	}

	public virtual void AllowCodePoints(IEnumerable<int> codePoints)
	{
		if (codePoints == null)
		{
			throw new ArgumentNullException("codePoints");
		}
		foreach (int allowedCodePoint in codePoints)
		{
			char codePointAsChar = (char)allowedCodePoint;
			if (allowedCodePoint == codePointAsChar)
			{
				_allowedCharactersBitmap.AllowCharacter(codePointAsChar);
			}
		}
	}

	public virtual void AllowRange(UnicodeRange range)
	{
		if (range == null)
		{
			throw new ArgumentNullException("range");
		}
		int firstCodePoint = range.FirstCodePoint;
		int rangeSize = range.Length;
		for (int i = 0; i < rangeSize; i++)
		{
			_allowedCharactersBitmap.AllowCharacter((char)(firstCodePoint + i));
		}
	}

	public virtual void AllowRanges(params UnicodeRange[] ranges)
	{
		if (ranges == null)
		{
			throw new ArgumentNullException("ranges");
		}
		for (int i = 0; i < ranges.Length; i++)
		{
			AllowRange(ranges[i]);
		}
	}

	public virtual void Clear()
	{
		_allowedCharactersBitmap.Clear();
	}

	public virtual void ForbidCharacter(char character)
	{
		_allowedCharactersBitmap.ForbidCharacter(character);
	}

	public virtual void ForbidCharacters(params char[] characters)
	{
		if (characters == null)
		{
			throw new ArgumentNullException("characters");
		}
		for (int i = 0; i < characters.Length; i++)
		{
			_allowedCharactersBitmap.ForbidCharacter(characters[i]);
		}
	}

	public virtual void ForbidRange(UnicodeRange range)
	{
		if (range == null)
		{
			throw new ArgumentNullException("range");
		}
		int firstCodePoint = range.FirstCodePoint;
		int rangeSize = range.Length;
		for (int i = 0; i < rangeSize; i++)
		{
			_allowedCharactersBitmap.ForbidCharacter((char)(firstCodePoint + i));
		}
	}

	public virtual void ForbidRanges(params UnicodeRange[] ranges)
	{
		if (ranges == null)
		{
			throw new ArgumentNullException("ranges");
		}
		for (int i = 0; i < ranges.Length; i++)
		{
			ForbidRange(ranges[i]);
		}
	}

	internal AllowedCharactersBitmap GetAllowedCharacters()
	{
		return _allowedCharactersBitmap.Clone();
	}

	public virtual IEnumerable<int> GetAllowedCodePoints()
	{
		for (int i = 0; i < 65536; i++)
		{
			if (_allowedCharactersBitmap.IsCharacterAllowed((char)i))
			{
				yield return i;
			}
		}
	}
}
