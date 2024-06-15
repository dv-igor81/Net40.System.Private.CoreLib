#define DEBUG
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json;

public sealed class JsonDocument : IDisposable
{
	internal struct DbRow
	{
		internal const int Size = 12;

		private int _location;

		private int _sizeOrLengthUnion;

		private readonly int _numberOfRowsAndTypeUnion;

		internal const int UnknownSize = -1;

		internal int Location => _location;

		internal int SizeOrLength => _sizeOrLengthUnion & 0x7FFFFFFF;

		internal bool IsUnknownSize => _sizeOrLengthUnion == -1;

		internal bool HasComplexChildren => _sizeOrLengthUnion < 0;

		internal int NumberOfRows => _numberOfRowsAndTypeUnion & 0xFFFFFFF;

		internal JsonTokenType TokenType => (JsonTokenType)((uint)_numberOfRowsAndTypeUnion >> 28);

		internal bool IsSimpleValue => (int)TokenType >= 5;

		static unsafe DbRow()
		{
			Debug.Assert(sizeof(DbRow) == 12);
		}

		internal DbRow(JsonTokenType jsonTokenType, int location, int sizeOrLength)
		{
			Debug.Assert((int)jsonTokenType > 0 && (int)jsonTokenType <= 11);
			Debug.Assert((int)jsonTokenType < 16);
			Debug.Assert(location >= 0);
			Debug.Assert(sizeOrLength >= -1);
			_location = location;
			_sizeOrLengthUnion = sizeOrLength;
			_numberOfRowsAndTypeUnion = (int)((uint)jsonTokenType << 28);
		}
	}

	private struct MetadataDb : IDisposable
	{
		private const int SizeOrLengthOffset = 4;

		private const int NumberOfRowsOffset = 8;

		private byte[] _data;

		private bool _isLocked;

		internal int Length { get; private set; }

		internal MetadataDb(byte[] completeDb)
		{
			_data = completeDb;
			Length = completeDb.Length;
			_isLocked = true;
		}

		internal MetadataDb(int payloadLength)
		{
			int initialSize = 12 + payloadLength;
			if (initialSize > 1048576 && initialSize <= 4194304)
			{
				initialSize = 1048576;
			}
			_data = ArrayPool<byte>.Shared.Rent(initialSize);
			Length = 0;
			_isLocked = false;
		}

		internal MetadataDb(MetadataDb source, bool useArrayPools)
		{
			Length = source.Length;
			_isLocked = !useArrayPools;
			if (useArrayPools)
			{
				_data = ArrayPool<byte>.Shared.Rent(Length);
				source._data.AsSpan(0, Length).CopyTo(_data);
			}
			else
			{
				_data = source._data.AsSpan(0, Length).ToArray();
			}
		}

		public void Dispose()
		{
			byte[] data = Interlocked.Exchange(ref _data, null);
			if (data != null)
			{
				Debug.Assert(!_isLocked, "Dispose called on a locked database");
				ArrayPool<byte>.Shared.Return(data);
				Length = 0;
			}
		}

		internal void TrimExcess()
		{
			if (Length <= _data.Length / 2)
			{
				byte[] newRent = ArrayPool<byte>.Shared.Rent(Length);
				byte[] returnBuf = newRent;
				if (newRent.Length < _data.Length)
				{
					Buffer.BlockCopy(_data, 0, newRent, 0, Length);
					returnBuf = _data;
					_data = newRent;
				}
				ArrayPool<byte>.Shared.Return(returnBuf);
			}
		}

		internal void Append(JsonTokenType tokenType, int startLocation, int length)
		{
			Debug.Assert((tokenType == JsonTokenType.StartArray || tokenType == JsonTokenType.StartObject) == (length == -1));
			Debug.Assert(!_isLocked, "Appending to a locked database");
			if (Length >= _data.Length - 12)
			{
				Enlarge();
			}
			DbRow row = new DbRow(tokenType, startLocation, length);
			MemoryMarshal.Write(_data.AsSpan(Length), ref row);
			Length += 12;
		}

		private void Enlarge()
		{
			byte[] toReturn = _data;
			_data = ArrayPool<byte>.Shared.Rent(toReturn.Length * 2);
			Buffer.BlockCopy(toReturn, 0, _data, 0, toReturn.Length);
			ArrayPool<byte>.Shared.Return(toReturn);
		}

		[Conditional("DEBUG")]
		private void AssertValidIndex(int index)
		{
			Debug.Assert(index >= 0);
			Debug.Assert(index <= Length - 12, $"index {index} is out of bounds");
			Debug.Assert(index % 12 == 0, $"index {index} is not at a record start position");
		}

		internal void SetLength(int index, int length)
		{
			AssertValidIndex(index);
			Debug.Assert(length >= 0);
			Span<byte> destination = _data.AsSpan(index + 4);
			MemoryMarshal.Write(destination, ref length);
		}

		internal void SetNumberOfRows(int index, int numberOfRows)
		{
			AssertValidIndex(index);
			Debug.Assert(numberOfRows >= 1 && numberOfRows <= 268435455);
			Span<byte> dataPos = _data.AsSpan(index + 8);
			int current = MemoryMarshal.Read<int>(dataPos);
			int value = (current & -268435456) | numberOfRows;
			MemoryMarshal.Write(dataPos, ref value);
		}

		internal void SetHasComplexChildren(int index)
		{
			AssertValidIndex(index);
			Span<byte> dataPos = _data.AsSpan(index + 4);
			int current = MemoryMarshal.Read<int>(dataPos);
			int value = current | int.MinValue;
			MemoryMarshal.Write(dataPos, ref value);
		}

		internal int FindIndexOfFirstUnsetSizeOrLength(JsonTokenType lookupType)
		{
			Debug.Assert(lookupType == JsonTokenType.StartObject || lookupType == JsonTokenType.StartArray);
			return FindOpenElement(lookupType);
		}

		private int FindOpenElement(JsonTokenType lookupType)
		{
			Span<byte> data = _data.AsSpan(0, Length);
			for (int i = Length - 12; i >= 0; i -= 12)
			{
				DbRow row = MemoryMarshal.Read<DbRow>(data.Slice(i));
				if (row.IsUnknownSize && row.TokenType == lookupType)
				{
					return i;
				}
			}
			Debug.Fail($"Unable to find expected {lookupType} token");
			return -1;
		}

		internal DbRow Get(int index)
		{
			AssertValidIndex(index);
			return MemoryMarshal.Read<DbRow>(_data.AsSpan(index));
		}

		internal JsonTokenType GetJsonTokenType(int index)
		{
			AssertValidIndex(index);
			uint union = MemoryMarshal.Read<uint>(_data.AsSpan(index + 8));
			return (JsonTokenType)(union >> 28);
		}

		internal MetadataDb CopySegment(int startIndex, int endIndex)
		{
			Debug.Assert(endIndex > startIndex, $"endIndex={endIndex} was at or before startIndex={startIndex}");
			AssertValidIndex(startIndex);
			Debug.Assert(endIndex <= Length);
			DbRow start = Get(startIndex);
			DbRow end = Get(endIndex - 12);
			if (start.TokenType == JsonTokenType.StartObject)
			{
				Debug.Assert(end.TokenType == JsonTokenType.EndObject, $"StartObject paired with {end.TokenType}");
			}
			else if (start.TokenType == JsonTokenType.StartArray)
			{
				Debug.Assert(end.TokenType == JsonTokenType.EndArray, $"StartArray paired with {end.TokenType}");
			}
			else
			{
				Debug.Assert(startIndex + 12 == endIndex, $"{start.TokenType} should have been one row");
			}
			int length = endIndex - startIndex;
			byte[] newDatabase = new byte[length];
			_data.AsSpan(startIndex, length).CopyTo(newDatabase);
			Span<int> newDbInts = MemoryMarshal.Cast<byte, int>(newDatabase);
			int locationOffset = newDbInts[0];
			if (start.TokenType == JsonTokenType.String)
			{
				locationOffset--;
			}
			for (int i = (length - 12) / 4; i >= 0; i -= 3)
			{
				Debug.Assert(newDbInts[i] >= locationOffset);
				newDbInts[i] -= locationOffset;
			}
			return new MetadataDb(newDatabase);
		}
	}

	private struct StackRow
	{
		internal const int Size = 8;

		internal int SizeOrLength;

		internal int NumberOfRows;

		internal StackRow(int sizeOrLength = 0, int numberOfRows = -1)
		{
			Debug.Assert(sizeOrLength >= 0);
			Debug.Assert(numberOfRows >= -1);
			SizeOrLength = sizeOrLength;
			NumberOfRows = numberOfRows;
		}
	}

	private struct StackRowStack : IDisposable
	{
		private byte[] _rentedBuffer;

		private int _topOfStack;

		internal StackRowStack(int initialSize)
		{
			_rentedBuffer = ArrayPool<byte>.Shared.Rent(initialSize);
			_topOfStack = _rentedBuffer.Length;
		}

		public void Dispose()
		{
			byte[] toReturn = _rentedBuffer;
			_rentedBuffer = null;
			_topOfStack = 0;
			if (toReturn != null)
			{
				ArrayPool<byte>.Shared.Return(toReturn);
			}
		}

		internal void Push(StackRow row)
		{
			if (_topOfStack < 8)
			{
				Enlarge();
			}
			_topOfStack -= 8;
			MemoryMarshal.Write(_rentedBuffer.AsSpan(_topOfStack), ref row);
		}

		internal StackRow Pop()
		{
			Debug.Assert(_topOfStack <= _rentedBuffer.Length - 8);
			StackRow row = MemoryMarshal.Read<StackRow>(_rentedBuffer.AsSpan(_topOfStack));
			_topOfStack += 8;
			return row;
		}

		private void Enlarge()
		{
			byte[] toReturn = _rentedBuffer;
			_rentedBuffer = ArrayPool<byte>.Shared.Rent(toReturn.Length * 2);
			Buffer.BlockCopy(toReturn, _topOfStack, _rentedBuffer, _rentedBuffer.Length - toReturn.Length + _topOfStack, toReturn.Length - _topOfStack);
			_topOfStack += _rentedBuffer.Length - toReturn.Length;
			ArrayPool<byte>.Shared.Return(toReturn);
		}
	}

	private ReadOnlyMemory<byte> _utf8Json;

	private MetadataDb _parsedData;

	private byte[] _extraRentedBytes;

	private (int, string) _lastIndexAndString = (-1, null);

	private const int UnseekableStreamInitialRentSize = 4096;

	internal bool IsDisposable { get; }

	public JsonElement RootElement => new JsonElement(this, 0);

	private JsonDocument(ReadOnlyMemory<byte> utf8Json, MetadataDb parsedData, byte[] extraRentedBytes, bool isDisposable = true)
	{
		Debug.Assert(!utf8Json.IsEmpty);
		_utf8Json = utf8Json;
		_parsedData = parsedData;
		_extraRentedBytes = extraRentedBytes;
		IsDisposable = isDisposable;
		Debug.Assert(isDisposable || extraRentedBytes == null);
	}

	public void Dispose()
	{
		int length = _utf8Json.Length;
		if (length != 0 && IsDisposable)
		{
			_parsedData.Dispose();
			_utf8Json = ReadOnlyMemory<byte>.Empty;
			byte[] extraRentedBytes = Interlocked.Exchange(ref _extraRentedBytes, null);
			if (extraRentedBytes != null)
			{
				extraRentedBytes.AsSpan(0, length).Clear();
				ArrayPool<byte>.Shared.Return(extraRentedBytes);
			}
		}
	}

	public void WriteTo(Utf8JsonWriter writer)
	{
		if (writer == null)
		{
			throw new ArgumentNullException("writer");
		}
		RootElement.WriteTo(writer);
	}

	internal JsonTokenType GetJsonTokenType(int index)
	{
		CheckNotDisposed();
		return _parsedData.GetJsonTokenType(index);
	}

	internal int GetArrayLength(int index)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.StartArray, row.TokenType);
		return row.SizeOrLength;
	}

	internal JsonElement GetArrayIndexElement(int currentIndex, int arrayIndex)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(currentIndex);
		CheckExpectedType(JsonTokenType.StartArray, row.TokenType);
		int arrayLength = row.SizeOrLength;
		if ((uint)arrayIndex >= (uint)arrayLength)
		{
			throw new IndexOutOfRangeException();
		}
		if (!row.HasComplexChildren)
		{
			return new JsonElement(this, currentIndex + (arrayIndex + 1) * 12);
		}
		int elementCount = 0;
		for (int objectOffset = currentIndex + 12; objectOffset < _parsedData.Length; objectOffset += 12)
		{
			if (arrayIndex == elementCount)
			{
				return new JsonElement(this, objectOffset);
			}
			row = _parsedData.Get(objectOffset);
			if (!row.IsSimpleValue)
			{
				objectOffset += 12 * row.NumberOfRows;
			}
			elementCount++;
		}
		Debug.Fail($"Ran out of database searching for array index {arrayIndex} from {currentIndex} when length was {arrayLength}");
		throw new IndexOutOfRangeException();
	}

	internal int GetEndIndex(int index, bool includeEndElement)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		if (row.IsSimpleValue)
		{
			return index + 12;
		}
		int endIndex = index + 12 * row.NumberOfRows;
		if (includeEndElement)
		{
			endIndex += 12;
		}
		return endIndex;
	}

	private ReadOnlyMemory<byte> GetRawValue(int index, bool includeQuotes)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		if (row.IsSimpleValue)
		{
			if (includeQuotes && row.TokenType == JsonTokenType.String)
			{
				return _utf8Json.Slice(row.Location - 1, row.SizeOrLength + 2);
			}
			return _utf8Json.Slice(row.Location, row.SizeOrLength);
		}
		int endElementIdx = GetEndIndex(index, includeEndElement: false);
		int start = row.Location;
		row = _parsedData.Get(endElementIdx);
		return _utf8Json.Slice(start, row.Location - start + row.SizeOrLength);
	}

	private ReadOnlyMemory<byte> GetPropertyRawValue(int valueIndex)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(valueIndex - 12);
		Debug.Assert(row.TokenType == JsonTokenType.PropertyName);
		int start = row.Location - 1;
		row = _parsedData.Get(valueIndex);
		int end;
		if (row.IsSimpleValue)
		{
			end = row.Location + row.SizeOrLength;
			if (row.TokenType == JsonTokenType.String)
			{
				end++;
			}
			return _utf8Json.Slice(start, end - start);
		}
		int endElementIdx = GetEndIndex(valueIndex, includeEndElement: false);
		row = _parsedData.Get(endElementIdx);
		end = row.Location + row.SizeOrLength;
		return _utf8Json.Slice(start, end - start);
	}

	internal string GetString(int index, JsonTokenType expectedType)
	{
		CheckNotDisposed();
		int lastIdx;
		string lastString;
		(lastIdx, lastString) = _lastIndexAndString;
		if (lastIdx == index)
		{
			return lastString;
		}
		DbRow row = _parsedData.Get(index);
		JsonTokenType tokenType = row.TokenType;
		if (tokenType == JsonTokenType.Null)
		{
			return null;
		}
		CheckExpectedType(expectedType, tokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (row.HasComplexChildren)
		{
			int backslash = segment.IndexOf<byte>(92);
			lastString = JsonReaderHelper.GetUnescapedString(segment, backslash);
		}
		else
		{
			lastString = JsonReaderHelper.TranscodeHelper(segment);
		}
		_lastIndexAndString = (index, lastString);
		return lastString;
	}

	internal bool TextEquals(int index, ReadOnlySpan<char> otherText, bool isPropertyName)
	{
		CheckNotDisposed();
		int matchIndex = (isPropertyName ? (index - 12) : index);
		var (lastIdx, lastString) = _lastIndexAndString;
		if (lastIdx == matchIndex)
		{
			return otherText.SequenceEqual(lastString.AsSpan());
		}
		byte[] otherUtf8TextArray = null;
		int length = checked(otherText.Length * 3);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(otherUtf8TextArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[256]);
		Span<byte> otherUtf8Text = span;
		ReadOnlySpan<byte> utf16Text = MemoryMarshal.AsBytes(otherText);
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
			result = TextEquals(index, otherUtf8Text.Slice(0, written), isPropertyName);
		}
		if (otherUtf8TextArray != null)
		{
			otherUtf8Text.Slice(0, written).Clear();
			ArrayPool<byte>.Shared.Return(otherUtf8TextArray);
		}
		return result;
	}

	internal bool TextEquals(int index, ReadOnlySpan<byte> otherUtf8Text, bool isPropertyName)
	{
		CheckNotDisposed();
		int matchIndex = (isPropertyName ? (index - 12) : index);
		DbRow row = _parsedData.Get(matchIndex);
		CheckExpectedType(isPropertyName ? JsonTokenType.PropertyName : JsonTokenType.String, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (otherUtf8Text.Length > segment.Length)
		{
			return false;
		}
		if (row.HasComplexChildren)
		{
			if (otherUtf8Text.Length < segment.Length / 6)
			{
				return false;
			}
			int idx = segment.IndexOf<byte>(92);
			Debug.Assert(idx != -1);
			if (!otherUtf8Text.StartsWith(segment.Slice(0, idx)))
			{
				return false;
			}
			return JsonReaderHelper.UnescapeAndCompare(segment.Slice(idx), otherUtf8Text.Slice(idx));
		}
		return segment.SequenceEqual(otherUtf8Text);
	}

	internal string GetNameOfPropertyValue(int index)
	{
		return GetString(index - 12, JsonTokenType.PropertyName);
	}

	internal bool TryGetValue(int index, out byte[] value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.String, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (row.HasComplexChildren)
		{
			int idx = segment.IndexOf<byte>(92);
			Debug.Assert(idx != -1);
			return JsonReaderHelper.TryGetUnescapedBase64Bytes(segment, idx, out value);
		}
		Debug.Assert(segment.IndexOf<byte>(92) == -1);
		return JsonReaderHelper.TryDecodeBase64(segment, out value);
	}

	internal bool TryGetValue(int index, out sbyte value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (Utf8Parser.TryParse(segment, out sbyte tmp, out int consumed, '\0') && consumed == segment.Length)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	internal bool TryGetValue(int index, out byte value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (Utf8Parser.TryParse(segment, out byte tmp, out int consumed, '\0') && consumed == segment.Length)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	internal bool TryGetValue(int index, out short value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (Utf8Parser.TryParse(segment, out short tmp, out int consumed, '\0') && consumed == segment.Length)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	internal bool TryGetValue(int index, out ushort value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (Utf8Parser.TryParse(segment, out ushort tmp, out int consumed, '\0') && consumed == segment.Length)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	internal bool TryGetValue(int index, out int value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (Utf8Parser.TryParse(segment, out int tmp, out int consumed, '\0') && consumed == segment.Length)
		{
			value = tmp;
			return true;
		}
		value = 0;
		return false;
	}

	internal bool TryGetValue(int index, out uint value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (Utf8Parser.TryParse(segment, out uint tmp, out int consumed, '\0') && consumed == segment.Length)
		{
			value = tmp;
			return true;
		}
		value = 0u;
		return false;
	}

	internal bool TryGetValue(int index, out long value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (Utf8Parser.TryParse(segment, out long tmp, out int consumed, '\0') && consumed == segment.Length)
		{
			value = tmp;
			return true;
		}
		value = 0L;
		return false;
	}

	internal bool TryGetValue(int index, out ulong value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (Utf8Parser.TryParse(segment, out ulong tmp, out int consumed, '\0') && consumed == segment.Length)
		{
			value = tmp;
			return true;
		}
		value = 0uL;
		return false;
	}

	internal bool TryGetValue(int index, out double value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		char standardFormat = (row.HasComplexChildren ? 'e' : '\0');
		if (Utf8Parser.TryParse(segment, out double tmp, out int bytesConsumed, standardFormat) && segment.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0.0;
		return false;
	}

	internal bool TryGetValue(int index, out float value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		char standardFormat = (row.HasComplexChildren ? 'e' : '\0');
		if (Utf8Parser.TryParse(segment, out float tmp, out int bytesConsumed, standardFormat) && segment.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = 0f;
		return false;
	}

	internal bool TryGetValue(int index, out decimal value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.Number, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		char standardFormat = (row.HasComplexChildren ? 'e' : '\0');
		if (Utf8Parser.TryParse(segment, out decimal tmp, out int bytesConsumed, standardFormat) && segment.Length == bytesConsumed)
		{
			value = tmp;
			return true;
		}
		value = default(decimal);
		return false;
	}

	internal bool TryGetValue(int index, out DateTime value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.String, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (!JsonReaderHelper.IsValidDateTimeOffsetParseLength(segment.Length))
		{
			value = default(DateTime);
			return false;
		}
		if (row.HasComplexChildren)
		{
			return JsonReaderHelper.TryGetEscapedDateTime(segment, out value);
		}
		Debug.Assert(segment.IndexOf<byte>(92) == -1);
		if (segment.Length <= 42 && JsonHelpers.TryParseAsISO(segment, out DateTime tmp))
		{
			value = tmp;
			return true;
		}
		value = default(DateTime);
		return false;
	}

	internal bool TryGetValue(int index, out DateTimeOffset value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.String, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (!JsonReaderHelper.IsValidDateTimeOffsetParseLength(segment.Length))
		{
			value = default(DateTimeOffset);
			return false;
		}
		if (row.HasComplexChildren)
		{
			return JsonReaderHelper.TryGetEscapedDateTimeOffset(segment, out value);
		}
		Debug.Assert(segment.IndexOf<byte>(92) == -1);
		if (segment.Length <= 42 && JsonHelpers.TryParseAsISO(segment, out DateTimeOffset tmp))
		{
			value = tmp;
			return true;
		}
		value = default(DateTimeOffset);
		return false;
	}

	internal bool TryGetValue(int index, out Guid value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.String, row.TokenType);
		ReadOnlySpan<byte> segment = _utf8Json.Span.Slice(row.Location, row.SizeOrLength);
		if (segment.Length > 216)
		{
			value = default(Guid);
			return false;
		}
		if (row.HasComplexChildren)
		{
			return JsonReaderHelper.TryGetEscapedGuid(segment, out value);
		}
		Debug.Assert(segment.IndexOf<byte>(92) == -1);
		if (segment.Length == 36 && Utf8Parser.TryParse(segment, out Guid tmp, out int _, 'D'))
		{
			value = tmp;
			return true;
		}
		value = default(Guid);
		return false;
	}

	internal string GetRawValueAsString(int index)
	{
		return JsonReaderHelper.TranscodeHelper(GetRawValue(index, includeQuotes: true).Span);
	}

	internal string GetPropertyRawValueAsString(int valueIndex)
	{
		return JsonReaderHelper.TranscodeHelper(GetPropertyRawValue(valueIndex).Span);
	}

	internal JsonElement CloneElement(int index)
	{
		int endIndex = GetEndIndex(index, includeEndElement: true);
		MetadataDb newDb = _parsedData.CopySegment(index, endIndex);
		ReadOnlyMemory<byte> segmentCopy = GetRawValue(index, includeQuotes: true).ToArray();
		JsonDocument newDocument = new JsonDocument(segmentCopy, newDb, null, isDisposable: false);
		return newDocument.RootElement;
	}

	internal void WriteElementTo(int index, Utf8JsonWriter writer)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		switch (row.TokenType)
		{
		case JsonTokenType.StartObject:
			writer.WriteStartObject();
			WriteComplexElement(index, writer);
			break;
		case JsonTokenType.StartArray:
			writer.WriteStartArray();
			WriteComplexElement(index, writer);
			break;
		case JsonTokenType.String:
			WriteString(in row, writer);
			break;
		case JsonTokenType.Number:
			writer.WriteNumberValue(_utf8Json.Slice(row.Location, row.SizeOrLength).Span);
			break;
		case JsonTokenType.True:
			writer.WriteBooleanValue(value: true);
			break;
		case JsonTokenType.False:
			writer.WriteBooleanValue(value: false);
			break;
		case JsonTokenType.Null:
			writer.WriteNullValue();
			break;
		default:
			Debug.Fail($"Unexpected encounter with JsonTokenType {row.TokenType}");
			break;
		}
	}

	private void WriteComplexElement(int index, Utf8JsonWriter writer)
	{
		int endIndex = GetEndIndex(index, includeEndElement: true);
		for (int i = index + 12; i < endIndex; i += 12)
		{
			DbRow row = _parsedData.Get(i);
			switch (row.TokenType)
			{
			case JsonTokenType.String:
				WriteString(in row, writer);
				break;
			case JsonTokenType.Number:
				writer.WriteNumberValue(_utf8Json.Slice(row.Location, row.SizeOrLength).Span);
				break;
			case JsonTokenType.True:
				writer.WriteBooleanValue(value: true);
				break;
			case JsonTokenType.False:
				writer.WriteBooleanValue(value: false);
				break;
			case JsonTokenType.Null:
				writer.WriteNullValue();
				break;
			case JsonTokenType.StartObject:
				writer.WriteStartObject();
				break;
			case JsonTokenType.EndObject:
				writer.WriteEndObject();
				break;
			case JsonTokenType.StartArray:
				writer.WriteStartArray();
				break;
			case JsonTokenType.EndArray:
				writer.WriteEndArray();
				break;
			case JsonTokenType.PropertyName:
				WritePropertyName(in row, writer);
				break;
			default:
				Debug.Fail($"Unexpected encounter with JsonTokenType {row.TokenType}");
				break;
			}
		}
	}

	private ReadOnlySpan<byte> UnescapeString(in DbRow row, out ArraySegment<byte> rented)
	{
		Debug.Assert(row.TokenType == JsonTokenType.String || row.TokenType == JsonTokenType.PropertyName);
		int loc = row.Location;
		int length = row.SizeOrLength;
		ReadOnlySpan<byte> text = _utf8Json.Slice(loc, length).Span;
		if (!row.HasComplexChildren)
		{
			rented = default(ArraySegment<byte>);
			return text;
		}
		int idx = text.IndexOf<byte>(92);
		Debug.Assert(idx >= 0);
		byte[] rent = ArrayPool<byte>.Shared.Rent(length);
		text.Slice(0, idx).CopyTo(rent);
		JsonReaderHelper.Unescape(text, rent, idx, out var written);
		rented = new ArraySegment<byte>(rent, 0, written);
		return rented.AsSpan();
	}

	private static void ClearAndReturn(ArraySegment<byte> rented)
	{
		if (rented.Array != null)
		{
			rented.AsSpan().Clear();
			ArrayPool<byte>.Shared.Return(rented.Array);
		}
	}

	private void WritePropertyName(in DbRow row, Utf8JsonWriter writer)
	{
		ArraySegment<byte> rented = default(ArraySegment<byte>);
		try
		{
			writer.WritePropertyName(UnescapeString(in row, out rented));
		}
		finally
		{
			ClearAndReturn(rented);
		}
	}

	private void WriteString(in DbRow row, Utf8JsonWriter writer)
	{
		ArraySegment<byte> rented = default(ArraySegment<byte>);
		try
		{
			writer.WriteStringValue(UnescapeString(in row, out rented));
		}
		finally
		{
			ClearAndReturn(rented);
		}
	}

	private static void Parse(ReadOnlySpan<byte> utf8JsonSpan, Utf8JsonReader reader, ref MetadataDb database, ref StackRowStack stack)
	{
		bool inArray = false;
		int arrayItemsCount = 0;
		int numberOfRowsForMembers = 0;
		int numberOfRowsForValues = 0;
		while (reader.Read())
		{
			JsonTokenType tokenType = reader.TokenType;
			Debug.Assert(reader.TokenStartIndex <= int.MaxValue);
			int tokenStart = (int)reader.TokenStartIndex;
			if (tokenType == JsonTokenType.StartObject)
			{
				if (inArray)
				{
					arrayItemsCount++;
				}
				numberOfRowsForValues++;
				database.Append(tokenType, tokenStart, -1);
				StackRow row = new StackRow(numberOfRowsForMembers + 1);
				stack.Push(row);
				numberOfRowsForMembers = 0;
			}
			else if (tokenType == JsonTokenType.EndObject)
			{
				int rowIndex = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartObject);
				numberOfRowsForValues++;
				numberOfRowsForMembers++;
				database.SetLength(rowIndex, numberOfRowsForMembers);
				int newRowIndex = database.Length;
				database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
				database.SetNumberOfRows(rowIndex, numberOfRowsForMembers);
				database.SetNumberOfRows(newRowIndex, numberOfRowsForMembers);
				numberOfRowsForMembers += stack.Pop().SizeOrLength;
			}
			else if (tokenType == JsonTokenType.StartArray)
			{
				if (inArray)
				{
					arrayItemsCount++;
				}
				numberOfRowsForMembers++;
				database.Append(tokenType, tokenStart, -1);
				StackRow row2 = new StackRow(arrayItemsCount, numberOfRowsForValues + 1);
				stack.Push(row2);
				arrayItemsCount = 0;
				numberOfRowsForValues = 0;
			}
			else if (tokenType == JsonTokenType.EndArray)
			{
				int rowIndex2 = database.FindIndexOfFirstUnsetSizeOrLength(JsonTokenType.StartArray);
				numberOfRowsForValues++;
				numberOfRowsForMembers++;
				database.SetLength(rowIndex2, arrayItemsCount);
				database.SetNumberOfRows(rowIndex2, numberOfRowsForValues);
				if (arrayItemsCount + 1 != numberOfRowsForValues)
				{
					database.SetHasComplexChildren(rowIndex2);
				}
				int newRowIndex2 = database.Length;
				database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
				database.SetNumberOfRows(newRowIndex2, numberOfRowsForValues);
				StackRow row3 = stack.Pop();
				arrayItemsCount = row3.SizeOrLength;
				numberOfRowsForValues += row3.NumberOfRows;
			}
			else if (tokenType == JsonTokenType.PropertyName)
			{
				numberOfRowsForValues++;
				numberOfRowsForMembers++;
				Debug.Assert(tokenStart < int.MaxValue);
				database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);
				if (reader._stringHasEscaping)
				{
					database.SetHasComplexChildren(database.Length - 12);
				}
				Debug.Assert(!inArray);
			}
			else
			{
				Debug.Assert((int)tokenType >= 7 && (int)tokenType <= 11);
				numberOfRowsForValues++;
				numberOfRowsForMembers++;
				if (inArray)
				{
					arrayItemsCount++;
				}
				if (tokenType == JsonTokenType.String)
				{
					Debug.Assert(tokenStart < int.MaxValue);
					database.Append(tokenType, tokenStart + 1, reader.ValueSpan.Length);
					if (reader._stringHasEscaping)
					{
						database.SetHasComplexChildren(database.Length - 12);
					}
				}
				else
				{
					database.Append(tokenType, tokenStart, reader.ValueSpan.Length);
					if (tokenType == JsonTokenType.Number)
					{
						char numberFormat = reader._numberFormat;
						char c = numberFormat;
						if (c == 'e')
						{
							database.SetHasComplexChildren(database.Length - 12);
						}
						else
						{
							Debug.Assert(reader._numberFormat == '\0', $"Unhandled numeric format {reader._numberFormat}");
						}
					}
				}
			}
			inArray = reader.IsInArray;
		}
		Debug.Assert(reader.BytesConsumed == utf8JsonSpan.Length);
		database.TrimExcess();
	}

	private void CheckNotDisposed()
	{
		if (_utf8Json.IsEmpty)
		{
			throw new ObjectDisposedException("JsonDocument");
		}
	}

	private void CheckExpectedType(JsonTokenType expected, JsonTokenType actual)
	{
		if (expected != actual)
		{
			throw ThrowHelper.GetJsonElementWrongTypeException(expected, actual);
		}
	}

	private static void CheckSupportedOptions(JsonReaderOptions readerOptions, string paramName)
	{
		Debug.Assert((int)readerOptions.CommentHandling >= 0 && (int)readerOptions.CommentHandling <= 2);
		if (readerOptions.CommentHandling == JsonCommentHandling.Allow)
		{
			throw new ArgumentException("SR.JsonDocumentDoesNotSupportComments", paramName);
		}
	}

	public static JsonDocument Parse(ReadOnlyMemory<byte> utf8Json, JsonDocumentOptions options = default(JsonDocumentOptions))
	{
		return Parse(utf8Json, options.GetReaderOptions(), null);
	}

	public static JsonDocument Parse(ReadOnlySequence<byte> utf8Json, JsonDocumentOptions options = default(JsonDocumentOptions))
	{
		JsonReaderOptions readerOptions = options.GetReaderOptions();
		if (utf8Json.IsSingleSegment)
		{
			return Parse(utf8Json.First, readerOptions, null);
		}
		int length = checked((int)utf8Json.Length);
		byte[] utf8Bytes = ArrayPool<byte>.Shared.Rent(length);
		try
		{
			utf8Json.CopyTo(utf8Bytes.AsSpan());
			return Parse(utf8Bytes.AsMemory(0, length), readerOptions, utf8Bytes);
		}
		catch
		{
			utf8Bytes.AsSpan(0, length).Clear();
			ArrayPool<byte>.Shared.Return(utf8Bytes);
			throw;
		}
	}

	public static JsonDocument Parse(Stream utf8Json, JsonDocumentOptions options = default(JsonDocumentOptions))
	{
		if (utf8Json == null)
		{
			throw new ArgumentNullException("utf8Json");
		}
		ArraySegment<byte> drained = ReadToEnd(utf8Json);
		try
		{
			return Parse(drained.AsMemory(), options.GetReaderOptions(), drained.Array);
		}
		catch
		{
			drained.AsSpan().Clear();
			ArrayPool<byte>.Shared.Return(drained.Array);
			throw;
		}
	}

	public static Task<JsonDocument> ParseAsync(Stream utf8Json, JsonDocumentOptions options = default(JsonDocumentOptions), CancellationToken cancellationToken = default(CancellationToken))
	{
		if (utf8Json == null)
		{
			throw new ArgumentNullException("utf8Json");
		}
		return ParseAsyncCore(utf8Json, options, cancellationToken);
	}

	private static async Task<JsonDocument> ParseAsyncCore(Stream utf8Json, JsonDocumentOptions options = default(JsonDocumentOptions), CancellationToken cancellationToken = default(CancellationToken))
	{
		ArraySegment<byte> drained = await TaskTheraotExtensions.ConfigureAwait(ReadToEndAsync(utf8Json, cancellationToken), continueOnCapturedContext: false);
		try
		{
			return Parse(drained.AsMemory(), options.GetReaderOptions(), drained.Array);
		}
		catch
		{
			drained.AsSpan().Clear();
			ArrayPool<byte>.Shared.Return(drained.Array);
			throw;
		}
	}

	public static JsonDocument Parse(ReadOnlyMemory<char> json, JsonDocumentOptions options = default(JsonDocumentOptions))
	{
		ReadOnlySpan<char> jsonChars = json.Span;
		int expectedByteCount = JsonReaderHelper.GetUtf8ByteCount(jsonChars);
		byte[] utf8Bytes = ArrayPool<byte>.Shared.Rent(expectedByteCount);
		try
		{
			int actualByteCount = JsonReaderHelper.GetUtf8FromText(jsonChars, utf8Bytes);
			Debug.Assert(expectedByteCount == actualByteCount);
			return Parse(utf8Bytes.AsMemory(0, actualByteCount), options.GetReaderOptions(), utf8Bytes);
		}
		catch
		{
			utf8Bytes.AsSpan(0, expectedByteCount).Clear();
			ArrayPool<byte>.Shared.Return(utf8Bytes);
			throw;
		}
	}

	public static JsonDocument Parse(string json, JsonDocumentOptions options = default(JsonDocumentOptions))
	{
		if (json == null)
		{
			throw new ArgumentNullException("json");
		}
		return Parse(json.AsMemory(), options);
	}

	public static bool TryParseValue(ref Utf8JsonReader reader, out JsonDocument document)
	{
		return TryParseValue(ref reader, out document, shouldThrow: false);
	}

	public static JsonDocument ParseValue(ref Utf8JsonReader reader)
	{
		JsonDocument document;
		bool ret = TryParseValue(ref reader, out document, shouldThrow: true);
		Debug.Assert(ret, "TryParseValue returned false with shouldThrow: true.");
		return document;
	}

	private static bool TryParseValue(ref Utf8JsonReader reader, out JsonDocument document, bool shouldThrow)
	{
		JsonReaderState state = reader.CurrentState;
		CheckSupportedOptions(state.Options, "reader");
		Utf8JsonReader restore = reader;
		ReadOnlySpan<byte> valueSpan = default(ReadOnlySpan<byte>);
		ReadOnlySequence<byte> valueSequence = default(ReadOnlySequence<byte>);
		try
		{
			JsonTokenType tokenType = reader.TokenType;
			JsonTokenType jsonTokenType = tokenType;
			if ((jsonTokenType == JsonTokenType.None || jsonTokenType == JsonTokenType.PropertyName) && !reader.Read())
			{
				if (shouldThrow)
				{
					ThrowHelper.ThrowJsonReaderException(ref reader, ExceptionResource.ExpectedJsonTokens, 0);
				}
				reader = restore;
				document = null;
				return false;
			}
			switch (reader.TokenType)
			{
			case JsonTokenType.StartObject:
			case JsonTokenType.StartArray:
			{
				long startingOffset = reader.TokenStartIndex;
				int depth = reader.CurrentDepth;
				do
				{
					if (!reader.Read())
					{
						if (shouldThrow)
						{
							ThrowHelper.ThrowJsonReaderException(ref reader, ExceptionResource.ExpectedJsonTokens, 0);
						}
						reader = restore;
						document = null;
						return false;
					}
				}
				while (reader.CurrentDepth > depth);
				long totalLength = reader.BytesConsumed - startingOffset;
				ReadOnlySequence<byte> sequence2 = reader.OriginalSequence;
				if (sequence2.IsEmpty)
				{
					valueSpan = checked(reader.OriginalSpan.Slice((int)startingOffset, (int)totalLength));
				}
				else
				{
					valueSequence = sequence2.Slice(startingOffset, totalLength);
				}
				Debug.Assert(reader.TokenType == JsonTokenType.EndObject || reader.TokenType == JsonTokenType.EndArray);
				break;
			}
			case JsonTokenType.Number:
			case JsonTokenType.True:
			case JsonTokenType.False:
			case JsonTokenType.Null:
				if (reader.HasValueSequence)
				{
					valueSequence = reader.ValueSequence;
				}
				else
				{
					valueSpan = reader.ValueSpan;
				}
				break;
			case JsonTokenType.String:
			{
				ReadOnlySequence<byte> sequence = reader.OriginalSequence;
				if (sequence.IsEmpty)
				{
					int payloadLength = reader.ValueSpan.Length + 2;
					Debug.Assert(payloadLength > 1);
					ReadOnlySpan<byte> readerSpan = reader.OriginalSpan;
					Debug.Assert(readerSpan[(int)reader.TokenStartIndex] == 34, $"Calculated span starts with {readerSpan[(int)reader.TokenStartIndex]}");
					Debug.Assert(readerSpan[(int)reader.TokenStartIndex + payloadLength - 1] == 34, $"Calculated span ends with {readerSpan[(int)reader.TokenStartIndex + payloadLength - 1]}");
					valueSpan = readerSpan.Slice((int)reader.TokenStartIndex, payloadLength);
				}
				else
				{
					long payloadLength2 = 2L;
					payloadLength2 = ((!reader.HasValueSequence) ? (payloadLength2 + reader.ValueSpan.Length) : (payloadLength2 + reader.ValueSequence.Length));
					valueSequence = sequence.Slice(reader.TokenStartIndex, payloadLength2);
					Debug.Assert(valueSequence.First.Span[0] == 34, $"Calculated sequence starts with {valueSequence.First.Span[0]}");
					Debug.Assert(BuffersExtensions.ToArray(in valueSequence)[payloadLength2 - 1] == 34, $"Calculated sequence ends with {BuffersExtensions.ToArray(in valueSequence)[payloadLength2 - 1]}");
				}
				break;
			}
			default:
				if (shouldThrow)
				{
					Debug.Assert(!reader.HasValueSequence);
					byte displayByte = reader.ValueSpan[0];
					ThrowHelper.ThrowJsonReaderException(ref reader, ExceptionResource.ExpectedStartOfValueNotFound, displayByte);
				}
				reader = restore;
				document = null;
				return false;
			}
		}
		catch
		{
			reader = restore;
			throw;
		}
		int length = (valueSpan.IsEmpty ? checked((int)valueSequence.Length) : valueSpan.Length);
		byte[] rented = ArrayPool<byte>.Shared.Rent(length);
		Span<byte> rentedSpan = rented.AsSpan(0, length);
		try
		{
			if (valueSpan.IsEmpty)
			{
				valueSequence.CopyTo(rentedSpan);
			}
			else
			{
				valueSpan.CopyTo(rentedSpan);
			}
			document = Parse(rented.AsMemory(0, length), state.Options, rented);
			return true;
		}
		catch
		{
			rentedSpan.Clear();
			ArrayPool<byte>.Shared.Return(rented);
			throw;
		}
	}

	private static JsonDocument Parse(ReadOnlyMemory<byte> utf8Json, JsonReaderOptions readerOptions, byte[] extraRentedBytes)
	{
		ReadOnlySpan<byte> utf8JsonSpan = utf8Json.Span;
		Utf8JsonReader reader = new Utf8JsonReader(utf8JsonSpan, isFinalBlock: true, new JsonReaderState(readerOptions));
		MetadataDb database = new MetadataDb(utf8Json.Length);
		StackRowStack stack = new StackRowStack(512);
		try
		{
			Parse(utf8JsonSpan, reader, ref database, ref stack);
		}
		catch
		{
			database.Dispose();
			throw;
		}
		finally
		{
			stack.Dispose();
		}
		return new JsonDocument(utf8Json, database, extraRentedBytes);
	}

	private static ArraySegment<byte> ReadToEnd(Stream stream)
	{
		int written = 0;
		byte[] rented = null;
		ReadOnlySpan<byte> utf8Bom = JsonConstants.Utf8Bom;
		try
		{
			if (stream.CanSeek)
			{
				long expectedLength = Math.Max(utf8Bom.Length, stream.Length - stream.Position) + 1;
				rented = ArrayPool<byte>.Shared.Rent(checked((int)expectedLength));
			}
			else
			{
				rented = ArrayPool<byte>.Shared.Rent(4096);
			}
			int lastRead;
			do
			{
				Debug.Assert(rented.Length >= utf8Bom.Length);
				lastRead = stream.Read(rented, written, utf8Bom.Length - written);
				written += lastRead;
			}
			while (lastRead > 0 && written < utf8Bom.Length);
			if (written == utf8Bom.Length && utf8Bom.SequenceEqual(rented.AsSpan(0, utf8Bom.Length)))
			{
				written = 0;
			}
			do
			{
				if (rented.Length == written)
				{
					byte[] toReturn = rented;
					rented = ArrayPool<byte>.Shared.Rent(checked(toReturn.Length * 2));
					Buffer.BlockCopy(toReturn, 0, rented, 0, toReturn.Length);
					ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
				}
				lastRead = stream.Read(rented, written, rented.Length - written);
				written += lastRead;
			}
			while (lastRead > 0);
			return new ArraySegment<byte>(rented, 0, written);
		}
		catch
		{
			if (rented != null)
			{
				rented.AsSpan(0, written).Clear();
				ArrayPool<byte>.Shared.Return(rented);
			}
			throw;
		}
	}

	private static async Task<ArraySegment<byte>> ReadToEndAsync(Stream stream, CancellationToken cancellationToken)
	{
		int written = 0;
		byte[] rented = null;
		try
		{
			int utf8BomLength = JsonConstants.Utf8Bom.Length;
			if (stream.CanSeek)
			{
				long expectedLength = Math.Max(utf8BomLength, stream.Length - stream.Position) + 1;
				rented = ArrayPool<byte>.Shared.Rent(checked((int)expectedLength));
			}
			else
			{
				rented = ArrayPool<byte>.Shared.Rent(4096);
			}
			int lastRead2;
			do
			{
				Debug.Assert(rented.Length >= JsonConstants.Utf8Bom.Length);
				lastRead2 = await TaskTheraotExtensions.ConfigureAwait(StreamTheraotExtensions.ReadAsync(stream, rented, written, utf8BomLength - written, cancellationToken), continueOnCapturedContext: false);
				written += lastRead2;
			}
			while (lastRead2 > 0 && written < utf8BomLength);
			if (written == utf8BomLength && JsonConstants.Utf8Bom.SequenceEqual(rented.AsSpan(0, utf8BomLength)))
			{
				written = 0;
			}
			do
			{
				if (rented.Length == written)
				{
					byte[] toReturn = rented;
					rented = ArrayPool<byte>.Shared.Rent(toReturn.Length * 2);
					Buffer.BlockCopy(toReturn, 0, rented, 0, toReturn.Length);
					ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
				}
				lastRead2 = await TaskTheraotExtensions.ConfigureAwait(StreamTheraotExtensions.ReadAsync(stream, rented, written, rented.Length - written, cancellationToken), continueOnCapturedContext: false);
				written += lastRead2;
			}
			while (lastRead2 > 0);
			return new ArraySegment<byte>(rented, 0, written);
		}
		catch
		{
			if (rented != null)
			{
				rented.AsSpan(0, written).Clear();
				ArrayPool<byte>.Shared.Return(rented);
			}
			throw;
		}
	}

	internal bool TryGetNamedPropertyValue(int index, ReadOnlySpan<char> propertyName, out JsonElement value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.StartObject, row.TokenType);
		if (row.NumberOfRows == 1)
		{
			value = default(JsonElement);
			return false;
		}
		int maxBytes = JsonReaderHelper.s_utf8Encoding.GetMaxByteCount(propertyName.Length);
		int startIndex = index + 12;
		int endIndex = checked(row.NumberOfRows * 12 + index);
		if (maxBytes < 256)
		{
			Span<byte> utf8Name2 = stackalloc byte[256];
			int len = JsonReaderHelper.GetUtf8FromText(propertyName, utf8Name2);
			utf8Name2 = utf8Name2.Slice(0, len);
			return TryGetNamedPropertyValue(startIndex, endIndex, utf8Name2, out value);
		}
		int minBytes = propertyName.Length;
		int candidateIndex;
		for (candidateIndex = endIndex - 12; candidateIndex > index; candidateIndex -= 12)
		{
			int passedIndex = candidateIndex;
			row = _parsedData.Get(candidateIndex);
			Debug.Assert(row.TokenType != JsonTokenType.PropertyName);
			if (row.IsSimpleValue)
			{
				candidateIndex -= 12;
			}
			else
			{
				Debug.Assert(row.NumberOfRows > 0);
				candidateIndex -= 12 * (row.NumberOfRows + 1);
			}
			row = _parsedData.Get(candidateIndex);
			Debug.Assert(row.TokenType == JsonTokenType.PropertyName);
			if (row.SizeOrLength >= minBytes)
			{
				byte[] tmpUtf8 = ArrayPool<byte>.Shared.Rent(maxBytes);
				Span<byte> utf8Name = default(Span<byte>);
				try
				{
					int len2 = JsonReaderHelper.GetUtf8FromText(propertyName, tmpUtf8);
					utf8Name = tmpUtf8.AsSpan(0, len2);
					return TryGetNamedPropertyValue(startIndex, passedIndex + 12, utf8Name, out value);
				}
				finally
				{
					utf8Name.Clear();
					ArrayPool<byte>.Shared.Return(tmpUtf8);
				}
			}
		}
		value = default(JsonElement);
		return false;
	}

	internal bool TryGetNamedPropertyValue(int index, ReadOnlySpan<byte> propertyName, out JsonElement value)
	{
		CheckNotDisposed();
		DbRow row = _parsedData.Get(index);
		CheckExpectedType(JsonTokenType.StartObject, row.TokenType);
		if (row.NumberOfRows == 1)
		{
			value = default(JsonElement);
			return false;
		}
		int endIndex = checked(row.NumberOfRows * 12 + index);
		return TryGetNamedPropertyValue(index + 12, endIndex, propertyName, out value);
	}

	private bool TryGetNamedPropertyValue(int startIndex, int endIndex, ReadOnlySpan<byte> propertyName, out JsonElement value)
	{
		ReadOnlySpan<byte> documentSpan = _utf8Json.Span;
		int index;
		for (index = endIndex - 12; index > startIndex; index -= 12)
		{
			DbRow row = _parsedData.Get(index);
			Debug.Assert(row.TokenType != JsonTokenType.PropertyName);
			if (row.IsSimpleValue)
			{
				index -= 12;
			}
			else
			{
				Debug.Assert(row.NumberOfRows > 0);
				index -= 12 * (row.NumberOfRows + 1);
			}
			row = _parsedData.Get(index);
			Debug.Assert(row.TokenType == JsonTokenType.PropertyName);
			ReadOnlySpan<byte> currentPropertyName = documentSpan.Slice(row.Location, row.SizeOrLength);
			if (row.HasComplexChildren)
			{
				if (currentPropertyName.Length > propertyName.Length)
				{
					int idx = currentPropertyName.IndexOf<byte>(92);
					Debug.Assert(idx >= 0);
					if (propertyName.Length > idx && currentPropertyName.Slice(0, idx).SequenceEqual(propertyName.Slice(0, idx)))
					{
						int remaining = currentPropertyName.Length - idx;
						int written = 0;
						byte[] rented = null;
						try
						{
							Span<byte> span = ((remaining > 256) ? ((Span<byte>)(rented = ArrayPool<byte>.Shared.Rent(remaining))) : stackalloc byte[remaining]);
							Span<byte> utf8Unescaped = span;
							JsonReaderHelper.Unescape(currentPropertyName.Slice(idx), utf8Unescaped, 0, out written);
							if (utf8Unescaped.Slice(0, written).SequenceEqual(propertyName.Slice(idx)))
							{
								value = new JsonElement(this, index + 12);
								return true;
							}
						}
						finally
						{
							if (rented != null)
							{
								rented.AsSpan(0, written).Clear();
								ArrayPool<byte>.Shared.Return(rented);
							}
						}
					}
				}
			}
			else if (currentPropertyName.SequenceEqual(propertyName))
			{
				value = new JsonElement(this, index + 12);
				return true;
			}
		}
		value = default(JsonElement);
		return false;
	}
}
