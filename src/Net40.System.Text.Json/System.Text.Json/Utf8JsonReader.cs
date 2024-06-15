#define DEBUG
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Text.Json;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public ref struct Utf8JsonReader
{
	private readonly struct PartialStateForRollback
	{
		public readonly long _prevTotalConsumed;

		public readonly long _prevBytePositionInLine;

		public readonly int _prevConsumed;

		public readonly SequencePosition _prevCurrentPosition;

		public PartialStateForRollback(long totalConsumed, long bytePositionInLine, int consumed, SequencePosition currentPosition)
		{
			_prevTotalConsumed = totalConsumed;
			_prevBytePositionInLine = bytePositionInLine;
			_prevConsumed = consumed;
			_prevCurrentPosition = currentPosition;
		}

		public SequencePosition GetStartPosition(int offset = 0)
		{
			return new SequencePosition(_prevCurrentPosition.GetObject(), _prevCurrentPosition.GetInteger() + _prevConsumed + offset);
		}
	}

	private ReadOnlySpan<byte> _buffer;

	private bool _isFinalBlock;

	private bool _isInputSequence;

	private long _lineNumber;

	private long _bytePositionInLine;

	private int _consumed;

	private bool _inObject;

	private bool _isNotPrimitive;

	internal char _numberFormat;

	private JsonTokenType _tokenType;

	private JsonTokenType _previousTokenType;

	private JsonReaderOptions _readerOptions;

	private BitStack _bitStack;

	private long _totalConsumed;

	private bool _isLastSegment;

	internal bool _stringHasEscaping;

	private readonly bool _isMultiSegment;

	private bool _trailingCommaBeforeComment;

	private SequencePosition _nextPosition;

	private SequencePosition _currentPosition;

	private ReadOnlySequence<byte> _sequence;

	private bool IsLastSpan => _isFinalBlock && (!_isMultiSegment || _isLastSegment);

	internal ReadOnlySequence<byte> OriginalSequence => _sequence;

	internal ReadOnlySpan<byte> OriginalSpan => _sequence.IsEmpty ? _buffer : default(ReadOnlySpan<byte>);

	public ReadOnlySpan<byte> ValueSpan { get; private set; }

	public long BytesConsumed
	{
		get
		{
			if (!_isInputSequence)
			{
				Debug.Assert(_totalConsumed == 0);
			}
			return _totalConsumed + _consumed;
		}
	}

	public long TokenStartIndex { get; private set; }

	public int CurrentDepth
	{
		get
		{
			int readerDepth = _bitStack.CurrentDepth;
			if (TokenType == JsonTokenType.StartArray || TokenType == JsonTokenType.StartObject)
			{
				Debug.Assert(readerDepth >= 1);
				readerDepth--;
			}
			return readerDepth;
		}
	}

	internal bool IsInArray => !_inObject;

	public JsonTokenType TokenType => _tokenType;

	public bool HasValueSequence { get; private set; }

	public bool IsFinalBlock => _isFinalBlock;

	public ReadOnlySequence<byte> ValueSequence { get; private set; }

	public SequencePosition Position
	{
		get
		{
			if (_isInputSequence)
			{
				Debug.Assert(_currentPosition.GetObject() != null);
				return _sequence.GetPosition(_consumed, _currentPosition);
			}
			return default(SequencePosition);
		}
	}

	public JsonReaderState CurrentState
	{
		get
		{
			JsonReaderState result = default(JsonReaderState);
			result._lineNumber = _lineNumber;
			result._bytePositionInLine = _bytePositionInLine;
			result._inObject = _inObject;
			result._isNotPrimitive = _isNotPrimitive;
			result._numberFormat = _numberFormat;
			result._stringHasEscaping = _stringHasEscaping;
			result._trailingCommaBeforeComment = _trailingCommaBeforeComment;
			result._tokenType = _tokenType;
			result._previousTokenType = _previousTokenType;
			result._readerOptions = _readerOptions;
			result._bitStack = _bitStack;
			return result;
		}
	}

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private string DebuggerDisplay => $"TokenType = {DebugTokenType} (TokenStartIndex = {TokenStartIndex}) Consumed = {BytesConsumed}";

	private string DebugTokenType
	{
		get
		{
			JsonTokenType tokenType = TokenType;
			if (1 == 0)
			{
			}
			string result = tokenType switch
			{
				JsonTokenType.Comment => "Comment", 
				JsonTokenType.EndArray => "EndArray", 
				JsonTokenType.EndObject => "EndObject", 
				JsonTokenType.False => "False", 
				JsonTokenType.None => "None", 
				JsonTokenType.Null => "Null", 
				JsonTokenType.Number => "Number", 
				JsonTokenType.PropertyName => "PropertyName", 
				JsonTokenType.StartArray => "StartArray", 
				JsonTokenType.StartObject => "StartObject", 
				JsonTokenType.String => "String", 
				JsonTokenType.True => "True", 
				_ => ((byte)TokenType).ToString(), 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public Utf8JsonReader(ReadOnlySpan<byte> jsonData, bool isFinalBlock, JsonReaderState state)
	{
		_buffer = jsonData;
		_isFinalBlock = isFinalBlock;
		_isInputSequence = false;
		_lineNumber = state._lineNumber;
		_bytePositionInLine = state._bytePositionInLine;
		_inObject = state._inObject;
		_isNotPrimitive = state._isNotPrimitive;
		_numberFormat = state._numberFormat;
		_stringHasEscaping = state._stringHasEscaping;
		_trailingCommaBeforeComment = state._trailingCommaBeforeComment;
		_tokenType = state._tokenType;
		_previousTokenType = state._previousTokenType;
		_readerOptions = state._readerOptions;
		if (_readerOptions.MaxDepth == 0)
		{
			_readerOptions.MaxDepth = 64;
		}
		_bitStack = state._bitStack;
		_consumed = 0;
		TokenStartIndex = 0L;
		_totalConsumed = 0L;
		_isLastSegment = _isFinalBlock;
		_isMultiSegment = false;
		ValueSpan = ReadOnlySpan<byte>.Empty;
		_currentPosition = default(SequencePosition);
		_nextPosition = default(SequencePosition);
		_sequence = default(ReadOnlySequence<byte>);
		HasValueSequence = false;
		ValueSequence = ReadOnlySequence<byte>.Empty;
	}

	public Utf8JsonReader(ReadOnlySpan<byte> jsonData, JsonReaderOptions options = default(JsonReaderOptions))
		: this(jsonData, isFinalBlock: true, new JsonReaderState(options))
	{
	}

	public bool Read()
	{
		bool retVal = (_isMultiSegment ? ReadMultiSegment() : ReadSingleSegment());
		if (!retVal && _isFinalBlock && TokenType == JsonTokenType.None)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedJsonTokens, 0);
		}
		return retVal;
	}

	public void Skip()
	{
		if (!_isFinalBlock)
		{
			throw ThrowHelper.GetInvalidOperationException_CannotSkipOnPartial();
		}
		SkipHelper();
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void SkipHelper()
	{
		Debug.Assert(_isFinalBlock);
		if (TokenType == JsonTokenType.PropertyName)
		{
			bool result2 = Read();
			Debug.Assert(result2);
		}
		if (TokenType == JsonTokenType.StartObject || TokenType == JsonTokenType.StartArray)
		{
			int depth = CurrentDepth;
			do
			{
				bool result = Read();
				Debug.Assert(result);
			}
			while (depth < CurrentDepth);
		}
	}

	public bool TrySkip()
	{
		if (_isFinalBlock)
		{
			SkipHelper();
			return true;
		}
		return TrySkipHelper();
	}

	private bool TrySkipHelper()
	{
		Debug.Assert(!_isFinalBlock);
		Utf8JsonReader restore = this;
		if (TokenType != JsonTokenType.PropertyName || Read())
		{
			if (TokenType != JsonTokenType.StartObject && TokenType != JsonTokenType.StartArray)
			{
				goto IL_007d;
			}
			int depth = CurrentDepth;
			while (Read())
			{
				if (depth < CurrentDepth)
				{
					continue;
				}
				goto IL_007d;
			}
		}
		this = restore;
		return false;
		IL_007d:
		return true;
	}

	public bool ValueTextEquals(ReadOnlySpan<byte> utf8Text)
	{
		if (!IsTokenTypeString(TokenType))
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedStringComparison(TokenType);
		}
		return TextEqualsHelper(utf8Text);
	}

	public bool ValueTextEquals(string text)
	{
		return ValueTextEquals(text.AsSpan());
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private bool TextEqualsHelper(ReadOnlySpan<byte> otherUtf8Text)
	{
		if (HasValueSequence)
		{
			return CompareToSequence(otherUtf8Text);
		}
		if (_stringHasEscaping)
		{
			return UnescapeAndCompare(otherUtf8Text);
		}
		return otherUtf8Text.SequenceEqual(ValueSpan);
	}

	public unsafe bool ValueTextEquals(ReadOnlySpan<char> text)
	{
		if (!IsTokenTypeString(TokenType))
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedStringComparison(TokenType);
		}
		if (MatchNotPossible(text.Length))
		{
			return false;
		}
		byte[] otherUtf8TextArray = null;
		int length = checked(text.Length * 3);
		Span<byte> otherUtf8Text;
		if (length > 256)
		{
			otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length);
			otherUtf8Text = otherUtf8TextArray;
		}
		else
		{
			byte* ptr = stackalloc byte[256];
			otherUtf8Text = new Span<byte>(ptr, 256);
		}
		ReadOnlySpan<byte> utf16Text = MemoryMarshal.AsBytes(text);
		int consumed;
		int written;
		OperationStatus status = JsonWriterHelper.ToUtf8(utf16Text, otherUtf8Text, out consumed, out written);
		Debug.Assert(status != OperationStatus.DestinationTooSmall);
		bool result;
		if (status > OperationStatus.DestinationTooSmall)
		{
			result = false;
		}
		else
		{
			Debug.Assert(status == OperationStatus.Done);
			Debug.Assert(consumed == utf16Text.Length);
			result = TextEqualsHelper(otherUtf8Text.Slice(0, written));
		}
		if (otherUtf8TextArray != null)
		{
			otherUtf8Text.Slice(0, written).Clear();
			ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
		}
		return result;
	}

	private bool CompareToSequence(ReadOnlySpan<byte> other)
	{
		Debug.Assert(HasValueSequence);
		if (_stringHasEscaping)
		{
			return UnescapeSequenceAndCompare(other);
		}
		ReadOnlySequence<byte> localSequence = ValueSequence;
		Debug.Assert(!localSequence.IsSingleSegment);
		if (localSequence.Length != other.Length)
		{
			return false;
		}
		int matchedSoFar = 0;
		ReadOnlySequence<byte>.Enumerator enumerator = localSequence.GetEnumerator();
		while (enumerator.MoveNext())
		{
			ReadOnlySpan<byte> span = enumerator.Current.Span;
			if (other.Slice(matchedSoFar).StartsWith(span))
			{
				matchedSoFar += span.Length;
				continue;
			}
			return false;
		}
		return true;
	}

	private bool UnescapeAndCompare(ReadOnlySpan<byte> other)
	{
		Debug.Assert(!HasValueSequence);
		ReadOnlySpan<byte> localSpan = ValueSpan;
		if (localSpan.Length < other.Length || localSpan.Length / 6 > other.Length)
		{
			return false;
		}
		int idx = localSpan.IndexOf<byte>(92);
		Debug.Assert(idx != -1);
		if (!other.StartsWith(localSpan.Slice(0, idx)))
		{
			return false;
		}
		return JsonReaderHelper.UnescapeAndCompare(localSpan.Slice(idx), other.Slice(idx));
	}

	private bool UnescapeSequenceAndCompare(ReadOnlySpan<byte> other)
	{
		Debug.Assert(HasValueSequence);
		Debug.Assert(!ValueSequence.IsSingleSegment);
		ReadOnlySequence<byte> localSequence = ValueSequence;
		long sequenceLength = localSequence.Length;
		if (sequenceLength < other.Length || sequenceLength / 6 > other.Length)
		{
			return false;
		}
		int matchedSoFar = 0;
		bool result = false;
		ReadOnlySequence<byte>.Enumerator enumerator = localSequence.GetEnumerator();
		while (enumerator.MoveNext())
		{
			ReadOnlySpan<byte> span = enumerator.Current.Span;
			int idx = span.IndexOf<byte>(92);
			if (idx != -1)
			{
				if (other.Slice(matchedSoFar).StartsWith(span.Slice(0, idx)))
				{
					matchedSoFar += idx;
					other = other.Slice(matchedSoFar);
					localSequence = localSequence.Slice(matchedSoFar);
					result = ((!localSequence.IsSingleSegment) ? JsonReaderHelper.UnescapeAndCompare(localSequence, other) : JsonReaderHelper.UnescapeAndCompare(localSequence.First.Span, other));
				}
				break;
			}
			if (!other.Slice(matchedSoFar).StartsWith(span))
			{
				break;
			}
			matchedSoFar += span.Length;
		}
		return result;
	}

	private static bool IsTokenTypeString(JsonTokenType tokenType)
	{
		return tokenType == JsonTokenType.PropertyName || tokenType == JsonTokenType.String;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private bool MatchNotPossible(int charTextLength)
	{
		if (HasValueSequence)
		{
			return MatchNotPossibleSequence(charTextLength);
		}
		int sourceLength = ValueSpan.Length;
		if (sourceLength < charTextLength || sourceLength / (_stringHasEscaping ? 6 : 3) > charTextLength)
		{
			return true;
		}
		return false;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool MatchNotPossibleSequence(int charTextLength)
	{
		long sourceLength = ValueSequence.Length;
		if (sourceLength < charTextLength || sourceLength / (_stringHasEscaping ? 6 : 3) > charTextLength)
		{
			return true;
		}
		return false;
	}

	private void StartObject()
	{
		if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ObjectDepthTooLarge, 0);
		}
		_bitStack.PushTrue();
		ValueSpan = _buffer.Slice(_consumed, 1);
		_consumed++;
		_bytePositionInLine++;
		_tokenType = JsonTokenType.StartObject;
		_inObject = true;
	}

	private void EndObject()
	{
		if (!_inObject || _bitStack.CurrentDepth <= 0)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.MismatchedObjectArray, 125);
		}
		if (_trailingCommaBeforeComment)
		{
			if (!_readerOptions.AllowTrailingCommas)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd, 0);
			}
			_trailingCommaBeforeComment = false;
		}
		_tokenType = JsonTokenType.EndObject;
		ValueSpan = _buffer.Slice(_consumed, 1);
		UpdateBitStackOnEndToken();
	}

	private void StartArray()
	{
		if (_bitStack.CurrentDepth >= _readerOptions.MaxDepth)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ArrayDepthTooLarge, 0);
		}
		_bitStack.PushFalse();
		ValueSpan = _buffer.Slice(_consumed, 1);
		_consumed++;
		_bytePositionInLine++;
		_tokenType = JsonTokenType.StartArray;
		_inObject = false;
	}

	private void EndArray()
	{
		if (_inObject || _bitStack.CurrentDepth <= 0)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.MismatchedObjectArray, 93);
		}
		if (_trailingCommaBeforeComment)
		{
			if (!_readerOptions.AllowTrailingCommas)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd, 0);
			}
			_trailingCommaBeforeComment = false;
		}
		_tokenType = JsonTokenType.EndArray;
		ValueSpan = _buffer.Slice(_consumed, 1);
		UpdateBitStackOnEndToken();
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void UpdateBitStackOnEndToken()
	{
		_consumed++;
		_bytePositionInLine++;
		_inObject = _bitStack.Pop();
	}

	private bool ReadSingleSegment()
	{
		bool retVal = false;
		ValueSpan = default(ReadOnlySpan<byte>);
		if (HasMoreData())
		{
			byte first = _buffer[_consumed];
			if (first <= 32)
			{
				SkipWhiteSpace();
				if (!HasMoreData())
				{
					goto IL_01ab;
				}
				first = _buffer[_consumed];
			}
			TokenStartIndex = _consumed;
			if (_tokenType != 0)
			{
				if (first == 47)
				{
					retVal = ConsumeNextTokenOrRollback(first);
				}
				else if (_tokenType == JsonTokenType.StartObject)
				{
					if (first == 125)
					{
						EndObject();
						goto IL_01a9;
					}
					if (first != 34)
					{
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
					}
					int prevConsumed = _consumed;
					long prevPosition = _bytePositionInLine;
					long prevLineNumber = _lineNumber;
					retVal = ConsumePropertyName();
					if (!retVal)
					{
						_consumed = prevConsumed;
						_tokenType = JsonTokenType.StartObject;
						_bytePositionInLine = prevPosition;
						_lineNumber = prevLineNumber;
					}
				}
				else if (_tokenType != JsonTokenType.StartArray)
				{
					retVal = ((_tokenType != JsonTokenType.PropertyName) ? ConsumeNextTokenOrRollback(first) : ConsumeValue(first));
				}
				else
				{
					if (first == 93)
					{
						EndArray();
						goto IL_01a9;
					}
					retVal = ConsumeValue(first);
				}
			}
			else
			{
				retVal = ReadFirstToken(first);
			}
		}
		goto IL_01ab;
		IL_01ab:
		return retVal;
		IL_01a9:
		retVal = true;
		goto IL_01ab;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private bool HasMoreData()
	{
		if (_consumed >= (uint)_buffer.Length)
		{
			if (_isNotPrimitive && IsLastSpan)
			{
				if (_bitStack.CurrentDepth != 0)
				{
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ZeroDepthAtEnd, 0);
				}
				if (_readerOptions.CommentHandling == JsonCommentHandling.Allow && _tokenType == JsonTokenType.Comment)
				{
					return false;
				}
				if (_tokenType != JsonTokenType.EndArray && _tokenType != JsonTokenType.EndObject)
				{
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidEndOfJsonNonPrimitive, 0);
				}
			}
			return false;
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private bool HasMoreData(ExceptionResource resource)
	{
		if (_consumed >= (uint)_buffer.Length)
		{
			if (IsLastSpan)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, resource, 0);
			}
			return false;
		}
		return true;
	}

	private bool ReadFirstToken(byte first)
	{
		switch (first)
		{
		case 123:
			_bitStack.SetFirstBit();
			_tokenType = JsonTokenType.StartObject;
			ValueSpan = _buffer.Slice(_consumed, 1);
			_consumed++;
			_bytePositionInLine++;
			_inObject = true;
			_isNotPrimitive = true;
			break;
		case 91:
			_bitStack.ResetFirstBit();
			_tokenType = JsonTokenType.StartArray;
			ValueSpan = _buffer.Slice(_consumed, 1);
			_consumed++;
			_bytePositionInLine++;
			_isNotPrimitive = true;
			break;
		default:
		{
			ReadOnlySpan<byte> localBuffer = _buffer;
			if (JsonHelpers.IsDigit(first) || first == 45)
			{
				if (!TryGetNumber(localBuffer.Slice(_consumed), out var numberOfBytes))
				{
					return false;
				}
				_tokenType = JsonTokenType.Number;
				_consumed += numberOfBytes;
				_bytePositionInLine += numberOfBytes;
				return true;
			}
			if (!ConsumeValue(first))
			{
				return false;
			}
			if (_tokenType == JsonTokenType.StartObject || _tokenType == JsonTokenType.StartArray)
			{
				_isNotPrimitive = true;
			}
			break;
		}
		}
		return true;
	}

	private void SkipWhiteSpace()
	{
		ReadOnlySpan<byte> localBuffer = _buffer;
		while (_consumed < localBuffer.Length)
		{
			byte val = localBuffer[_consumed];
			if (val != 32 && val != 13 && val != 10 && val != 9)
			{
				break;
			}
			if (val == 10)
			{
				_lineNumber++;
				_bytePositionInLine = 0L;
			}
			else
			{
				_bytePositionInLine++;
			}
			_consumed++;
		}
	}

	private bool ConsumeValue(byte marker)
	{
		while (true)
		{
			Debug.Assert((_trailingCommaBeforeComment && _readerOptions.CommentHandling == JsonCommentHandling.Allow) || !_trailingCommaBeforeComment);
			Debug.Assert((_trailingCommaBeforeComment && marker != 47) || !_trailingCommaBeforeComment);
			_trailingCommaBeforeComment = false;
			switch (marker)
			{
			case 34:
				return ConsumeString();
			case 123:
				StartObject();
				break;
			case 91:
				StartArray();
				break;
			default:
				if (JsonHelpers.IsDigit(marker) || marker == 45)
				{
					return ConsumeNumber();
				}
				switch (marker)
				{
				case 102:
					return ConsumeLiteral(JsonConstants.FalseValue, JsonTokenType.False);
				case 116:
					return ConsumeLiteral(JsonConstants.TrueValue, JsonTokenType.True);
				case 110:
					return ConsumeLiteral(JsonConstants.NullValue, JsonTokenType.Null);
				}
				switch (_readerOptions.CommentHandling)
				{
				case JsonCommentHandling.Allow:
					if (marker == 47)
					{
						return ConsumeComment();
					}
					break;
				default:
					Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Skip);
					if (marker != 47)
					{
						break;
					}
					if (SkipComment())
					{
						if (_consumed >= (uint)_buffer.Length)
						{
							if (_isNotPrimitive && IsLastSpan && _tokenType != JsonTokenType.EndArray && _tokenType != JsonTokenType.EndObject)
							{
								ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidEndOfJsonNonPrimitive, 0);
							}
							return false;
						}
						marker = _buffer[_consumed];
						if (marker <= 32)
						{
							SkipWhiteSpace();
							if (!HasMoreData())
							{
								return false;
							}
							marker = _buffer[_consumed];
						}
						goto IL_024a;
					}
					return false;
				case JsonCommentHandling.Disallow:
					break;
				}
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, marker);
				break;
			}
			break;
			IL_024a:
			TokenStartIndex = _consumed;
		}
		return true;
	}

	private bool ConsumeLiteral(ReadOnlySpan<byte> literal, JsonTokenType tokenType)
	{
		ReadOnlySpan<byte> span = _buffer.Slice(_consumed);
		Debug.Assert(span.Length > 0);
		Debug.Assert(span[0] == 110 || span[0] == 116 || span[0] == 102);
		if (!span.StartsWith(literal))
		{
			return CheckLiteral(span, literal);
		}
		ValueSpan = span.Slice(0, literal.Length);
		_tokenType = tokenType;
		_consumed += literal.Length;
		_bytePositionInLine += literal.Length;
		return true;
	}

	private bool CheckLiteral(ReadOnlySpan<byte> span, ReadOnlySpan<byte> literal)
	{
		Debug.Assert(span.Length > 0 && span[0] == literal[0]);
		int indexOfFirstMismatch = 0;
		for (int i = 1; i < literal.Length; i++)
		{
			if (span.Length > i)
			{
				if (span[i] != literal[i])
				{
					_bytePositionInLine += i;
					ThrowInvalidLiteral(span);
				}
				continue;
			}
			indexOfFirstMismatch = i;
			break;
		}
		Debug.Assert(indexOfFirstMismatch > 0 && indexOfFirstMismatch < literal.Length);
		if (IsLastSpan)
		{
			_bytePositionInLine += indexOfFirstMismatch;
			ThrowInvalidLiteral(span);
		}
		return false;
	}

	private void ThrowInvalidLiteral(ReadOnlySpan<byte> span)
	{
		byte firstByte = span[0];
		ExceptionResource resource;
		switch (firstByte)
		{
		case 116:
			resource = ExceptionResource.ExpectedTrue;
			break;
		case 102:
			resource = ExceptionResource.ExpectedFalse;
			break;
		default:
			Debug.Assert(firstByte == 110);
			resource = ExceptionResource.ExpectedNull;
			break;
		}
		ThrowHelper.ThrowJsonReaderException(ref this, resource, 0, span);
	}

	private bool ConsumeNumber()
	{
		if (!TryGetNumber(_buffer.Slice(_consumed), out var consumed))
		{
			return false;
		}
		_tokenType = JsonTokenType.Number;
		_consumed += consumed;
		_bytePositionInLine += consumed;
		if (_consumed >= (uint)_buffer.Length)
		{
			Debug.Assert(IsLastSpan);
			if (_isNotPrimitive)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, _buffer[_consumed - 1]);
			}
		}
		Debug.Assert((_consumed < _buffer.Length && !_isNotPrimitive && JsonConstants.Delimiters.IndexOf(_buffer[_consumed]) >= 0) || (_isNotPrimitive ^ (_consumed >= (uint)_buffer.Length)));
		return true;
	}

	private bool ConsumePropertyName()
	{
		_trailingCommaBeforeComment = false;
		if (!ConsumeString())
		{
			return false;
		}
		if (!HasMoreData(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
		{
			return false;
		}
		byte first = _buffer[_consumed];
		if (first <= 32)
		{
			SkipWhiteSpace();
			if (!HasMoreData(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
			{
				return false;
			}
			first = _buffer[_consumed];
		}
		if (first != 58)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedSeparatorAfterPropertyNameNotFound, first);
		}
		_consumed++;
		_bytePositionInLine++;
		_tokenType = JsonTokenType.PropertyName;
		return true;
	}

	private bool ConsumeString()
	{
		Debug.Assert(_buffer.Length >= _consumed + 1);
		Debug.Assert(_buffer[_consumed] == 34);
		ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);
		int idx = localBuffer.IndexOfQuoteOrAnyControlOrBackSlash();
		if (idx >= 0)
		{
			byte foundByte = localBuffer[idx];
			if (foundByte == 34)
			{
				_bytePositionInLine += idx + 2;
				ValueSpan = localBuffer.Slice(0, idx);
				_stringHasEscaping = false;
				_tokenType = JsonTokenType.String;
				_consumed += idx + 2;
				return true;
			}
			return ConsumeStringAndValidate(localBuffer, idx);
		}
		if (IsLastSpan)
		{
			_bytePositionInLine += localBuffer.Length + 1;
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound, 0);
		}
		return false;
	}

	private bool ConsumeStringAndValidate(ReadOnlySpan<byte> data, int idx)
	{
		Debug.Assert(idx >= 0 && idx < data.Length);
		Debug.Assert(data[idx] != 34);
		Debug.Assert(data[idx] == 92 || data[idx] < 32);
		long prevLineBytePosition = _bytePositionInLine;
		long prevLineNumber = _lineNumber;
		_bytePositionInLine += idx + 1;
		bool nextCharEscaped = false;
		while (true)
		{
			if (idx < data.Length)
			{
				byte currentByte = data[idx];
				if (currentByte == 34)
				{
					if (!nextCharEscaped)
					{
						break;
					}
					nextCharEscaped = false;
				}
				else if (currentByte == 92)
				{
					nextCharEscaped = !nextCharEscaped;
				}
				else if (nextCharEscaped)
				{
					int index = JsonConstants.EscapableChars.IndexOf(currentByte);
					if (index == -1)
					{
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterAfterEscapeWithinString, currentByte);
					}
					if (currentByte == 117)
					{
						_bytePositionInLine++;
						if (!ValidateHexDigits(data, idx + 1))
						{
							idx = data.Length;
							goto IL_0182;
						}
						idx += 4;
					}
					nextCharEscaped = false;
				}
				else if (currentByte < 32)
				{
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterWithinString, currentByte);
				}
				_bytePositionInLine++;
				idx++;
				continue;
			}
			goto IL_0182;
			IL_0182:
			if (idx < data.Length)
			{
				break;
			}
			if (IsLastSpan)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound, 0);
			}
			_lineNumber = prevLineNumber;
			_bytePositionInLine = prevLineBytePosition;
			return false;
		}
		_bytePositionInLine++;
		ValueSpan = data.Slice(0, idx);
		_stringHasEscaping = true;
		_tokenType = JsonTokenType.String;
		_consumed += idx + 2;
		return true;
	}

	private bool ValidateHexDigits(ReadOnlySpan<byte> data, int idx)
	{
		for (int i = idx; i < data.Length; i++)
		{
			byte nextByte = data[i];
			if (!JsonReaderHelper.IsHexDigit(nextByte))
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidHexCharacterWithinString, nextByte);
			}
			if (i - idx >= 3)
			{
				return true;
			}
			_bytePositionInLine++;
		}
		return false;
	}

	private bool TryGetNumber(ReadOnlySpan<byte> data, out int consumed)
	{
		Debug.Assert(data.Length > 0);
		_numberFormat = '\0';
		consumed = 0;
		int i = 0;
		ConsumeNumberResult signResult = ConsumeNegativeSign(ref data, ref i);
		if (signResult == ConsumeNumberResult.NeedMoreData)
		{
			return false;
		}
		Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);
		byte nextByte = data[i];
		Debug.Assert(nextByte >= 48 && nextByte <= 57);
		if (nextByte == 48)
		{
			ConsumeNumberResult result2 = ConsumeZero(ref data, ref i);
			if (result2 == ConsumeNumberResult.NeedMoreData)
			{
				return false;
			}
			if (result2 != 0)
			{
				Debug.Assert(result2 == ConsumeNumberResult.OperationIncomplete);
				nextByte = data[i];
				goto IL_0148;
			}
		}
		else
		{
			i++;
			ConsumeNumberResult result = ConsumeIntegerDigits(ref data, ref i);
			if (result == ConsumeNumberResult.NeedMoreData)
			{
				return false;
			}
			if (result != 0)
			{
				Debug.Assert(result == ConsumeNumberResult.OperationIncomplete);
				nextByte = data[i];
				if (nextByte != 46 && nextByte != 69 && nextByte != 101)
				{
					_bytePositionInLine += i;
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, nextByte);
				}
				goto IL_0148;
			}
		}
		goto IL_029c;
		IL_029c:
		ValueSpan = data.Slice(0, i);
		consumed = i;
		return true;
		IL_0148:
		Debug.Assert(nextByte == 46 || nextByte == 69 || nextByte == 101);
		if (nextByte == 46)
		{
			i++;
			ConsumeNumberResult result3 = ConsumeDecimalDigits(ref data, ref i);
			if (result3 == ConsumeNumberResult.NeedMoreData)
			{
				return false;
			}
			if (result3 == ConsumeNumberResult.Success)
			{
				goto IL_029c;
			}
			Debug.Assert(result3 == ConsumeNumberResult.OperationIncomplete);
			nextByte = data[i];
			if (nextByte != 69 && nextByte != 101)
			{
				_bytePositionInLine += i;
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedNextDigitEValueNotFound, nextByte);
			}
		}
		Debug.Assert(nextByte == 69 || nextByte == 101);
		i++;
		_numberFormat = 'e';
		signResult = ConsumeSign(ref data, ref i);
		if (signResult == ConsumeNumberResult.NeedMoreData)
		{
			return false;
		}
		Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);
		i++;
		ConsumeNumberResult resultExponent = ConsumeIntegerDigits(ref data, ref i);
		switch (resultExponent)
		{
		case ConsumeNumberResult.NeedMoreData:
			return false;
		default:
			Debug.Assert(resultExponent == ConsumeNumberResult.OperationIncomplete);
			_bytePositionInLine += i;
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, data[i]);
			break;
		case ConsumeNumberResult.Success:
			break;
		}
		goto IL_029c;
	}

	private ConsumeNumberResult ConsumeNegativeSign(ref ReadOnlySpan<byte> data, ref int i)
	{
		byte nextByte = data[i];
		if (nextByte == 45)
		{
			i++;
			if (i >= data.Length)
			{
				if (IsLastSpan)
				{
					_bytePositionInLine += i;
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
				}
				return ConsumeNumberResult.NeedMoreData;
			}
			nextByte = data[i];
			if (!JsonHelpers.IsDigit(nextByte))
			{
				_bytePositionInLine += i;
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
			}
		}
		return ConsumeNumberResult.OperationIncomplete;
	}

	private ConsumeNumberResult ConsumeZero(ref ReadOnlySpan<byte> data, ref int i)
	{
		Debug.Assert(data[i] == 48);
		i++;
		byte nextByte = 0;
		if (i < data.Length)
		{
			nextByte = data[i];
			if (JsonConstants.Delimiters.IndexOf(nextByte) >= 0)
			{
				return ConsumeNumberResult.Success;
			}
			nextByte = data[i];
			if (nextByte != 46 && nextByte != 69 && nextByte != 101)
			{
				_bytePositionInLine += i;
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, nextByte);
			}
			return ConsumeNumberResult.OperationIncomplete;
		}
		if (IsLastSpan)
		{
			return ConsumeNumberResult.Success;
		}
		return ConsumeNumberResult.NeedMoreData;
	}

	private ConsumeNumberResult ConsumeIntegerDigits(ref ReadOnlySpan<byte> data, ref int i)
	{
		byte nextByte = 0;
		while (i < data.Length)
		{
			nextByte = data[i];
			if (!JsonHelpers.IsDigit(nextByte))
			{
				break;
			}
			i++;
		}
		if (i >= data.Length)
		{
			if (IsLastSpan)
			{
				return ConsumeNumberResult.Success;
			}
			return ConsumeNumberResult.NeedMoreData;
		}
		if (JsonConstants.Delimiters.IndexOf(nextByte) >= 0)
		{
			return ConsumeNumberResult.Success;
		}
		return ConsumeNumberResult.OperationIncomplete;
	}

	private ConsumeNumberResult ConsumeDecimalDigits(ref ReadOnlySpan<byte> data, ref int i)
	{
		if (i >= data.Length)
		{
			if (IsLastSpan)
			{
				_bytePositionInLine += i;
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
			}
			return ConsumeNumberResult.NeedMoreData;
		}
		byte nextByte = data[i];
		if (!JsonHelpers.IsDigit(nextByte))
		{
			_bytePositionInLine += i;
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterDecimal, nextByte);
		}
		i++;
		return ConsumeIntegerDigits(ref data, ref i);
	}

	private ConsumeNumberResult ConsumeSign(ref ReadOnlySpan<byte> data, ref int i)
	{
		if (i >= data.Length)
		{
			if (IsLastSpan)
			{
				_bytePositionInLine += i;
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
			}
			return ConsumeNumberResult.NeedMoreData;
		}
		byte nextByte = data[i];
		if (nextByte == 43 || nextByte == 45)
		{
			i++;
			if (i >= data.Length)
			{
				if (IsLastSpan)
				{
					_bytePositionInLine += i;
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
				}
				return ConsumeNumberResult.NeedMoreData;
			}
			nextByte = data[i];
		}
		if (!JsonHelpers.IsDigit(nextByte))
		{
			_bytePositionInLine += i;
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
		}
		return ConsumeNumberResult.OperationIncomplete;
	}

	private bool ConsumeNextTokenOrRollback(byte marker)
	{
		int prevConsumed = _consumed;
		long prevPosition = _bytePositionInLine;
		long prevLineNumber = _lineNumber;
		JsonTokenType prevTokenType = _tokenType;
		bool prevTrailingCommaBeforeComment = _trailingCommaBeforeComment;
		switch (ConsumeNextToken(marker))
		{
		case ConsumeTokenResult.Success:
			return true;
		case ConsumeTokenResult.NotEnoughDataRollBackState:
			_consumed = prevConsumed;
			_tokenType = prevTokenType;
			_bytePositionInLine = prevPosition;
			_lineNumber = prevLineNumber;
			_trailingCommaBeforeComment = prevTrailingCommaBeforeComment;
			break;
		}
		return false;
	}

	private ConsumeTokenResult ConsumeNextToken(byte marker)
	{
		if (_readerOptions.CommentHandling != 0)
		{
			if (_readerOptions.CommentHandling != JsonCommentHandling.Allow)
			{
				Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Skip);
				return ConsumeNextTokenUntilAfterAllCommentsAreSkipped(marker);
			}
			if (marker == 47)
			{
				return (!ConsumeComment()) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
			}
			if (_tokenType == JsonTokenType.Comment)
			{
				return ConsumeNextTokenFromLastNonCommentToken();
			}
		}
		if (_bitStack.CurrentDepth == 0)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleJson, marker);
		}
		switch (marker)
		{
		case 44:
		{
			_consumed++;
			_bytePositionInLine++;
			if (_consumed >= (uint)_buffer.Length)
			{
				if (IsLastSpan)
				{
					_consumed--;
					_bytePositionInLine--;
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound, 0);
				}
				return ConsumeTokenResult.NotEnoughDataRollBackState;
			}
			byte first = _buffer[_consumed];
			if (first <= 32)
			{
				SkipWhiteSpace();
				if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
				{
					return ConsumeTokenResult.NotEnoughDataRollBackState;
				}
				first = _buffer[_consumed];
			}
			TokenStartIndex = _consumed;
			if (_readerOptions.CommentHandling == JsonCommentHandling.Allow && first == 47)
			{
				_trailingCommaBeforeComment = true;
				return (!ConsumeComment()) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
			}
			if (_inObject)
			{
				if (first != 34)
				{
					if (first == 125)
					{
						if (_readerOptions.AllowTrailingCommas)
						{
							EndObject();
							return ConsumeTokenResult.Success;
						}
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd, 0);
					}
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
				}
				return (!ConsumePropertyName()) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
			}
			if (first == 93)
			{
				if (_readerOptions.AllowTrailingCommas)
				{
					EndArray();
					return ConsumeTokenResult.Success;
				}
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd, 0);
			}
			return (!ConsumeValue(first)) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
		}
		case 125:
			EndObject();
			break;
		case 93:
			EndArray();
			break;
		default:
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.FoundInvalidCharacter, marker);
			break;
		}
		return ConsumeTokenResult.Success;
	}

	private ConsumeTokenResult ConsumeNextTokenFromLastNonCommentToken()
	{
		Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Allow);
		Debug.Assert(_tokenType == JsonTokenType.Comment);
		if (JsonReaderHelper.IsTokenTypePrimitive(_previousTokenType))
		{
			_tokenType = (_inObject ? JsonTokenType.StartObject : JsonTokenType.StartArray);
		}
		else
		{
			_tokenType = _previousTokenType;
		}
		Debug.Assert(_tokenType != JsonTokenType.Comment);
		if (HasMoreData())
		{
			byte first = _buffer[_consumed];
			if (first <= 32)
			{
				SkipWhiteSpace();
				if (!HasMoreData())
				{
					goto IL_053a;
				}
				first = _buffer[_consumed];
			}
			if (_bitStack.CurrentDepth == 0 && _tokenType != 0)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleJson, first);
			}
			Debug.Assert(first != 47);
			TokenStartIndex = _consumed;
			if (first != 44)
			{
				if (first == 125)
				{
					EndObject();
				}
				else
				{
					if (first != 93)
					{
						if (_tokenType == JsonTokenType.None)
						{
							if (ReadFirstToken(first))
							{
								goto IL_0534;
							}
						}
						else if (_tokenType == JsonTokenType.StartObject)
						{
							Debug.Assert(first != 125);
							if (first != 34)
							{
								ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
							}
							int prevConsumed = _consumed;
							long prevPosition = _bytePositionInLine;
							long prevLineNumber = _lineNumber;
							if (ConsumePropertyName())
							{
								goto IL_0534;
							}
							_consumed = prevConsumed;
							_tokenType = JsonTokenType.StartObject;
							_bytePositionInLine = prevPosition;
							_lineNumber = prevLineNumber;
						}
						else if (_tokenType == JsonTokenType.StartArray)
						{
							Debug.Assert(first != 93);
							if (ConsumeValue(first))
							{
								goto IL_0534;
							}
						}
						else if (_tokenType == JsonTokenType.PropertyName)
						{
							if (ConsumeValue(first))
							{
								goto IL_0534;
							}
						}
						else
						{
							Debug.Assert(_tokenType == JsonTokenType.EndArray || _tokenType == JsonTokenType.EndObject);
							if (_inObject)
							{
								Debug.Assert(first != 125);
								if (first != 34)
								{
									ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
								}
								if (ConsumePropertyName())
								{
									goto IL_0534;
								}
							}
							else
							{
								Debug.Assert(first != 93);
								if (ConsumeValue(first))
								{
									goto IL_0534;
								}
							}
						}
						goto IL_053a;
					}
					EndArray();
				}
				goto IL_0534;
			}
			if ((int)_previousTokenType <= 1 || _previousTokenType == JsonTokenType.StartArray || _trailingCommaBeforeComment)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueAfterComment, first);
			}
			_consumed++;
			_bytePositionInLine++;
			if (_consumed >= (uint)_buffer.Length)
			{
				if (IsLastSpan)
				{
					_consumed--;
					_bytePositionInLine--;
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound, 0);
				}
			}
			else
			{
				first = _buffer[_consumed];
				if (first <= 32)
				{
					SkipWhiteSpace();
					if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
					{
						goto IL_053a;
					}
					first = _buffer[_consumed];
				}
				TokenStartIndex = _consumed;
				if (first == 47)
				{
					_trailingCommaBeforeComment = true;
					if (ConsumeComment())
					{
						goto IL_0534;
					}
				}
				else if (_inObject)
				{
					if (first != 34)
					{
						if (first == 125)
						{
							if (_readerOptions.AllowTrailingCommas)
							{
								EndObject();
								goto IL_0534;
							}
							ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd, 0);
						}
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
					}
					if (ConsumePropertyName())
					{
						goto IL_0534;
					}
				}
				else
				{
					if (first == 93)
					{
						if (_readerOptions.AllowTrailingCommas)
						{
							EndArray();
							goto IL_0534;
						}
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd, 0);
					}
					if (ConsumeValue(first))
					{
						goto IL_0534;
					}
				}
			}
		}
		goto IL_053a;
		IL_0534:
		return ConsumeTokenResult.Success;
		IL_053a:
		return ConsumeTokenResult.NotEnoughDataRollBackState;
	}

	private bool SkipAllComments(ref byte marker)
	{
		while (true)
		{
			if (marker == 47)
			{
				if (!SkipComment() || !HasMoreData())
				{
					break;
				}
				marker = _buffer[_consumed];
				if (marker <= 32)
				{
					SkipWhiteSpace();
					if (!HasMoreData())
					{
						break;
					}
					marker = _buffer[_consumed];
				}
				continue;
			}
			return true;
		}
		return false;
	}

	private bool SkipAllComments(ref byte marker, ExceptionResource resource)
	{
		while (true)
		{
			if (marker == 47)
			{
				if (!SkipComment() || !HasMoreData(resource))
				{
					break;
				}
				marker = _buffer[_consumed];
				if (marker <= 32)
				{
					SkipWhiteSpace();
					if (!HasMoreData(resource))
					{
						break;
					}
					marker = _buffer[_consumed];
				}
				continue;
			}
			return true;
		}
		return false;
	}

	private ConsumeTokenResult ConsumeNextTokenUntilAfterAllCommentsAreSkipped(byte marker)
	{
		if (!SkipAllComments(ref marker))
		{
			goto IL_038f;
		}
		TokenStartIndex = _consumed;
		if (_tokenType == JsonTokenType.StartObject)
		{
			if (marker == 125)
			{
				EndObject();
			}
			else
			{
				if (marker != 34)
				{
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, marker);
				}
				int prevConsumed = _consumed;
				long prevPosition = _bytePositionInLine;
				long prevLineNumber = _lineNumber;
				if (!ConsumePropertyName())
				{
					_consumed = prevConsumed;
					_tokenType = JsonTokenType.StartObject;
					_bytePositionInLine = prevPosition;
					_lineNumber = prevLineNumber;
					goto IL_038f;
				}
			}
		}
		else if (_tokenType == JsonTokenType.StartArray)
		{
			if (marker == 93)
			{
				EndArray();
			}
			else if (!ConsumeValue(marker))
			{
				goto IL_038f;
			}
		}
		else if (_tokenType == JsonTokenType.PropertyName)
		{
			if (!ConsumeValue(marker))
			{
				goto IL_038f;
			}
		}
		else if (_bitStack.CurrentDepth == 0)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleJson, marker);
		}
		else
		{
			switch (marker)
			{
			case 44:
				_consumed++;
				_bytePositionInLine++;
				if (_consumed >= (uint)_buffer.Length)
				{
					if (IsLastSpan)
					{
						_consumed--;
						_bytePositionInLine--;
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound, 0);
					}
					return ConsumeTokenResult.NotEnoughDataRollBackState;
				}
				marker = _buffer[_consumed];
				if (marker <= 32)
				{
					SkipWhiteSpace();
					if (!HasMoreData(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
					{
						return ConsumeTokenResult.NotEnoughDataRollBackState;
					}
					marker = _buffer[_consumed];
				}
				if (SkipAllComments(ref marker, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
				{
					TokenStartIndex = _consumed;
					if (_inObject)
					{
						if (marker != 34)
						{
							if (marker == 125)
							{
								if (_readerOptions.AllowTrailingCommas)
								{
									EndObject();
									break;
								}
								ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd, 0);
							}
							ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, marker);
						}
						return (!ConsumePropertyName()) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
					}
					if (marker == 93)
					{
						if (_readerOptions.AllowTrailingCommas)
						{
							EndArray();
							break;
						}
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd, 0);
					}
					return (!ConsumeValue(marker)) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
				}
				return ConsumeTokenResult.NotEnoughDataRollBackState;
			case 125:
				EndObject();
				break;
			case 93:
				EndArray();
				break;
			default:
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.FoundInvalidCharacter, marker);
				break;
			}
		}
		return ConsumeTokenResult.Success;
		IL_038f:
		return ConsumeTokenResult.IncompleteNoRollBackNecessary;
	}

	private bool SkipComment()
	{
		ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);
		if (localBuffer.Length > 0)
		{
			int idx;
			switch (localBuffer[0])
			{
			case 47:
				return SkipSingleLineComment(localBuffer.Slice(1), out idx);
			case 42:
				return SkipMultiLineComment(localBuffer.Slice(1), out idx);
			}
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, 47);
		}
		if (IsLastSpan)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, 47);
		}
		return false;
	}

	private bool SkipSingleLineComment(ReadOnlySpan<byte> localBuffer, out int idx)
	{
		idx = FindLineSeparator(localBuffer);
		int toConsume = 0;
		if (idx != -1)
		{
			toConsume = idx;
			if (localBuffer[idx] != 10)
			{
				Debug.Assert(localBuffer[idx] == 13);
				if (idx < localBuffer.Length - 1)
				{
					if (localBuffer[idx + 1] == 10)
					{
						toConsume++;
					}
				}
				else if (!IsLastSpan)
				{
					return false;
				}
			}
			toConsume++;
			_bytePositionInLine = 0L;
			_lineNumber++;
		}
		else
		{
			if (!IsLastSpan)
			{
				return false;
			}
			idx = localBuffer.Length;
			toConsume = idx;
			_bytePositionInLine += 2 + localBuffer.Length;
		}
		_consumed += 2 + toConsume;
		return true;
	}

	private int FindLineSeparator(ReadOnlySpan<byte> localBuffer)
	{
		int totalIdx = 0;
		while (true)
		{
			int idx = localBuffer.IndexOfAny<byte>(10, 13, 226);
			if (idx == -1)
			{
				return -1;
			}
			totalIdx += idx;
			if (localBuffer[idx] != 226)
			{
				break;
			}
			totalIdx++;
			localBuffer = localBuffer.Slice(idx + 1);
			ThrowOnDangerousLineSeparator(localBuffer);
		}
		return totalIdx;
	}

	private void ThrowOnDangerousLineSeparator(ReadOnlySpan<byte> localBuffer)
	{
		if (localBuffer.Length >= 2)
		{
			byte next = localBuffer[1];
			if (localBuffer[0] == 128 && (next == 168 || next == 169))
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfLineSeparator, 0);
			}
		}
	}

	private bool SkipMultiLineComment(ReadOnlySpan<byte> localBuffer, out int idx)
	{
		idx = 0;
		while (true)
		{
			int foundIdx = localBuffer.Slice(idx).IndexOf<byte>(47);
			switch (foundIdx)
			{
			case -1:
				if (IsLastSpan)
				{
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfCommentNotFound, 0);
				}
				return false;
			default:
				if (localBuffer[foundIdx + idx - 1] == 42)
				{
					idx += foundIdx - 1;
					_consumed += 4 + idx;
					var (newLines, newLineIndex) = JsonReaderHelper.CountNewLines(localBuffer.Slice(0, idx));
					_lineNumber += newLines;
					if (newLineIndex != -1)
					{
						_bytePositionInLine = idx - newLineIndex + 1;
					}
					else
					{
						_bytePositionInLine += 4 + idx;
					}
					return true;
				}
				break;
			case 0:
				break;
			}
			idx += foundIdx + 1;
		}
	}

	private bool ConsumeComment()
	{
		ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);
		if (localBuffer.Length > 0)
		{
			byte marker = localBuffer[0];
			switch (marker)
			{
			case 47:
				return ConsumeSingleLineComment(localBuffer.Slice(1), _consumed);
			case 42:
				return ConsumeMultiLineComment(localBuffer.Slice(1), _consumed);
			}
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterAtStartOfComment, marker);
		}
		if (IsLastSpan)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment, 0);
		}
		return false;
	}

	private bool ConsumeSingleLineComment(ReadOnlySpan<byte> localBuffer, int previousConsumed)
	{
		if (!SkipSingleLineComment(localBuffer, out var idx))
		{
			return false;
		}
		ValueSpan = _buffer.Slice(previousConsumed + 2, idx);
		if (_tokenType != JsonTokenType.Comment)
		{
			_previousTokenType = _tokenType;
		}
		_tokenType = JsonTokenType.Comment;
		return true;
	}

	private bool ConsumeMultiLineComment(ReadOnlySpan<byte> localBuffer, int previousConsumed)
	{
		if (!SkipMultiLineComment(localBuffer, out var idx))
		{
			return false;
		}
		ValueSpan = _buffer.Slice(previousConsumed + 2, idx);
		if (_tokenType != JsonTokenType.Comment)
		{
			_previousTokenType = _tokenType;
		}
		_tokenType = JsonTokenType.Comment;
		return true;
	}

	public Utf8JsonReader(ReadOnlySequence<byte> jsonData, bool isFinalBlock, JsonReaderState state)
	{
		ReadOnlyMemory<byte> memory2 = jsonData.First;
		_buffer = memory2.Span;
		_isFinalBlock = isFinalBlock;
		_isInputSequence = true;
		_lineNumber = state._lineNumber;
		_bytePositionInLine = state._bytePositionInLine;
		_inObject = state._inObject;
		_isNotPrimitive = state._isNotPrimitive;
		_numberFormat = state._numberFormat;
		_stringHasEscaping = state._stringHasEscaping;
		_trailingCommaBeforeComment = state._trailingCommaBeforeComment;
		_tokenType = state._tokenType;
		_previousTokenType = state._previousTokenType;
		_readerOptions = state._readerOptions;
		if (_readerOptions.MaxDepth == 0)
		{
			_readerOptions.MaxDepth = 64;
		}
		_bitStack = state._bitStack;
		_consumed = 0;
		TokenStartIndex = 0L;
		_totalConsumed = 0L;
		ValueSpan = ReadOnlySpan<byte>.Empty;
		_sequence = jsonData;
		HasValueSequence = false;
		ValueSequence = ReadOnlySequence<byte>.Empty;
		if (jsonData.IsSingleSegment)
		{
			_nextPosition = default(SequencePosition);
			_currentPosition = jsonData.Start;
			_isLastSegment = isFinalBlock;
			_isMultiSegment = false;
			return;
		}
		_currentPosition = jsonData.Start;
		_nextPosition = _currentPosition;
		bool firstSegmentIsEmpty = _buffer.Length == 0;
		if (firstSegmentIsEmpty)
		{
			SequencePosition previousNextPosition = _nextPosition;
			ReadOnlyMemory<byte> memory;
			while (jsonData.TryGet(ref _nextPosition, out memory))
			{
				_currentPosition = previousNextPosition;
				if (memory.Length != 0)
				{
					_buffer = memory.Span;
					break;
				}
				previousNextPosition = _nextPosition;
			}
		}
		_isLastSegment = !jsonData.TryGet(ref _nextPosition, out memory2, !firstSegmentIsEmpty) && isFinalBlock;
		Debug.Assert(!_nextPosition.Equals(_currentPosition));
		_isMultiSegment = true;
	}

	public Utf8JsonReader(ReadOnlySequence<byte> jsonData, JsonReaderOptions options = default(JsonReaderOptions))
		: this(jsonData, isFinalBlock: true, new JsonReaderState(options))
	{
	}

	private bool ReadMultiSegment()
	{
		bool retVal = false;
		HasValueSequence = false;
		ValueSpan = default(ReadOnlySpan<byte>);
		ValueSequence = default(ReadOnlySequence<byte>);
		if (HasMoreDataMultiSegment())
		{
			byte first = _buffer[_consumed];
			if (first <= 32)
			{
				SkipWhiteSpaceMultiSegment();
				if (!HasMoreDataMultiSegment())
				{
					goto IL_01e7;
				}
				first = _buffer[_consumed];
			}
			TokenStartIndex = BytesConsumed;
			if (_tokenType != 0)
			{
				if (first == 47)
				{
					retVal = ConsumeNextTokenOrRollbackMultiSegment(first);
				}
				else if (_tokenType == JsonTokenType.StartObject)
				{
					if (first == 125)
					{
						EndObject();
						goto IL_01e5;
					}
					if (first != 34)
					{
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
					}
					long prevTotalConsumed = _totalConsumed;
					int prevConsumed = _consumed;
					long prevPosition = _bytePositionInLine;
					long prevLineNumber = _lineNumber;
					SequencePosition copy = _currentPosition;
					retVal = ConsumePropertyNameMultiSegment();
					if (!retVal)
					{
						_consumed = prevConsumed;
						_tokenType = JsonTokenType.StartObject;
						_bytePositionInLine = prevPosition;
						_lineNumber = prevLineNumber;
						_totalConsumed = prevTotalConsumed;
						_currentPosition = copy;
					}
				}
				else if (_tokenType != JsonTokenType.StartArray)
				{
					retVal = ((_tokenType != JsonTokenType.PropertyName) ? ConsumeNextTokenOrRollbackMultiSegment(first) : ConsumeValueMultiSegment(first));
				}
				else
				{
					if (first == 93)
					{
						EndArray();
						goto IL_01e5;
					}
					retVal = ConsumeValueMultiSegment(first);
				}
			}
			else
			{
				retVal = ReadFirstTokenMultiSegment(first);
			}
		}
		goto IL_01e7;
		IL_01e7:
		return retVal;
		IL_01e5:
		retVal = true;
		goto IL_01e7;
	}

	private bool ValidateStateAtEndOfData()
	{
		Debug.Assert(_isNotPrimitive && IsLastSpan);
		if (_bitStack.CurrentDepth != 0)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ZeroDepthAtEnd, 0);
		}
		if (_readerOptions.CommentHandling == JsonCommentHandling.Allow && _tokenType == JsonTokenType.Comment)
		{
			return false;
		}
		if (_tokenType != JsonTokenType.EndArray && _tokenType != JsonTokenType.EndObject)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidEndOfJsonNonPrimitive, 0);
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private bool HasMoreDataMultiSegment()
	{
		if (_consumed >= (uint)_buffer.Length)
		{
			if (_isNotPrimitive && IsLastSpan && !ValidateStateAtEndOfData())
			{
				return false;
			}
			if (!GetNextSpan())
			{
				if (_isNotPrimitive && IsLastSpan)
				{
					ValidateStateAtEndOfData();
				}
				return false;
			}
		}
		return true;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private bool HasMoreDataMultiSegment(ExceptionResource resource)
	{
		if (_consumed >= (uint)_buffer.Length)
		{
			if (IsLastSpan)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, resource, 0);
			}
			if (!GetNextSpan())
			{
				if (IsLastSpan)
				{
					ThrowHelper.ThrowJsonReaderException(ref this, resource, 0);
				}
				return false;
			}
		}
		return true;
	}

	private bool GetNextSpan()
	{
		ReadOnlyMemory<byte> memory = default(ReadOnlyMemory<byte>);
		while (true)
		{
			Debug.Assert(!_isMultiSegment || _currentPosition.GetObject() != null);
			SequencePosition copy = _currentPosition;
			_currentPosition = _nextPosition;
			if (!_sequence.TryGet(ref _nextPosition, out memory))
			{
				_currentPosition = copy;
				_isLastSegment = true;
				return false;
			}
			if (memory.Length != 0)
			{
				break;
			}
			_currentPosition = copy;
			Debug.Assert(!_isMultiSegment || _currentPosition.GetObject() != null);
		}
		if (_isFinalBlock)
		{
			_isLastSegment = !_sequence.TryGet(ref _nextPosition, out var _, advance: false);
		}
		_buffer = memory.Span;
		_totalConsumed += _consumed;
		_consumed = 0;
		return true;
	}

	private bool ReadFirstTokenMultiSegment(byte first)
	{
		switch (first)
		{
		case 123:
			_bitStack.SetFirstBit();
			_tokenType = JsonTokenType.StartObject;
			ValueSpan = _buffer.Slice(_consumed, 1);
			_consumed++;
			_bytePositionInLine++;
			_inObject = true;
			_isNotPrimitive = true;
			break;
		case 91:
			_bitStack.ResetFirstBit();
			_tokenType = JsonTokenType.StartArray;
			ValueSpan = _buffer.Slice(_consumed, 1);
			_consumed++;
			_bytePositionInLine++;
			_isNotPrimitive = true;
			break;
		default:
			if (JsonHelpers.IsDigit(first) || first == 45)
			{
				if (!TryGetNumberMultiSegment(_buffer.Slice(_consumed), out var numberOfBytes))
				{
					return false;
				}
				_tokenType = JsonTokenType.Number;
				_consumed += numberOfBytes;
				return true;
			}
			if (!ConsumeValueMultiSegment(first))
			{
				return false;
			}
			if (_tokenType == JsonTokenType.StartObject || _tokenType == JsonTokenType.StartArray)
			{
				_isNotPrimitive = true;
			}
			break;
		}
		return true;
	}

	private void SkipWhiteSpaceMultiSegment()
	{
		do
		{
			SkipWhiteSpace();
		}
		while (_consumed >= _buffer.Length && GetNextSpan());
	}

	private bool ConsumeValueMultiSegment(byte marker)
	{
		while (true)
		{
			Debug.Assert((_trailingCommaBeforeComment && _readerOptions.CommentHandling == JsonCommentHandling.Allow) || !_trailingCommaBeforeComment);
			Debug.Assert((_trailingCommaBeforeComment && marker != 47) || !_trailingCommaBeforeComment);
			_trailingCommaBeforeComment = false;
			switch (marker)
			{
			case 34:
				return ConsumeStringMultiSegment();
			case 123:
				StartObject();
				break;
			case 91:
				StartArray();
				break;
			default:
				if (JsonHelpers.IsDigit(marker) || marker == 45)
				{
					return ConsumeNumberMultiSegment();
				}
				switch (marker)
				{
				case 102:
					return ConsumeLiteralMultiSegment(JsonConstants.FalseValue, JsonTokenType.False);
				case 116:
					return ConsumeLiteralMultiSegment(JsonConstants.TrueValue, JsonTokenType.True);
				case 110:
					return ConsumeLiteralMultiSegment(JsonConstants.NullValue, JsonTokenType.Null);
				}
				switch (_readerOptions.CommentHandling)
				{
				case JsonCommentHandling.Allow:
					if (marker == 47)
					{
						SequencePosition copy = _currentPosition;
						if (!SkipOrConsumeCommentMultiSegmentWithRollback())
						{
							_currentPosition = copy;
							return false;
						}
						return true;
					}
					break;
				default:
				{
					Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Skip);
					if (marker != 47)
					{
						break;
					}
					SequencePosition copy2 = _currentPosition;
					if (SkipCommentMultiSegment(out var _))
					{
						if (_consumed >= (uint)_buffer.Length)
						{
							if (_isNotPrimitive && IsLastSpan && _tokenType != JsonTokenType.EndArray && _tokenType != JsonTokenType.EndObject)
							{
								ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidEndOfJsonNonPrimitive, 0);
							}
							if (!GetNextSpan())
							{
								if (_isNotPrimitive && IsLastSpan && _tokenType != JsonTokenType.EndArray && _tokenType != JsonTokenType.EndObject)
								{
									ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidEndOfJsonNonPrimitive, 0);
								}
								_currentPosition = copy2;
								return false;
							}
						}
						marker = _buffer[_consumed];
						if (marker <= 32)
						{
							SkipWhiteSpaceMultiSegment();
							if (!HasMoreDataMultiSegment())
							{
								_currentPosition = copy2;
								return false;
							}
							marker = _buffer[_consumed];
						}
						goto IL_02de;
					}
					_currentPosition = copy2;
					return false;
				}
				case JsonCommentHandling.Disallow:
					break;
				}
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfValueNotFound, marker);
				break;
			}
			break;
			IL_02de:
			TokenStartIndex = BytesConsumed;
		}
		return true;
	}

	private bool ConsumeLiteralMultiSegment(ReadOnlySpan<byte> literal, JsonTokenType tokenType)
	{
		ReadOnlySpan<byte> span = _buffer.Slice(_consumed);
		Debug.Assert(span.Length > 0);
		Debug.Assert(span[0] == 110 || span[0] == 116 || span[0] == 102);
		int consumed = literal.Length;
		if (!span.StartsWith(literal))
		{
			int prevConsumed = _consumed;
			if (!CheckLiteralMultiSegment(span, literal, out consumed))
			{
				_consumed = prevConsumed;
				return false;
			}
		}
		else
		{
			ValueSpan = span.Slice(0, literal.Length);
			HasValueSequence = false;
		}
		_tokenType = tokenType;
		_consumed += consumed;
		_bytePositionInLine += consumed;
		return true;
	}

	private bool CheckLiteralMultiSegment(ReadOnlySpan<byte> span, ReadOnlySpan<byte> literal, out int consumed)
	{
		Debug.Assert(span.Length > 0 && span[0] == literal[0]);
		Span<byte> readSoFar = stackalloc byte[literal.Length];
		int written = 0;
		long prevTotalConsumed = _totalConsumed;
		SequencePosition copy = _currentPosition;
		if (span.Length >= literal.Length || IsLastSpan)
		{
			_bytePositionInLine += FindMismatch(span, literal);
			int amountToWrite = Math.Min(span.Length, (int)_bytePositionInLine + 1);
			span.Slice(0, amountToWrite).CopyTo(readSoFar);
			written += amountToWrite;
		}
		else if (!literal.StartsWith(span))
		{
			_bytePositionInLine += FindMismatch(span, literal);
			int amountToWrite2 = Math.Min(span.Length, (int)_bytePositionInLine + 1);
			span.Slice(0, amountToWrite2).CopyTo(readSoFar);
			written += amountToWrite2;
		}
		else
		{
			ReadOnlySpan<byte> leftToMatch = literal.Slice(span.Length);
			SequencePosition startPosition = _currentPosition;
			int startConsumed = _consumed;
			int alreadyMatched = literal.Length - leftToMatch.Length;
			while (true)
			{
				_totalConsumed += alreadyMatched;
				_bytePositionInLine += alreadyMatched;
				if (!GetNextSpan())
				{
					_totalConsumed = prevTotalConsumed;
					consumed = 0;
					_currentPosition = copy;
					if (IsLastSpan)
					{
						break;
					}
					return false;
				}
				int amountToWrite3 = Math.Min(span.Length, readSoFar.Length - written);
				span.Slice(0, amountToWrite3).CopyTo(readSoFar.Slice(written));
				written += amountToWrite3;
				span = _buffer;
				if (span.StartsWith(leftToMatch))
				{
					HasValueSequence = true;
					SequencePosition start = new SequencePosition(startPosition.GetObject(), startPosition.GetInteger() + startConsumed);
					SequencePosition end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + leftToMatch.Length);
					ValueSequence = _sequence.Slice(start, end);
					consumed = leftToMatch.Length;
					return true;
				}
				if (!leftToMatch.StartsWith(span))
				{
					_bytePositionInLine += FindMismatch(span, leftToMatch);
					amountToWrite3 = Math.Min(span.Length, (int)_bytePositionInLine + 1);
					span.Slice(0, amountToWrite3).CopyTo(readSoFar.Slice(written));
					written += amountToWrite3;
					break;
				}
				leftToMatch = leftToMatch.Slice(span.Length);
				alreadyMatched = span.Length;
			}
		}
		_totalConsumed = prevTotalConsumed;
		consumed = 0;
		_currentPosition = copy;
		throw GetInvalidLiteralMultiSegment(readSoFar.Slice(0, written).ToArray());
	}

	private int FindMismatch(ReadOnlySpan<byte> span, ReadOnlySpan<byte> literal)
	{
		Debug.Assert(span.Length > 0);
		int indexOfFirstMismatch = 0;
		int minLength = Math.Min(span.Length, literal.Length);
		int i;
		for (i = 0; i < minLength && span[i] == literal[i]; i++)
		{
		}
		indexOfFirstMismatch = i;
		Debug.Assert(indexOfFirstMismatch >= 0 && indexOfFirstMismatch < literal.Length);
		return indexOfFirstMismatch;
	}

	private JsonException GetInvalidLiteralMultiSegment(ReadOnlySpan<byte> span)
	{
		byte firstByte = span[0];
		ExceptionResource resource;
		switch (firstByte)
		{
		case 116:
			resource = ExceptionResource.ExpectedTrue;
			break;
		case 102:
			resource = ExceptionResource.ExpectedFalse;
			break;
		default:
			Debug.Assert(firstByte == 110);
			resource = ExceptionResource.ExpectedNull;
			break;
		}
		return ThrowHelper.GetJsonReaderException(ref this, resource, 0, span);
	}

	private bool ConsumeNumberMultiSegment()
	{
		if (!TryGetNumberMultiSegment(_buffer.Slice(_consumed), out var consumed))
		{
			return false;
		}
		_tokenType = JsonTokenType.Number;
		_consumed += consumed;
		if (_consumed >= (uint)_buffer.Length)
		{
			Debug.Assert(IsLastSpan);
			if (_isNotPrimitive)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, _buffer[_consumed - 1]);
			}
		}
		Debug.Assert((_consumed < _buffer.Length && !_isNotPrimitive && JsonConstants.Delimiters.IndexOf(_buffer[_consumed]) >= 0) || (_isNotPrimitive ^ (_consumed >= (uint)_buffer.Length)));
		return true;
	}

	private bool ConsumePropertyNameMultiSegment()
	{
		_trailingCommaBeforeComment = false;
		if (!ConsumeStringMultiSegment())
		{
			return false;
		}
		if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
		{
			return false;
		}
		byte first = _buffer[_consumed];
		if (first <= 32)
		{
			SkipWhiteSpaceMultiSegment();
			if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedValueAfterPropertyNameNotFound))
			{
				return false;
			}
			first = _buffer[_consumed];
		}
		if (first != 58)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedSeparatorAfterPropertyNameNotFound, first);
		}
		_consumed++;
		_bytePositionInLine++;
		_tokenType = JsonTokenType.PropertyName;
		return true;
	}

	private bool ConsumeStringMultiSegment()
	{
		Debug.Assert(_buffer.Length >= _consumed + 1);
		Debug.Assert(_buffer[_consumed] == 34);
		ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed + 1);
		int idx = localBuffer.IndexOfQuoteOrAnyControlOrBackSlash();
		if (idx >= 0)
		{
			byte foundByte = localBuffer[idx];
			if (foundByte == 34)
			{
				_bytePositionInLine += idx + 2;
				ValueSpan = localBuffer.Slice(0, idx);
				HasValueSequence = false;
				_stringHasEscaping = false;
				_tokenType = JsonTokenType.String;
				_consumed += idx + 2;
				return true;
			}
			return ConsumeStringAndValidateMultiSegment(localBuffer, idx);
		}
		if (IsLastSpan)
		{
			_bytePositionInLine += localBuffer.Length + 1;
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound, 0);
		}
		return ConsumeStringNextSegment();
	}

	private bool ConsumeStringNextSegment()
	{
		PartialStateForRollback rollBackState = CaptureState();
		HasValueSequence = true;
		int leftOver = _buffer.Length - _consumed;
		ReadOnlySpan<byte> localBuffer;
		int idx;
		while (true)
		{
			if (!GetNextSpan())
			{
				if (IsLastSpan)
				{
					_bytePositionInLine += leftOver;
					RollBackState(in rollBackState, isError: true);
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound, 0);
				}
				RollBackState(in rollBackState);
				return false;
			}
			localBuffer = _buffer;
			idx = localBuffer.IndexOfQuoteOrAnyControlOrBackSlash();
			if (idx >= 0)
			{
				break;
			}
			_totalConsumed += localBuffer.Length;
			_bytePositionInLine += localBuffer.Length;
		}
		byte foundByte = localBuffer[idx];
		SequencePosition end;
		if (foundByte == 34)
		{
			end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + idx);
			_bytePositionInLine += leftOver + idx + 1;
			_totalConsumed += leftOver;
			_consumed = idx + 1;
			_stringHasEscaping = false;
		}
		else
		{
			_bytePositionInLine += leftOver + idx;
			_stringHasEscaping = true;
			bool nextCharEscaped = false;
			while (true)
			{
				bool flag = true;
				while (idx < localBuffer.Length)
				{
					byte currentByte = localBuffer[idx];
					if (currentByte == 34)
					{
						if (!nextCharEscaped)
						{
							goto end_IL_03d3;
						}
						nextCharEscaped = false;
					}
					else if (currentByte == 92)
					{
						nextCharEscaped = !nextCharEscaped;
					}
					else if (nextCharEscaped)
					{
						int index = JsonConstants.EscapableChars.IndexOf(currentByte);
						if (index == -1)
						{
							RollBackState(in rollBackState, isError: true);
							ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterAfterEscapeWithinString, currentByte);
						}
						if (currentByte == 117)
						{
							_bytePositionInLine++;
							int numberOfHexDigits = 0;
							int i = idx + 1;
							while (true)
							{
								bool flag2 = true;
								for (; i < localBuffer.Length; i++)
								{
									byte nextByte = localBuffer[i];
									if (!JsonReaderHelper.IsHexDigit(nextByte))
									{
										RollBackState(in rollBackState, isError: true);
										ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidHexCharacterWithinString, nextByte);
									}
									numberOfHexDigits++;
									_bytePositionInLine++;
									if (numberOfHexDigits >= 4)
									{
										goto end_IL_0300;
									}
								}
								if (!GetNextSpan())
								{
									if (IsLastSpan)
									{
										RollBackState(in rollBackState, isError: true);
										ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound, 0);
									}
									RollBackState(in rollBackState);
									return false;
								}
								_totalConsumed += localBuffer.Length;
								localBuffer = _buffer;
								i = 0;
								continue;
								end_IL_0300:
								break;
							}
							nextCharEscaped = false;
							idx = i + 1;
							continue;
						}
						nextCharEscaped = false;
					}
					else if (currentByte < 32)
					{
						RollBackState(in rollBackState, isError: true);
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterWithinString, currentByte);
					}
					_bytePositionInLine++;
					idx++;
				}
				if (!GetNextSpan())
				{
					if (IsLastSpan)
					{
						RollBackState(in rollBackState, isError: true);
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound, 0);
					}
					RollBackState(in rollBackState);
					return false;
				}
				_totalConsumed += localBuffer.Length;
				localBuffer = _buffer;
				idx = 0;
				continue;
				end_IL_03d3:
				break;
			}
			_bytePositionInLine++;
			_consumed = idx + 1;
			_totalConsumed += leftOver;
			end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + idx);
		}
		SequencePosition start = rollBackState.GetStartPosition(1);
		ValueSequence = _sequence.Slice(start, end);
		_tokenType = JsonTokenType.String;
		return true;
	}

	private bool ConsumeStringAndValidateMultiSegment(ReadOnlySpan<byte> data, int idx)
	{
		Debug.Assert(idx >= 0 && idx < data.Length);
		Debug.Assert(data[idx] != 34);
		Debug.Assert(data[idx] == 92 || data[idx] < 32);
		PartialStateForRollback rollBackState = CaptureState();
		HasValueSequence = false;
		int leftOverFromConsumed = _buffer.Length - _consumed;
		_bytePositionInLine += idx + 1;
		bool nextCharEscaped = false;
		while (true)
		{
			bool flag = true;
			while (idx < data.Length)
			{
				byte currentByte = data[idx];
				switch (currentByte)
				{
				case 34:
					if (nextCharEscaped)
					{
						nextCharEscaped = false;
						goto IL_0297;
					}
					if (HasValueSequence)
					{
						_bytePositionInLine++;
						_consumed = idx + 1;
						_totalConsumed += leftOverFromConsumed;
						SequencePosition end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + idx);
						SequencePosition start = rollBackState.GetStartPosition(1);
						ValueSequence = _sequence.Slice(start, end);
					}
					else
					{
						_bytePositionInLine++;
						_consumed += idx + 2;
						ValueSpan = data.Slice(0, idx);
					}
					_stringHasEscaping = true;
					_tokenType = JsonTokenType.String;
					return true;
				case 92:
					nextCharEscaped = !nextCharEscaped;
					goto IL_0297;
				default:
					{
						if (nextCharEscaped)
						{
							int index = JsonConstants.EscapableChars.IndexOf(currentByte);
							if (index == -1)
							{
								RollBackState(in rollBackState, isError: true);
								ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterAfterEscapeWithinString, currentByte);
							}
							if (currentByte == 117)
							{
								_bytePositionInLine++;
								int numberOfHexDigits = 0;
								int i = idx + 1;
								while (true)
								{
									bool flag2 = true;
									for (; i < data.Length; i++)
									{
										byte nextByte = data[i];
										if (!JsonReaderHelper.IsHexDigit(nextByte))
										{
											RollBackState(in rollBackState, isError: true);
											ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidHexCharacterWithinString, nextByte);
										}
										numberOfHexDigits++;
										_bytePositionInLine++;
										if (numberOfHexDigits >= 4)
										{
											goto end_IL_025d;
										}
									}
									if (!GetNextSpan())
									{
										if (IsLastSpan)
										{
											RollBackState(in rollBackState, isError: true);
											ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound, 0);
										}
										RollBackState(in rollBackState);
										return false;
									}
									if (HasValueSequence)
									{
										_totalConsumed += data.Length;
									}
									data = _buffer;
									i = 0;
									HasValueSequence = true;
									continue;
									end_IL_025d:
									break;
								}
								nextCharEscaped = false;
								idx = i + 1;
								break;
							}
							nextCharEscaped = false;
						}
						else if (currentByte < 32)
						{
							RollBackState(in rollBackState, isError: true);
							ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterWithinString, currentByte);
						}
						goto IL_0297;
					}
					IL_0297:
					_bytePositionInLine++;
					idx++;
					break;
				}
			}
			if (!GetNextSpan())
			{
				break;
			}
			if (HasValueSequence)
			{
				_totalConsumed += data.Length;
			}
			data = _buffer;
			idx = 0;
			HasValueSequence = true;
		}
		if (IsLastSpan)
		{
			RollBackState(in rollBackState, isError: true);
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.EndOfStringNotFound, 0);
		}
		RollBackState(in rollBackState);
		return false;
	}

	private void RollBackState(in PartialStateForRollback state, bool isError = false)
	{
		_totalConsumed = state._prevTotalConsumed;
		if (!isError)
		{
			_bytePositionInLine = state._prevBytePositionInLine;
		}
		_consumed = state._prevConsumed;
		_currentPosition = state._prevCurrentPosition;
	}

	private bool TryGetNumberMultiSegment(ReadOnlySpan<byte> data, out int consumed)
	{
		Debug.Assert(data.Length > 0);
		_numberFormat = '\0';
		PartialStateForRollback rollBackState = CaptureState();
		consumed = 0;
		int i = 0;
		ConsumeNumberResult signResult = ConsumeNegativeSignMultiSegment(ref data, ref i, in rollBackState);
		if (signResult == ConsumeNumberResult.NeedMoreData)
		{
			RollBackState(in rollBackState);
			return false;
		}
		Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);
		byte nextByte = data[i];
		Debug.Assert(nextByte >= 48 && nextByte <= 57);
		if (nextByte == 48)
		{
			ConsumeNumberResult result2 = ConsumeZeroMultiSegment(ref data, ref i, in rollBackState);
			if (result2 == ConsumeNumberResult.NeedMoreData)
			{
				RollBackState(in rollBackState);
				return false;
			}
			if (result2 != 0)
			{
				Debug.Assert(result2 == ConsumeNumberResult.OperationIncomplete);
				nextByte = data[i];
				goto IL_0168;
			}
		}
		else
		{
			ConsumeNumberResult result = ConsumeIntegerDigitsMultiSegment(ref data, ref i);
			if (result == ConsumeNumberResult.NeedMoreData)
			{
				RollBackState(in rollBackState);
				return false;
			}
			if (result != 0)
			{
				Debug.Assert(result == ConsumeNumberResult.OperationIncomplete);
				nextByte = data[i];
				if (nextByte != 46 && nextByte != 69 && nextByte != 101)
				{
					RollBackState(in rollBackState, isError: true);
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, nextByte);
				}
				goto IL_0168;
			}
		}
		goto IL_0308;
		IL_0308:
		if (HasValueSequence)
		{
			SequencePosition start = rollBackState.GetStartPosition();
			SequencePosition end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + i);
			ValueSequence = _sequence.Slice(start, end);
			consumed = i;
		}
		else
		{
			ValueSpan = data.Slice(0, i);
			consumed = i;
		}
		return true;
		IL_0168:
		Debug.Assert(nextByte == 46 || nextByte == 69 || nextByte == 101);
		if (nextByte == 46)
		{
			i++;
			_bytePositionInLine++;
			ConsumeNumberResult result3 = ConsumeDecimalDigitsMultiSegment(ref data, ref i, in rollBackState);
			if (result3 == ConsumeNumberResult.NeedMoreData)
			{
				RollBackState(in rollBackState);
				return false;
			}
			if (result3 == ConsumeNumberResult.Success)
			{
				goto IL_0308;
			}
			Debug.Assert(result3 == ConsumeNumberResult.OperationIncomplete);
			nextByte = data[i];
			if (nextByte != 69 && nextByte != 101)
			{
				RollBackState(in rollBackState, isError: true);
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedNextDigitEValueNotFound, nextByte);
			}
		}
		Debug.Assert(nextByte == 69 || nextByte == 101);
		i++;
		_numberFormat = 'e';
		_bytePositionInLine++;
		signResult = ConsumeSignMultiSegment(ref data, ref i, in rollBackState);
		if (signResult == ConsumeNumberResult.NeedMoreData)
		{
			RollBackState(in rollBackState);
			return false;
		}
		Debug.Assert(signResult == ConsumeNumberResult.OperationIncomplete);
		i++;
		_bytePositionInLine++;
		ConsumeNumberResult resultExponent = ConsumeIntegerDigitsMultiSegment(ref data, ref i);
		switch (resultExponent)
		{
		case ConsumeNumberResult.NeedMoreData:
			RollBackState(in rollBackState);
			return false;
		default:
			Debug.Assert(resultExponent == ConsumeNumberResult.OperationIncomplete);
			RollBackState(in rollBackState, isError: true);
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, data[i]);
			break;
		case ConsumeNumberResult.Success:
			break;
		}
		goto IL_0308;
	}

	private ConsumeNumberResult ConsumeNegativeSignMultiSegment(ref ReadOnlySpan<byte> data, ref int i, in PartialStateForRollback rollBackState)
	{
		Debug.Assert(i == 0);
		byte nextByte = data[i];
		if (nextByte == 45)
		{
			i++;
			_bytePositionInLine++;
			if (i >= data.Length)
			{
				if (IsLastSpan)
				{
					RollBackState(in rollBackState, isError: true);
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
				}
				if (!GetNextSpan())
				{
					if (IsLastSpan)
					{
						RollBackState(in rollBackState, isError: true);
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
					}
					return ConsumeNumberResult.NeedMoreData;
				}
				Debug.Assert(i == 1);
				_totalConsumed += i;
				HasValueSequence = true;
				i = 0;
				data = _buffer;
			}
			nextByte = data[i];
			if (!JsonHelpers.IsDigit(nextByte))
			{
				RollBackState(in rollBackState, isError: true);
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
			}
		}
		return ConsumeNumberResult.OperationIncomplete;
	}

	private ConsumeNumberResult ConsumeZeroMultiSegment(ref ReadOnlySpan<byte> data, ref int i, in PartialStateForRollback rollBackState)
	{
		Debug.Assert(data[i] == 48);
		Debug.Assert(i == 0 || i == 1);
		i++;
		_bytePositionInLine++;
		byte nextByte;
		if (i < data.Length)
		{
			nextByte = data[i];
			if (JsonConstants.Delimiters.IndexOf(nextByte) >= 0)
			{
				return ConsumeNumberResult.Success;
			}
		}
		else
		{
			if (IsLastSpan)
			{
				return ConsumeNumberResult.Success;
			}
			if (!GetNextSpan())
			{
				if (IsLastSpan)
				{
					return ConsumeNumberResult.Success;
				}
				return ConsumeNumberResult.NeedMoreData;
			}
			_totalConsumed += i;
			HasValueSequence = true;
			i = 0;
			data = _buffer;
			nextByte = data[i];
			if (JsonConstants.Delimiters.IndexOf(nextByte) >= 0)
			{
				return ConsumeNumberResult.Success;
			}
		}
		nextByte = data[i];
		if (nextByte != 46 && nextByte != 69 && nextByte != 101)
		{
			RollBackState(in rollBackState, isError: true);
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndOfDigitNotFound, nextByte);
		}
		return ConsumeNumberResult.OperationIncomplete;
	}

	private ConsumeNumberResult ConsumeIntegerDigitsMultiSegment(ref ReadOnlySpan<byte> data, ref int i)
	{
		byte nextByte = 0;
		int counter = 0;
		while (i < data.Length)
		{
			nextByte = data[i];
			if (!JsonHelpers.IsDigit(nextByte))
			{
				break;
			}
			counter++;
			i++;
		}
		if (i >= data.Length)
		{
			if (IsLastSpan)
			{
				_bytePositionInLine += counter;
				return ConsumeNumberResult.Success;
			}
			while (true)
			{
				if (!GetNextSpan())
				{
					if (IsLastSpan)
					{
						_bytePositionInLine += counter;
						return ConsumeNumberResult.Success;
					}
					return ConsumeNumberResult.NeedMoreData;
				}
				_totalConsumed += i;
				_bytePositionInLine += counter;
				counter = 0;
				HasValueSequence = true;
				i = 0;
				data = _buffer;
				while (i < data.Length)
				{
					nextByte = data[i];
					if (!JsonHelpers.IsDigit(nextByte))
					{
						break;
					}
					i++;
				}
				_bytePositionInLine += i;
				if (i >= data.Length)
				{
					if (IsLastSpan)
					{
						return ConsumeNumberResult.Success;
					}
					continue;
				}
				break;
			}
		}
		else
		{
			_bytePositionInLine += counter;
		}
		if (JsonConstants.Delimiters.IndexOf(nextByte) >= 0)
		{
			return ConsumeNumberResult.Success;
		}
		return ConsumeNumberResult.OperationIncomplete;
	}

	private ConsumeNumberResult ConsumeDecimalDigitsMultiSegment(ref ReadOnlySpan<byte> data, ref int i, in PartialStateForRollback rollBackState)
	{
		if (i >= data.Length)
		{
			if (IsLastSpan)
			{
				RollBackState(in rollBackState, isError: true);
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
			}
			if (!GetNextSpan())
			{
				if (IsLastSpan)
				{
					RollBackState(in rollBackState, isError: true);
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
				}
				return ConsumeNumberResult.NeedMoreData;
			}
			_totalConsumed += i;
			HasValueSequence = true;
			i = 0;
			data = _buffer;
		}
		byte nextByte = data[i];
		if (!JsonHelpers.IsDigit(nextByte))
		{
			RollBackState(in rollBackState, isError: true);
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterDecimal, nextByte);
		}
		i++;
		_bytePositionInLine++;
		return ConsumeIntegerDigitsMultiSegment(ref data, ref i);
	}

	private ConsumeNumberResult ConsumeSignMultiSegment(ref ReadOnlySpan<byte> data, ref int i, in PartialStateForRollback rollBackState)
	{
		if (i >= data.Length)
		{
			if (IsLastSpan)
			{
				RollBackState(in rollBackState, isError: true);
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
			}
			if (!GetNextSpan())
			{
				if (IsLastSpan)
				{
					RollBackState(in rollBackState, isError: true);
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
				}
				return ConsumeNumberResult.NeedMoreData;
			}
			_totalConsumed += i;
			HasValueSequence = true;
			i = 0;
			data = _buffer;
		}
		byte nextByte = data[i];
		if (nextByte == 43 || nextByte == 45)
		{
			i++;
			_bytePositionInLine++;
			if (i >= data.Length)
			{
				if (IsLastSpan)
				{
					RollBackState(in rollBackState, isError: true);
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
				}
				if (!GetNextSpan())
				{
					if (IsLastSpan)
					{
						RollBackState(in rollBackState, isError: true);
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundEndOfData, 0);
					}
					return ConsumeNumberResult.NeedMoreData;
				}
				_totalConsumed += i;
				HasValueSequence = true;
				i = 0;
				data = _buffer;
			}
			nextByte = data[i];
		}
		if (!JsonHelpers.IsDigit(nextByte))
		{
			RollBackState(in rollBackState, isError: true);
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.RequiredDigitNotFoundAfterSign, nextByte);
		}
		return ConsumeNumberResult.OperationIncomplete;
	}

	private bool ConsumeNextTokenOrRollbackMultiSegment(byte marker)
	{
		long prevTotalConsumed = _totalConsumed;
		int prevConsumed = _consumed;
		long prevPosition = _bytePositionInLine;
		long prevLineNumber = _lineNumber;
		JsonTokenType prevTokenType = _tokenType;
		SequencePosition prevSequencePosition = _currentPosition;
		bool prevTrailingCommaBeforeComment = _trailingCommaBeforeComment;
		switch (ConsumeNextTokenMultiSegment(marker))
		{
		case ConsumeTokenResult.Success:
			return true;
		case ConsumeTokenResult.NotEnoughDataRollBackState:
			_consumed = prevConsumed;
			_tokenType = prevTokenType;
			_bytePositionInLine = prevPosition;
			_lineNumber = prevLineNumber;
			_totalConsumed = prevTotalConsumed;
			_currentPosition = prevSequencePosition;
			_trailingCommaBeforeComment = prevTrailingCommaBeforeComment;
			break;
		}
		return false;
	}

	private ConsumeTokenResult ConsumeNextTokenMultiSegment(byte marker)
	{
		if (_readerOptions.CommentHandling != 0)
		{
			if (_readerOptions.CommentHandling != JsonCommentHandling.Allow)
			{
				Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Skip);
				return ConsumeNextTokenUntilAfterAllCommentsAreSkippedMultiSegment(marker);
			}
			if (marker == 47)
			{
				return (!SkipOrConsumeCommentMultiSegmentWithRollback()) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
			}
			if (_tokenType == JsonTokenType.Comment)
			{
				return ConsumeNextTokenFromLastNonCommentTokenMultiSegment();
			}
		}
		if (_bitStack.CurrentDepth == 0)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleJson, marker);
		}
		switch (marker)
		{
		case 44:
		{
			_consumed++;
			_bytePositionInLine++;
			if (_consumed >= (uint)_buffer.Length)
			{
				if (IsLastSpan)
				{
					_consumed--;
					_bytePositionInLine--;
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound, 0);
				}
				if (!GetNextSpan())
				{
					if (IsLastSpan)
					{
						_consumed--;
						_bytePositionInLine--;
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound, 0);
					}
					return ConsumeTokenResult.NotEnoughDataRollBackState;
				}
			}
			byte first = _buffer[_consumed];
			if (first <= 32)
			{
				SkipWhiteSpaceMultiSegment();
				if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
				{
					return ConsumeTokenResult.NotEnoughDataRollBackState;
				}
				first = _buffer[_consumed];
			}
			TokenStartIndex = BytesConsumed;
			if (_readerOptions.CommentHandling == JsonCommentHandling.Allow && first == 47)
			{
				_trailingCommaBeforeComment = true;
				return (!SkipOrConsumeCommentMultiSegmentWithRollback()) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
			}
			if (_inObject)
			{
				if (first != 34)
				{
					if (first == 125)
					{
						if (_readerOptions.AllowTrailingCommas)
						{
							EndObject();
							return ConsumeTokenResult.Success;
						}
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd, 0);
					}
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
				}
				return (!ConsumePropertyNameMultiSegment()) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
			}
			if (first == 93)
			{
				if (_readerOptions.AllowTrailingCommas)
				{
					EndArray();
					return ConsumeTokenResult.Success;
				}
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd, 0);
			}
			return (!ConsumeValueMultiSegment(first)) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
		}
		case 125:
			EndObject();
			break;
		case 93:
			EndArray();
			break;
		default:
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.FoundInvalidCharacter, marker);
			break;
		}
		return ConsumeTokenResult.Success;
	}

	private ConsumeTokenResult ConsumeNextTokenFromLastNonCommentTokenMultiSegment()
	{
		Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Allow);
		Debug.Assert(_tokenType == JsonTokenType.Comment);
		if (JsonReaderHelper.IsTokenTypePrimitive(_previousTokenType))
		{
			_tokenType = (_inObject ? JsonTokenType.StartObject : JsonTokenType.StartArray);
		}
		else
		{
			_tokenType = _previousTokenType;
		}
		Debug.Assert(_tokenType != JsonTokenType.Comment);
		if (HasMoreDataMultiSegment())
		{
			byte first = _buffer[_consumed];
			if (first <= 32)
			{
				SkipWhiteSpaceMultiSegment();
				if (!HasMoreDataMultiSegment())
				{
					goto IL_059b;
				}
				first = _buffer[_consumed];
			}
			if (_bitStack.CurrentDepth == 0 && _tokenType != 0)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleJson, first);
			}
			Debug.Assert(first != 47);
			TokenStartIndex = BytesConsumed;
			if (first != 44)
			{
				if (first == 125)
				{
					EndObject();
				}
				else
				{
					if (first != 93)
					{
						if (_tokenType == JsonTokenType.None)
						{
							if (ReadFirstTokenMultiSegment(first))
							{
								goto IL_0595;
							}
						}
						else if (_tokenType == JsonTokenType.StartObject)
						{
							Debug.Assert(first != 125);
							if (first != 34)
							{
								ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
							}
							long prevTotalConsumed = _totalConsumed;
							int prevConsumed = _consumed;
							long prevPosition = _bytePositionInLine;
							long prevLineNumber = _lineNumber;
							if (ConsumePropertyNameMultiSegment())
							{
								goto IL_0595;
							}
							_consumed = prevConsumed;
							_tokenType = JsonTokenType.StartObject;
							_bytePositionInLine = prevPosition;
							_lineNumber = prevLineNumber;
							_totalConsumed = prevTotalConsumed;
						}
						else if (_tokenType == JsonTokenType.StartArray)
						{
							Debug.Assert(first != 93);
							if (ConsumeValueMultiSegment(first))
							{
								goto IL_0595;
							}
						}
						else if (_tokenType == JsonTokenType.PropertyName)
						{
							if (ConsumeValueMultiSegment(first))
							{
								goto IL_0595;
							}
						}
						else
						{
							Debug.Assert(_tokenType == JsonTokenType.EndArray || _tokenType == JsonTokenType.EndObject);
							if (_inObject)
							{
								Debug.Assert(first != 125);
								if (first != 34)
								{
									ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
								}
								if (ConsumePropertyNameMultiSegment())
								{
									goto IL_0595;
								}
							}
							else
							{
								Debug.Assert(first != 93);
								if (ConsumeValueMultiSegment(first))
								{
									goto IL_0595;
								}
							}
						}
						goto IL_059b;
					}
					EndArray();
				}
				goto IL_0595;
			}
			if ((int)_previousTokenType <= 1 || _previousTokenType == JsonTokenType.StartArray || _trailingCommaBeforeComment)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueAfterComment, first);
			}
			_consumed++;
			_bytePositionInLine++;
			if (_consumed >= (uint)_buffer.Length)
			{
				if (IsLastSpan)
				{
					_consumed--;
					_bytePositionInLine--;
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound, 0);
				}
				if (!GetNextSpan())
				{
					if (IsLastSpan)
					{
						_consumed--;
						_bytePositionInLine--;
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound, 0);
					}
					goto IL_059b;
				}
			}
			first = _buffer[_consumed];
			if (first <= 32)
			{
				SkipWhiteSpaceMultiSegment();
				if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
				{
					goto IL_059b;
				}
				first = _buffer[_consumed];
			}
			TokenStartIndex = BytesConsumed;
			if (first == 47)
			{
				_trailingCommaBeforeComment = true;
				if (SkipOrConsumeCommentMultiSegmentWithRollback())
				{
					goto IL_0595;
				}
			}
			else if (_inObject)
			{
				if (first != 34)
				{
					if (first == 125)
					{
						if (_readerOptions.AllowTrailingCommas)
						{
							EndObject();
							goto IL_0595;
						}
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd, 0);
					}
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, first);
				}
				if (ConsumePropertyNameMultiSegment())
				{
					goto IL_0595;
				}
			}
			else
			{
				if (first == 93)
				{
					if (_readerOptions.AllowTrailingCommas)
					{
						EndArray();
						goto IL_0595;
					}
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd, 0);
				}
				if (ConsumeValueMultiSegment(first))
				{
					goto IL_0595;
				}
			}
		}
		goto IL_059b;
		IL_059b:
		return ConsumeTokenResult.NotEnoughDataRollBackState;
		IL_0595:
		return ConsumeTokenResult.Success;
	}

	private bool SkipAllCommentsMultiSegment(ref byte marker)
	{
		while (true)
		{
			if (marker == 47)
			{
				if (!SkipOrConsumeCommentMultiSegmentWithRollback() || !HasMoreDataMultiSegment())
				{
					break;
				}
				marker = _buffer[_consumed];
				if (marker <= 32)
				{
					SkipWhiteSpaceMultiSegment();
					if (!HasMoreDataMultiSegment())
					{
						break;
					}
					marker = _buffer[_consumed];
				}
				continue;
			}
			return true;
		}
		return false;
	}

	private bool SkipAllCommentsMultiSegment(ref byte marker, ExceptionResource resource)
	{
		while (true)
		{
			if (marker == 47)
			{
				if (!SkipOrConsumeCommentMultiSegmentWithRollback() || !HasMoreDataMultiSegment(resource))
				{
					break;
				}
				marker = _buffer[_consumed];
				if (marker <= 32)
				{
					SkipWhiteSpaceMultiSegment();
					if (!HasMoreDataMultiSegment(resource))
					{
						break;
					}
					marker = _buffer[_consumed];
				}
				continue;
			}
			return true;
		}
		return false;
	}

	private ConsumeTokenResult ConsumeNextTokenUntilAfterAllCommentsAreSkippedMultiSegment(byte marker)
	{
		if (!SkipAllCommentsMultiSegment(ref marker))
		{
			goto IL_0403;
		}
		TokenStartIndex = BytesConsumed;
		if (_tokenType == JsonTokenType.StartObject)
		{
			if (marker == 125)
			{
				EndObject();
			}
			else
			{
				if (marker != 34)
				{
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, marker);
				}
				long prevTotalConsumed = _totalConsumed;
				int prevConsumed = _consumed;
				long prevPosition = _bytePositionInLine;
				long prevLineNumber = _lineNumber;
				SequencePosition copy = _currentPosition;
				if (!ConsumePropertyNameMultiSegment())
				{
					_consumed = prevConsumed;
					_tokenType = JsonTokenType.StartObject;
					_bytePositionInLine = prevPosition;
					_lineNumber = prevLineNumber;
					_totalConsumed = prevTotalConsumed;
					_currentPosition = copy;
					goto IL_0403;
				}
			}
		}
		else if (_tokenType == JsonTokenType.StartArray)
		{
			if (marker == 93)
			{
				EndArray();
			}
			else if (!ConsumeValueMultiSegment(marker))
			{
				goto IL_0403;
			}
		}
		else if (_tokenType == JsonTokenType.PropertyName)
		{
			if (!ConsumeValueMultiSegment(marker))
			{
				goto IL_0403;
			}
		}
		else if (_bitStack.CurrentDepth == 0)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedEndAfterSingleJson, marker);
		}
		else
		{
			switch (marker)
			{
			case 44:
				_consumed++;
				_bytePositionInLine++;
				if (_consumed >= (uint)_buffer.Length)
				{
					if (IsLastSpan)
					{
						_consumed--;
						_bytePositionInLine--;
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound, 0);
					}
					if (!GetNextSpan())
					{
						if (IsLastSpan)
						{
							_consumed--;
							_bytePositionInLine--;
							ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound, 0);
						}
						return ConsumeTokenResult.NotEnoughDataRollBackState;
					}
				}
				marker = _buffer[_consumed];
				if (marker <= 32)
				{
					SkipWhiteSpaceMultiSegment();
					if (!HasMoreDataMultiSegment(ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
					{
						return ConsumeTokenResult.NotEnoughDataRollBackState;
					}
					marker = _buffer[_consumed];
				}
				if (SkipAllCommentsMultiSegment(ref marker, ExceptionResource.ExpectedStartOfPropertyOrValueNotFound))
				{
					TokenStartIndex = BytesConsumed;
					if (_inObject)
					{
						if (marker != 34)
						{
							if (marker == 125)
							{
								if (_readerOptions.AllowTrailingCommas)
								{
									EndObject();
									break;
								}
								ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeObjectEnd, 0);
							}
							ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.ExpectedStartOfPropertyNotFound, marker);
						}
						return (!ConsumePropertyNameMultiSegment()) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
					}
					if (marker == 93)
					{
						if (_readerOptions.AllowTrailingCommas)
						{
							EndArray();
							break;
						}
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.TrailingCommaNotAllowedBeforeArrayEnd, 0);
					}
					return (!ConsumeValueMultiSegment(marker)) ? ConsumeTokenResult.NotEnoughDataRollBackState : ConsumeTokenResult.Success;
				}
				return ConsumeTokenResult.NotEnoughDataRollBackState;
			case 125:
				EndObject();
				break;
			case 93:
				EndArray();
				break;
			default:
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.FoundInvalidCharacter, marker);
				break;
			}
		}
		return ConsumeTokenResult.Success;
		IL_0403:
		return ConsumeTokenResult.IncompleteNoRollBackNecessary;
	}

	private bool SkipOrConsumeCommentMultiSegmentWithRollback()
	{
		long prevTotalConsumed = BytesConsumed;
		SequencePosition start = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + _consumed);
		int tailBytesToIgnore;
		bool skipSucceeded = SkipCommentMultiSegment(out tailBytesToIgnore);
		if (skipSucceeded)
		{
			Debug.Assert(_readerOptions.CommentHandling == JsonCommentHandling.Allow || _readerOptions.CommentHandling == JsonCommentHandling.Skip);
			if (_readerOptions.CommentHandling == JsonCommentHandling.Allow)
			{
				SequencePosition end = new SequencePosition(_currentPosition.GetObject(), _currentPosition.GetInteger() + _consumed);
				ReadOnlySequence<byte> commentSequence = _sequence.Slice(start, end);
				commentSequence = commentSequence.Slice(2L, commentSequence.Length - 2 - tailBytesToIgnore);
				HasValueSequence = !commentSequence.IsSingleSegment;
				if (HasValueSequence)
				{
					ValueSequence = commentSequence;
				}
				else
				{
					ValueSpan = commentSequence.First.Span;
				}
				if (_tokenType != JsonTokenType.Comment)
				{
					_previousTokenType = _tokenType;
				}
				_tokenType = JsonTokenType.Comment;
			}
		}
		else
		{
			_totalConsumed = prevTotalConsumed;
			_consumed = 0;
		}
		return skipSucceeded;
	}

	private bool SkipCommentMultiSegment(out int tailBytesToIgnore)
	{
		_consumed++;
		_bytePositionInLine++;
		ReadOnlySpan<byte> localBuffer = _buffer.Slice(_consumed);
		if (localBuffer.Length == 0)
		{
			if (IsLastSpan)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment, 0);
			}
			if (!GetNextSpan())
			{
				if (IsLastSpan)
				{
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment, 0);
				}
				tailBytesToIgnore = 0;
				return false;
			}
			localBuffer = _buffer;
		}
		byte marker = localBuffer[0];
		if (marker != 47 && marker != 42)
		{
			ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.InvalidCharacterAtStartOfComment, marker);
		}
		bool multiLine = marker == 42;
		_consumed++;
		_bytePositionInLine++;
		localBuffer = localBuffer.Slice(1);
		if (localBuffer.Length == 0)
		{
			if (IsLastSpan)
			{
				tailBytesToIgnore = 0;
				if (multiLine)
				{
					ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment, 0);
				}
				return true;
			}
			if (!GetNextSpan())
			{
				tailBytesToIgnore = 0;
				if (IsLastSpan)
				{
					if (multiLine)
					{
						ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment, 0);
					}
					return true;
				}
				return false;
			}
			localBuffer = _buffer;
		}
		if (multiLine)
		{
			tailBytesToIgnore = 2;
			return SkipMultiLineCommentMultiSegment(localBuffer);
		}
		return SkipSingleLineCommentMultiSegment(localBuffer, out tailBytesToIgnore);
	}

	private bool SkipSingleLineCommentMultiSegment(ReadOnlySpan<byte> localBuffer, out int tailBytesToSkip)
	{
		bool expectLF = false;
		int dangerousLineSeparatorBytesConsumed = 0;
		tailBytesToSkip = 0;
		while (true)
		{
			if (expectLF)
			{
				if (localBuffer[0] == 10)
				{
					tailBytesToSkip++;
					_consumed++;
				}
				break;
			}
			int idx = FindLineSeparatorMultiSegment(localBuffer, ref dangerousLineSeparatorBytesConsumed);
			Debug.Assert(dangerousLineSeparatorBytesConsumed >= 0 && dangerousLineSeparatorBytesConsumed <= 2);
			if (idx != -1)
			{
				tailBytesToSkip++;
				_consumed += idx + 1;
				_bytePositionInLine += idx + 1;
				if (localBuffer[idx] == 10)
				{
					break;
				}
				Debug.Assert(localBuffer[idx] == 13);
				if (idx < localBuffer.Length - 1)
				{
					if (localBuffer[idx + 1] == 10)
					{
						tailBytesToSkip++;
						_consumed++;
						_bytePositionInLine++;
					}
					break;
				}
				expectLF = true;
			}
			else
			{
				_consumed += localBuffer.Length;
				_bytePositionInLine += localBuffer.Length;
			}
			if (IsLastSpan)
			{
				if (expectLF)
				{
					break;
				}
				return true;
			}
			if (!GetNextSpan())
			{
				if (IsLastSpan)
				{
					if (expectLF)
					{
						break;
					}
					return true;
				}
				return false;
			}
			localBuffer = _buffer;
		}
		_bytePositionInLine = 0L;
		_lineNumber++;
		return true;
	}

	private int FindLineSeparatorMultiSegment(ReadOnlySpan<byte> localBuffer, ref int dangerousLineSeparatorBytesConsumed)
	{
		Debug.Assert(dangerousLineSeparatorBytesConsumed >= 0 && dangerousLineSeparatorBytesConsumed <= 2);
		if (dangerousLineSeparatorBytesConsumed != 0)
		{
			ThrowOnDangerousLineSeparatorMultiSegment(localBuffer, ref dangerousLineSeparatorBytesConsumed);
			if (dangerousLineSeparatorBytesConsumed != 0)
			{
				Debug.Assert(dangerousLineSeparatorBytesConsumed >= 1 && dangerousLineSeparatorBytesConsumed <= 2 && localBuffer.Length <= 1);
				return -1;
			}
		}
		int totalIdx = 0;
		do
		{
			int idx = localBuffer.IndexOfAny<byte>(10, 13, 226);
			dangerousLineSeparatorBytesConsumed = 0;
			if (idx == -1)
			{
				return -1;
			}
			if (localBuffer[idx] != 226)
			{
				return totalIdx + idx;
			}
			int p = idx + 1;
			localBuffer = localBuffer.Slice(p);
			totalIdx += p;
			dangerousLineSeparatorBytesConsumed++;
			ThrowOnDangerousLineSeparatorMultiSegment(localBuffer, ref dangerousLineSeparatorBytesConsumed);
		}
		while (dangerousLineSeparatorBytesConsumed == 0);
		Debug.Assert(localBuffer.Length < 2);
		return -1;
	}

	private void ThrowOnDangerousLineSeparatorMultiSegment(ReadOnlySpan<byte> localBuffer, ref int dangerousLineSeparatorBytesConsumed)
	{
		Debug.Assert(dangerousLineSeparatorBytesConsumed == 1 || dangerousLineSeparatorBytesConsumed == 2);
		if (localBuffer.IsEmpty)
		{
			return;
		}
		if (dangerousLineSeparatorBytesConsumed == 1)
		{
			if (localBuffer[0] != 128)
			{
				dangerousLineSeparatorBytesConsumed = 0;
				return;
			}
			localBuffer = localBuffer.Slice(1);
			dangerousLineSeparatorBytesConsumed++;
			if (localBuffer.IsEmpty)
			{
				return;
			}
		}
		if (dangerousLineSeparatorBytesConsumed == 2)
		{
			byte lastByte = localBuffer[0];
			if (lastByte == 168 || lastByte == 169)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfLineSeparator, 0);
			}
			else
			{
				dangerousLineSeparatorBytesConsumed = 0;
			}
		}
	}

	private bool SkipMultiLineCommentMultiSegment(ReadOnlySpan<byte> localBuffer)
	{
		bool expectSlash = false;
		bool ignoreNextLfForLineTracking = false;
		while (true)
		{
			Debug.Assert(localBuffer.Length > 0);
			if (expectSlash)
			{
				if (localBuffer[0] == 47)
				{
					_consumed++;
					_bytePositionInLine++;
					return true;
				}
				expectSlash = false;
			}
			if (ignoreNextLfForLineTracking)
			{
				if (localBuffer[0] == 10)
				{
					_consumed++;
					localBuffer = localBuffer.Slice(1);
				}
				ignoreNextLfForLineTracking = false;
			}
			int idx = localBuffer.IndexOfAny<byte>(42, 10, 13);
			if (idx != -1)
			{
				int nextIdx = idx + 1;
				byte marker = localBuffer[idx];
				localBuffer = localBuffer.Slice(nextIdx);
				_consumed += nextIdx;
				switch (marker)
				{
				case 42:
					expectSlash = true;
					_bytePositionInLine += nextIdx;
					break;
				case 10:
					_bytePositionInLine = 0L;
					_lineNumber++;
					break;
				default:
					Debug.Assert(marker == 13);
					_bytePositionInLine = 0L;
					_lineNumber++;
					ignoreNextLfForLineTracking = true;
					break;
				}
			}
			else
			{
				_consumed += localBuffer.Length;
				_bytePositionInLine += localBuffer.Length;
				localBuffer = ReadOnlySpan<byte>.Empty;
			}
			if (!localBuffer.IsEmpty)
			{
				continue;
			}
			if (IsLastSpan)
			{
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment, 0);
			}
			if (!GetNextSpan())
			{
				if (!IsLastSpan)
				{
					break;
				}
				ThrowHelper.ThrowJsonReaderException(ref this, ExceptionResource.UnexpectedEndOfDataWhileReadingComment, 0);
			}
			localBuffer = _buffer;
			Debug.Assert(!localBuffer.IsEmpty);
		}
		return false;
	}

	private PartialStateForRollback CaptureState()
	{
		return new PartialStateForRollback(_totalConsumed, _bytePositionInLine, _consumed, _currentPosition);
	}

	public string GetString()
	{
		if (TokenType == JsonTokenType.Null)
		{
			return null;
		}
		if (TokenType != JsonTokenType.String && TokenType != JsonTokenType.PropertyName)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedString(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (_stringHasEscaping)
		{
			int idx = span.IndexOf<byte>(92);
			Debug.Assert(idx != -1);
			return JsonReaderHelper.GetUnescapedString(span, idx);
		}
		Debug.Assert(span.IndexOf<byte>(92) == -1);
		return JsonReaderHelper.TranscodeHelper(span);
	}

	public string GetComment()
	{
		if (TokenType != JsonTokenType.Comment)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedComment(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		return JsonReaderHelper.TranscodeHelper(span);
	}

	public bool GetBoolean()
	{
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (TokenType == JsonTokenType.True)
		{
			Debug.Assert(span.Length == 4);
			return true;
		}
		if (TokenType == JsonTokenType.False)
		{
			Debug.Assert(span.Length == 5);
			return false;
		}
		throw ThrowHelper.GetInvalidOperationException_ExpectedBoolean(TokenType);
	}

	public byte[] GetBytesFromBase64()
	{
		if (!TryGetBytesFromBase64(out var value))
		{
			throw ThrowHelper.GetFormatException(DataType.Base64String);
		}
		return value;
	}

	public byte GetByte()
	{
		if (!TryGetByte(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.Byte);
		}
		return value;
	}

	[CLSCompliant(false)]
	public sbyte GetSByte()
	{
		if (!TryGetSByte(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.SByte);
		}
		return value;
	}

	public short GetInt16()
	{
		if (!TryGetInt16(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.Int16);
		}
		return value;
	}

	public int GetInt32()
	{
		if (!TryGetInt32(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.Int32);
		}
		return value;
	}

	public long GetInt64()
	{
		if (!TryGetInt64(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.Int64);
		}
		return value;
	}

	[CLSCompliant(false)]
	public ushort GetUInt16()
	{
		if (!TryGetUInt16(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.UInt16);
		}
		return value;
	}

	[CLSCompliant(false)]
	public uint GetUInt32()
	{
		if (!TryGetUInt32(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.UInt32);
		}
		return value;
	}

	[CLSCompliant(false)]
	public ulong GetUInt64()
	{
		if (!TryGetUInt64(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.UInt64);
		}
		return value;
	}

	public float GetSingle()
	{
		if (!TryGetSingle(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.Single);
		}
		return value;
	}

	public double GetDouble()
	{
		if (!TryGetDouble(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.Double);
		}
		return value;
	}

	public decimal GetDecimal()
	{
		if (!TryGetDecimal(out var value))
		{
			throw ThrowHelper.GetFormatException(NumericType.Decimal);
		}
		return value;
	}

	public DateTime GetDateTime()
	{
		if (!TryGetDateTime(out var value))
		{
			throw ThrowHelper.GetFormatException(DataType.DateTime);
		}
		return value;
	}

	public DateTimeOffset GetDateTimeOffset()
	{
		if (!TryGetDateTimeOffset(out var value))
		{
			throw ThrowHelper.GetFormatException(DataType.DateTimeOffset);
		}
		return value;
	}

	public Guid GetGuid()
	{
		if (!TryGetGuid(out var value))
		{
			throw ThrowHelper.GetFormatException(DataType.Guid);
		}
		return value;
	}

	public bool TryGetBytesFromBase64(out byte[] value)
	{
		if (TokenType != JsonTokenType.String)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedString(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (_stringHasEscaping)
		{
			int idx = span.IndexOf<byte>(92);
			Debug.Assert(idx != -1);
			return JsonReaderHelper.TryGetUnescapedBase64Bytes(span, idx, out value);
		}
		Debug.Assert(span.IndexOf<byte>(92) == -1);
		return JsonReaderHelper.TryDecodeBase64(span, out value);
	}

	public bool TryGetByte(out byte value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out byte tmp, out int bytesConsumed, '\0') && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	[CLSCompliant(false)]
	public bool TryGetSByte(out sbyte value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out sbyte tmp, out int bytesConsumed, '\0') && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	public bool TryGetInt16(out short value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out short tmp, out int bytesConsumed, '\0') && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	public bool TryGetInt32(out int value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out int tmp, out int bytesConsumed, '\0') && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	public bool TryGetInt64(out long value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out long tmp, out int bytesConsumed, '\0') && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0L;
		return false;
	}

	[CLSCompliant(false)]
	public bool TryGetUInt16(out ushort value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out ushort tmp, out int bytesConsumed, '\0') && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	[CLSCompliant(false)]
	public bool TryGetUInt32(out uint value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out uint tmp, out int bytesConsumed, '\0') && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0u;
		return false;
	}

	[CLSCompliant(false)]
	public bool TryGetUInt64(out ulong value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out ulong tmp, out int bytesConsumed, '\0') && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0uL;
		return false;
	}

	public bool TryGetSingle(out float value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out float tmp, out int bytesConsumed, _numberFormat) && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0f;
		return false;
	}

	public bool TryGetDouble(out double value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out double tmp, out int bytesConsumed, _numberFormat) && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0.0;
		return false;
	}

	public bool TryGetDecimal(out decimal value)
	{
		if (TokenType != JsonTokenType.Number)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedNumber(TokenType);
		}
		ReadOnlySpan<byte> readOnlySpan;
		if (!HasValueSequence)
		{
			readOnlySpan = ValueSpan;
		}
		else
		{
			ReadOnlySequence<byte> sequence = ValueSequence;
			readOnlySpan = BuffersExtensions.ToArray(in sequence);
		}
		ReadOnlySpan<byte> span = readOnlySpan;
		if (Utf8Parser.TryParse(span, out decimal tmp, out int bytesConsumed, _numberFormat) && span.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = default(decimal);
		return false;
	}

	public bool TryGetDateTime(out DateTime value)
	{
		if (TokenType != JsonTokenType.String)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedString(TokenType);
		}
		ReadOnlySpan<byte> span = default(Span<byte>);
		if (HasValueSequence)
		{
			long sequenceLength = ValueSequence.Length;
			if (!JsonReaderHelper.IsValidDateTimeOffsetParseLength(sequenceLength))
			{
				value = default(DateTime);
				return false;
			}
			Debug.Assert(sequenceLength <= 252);
			Span<byte> stackSpan = stackalloc byte[(int)sequenceLength];
			ReadOnlySequence<byte> source = ValueSequence;
			source.CopyTo(stackSpan);
			span = stackSpan;
		}
		else
		{
			if (!JsonReaderHelper.IsValidDateTimeOffsetParseLength(ValueSpan.Length))
			{
				value = default(DateTime);
				return false;
			}
			span = ValueSpan;
		}
		if (_stringHasEscaping)
		{
			return JsonReaderHelper.TryGetEscapedDateTime(span, out value);
		}
		Debug.Assert(span.IndexOf<byte>(92) == -1);
		if (span.Length <= 42 && JsonHelpers.TryParseAsISO(span, out DateTime tmp))
		{
			value = tmp;
			return true;
		}
		value = default(DateTime);
		return false;
	}

	public bool TryGetDateTimeOffset(out DateTimeOffset value)
	{
		if (TokenType != JsonTokenType.String)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedString(TokenType);
		}
		ReadOnlySpan<byte> span = default(Span<byte>);
		if (HasValueSequence)
		{
			long sequenceLength = ValueSequence.Length;
			if (!JsonReaderHelper.IsValidDateTimeOffsetParseLength(sequenceLength))
			{
				value = default(DateTimeOffset);
				return false;
			}
			Debug.Assert(sequenceLength <= 252);
			Span<byte> stackSpan = stackalloc byte[(int)sequenceLength];
			ReadOnlySequence<byte> source = ValueSequence;
			source.CopyTo(stackSpan);
			span = stackSpan;
		}
		else
		{
			if (!JsonReaderHelper.IsValidDateTimeOffsetParseLength(ValueSpan.Length))
			{
				value = default(DateTimeOffset);
				return false;
			}
			span = ValueSpan;
		}
		if (_stringHasEscaping)
		{
			return JsonReaderHelper.TryGetEscapedDateTimeOffset(span, out value);
		}
		Debug.Assert(span.IndexOf<byte>(92) == -1);
		if (span.Length <= 42 && JsonHelpers.TryParseAsISO(span, out DateTimeOffset tmp))
		{
			value = tmp;
			return true;
		}
		value = default(DateTimeOffset);
		return false;
	}

	public bool TryGetGuid(out Guid value)
	{
		if (TokenType != JsonTokenType.String)
		{
			throw ThrowHelper.GetInvalidOperationException_ExpectedString(TokenType);
		}
		ReadOnlySpan<byte> span = default(Span<byte>);
		if (HasValueSequence)
		{
			long sequenceLength = ValueSequence.Length;
			if (sequenceLength > 216)
			{
				value = default(Guid);
				return false;
			}
			Debug.Assert(sequenceLength <= 216);
			Span<byte> stackSpan = stackalloc byte[(int)sequenceLength];
			ReadOnlySequence<byte> source = ValueSequence;
			source.CopyTo(stackSpan);
			span = stackSpan;
		}
		else
		{
			if (ValueSpan.Length > 216)
			{
				value = default(Guid);
				return false;
			}
			span = ValueSpan;
		}
		if (_stringHasEscaping)
		{
			return JsonReaderHelper.TryGetEscapedGuid(span, out value);
		}
		Debug.Assert(span.IndexOf<byte>(92) == -1);
		if (span.Length == 36 && Utf8Parser.TryParse(span, out Guid tmp, out int _, 'D'))
		{
			value = tmp;
			return true;
		}
		value = default(Guid);
		return false;
	}
}
