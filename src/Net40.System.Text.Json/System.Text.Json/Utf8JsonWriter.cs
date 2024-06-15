#define DEBUG
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.Json;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class Utf8JsonWriter : IDisposable, IAsyncDisposable
{
	private static readonly int s_newLineLength = Environment.NewLine.Length;

	private const int DefaultGrowthSize = 4096;

	private const int InitialGrowthSize = 256;

	private IBufferWriter<byte> _output;

	private Stream _stream;

	private ArrayBufferWriter<byte> _arrayBufferWriter;

	private Memory<byte> _memory;

	private bool _inObject;

	private JsonTokenType _tokenType;

	private BitStack _bitStack;

	private int _currentDepth;

	private JsonWriterOptions _options;

	private static char[] s_singleLineCommentDelimiter = new char[2] { '*', '/' };

	private static readonly StandardFormat s_dateTimeStandardFormat = new StandardFormat('O');

	public int BytesPending { get; private set; }

	public long BytesCommitted { get; private set; }

	public JsonWriterOptions Options => _options;

	private int Indentation => CurrentDepth * 2;

	public int CurrentDepth => _currentDepth & 0x7FFFFFFF;

	[DebuggerBrowsable(DebuggerBrowsableState.Never)]
	private string DebuggerDisplay => $"BytesCommitted = {BytesCommitted} BytesPending = {BytesPending} CurrentDepth = {CurrentDepth}";

	private static ReadOnlySpan<byte> SingleLineCommentDelimiterUtf8 => "*/"u8;

	public Utf8JsonWriter(IBufferWriter<byte> bufferWriter, JsonWriterOptions options = default(JsonWriterOptions))
	{
		_output = bufferWriter ?? throw new ArgumentNullException("bufferWriter");
		_stream = null;
		_arrayBufferWriter = null;
		BytesPending = 0;
		BytesCommitted = 0L;
		_memory = default(Memory<byte>);
		_inObject = false;
		_tokenType = JsonTokenType.None;
		_currentDepth = 0;
		_options = options;
		_bitStack = default(BitStack);
	}

	public Utf8JsonWriter(Stream utf8Json, JsonWriterOptions options = default(JsonWriterOptions))
	{
		if (utf8Json == null)
		{
			throw new ArgumentNullException("utf8Json");
		}
		if (!utf8Json.CanWrite)
		{
			throw new ArgumentException("SR.StreamNotWritable");
		}
		_stream = utf8Json;
		_arrayBufferWriter = new ArrayBufferWriter<byte>();
		_output = null;
		BytesPending = 0;
		BytesCommitted = 0L;
		_memory = default(Memory<byte>);
		_inObject = false;
		_tokenType = JsonTokenType.None;
		_currentDepth = 0;
		_options = options;
		_bitStack = default(BitStack);
	}

	public void Reset()
	{
		CheckNotDisposed();
		_arrayBufferWriter?.Clear();
		ResetHelper();
	}

	public void Reset(Stream utf8Json)
	{
		CheckNotDisposed();
		if (utf8Json == null)
		{
			throw new ArgumentNullException("utf8Json");
		}
		if (!utf8Json.CanWrite)
		{
			throw new ArgumentException("SR.StreamNotWritable");
		}
		_stream = utf8Json;
		if (_arrayBufferWriter == null)
		{
			_arrayBufferWriter = new ArrayBufferWriter<byte>();
		}
		else
		{
			_arrayBufferWriter.Clear();
		}
		_output = null;
		ResetHelper();
	}

	public void Reset(IBufferWriter<byte> bufferWriter)
	{
		CheckNotDisposed();
		_output = bufferWriter ?? throw new ArgumentNullException("bufferWriter");
		_stream = null;
		_arrayBufferWriter = null;
		ResetHelper();
	}

	private void ResetHelper()
	{
		BytesPending = 0;
		BytesCommitted = 0L;
		_memory = default(Memory<byte>);
		_inObject = false;
		_tokenType = JsonTokenType.None;
		_currentDepth = 0;
		_bitStack = default(BitStack);
	}

	private void CheckNotDisposed()
	{
		if (_stream == null && _output == null)
		{
			throw new ObjectDisposedException("Utf8JsonWriter");
		}
	}

	public void Flush()
	{
		CheckNotDisposed();
		_memory = default(Memory<byte>);
		if (_stream != null)
		{
			Debug.Assert(_arrayBufferWriter != null);
			if (BytesPending != 0)
			{
				_arrayBufferWriter.Advance(BytesPending);
				BytesPending = 0;
				Debug.Assert(_arrayBufferWriter.WrittenMemory.Length == _arrayBufferWriter.WrittenCount);
				ArraySegment<byte> underlyingBuffer;
				bool result = MemoryMarshal.TryGetArray(_arrayBufferWriter.WrittenMemory, out underlyingBuffer);
				Debug.Assert(underlyingBuffer.Offset == 0);
				Debug.Assert(_arrayBufferWriter.WrittenCount == underlyingBuffer.Count);
				_stream.Write(underlyingBuffer.Array, underlyingBuffer.Offset, underlyingBuffer.Count);
				BytesCommitted += _arrayBufferWriter.WrittenCount;
				_arrayBufferWriter.Clear();
			}
			_stream.Flush();
		}
		else
		{
			Debug.Assert(_output != null);
			if (BytesPending != 0)
			{
				_output.Advance(BytesPending);
				BytesCommitted += BytesPending;
				BytesPending = 0;
			}
		}
	}

	public void Dispose()
	{
		if (_stream != null || _output != null)
		{
			Flush();
			ResetHelper();
			_stream = null;
			_arrayBufferWriter = null;
			_output = null;
		}
	}

	public async ValueTask DisposeAsync()
	{
		if (_stream != null || _output != null)
		{
			await TaskTheraotExtensions.ConfigureAwait(FlushAsync(), continueOnCapturedContext: false);
			ResetHelper();
			_stream = null;
			_arrayBufferWriter = null;
			_output = null;
		}
	}

	public async Task FlushAsync(CancellationToken cancellationToken = default(CancellationToken))
	{
		CheckNotDisposed();
		_memory = default(Memory<byte>);
		if (_stream != null)
		{
			Debug.Assert(_arrayBufferWriter != null);
			if (BytesPending != 0)
			{
				_arrayBufferWriter.Advance(BytesPending);
				BytesPending = 0;
				Debug.Assert(_arrayBufferWriter.WrittenMemory.Length == _arrayBufferWriter.WrittenCount);
				MemoryMarshal.TryGetArray(_arrayBufferWriter.WrittenMemory, out var underlyingBuffer);
				Debug.Assert(underlyingBuffer.Offset == 0);
				Debug.Assert(_arrayBufferWriter.WrittenCount == underlyingBuffer.Count);
				await TaskTheraotExtensions.ConfigureAwait(StreamTheraotExtensions.WriteAsync(_stream, underlyingBuffer.Array, underlyingBuffer.Offset, underlyingBuffer.Count, cancellationToken), continueOnCapturedContext: false);
				BytesCommitted += _arrayBufferWriter.WrittenCount;
				_arrayBufferWriter.Clear();
			}
			await TaskTheraotExtensions.ConfigureAwait(StreamTheraotExtensions.FlushAsync(_stream, cancellationToken), continueOnCapturedContext: false);
		}
		else
		{
			Debug.Assert(_output != null);
			if (BytesPending != 0)
			{
				_output.Advance(BytesPending);
				BytesCommitted += BytesPending;
				BytesPending = 0;
			}
		}
	}

	public void WriteStartArray()
	{
		WriteStart(91);
		_tokenType = JsonTokenType.StartArray;
	}

	public void WriteStartObject()
	{
		WriteStart(123);
		_tokenType = JsonTokenType.StartObject;
	}

	private void WriteStart(byte token)
	{
		if (CurrentDepth >= 1000)
		{
			ThrowHelper.ThrowInvalidOperationException(ExceptionResource.DepthTooLarge, _currentDepth, 0, JsonTokenType.None);
		}
		if (_options.IndentedOrNotSkipValidation)
		{
			WriteStartSlow(token);
		}
		else
		{
			WriteStartMinimized(token);
		}
		_currentDepth &= int.MaxValue;
		_currentDepth++;
	}

	private void WriteStartMinimized(byte token)
	{
		if (_memory.Length - BytesPending < 2)
		{
			Grow(2);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = token;
	}

	private void WriteStartSlow(byte token)
	{
		Debug.Assert(_options.Indented || !_options.SkipValidation);
		if (_options.Indented)
		{
			if (!_options.SkipValidation)
			{
				ValidateStart();
				UpdateBitStackOnStart(token);
			}
			WriteStartIndented(token);
		}
		else
		{
			Debug.Assert(!_options.SkipValidation);
			ValidateStart();
			UpdateBitStackOnStart(token);
			WriteStartMinimized(token);
		}
	}

	private void ValidateStart()
	{
		if (_inObject)
		{
			if (_tokenType != JsonTokenType.PropertyName)
			{
				Debug.Assert(_tokenType != 0 && _tokenType != JsonTokenType.StartArray);
				ThrowHelper.ThrowInvalidOperationException(ExceptionResource.CannotStartObjectArrayWithoutProperty, 0, 0, _tokenType);
			}
			return;
		}
		Debug.Assert(_tokenType != JsonTokenType.PropertyName);
		Debug.Assert(_tokenType != JsonTokenType.StartObject);
		if (CurrentDepth == 0 && _tokenType != 0)
		{
			ThrowHelper.ThrowInvalidOperationException(ExceptionResource.CannotStartObjectArrayAfterPrimitiveOrClose, 0, 0, _tokenType);
		}
	}

	private void WriteStartIndented(byte token)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int minRequired = indent + 1;
		int maxRequired = minRequired + 3;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		output[BytesPending++] = token;
	}

	public void WriteStartArray(JsonEncodedText propertyName)
	{
		WriteStartHelper(propertyName.EncodedUtf8Bytes, 91);
		_tokenType = JsonTokenType.StartArray;
	}

	public void WriteStartObject(JsonEncodedText propertyName)
	{
		WriteStartHelper(propertyName.EncodedUtf8Bytes, 123);
		_tokenType = JsonTokenType.StartObject;
	}

	private void WriteStartHelper(ReadOnlySpan<byte> utf8PropertyName, byte token)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		ValidateDepth();
		WriteStartByOptions(utf8PropertyName, token);
		_currentDepth &= int.MaxValue;
		_currentDepth++;
	}

	public void WriteStartArray(ReadOnlySpan<byte> utf8PropertyName)
	{
		ValidatePropertyNameAndDepth(utf8PropertyName);
		WriteStartEscape(utf8PropertyName, 91);
		_currentDepth &= int.MaxValue;
		_currentDepth++;
		_tokenType = JsonTokenType.StartArray;
	}

	public void WriteStartObject(ReadOnlySpan<byte> utf8PropertyName)
	{
		ValidatePropertyNameAndDepth(utf8PropertyName);
		WriteStartEscape(utf8PropertyName, 123);
		_currentDepth &= int.MaxValue;
		_currentDepth++;
		_tokenType = JsonTokenType.StartObject;
	}

	private void WriteStartEscape(ReadOnlySpan<byte> utf8PropertyName, byte token)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteStartEscapeProperty(utf8PropertyName, token, propertyIdx);
		}
		else
		{
			WriteStartByOptions(utf8PropertyName, token);
		}
	}

	private void WriteStartByOptions(ReadOnlySpan<byte> utf8PropertyName, byte token)
	{
		ValidateWritingProperty(token);
		if (_options.Indented)
		{
			WritePropertyNameIndented(utf8PropertyName, token);
		}
		else
		{
			WritePropertyNameMinimized(utf8PropertyName, token);
		}
	}

	private void WriteStartEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, byte token, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteStartByOptions(escapedPropertyName.Slice(0, written), token);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	public void WriteStartArray(string propertyName)
	{
		WriteStartArray((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan());
	}

	public void WriteStartObject(string propertyName)
	{
		WriteStartObject((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan());
	}

	public void WriteStartArray(ReadOnlySpan<char> propertyName)
	{
		ValidatePropertyNameAndDepth(propertyName);
		WriteStartEscape(propertyName, 91);
		_currentDepth &= int.MaxValue;
		_currentDepth++;
		_tokenType = JsonTokenType.StartArray;
	}

	public void WriteStartObject(ReadOnlySpan<char> propertyName)
	{
		ValidatePropertyNameAndDepth(propertyName);
		WriteStartEscape(propertyName, 123);
		_currentDepth &= int.MaxValue;
		_currentDepth++;
		_tokenType = JsonTokenType.StartObject;
	}

	private void WriteStartEscape(ReadOnlySpan<char> propertyName, byte token)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteStartEscapeProperty(propertyName, token, propertyIdx);
		}
		else
		{
			WriteStartByOptions(propertyName, token);
		}
	}

	private void WriteStartByOptions(ReadOnlySpan<char> propertyName, byte token)
	{
		ValidateWritingProperty(token);
		if (_options.Indented)
		{
			WritePropertyNameIndented(propertyName, token);
		}
		else
		{
			WritePropertyNameMinimized(propertyName, token);
		}
	}

	private void WriteStartEscapeProperty(ReadOnlySpan<char> propertyName, byte token, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteStartByOptions(escapedPropertyName.Slice(0, written), token);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	public void WriteEndArray()
	{
		WriteEnd(93);
		_tokenType = JsonTokenType.EndArray;
	}

	public void WriteEndObject()
	{
		WriteEnd(125);
		_tokenType = JsonTokenType.EndObject;
	}

	private void WriteEnd(byte token)
	{
		if (_options.IndentedOrNotSkipValidation)
		{
			WriteEndSlow(token);
		}
		else
		{
			WriteEndMinimized(token);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		if (CurrentDepth != 0)
		{
			_currentDepth--;
		}
	}

	private void WriteEndMinimized(byte token)
	{
		if (_memory.Length - BytesPending < 1)
		{
			Grow(1);
		}
		_memory.Span[BytesPending++] = token;
	}

	private void WriteEndSlow(byte token)
	{
		Debug.Assert(_options.Indented || !_options.SkipValidation);
		if (_options.Indented)
		{
			if (!_options.SkipValidation)
			{
				ValidateEnd(token);
			}
			WriteEndIndented(token);
		}
		else
		{
			Debug.Assert(!_options.SkipValidation);
			ValidateEnd(token);
			WriteEndMinimized(token);
		}
	}

	private void ValidateEnd(byte token)
	{
		if (_bitStack.CurrentDepth <= 0 || _tokenType == JsonTokenType.PropertyName)
		{
			ThrowHelper.ThrowInvalidOperationException(ExceptionResource.MismatchedObjectArray, 0, token, _tokenType);
		}
		if (token == 93)
		{
			if (_inObject)
			{
				Debug.Assert(_tokenType != JsonTokenType.None);
				ThrowHelper.ThrowInvalidOperationException(ExceptionResource.MismatchedObjectArray, 0, token, _tokenType);
			}
		}
		else
		{
			Debug.Assert(token == 125);
			if (!_inObject)
			{
				ThrowHelper.ThrowInvalidOperationException(ExceptionResource.MismatchedObjectArray, 0, token, _tokenType);
			}
		}
		_inObject = _bitStack.Pop();
	}

	private void WriteEndIndented(byte token)
	{
		if (_tokenType == JsonTokenType.StartObject || _tokenType == JsonTokenType.StartArray)
		{
			WriteEndMinimized(token);
			return;
		}
		int indent = Indentation;
		if (indent != 0)
		{
			indent -= 2;
		}
		Debug.Assert(indent <= 2000);
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.None);
		int maxRequired = indent + 3;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		WriteNewLine(output);
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = token;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void WriteNewLine(Span<byte> output)
	{
		if (s_newLineLength == 2)
		{
			output[BytesPending++] = 13;
		}
		output[BytesPending++] = 10;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void UpdateBitStackOnStart(byte token)
	{
		if (token == 91)
		{
			_bitStack.PushFalse();
			_inObject = false;
		}
		else
		{
			Debug.Assert(token == 123);
			_bitStack.PushTrue();
			_inObject = true;
		}
	}

	private void Grow(int requiredSize)
	{
		Debug.Assert(requiredSize > 0);
		if (_memory.Length == 0)
		{
			FirstCallToGetMemory(requiredSize);
			return;
		}
		int sizeHint = Math.Max(4096, requiredSize);
		Debug.Assert(BytesPending != 0);
		if (_stream != null)
		{
			Debug.Assert(_arrayBufferWriter != null);
			_memory = _arrayBufferWriter.GetMemory(checked(BytesPending + sizeHint));
			Debug.Assert(_memory.Length >= sizeHint);
			return;
		}
		Debug.Assert(_output != null);
		_output.Advance(BytesPending);
		BytesCommitted += BytesPending;
		BytesPending = 0;
		_memory = _output.GetMemory(sizeHint);
		if (_memory.Length < sizeHint)
		{
			ThrowHelper.ThrowInvalidOperationException_NeedLargerSpan();
		}
	}

	private void FirstCallToGetMemory(int requiredSize)
	{
		Debug.Assert(_memory.Length == 0);
		Debug.Assert(BytesPending == 0);
		int sizeHint = Math.Max(256, requiredSize);
		if (_stream != null)
		{
			Debug.Assert(_arrayBufferWriter != null);
			_memory = _arrayBufferWriter.GetMemory(sizeHint);
			Debug.Assert(_memory.Length >= sizeHint);
			return;
		}
		Debug.Assert(_output != null);
		_memory = _output.GetMemory(sizeHint);
		if (_memory.Length < sizeHint)
		{
			ThrowHelper.ThrowInvalidOperationException_NeedLargerSpan();
		}
	}

	private void SetFlagToAddListSeparatorBeforeNextItem()
	{
		_currentDepth |= int.MinValue;
	}

	public void WriteBase64String(JsonEncodedText propertyName, ReadOnlySpan<byte> bytes)
	{
		WriteBase64StringHelper(propertyName.EncodedUtf8Bytes, bytes);
	}

	private void WriteBase64StringHelper(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		JsonWriterHelper.ValidateBytes(bytes);
		WriteBase64ByOptions(utf8PropertyName, bytes);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteBase64String(string propertyName, ReadOnlySpan<byte> bytes)
	{
		WriteBase64String((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), bytes);
	}

	public void WriteBase64String(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
	{
		JsonWriterHelper.ValidatePropertyAndBytes(propertyName, bytes);
		WriteBase64Escape(propertyName, bytes);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteBase64String(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
	{
		JsonWriterHelper.ValidatePropertyAndBytes(utf8PropertyName, bytes);
		WriteBase64Escape(utf8PropertyName, bytes);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteBase64Escape(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteBase64EscapeProperty(propertyName, bytes, propertyIdx);
		}
		else
		{
			WriteBase64ByOptions(propertyName, bytes);
		}
	}

	private void WriteBase64Escape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteBase64EscapeProperty(utf8PropertyName, bytes, propertyIdx);
		}
		else
		{
			WriteBase64ByOptions(utf8PropertyName, bytes);
		}
	}

	private void WriteBase64EscapeProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteBase64ByOptions(escapedPropertyName.Slice(0, written), bytes);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteBase64EscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteBase64ByOptions(escapedPropertyName.Slice(0, written), bytes);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteBase64ByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> bytes)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteBase64Indented(propertyName, bytes);
		}
		else
		{
			WriteBase64Minimized(propertyName, bytes);
		}
	}

	private void WriteBase64ByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> bytes)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteBase64Indented(utf8PropertyName, bytes);
		}
		else
		{
			WriteBase64Minimized(utf8PropertyName, bytes);
		}
	}

	private void WriteBase64Minimized(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> bytes)
	{
		int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
		Debug.Assert(escapedPropertyName.Length * 3 < int.MaxValue - encodedLength - 6);
		int maxRequired = escapedPropertyName.Length * 3 + encodedLength + 6;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		Base64EncodeAndWrite(bytes, output, encodedLength);
		output[BytesPending++] = 34;
	}

	private void WriteBase64Minimized(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> bytes)
	{
		int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - encodedLength - 6);
		int maxRequired = escapedPropertyName.Length + encodedLength + 6;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		Base64EncodeAndWrite(bytes, output, encodedLength);
		output[BytesPending++] = 34;
	}

	private void WriteBase64Indented(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> bytes)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
		Debug.Assert(escapedPropertyName.Length * 3 < int.MaxValue - indent - encodedLength - 7 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + encodedLength + 7 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		Base64EncodeAndWrite(bytes, output, encodedLength);
		output[BytesPending++] = 34;
	}

	private void WriteBase64Indented(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> bytes)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int encodedLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - encodedLength - 7 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length + encodedLength + 7 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		Base64EncodeAndWrite(bytes, output, encodedLength);
		output[BytesPending++] = 34;
	}

	public void WriteString(JsonEncodedText propertyName, DateTime value)
	{
		WriteStringHelper(propertyName.EncodedUtf8Bytes, value);
	}

	private void WriteStringHelper(ReadOnlySpan<byte> utf8PropertyName, DateTime value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		WriteStringByOptions(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(string propertyName, DateTime value)
	{
		WriteString((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteString(ReadOnlySpan<char> propertyName, DateTime value)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		WriteStringEscape(propertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(ReadOnlySpan<byte> utf8PropertyName, DateTime value)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		WriteStringEscape(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteStringEscape(ReadOnlySpan<char> propertyName, DateTime value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteStringEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteStringByOptions(propertyName, value);
		}
	}

	private void WriteStringEscape(ReadOnlySpan<byte> utf8PropertyName, DateTime value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteStringEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteStringByOptions(utf8PropertyName, value);
		}
	}

	private void WriteStringEscapeProperty(ReadOnlySpan<char> propertyName, DateTime value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, DateTime value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<char> propertyName, DateTime value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(propertyName, value);
		}
		else
		{
			WriteStringMinimized(propertyName, value);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<byte> utf8PropertyName, DateTime value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(utf8PropertyName, value);
		}
		else
		{
			WriteStringMinimized(utf8PropertyName, value);
		}
	}

	private void WriteStringMinimized(ReadOnlySpan<char> escapedPropertyName, DateTime value)
	{
		Debug.Assert(escapedPropertyName.Length < 715827843);
		int maxRequired = escapedPropertyName.Length * 3 + 33 + 6;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringMinimized(ReadOnlySpan<byte> escapedPropertyName, DateTime value)
	{
		Debug.Assert(escapedPropertyName.Length < 2147483608);
		int minRequired = escapedPropertyName.Length + 33 + 5;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<char> escapedPropertyName, DateTime value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - 33 - 7 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 33 + 7 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<byte> escapedPropertyName, DateTime value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 33 - 7 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 33 + 6;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	public void WriteString(JsonEncodedText propertyName, DateTimeOffset value)
	{
		WriteStringHelper(propertyName.EncodedUtf8Bytes, value);
	}

	private void WriteStringHelper(ReadOnlySpan<byte> utf8PropertyName, DateTimeOffset value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		WriteStringByOptions(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(string propertyName, DateTimeOffset value)
	{
		WriteString((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteString(ReadOnlySpan<char> propertyName, DateTimeOffset value)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		WriteStringEscape(propertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(ReadOnlySpan<byte> utf8PropertyName, DateTimeOffset value)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		WriteStringEscape(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteStringEscape(ReadOnlySpan<char> propertyName, DateTimeOffset value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteStringEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteStringByOptions(propertyName, value);
		}
	}

	private void WriteStringEscape(ReadOnlySpan<byte> utf8PropertyName, DateTimeOffset value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteStringEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteStringByOptions(utf8PropertyName, value);
		}
	}

	private void WriteStringEscapeProperty(ReadOnlySpan<char> propertyName, DateTimeOffset value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, DateTimeOffset value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<char> propertyName, DateTimeOffset value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(propertyName, value);
		}
		else
		{
			WriteStringMinimized(propertyName, value);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<byte> utf8PropertyName, DateTimeOffset value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(utf8PropertyName, value);
		}
		else
		{
			WriteStringMinimized(utf8PropertyName, value);
		}
	}

	private void WriteStringMinimized(ReadOnlySpan<char> escapedPropertyName, DateTimeOffset value)
	{
		Debug.Assert(escapedPropertyName.Length < 715827843);
		int maxRequired = escapedPropertyName.Length * 3 + 33 + 6;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringMinimized(ReadOnlySpan<byte> escapedPropertyName, DateTimeOffset value)
	{
		Debug.Assert(escapedPropertyName.Length < 2147483608);
		int minRequired = escapedPropertyName.Length + 33 + 5;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<char> escapedPropertyName, DateTimeOffset value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - 33 - 7 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 33 + 7 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<byte> escapedPropertyName, DateTimeOffset value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 33 - 7 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 33 + 6;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	public void WriteNumber(JsonEncodedText propertyName, decimal value)
	{
		WriteNumberHelper(propertyName.EncodedUtf8Bytes, value);
	}

	private void WriteNumberHelper(ReadOnlySpan<byte> utf8PropertyName, decimal value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		WriteNumberByOptions(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	public void WriteNumber(string propertyName, decimal value)
	{
		WriteNumber((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteNumber(ReadOnlySpan<char> propertyName, decimal value)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		WriteNumberEscape(propertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, decimal value)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		WriteNumberEscape(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberEscape(ReadOnlySpan<char> propertyName, decimal value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(propertyName, value);
		}
	}

	private void WriteNumberEscape(ReadOnlySpan<byte> utf8PropertyName, decimal value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(utf8PropertyName, value);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<char> propertyName, decimal value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, decimal value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<char> propertyName, decimal value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(propertyName, value);
		}
		else
		{
			WriteNumberMinimized(propertyName, value);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<byte> utf8PropertyName, decimal value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(utf8PropertyName, value);
		}
		else
		{
			WriteNumberMinimized(utf8PropertyName, value);
		}
	}

	private void WriteNumberMinimized(ReadOnlySpan<char> escapedPropertyName, decimal value)
	{
		Debug.Assert(escapedPropertyName.Length < 715827847);
		int maxRequired = escapedPropertyName.Length * 3 + 31 + 4;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberMinimized(ReadOnlySpan<byte> escapedPropertyName, decimal value)
	{
		Debug.Assert(escapedPropertyName.Length < 2147483612);
		int minRequired = escapedPropertyName.Length + 31 + 3;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<char> escapedPropertyName, decimal value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - 31 - 5 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 31 + 5 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<byte> escapedPropertyName, decimal value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 31 - 5 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 31 + 4;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	public void WriteNumber(JsonEncodedText propertyName, double value)
	{
		WriteNumberHelper(propertyName.EncodedUtf8Bytes, value);
	}

	private void WriteNumberHelper(ReadOnlySpan<byte> utf8PropertyName, double value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		JsonWriterHelper.ValidateDouble(value);
		WriteNumberByOptions(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	public void WriteNumber(string propertyName, double value)
	{
		WriteNumber((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteNumber(ReadOnlySpan<char> propertyName, double value)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		JsonWriterHelper.ValidateDouble(value);
		WriteNumberEscape(propertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, double value)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		JsonWriterHelper.ValidateDouble(value);
		WriteNumberEscape(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberEscape(ReadOnlySpan<char> propertyName, double value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(propertyName, value);
		}
	}

	private void WriteNumberEscape(ReadOnlySpan<byte> utf8PropertyName, double value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(utf8PropertyName, value);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<char> propertyName, double value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, double value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<char> propertyName, double value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(propertyName, value);
		}
		else
		{
			WriteNumberMinimized(propertyName, value);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<byte> utf8PropertyName, double value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(utf8PropertyName, value);
		}
		else
		{
			WriteNumberMinimized(utf8PropertyName, value);
		}
	}

	private void WriteNumberMinimized(ReadOnlySpan<char> escapedPropertyName, double value)
	{
		Debug.Assert(escapedPropertyName.Length < 715827750);
		int maxRequired = escapedPropertyName.Length * 3 + 128 + 4;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = TryFormatDouble(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberMinimized(ReadOnlySpan<byte> escapedPropertyName, double value)
	{
		Debug.Assert(escapedPropertyName.Length < 2147483515);
		int minRequired = escapedPropertyName.Length + 128 + 3;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = TryFormatDouble(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<char> escapedPropertyName, double value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - 128 - 5 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 128 + 5 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = TryFormatDouble(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<byte> escapedPropertyName, double value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 128 - 5 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 128 + 4;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = TryFormatDouble(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	public void WriteNumber(JsonEncodedText propertyName, float value)
	{
		WriteNumberHelper(propertyName.EncodedUtf8Bytes, value);
	}

	private void WriteNumberHelper(ReadOnlySpan<byte> utf8PropertyName, float value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		JsonWriterHelper.ValidateSingle(value);
		WriteNumberByOptions(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	public void WriteNumber(string propertyName, float value)
	{
		WriteNumber((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteNumber(ReadOnlySpan<char> propertyName, float value)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		JsonWriterHelper.ValidateSingle(value);
		WriteNumberEscape(propertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, float value)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		JsonWriterHelper.ValidateSingle(value);
		WriteNumberEscape(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberEscape(ReadOnlySpan<char> propertyName, float value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(propertyName, value);
		}
	}

	private void WriteNumberEscape(ReadOnlySpan<byte> utf8PropertyName, float value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(utf8PropertyName, value);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<char> propertyName, float value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, float value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<char> propertyName, float value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(propertyName, value);
		}
		else
		{
			WriteNumberMinimized(propertyName, value);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<byte> utf8PropertyName, float value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(utf8PropertyName, value);
		}
		else
		{
			WriteNumberMinimized(utf8PropertyName, value);
		}
	}

	private void WriteNumberMinimized(ReadOnlySpan<char> escapedPropertyName, float value)
	{
		Debug.Assert(escapedPropertyName.Length < 715827750);
		int maxRequired = escapedPropertyName.Length * 3 + 128 + 4;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = TryFormatSingle(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberMinimized(ReadOnlySpan<byte> escapedPropertyName, float value)
	{
		Debug.Assert(escapedPropertyName.Length < 2147483515);
		int minRequired = escapedPropertyName.Length + 128 + 3;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = TryFormatSingle(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<char> escapedPropertyName, float value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - 128 - 5 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 128 + 5 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = TryFormatSingle(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<byte> escapedPropertyName, float value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 128 - 5 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 128 + 4;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = TryFormatSingle(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	internal void WriteNumber(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8FormattedNumber)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		JsonWriterHelper.ValidateValue(utf8FormattedNumber);
		JsonWriterHelper.ValidateNumber(utf8FormattedNumber);
		WriteNumberEscape(propertyName, utf8FormattedNumber);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	internal void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8FormattedNumber)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		JsonWriterHelper.ValidateValue(utf8FormattedNumber);
		JsonWriterHelper.ValidateNumber(utf8FormattedNumber);
		WriteNumberEscape(utf8PropertyName, utf8FormattedNumber);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	internal void WriteNumber(JsonEncodedText propertyName, ReadOnlySpan<byte> utf8FormattedNumber)
	{
		JsonWriterHelper.ValidateValue(utf8FormattedNumber);
		JsonWriterHelper.ValidateNumber(utf8FormattedNumber);
		WriteNumberByOptions(propertyName.EncodedUtf8Bytes, utf8FormattedNumber);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberEscape(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(propertyName, value);
		}
	}

	private void WriteNumberEscape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(utf8PropertyName, value);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteLiteralIndented(propertyName, value);
		}
		else
		{
			WriteLiteralMinimized(propertyName, value);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteLiteralIndented(utf8PropertyName, value);
		}
		else
		{
			WriteLiteralMinimized(utf8PropertyName, value);
		}
	}

	public void WriteString(JsonEncodedText propertyName, Guid value)
	{
		WriteStringHelper(propertyName.EncodedUtf8Bytes, value);
	}

	private void WriteStringHelper(ReadOnlySpan<byte> utf8PropertyName, Guid value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		WriteStringByOptions(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(string propertyName, Guid value)
	{
		WriteString((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteString(ReadOnlySpan<char> propertyName, Guid value)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		WriteStringEscape(propertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(ReadOnlySpan<byte> utf8PropertyName, Guid value)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		WriteStringEscape(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteStringEscape(ReadOnlySpan<char> propertyName, Guid value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteStringEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteStringByOptions(propertyName, value);
		}
	}

	private void WriteStringEscape(ReadOnlySpan<byte> utf8PropertyName, Guid value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteStringEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteStringByOptions(utf8PropertyName, value);
		}
	}

	private void WriteStringEscapeProperty(ReadOnlySpan<char> propertyName, Guid value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, Guid value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<char> propertyName, Guid value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(propertyName, value);
		}
		else
		{
			WriteStringMinimized(propertyName, value);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<byte> utf8PropertyName, Guid value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(utf8PropertyName, value);
		}
		else
		{
			WriteStringMinimized(utf8PropertyName, value);
		}
	}

	private void WriteStringMinimized(ReadOnlySpan<char> escapedPropertyName, Guid value)
	{
		Debug.Assert(escapedPropertyName.Length < 715827840);
		int maxRequired = escapedPropertyName.Length * 3 + 36 + 6;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringMinimized(ReadOnlySpan<byte> escapedPropertyName, Guid value)
	{
		Debug.Assert(escapedPropertyName.Length < 2147483605);
		int minRequired = escapedPropertyName.Length + 36 + 5;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<char> escapedPropertyName, Guid value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - 36 - 7 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 36 + 7 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<byte> escapedPropertyName, Guid value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 36 - 7 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 36 + 6;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void ValidatePropertyNameAndDepth(ReadOnlySpan<char> propertyName)
	{
		if (propertyName.Length > 166666666 || CurrentDepth >= 1000)
		{
			ThrowHelper.ThrowInvalidOperationOrArgumentException(propertyName, _currentDepth);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void ValidatePropertyNameAndDepth(ReadOnlySpan<byte> utf8PropertyName)
	{
		if (utf8PropertyName.Length > 166666666 || CurrentDepth >= 1000)
		{
			ThrowHelper.ThrowInvalidOperationOrArgumentException(utf8PropertyName, _currentDepth);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void ValidateDepth()
	{
		if (CurrentDepth >= 1000)
		{
			ThrowHelper.ThrowInvalidOperationException(_currentDepth);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void ValidateWritingProperty()
	{
		if (!_options.SkipValidation && (!_inObject || _tokenType == JsonTokenType.PropertyName))
		{
			Debug.Assert(_tokenType != JsonTokenType.StartObject);
			ThrowHelper.ThrowInvalidOperationException(ExceptionResource.CannotWritePropertyWithinArray, 0, 0, _tokenType);
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void ValidateWritingProperty(byte token)
	{
		if (!_options.SkipValidation)
		{
			if (!_inObject || _tokenType == JsonTokenType.PropertyName)
			{
				Debug.Assert(_tokenType != JsonTokenType.StartObject);
				ThrowHelper.ThrowInvalidOperationException(ExceptionResource.CannotWritePropertyWithinArray, 0, 0, _tokenType);
			}
			UpdateBitStackOnStart(token);
		}
	}

	private void WritePropertyNameMinimized(ReadOnlySpan<byte> escapedPropertyName, byte token)
	{
		Debug.Assert(escapedPropertyName.Length < 2147483642);
		int minRequired = escapedPropertyName.Length + 4;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = token;
	}

	private void WritePropertyNameIndented(ReadOnlySpan<byte> escapedPropertyName, byte token)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 6 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 5;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = token;
	}

	private void WritePropertyNameMinimized(ReadOnlySpan<char> escapedPropertyName, byte token)
	{
		Debug.Assert(escapedPropertyName.Length < 715827877);
		int maxRequired = escapedPropertyName.Length * 3 + 5;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = token;
	}

	private void WritePropertyNameIndented(ReadOnlySpan<char> escapedPropertyName, byte token)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - 6 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 6 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = token;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void TranscodeAndWrite(ReadOnlySpan<char> escapedPropertyName, Span<byte> output)
	{
		ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(escapedPropertyName);
		int consumed;
		int written;
		OperationStatus status = JsonWriterHelper.ToUtf8(byteSpan, output.Slice(BytesPending), out consumed, out written);
		Debug.Assert(status == OperationStatus.Done);
		Debug.Assert(consumed == byteSpan.Length);
		BytesPending += written;
	}

	public void WriteNull(JsonEncodedText propertyName)
	{
		WriteLiteralHelper(propertyName.EncodedUtf8Bytes, JsonConstants.NullValue);
		_tokenType = JsonTokenType.Null;
	}

	private void WriteLiteralHelper(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		WriteLiteralByOptions(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
	}

	public void WriteNull(string propertyName)
	{
		WriteNull((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan());
	}

	public void WriteNull(ReadOnlySpan<char> propertyName)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		ReadOnlySpan<byte> span = JsonConstants.NullValue;
		WriteLiteralEscape(propertyName, span);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Null;
	}

	public void WriteNull(ReadOnlySpan<byte> utf8PropertyName)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		ReadOnlySpan<byte> span = JsonConstants.NullValue;
		WriteLiteralEscape(utf8PropertyName, span);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Null;
	}

	public void WriteBoolean(JsonEncodedText propertyName, bool value)
	{
		if (value)
		{
			WriteLiteralHelper(propertyName.EncodedUtf8Bytes, JsonConstants.TrueValue);
			_tokenType = JsonTokenType.True;
		}
		else
		{
			WriteLiteralHelper(propertyName.EncodedUtf8Bytes, JsonConstants.FalseValue);
			_tokenType = JsonTokenType.False;
		}
	}

	public void WriteBoolean(string propertyName, bool value)
	{
		WriteBoolean((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteBoolean(ReadOnlySpan<char> propertyName, bool value)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		ReadOnlySpan<byte> span = (value ? JsonConstants.TrueValue : JsonConstants.FalseValue);
		WriteLiteralEscape(propertyName, span);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = (value ? JsonTokenType.True : JsonTokenType.False);
	}

	public void WriteBoolean(ReadOnlySpan<byte> utf8PropertyName, bool value)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		ReadOnlySpan<byte> span = (value ? JsonConstants.TrueValue : JsonConstants.FalseValue);
		WriteLiteralEscape(utf8PropertyName, span);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = (value ? JsonTokenType.True : JsonTokenType.False);
	}

	private void WriteLiteralEscape(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteLiteralEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteLiteralByOptions(propertyName, value);
		}
	}

	private void WriteLiteralEscape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteLiteralEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteLiteralByOptions(utf8PropertyName, value);
		}
	}

	private void WriteLiteralEscapeProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteLiteralByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteLiteralEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteLiteralByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteLiteralByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteLiteralIndented(propertyName, value);
		}
		else
		{
			WriteLiteralMinimized(propertyName, value);
		}
	}

	private void WriteLiteralByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteLiteralIndented(utf8PropertyName, value);
		}
		else
		{
			WriteLiteralMinimized(utf8PropertyName, value);
		}
	}

	private void WriteLiteralMinimized(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> value)
	{
		Debug.Assert(value.Length <= 166666666);
		Debug.Assert(escapedPropertyName.Length < 715827882 - value.Length - 4);
		int maxRequired = escapedPropertyName.Length * 3 + value.Length + 4;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		value.CopyTo(output.Slice(BytesPending));
		BytesPending += value.Length;
	}

	private void WriteLiteralMinimized(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> value)
	{
		Debug.Assert(value.Length <= 166666666);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - value.Length - 4);
		int minRequired = escapedPropertyName.Length + value.Length + 3;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		value.CopyTo(output.Slice(BytesPending));
		BytesPending += value.Length;
	}

	private void WriteLiteralIndented(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(value.Length <= 166666666);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - value.Length - 5 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + value.Length + 5 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		value.CopyTo(output.Slice(BytesPending));
		BytesPending += value.Length;
	}

	private void WriteLiteralIndented(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(value.Length <= 166666666);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - value.Length - 5 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + value.Length + 4;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		value.CopyTo(output.Slice(BytesPending));
		BytesPending += value.Length;
	}

	public void WriteNumber(JsonEncodedText propertyName, long value)
	{
		WriteNumberHelper(propertyName.EncodedUtf8Bytes, value);
	}

	private void WriteNumberHelper(ReadOnlySpan<byte> utf8PropertyName, long value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		WriteNumberByOptions(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	public void WriteNumber(string propertyName, long value)
	{
		WriteNumber((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteNumber(ReadOnlySpan<char> propertyName, long value)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		WriteNumberEscape(propertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, long value)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		WriteNumberEscape(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	public void WriteNumber(JsonEncodedText propertyName, int value)
	{
		WriteNumber(propertyName, (long)value);
	}

	public void WriteNumber(string propertyName, int value)
	{
		WriteNumber((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), (long)value);
	}

	public void WriteNumber(ReadOnlySpan<char> propertyName, int value)
	{
		WriteNumber(propertyName, (long)value);
	}

	public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, int value)
	{
		WriteNumber(utf8PropertyName, (long)value);
	}

	private void WriteNumberEscape(ReadOnlySpan<char> propertyName, long value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(propertyName, value);
		}
	}

	private void WriteNumberEscape(ReadOnlySpan<byte> utf8PropertyName, long value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(utf8PropertyName, value);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<char> propertyName, long value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, long value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<char> propertyName, long value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(propertyName, value);
		}
		else
		{
			WriteNumberMinimized(propertyName, value);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<byte> utf8PropertyName, long value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(utf8PropertyName, value);
		}
		else
		{
			WriteNumberMinimized(utf8PropertyName, value);
		}
	}

	private void WriteNumberMinimized(ReadOnlySpan<char> escapedPropertyName, long value)
	{
		Debug.Assert(escapedPropertyName.Length < 715827858);
		int maxRequired = escapedPropertyName.Length * 3 + 20 + 4;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberMinimized(ReadOnlySpan<byte> escapedPropertyName, long value)
	{
		Debug.Assert(escapedPropertyName.Length < 2147483623);
		int minRequired = escapedPropertyName.Length + 20 + 3;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<char> escapedPropertyName, long value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - 20 - 5 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 20 + 5 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<byte> escapedPropertyName, long value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 20 - 5 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 20 + 4;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	public void WritePropertyName(JsonEncodedText propertyName)
	{
		WritePropertyNameHelper(propertyName.EncodedUtf8Bytes);
	}

	private void WritePropertyNameHelper(ReadOnlySpan<byte> utf8PropertyName)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		WriteStringByOptionsPropertyName(utf8PropertyName);
		_currentDepth &= int.MaxValue;
		_tokenType = JsonTokenType.PropertyName;
	}

	public void WritePropertyName(string propertyName)
	{
		WritePropertyName((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan());
	}

	public void WritePropertyName(ReadOnlySpan<char> propertyName)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length && propertyIdx < 1073741823);
		if (propertyIdx != -1)
		{
			WriteStringEscapeProperty(propertyName, propertyIdx);
		}
		else
		{
			WriteStringByOptionsPropertyName(propertyName);
		}
		_currentDepth &= int.MaxValue;
		_tokenType = JsonTokenType.PropertyName;
	}

	private unsafe void WriteStringEscapeProperty(ReadOnlySpan<char> propertyName, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		char[] propertyArray = null;
		if (firstEscapeIndexProp != -1)
		{
			int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
			Span<char> escapedPropertyName;
			if (length > 256)
			{
				propertyArray = ArrayPool<char>.Shared.Rent(length);
				escapedPropertyName = propertyArray;
			}
			else
			{
				char* ptr = stackalloc char[length];
				escapedPropertyName = new Span<char>(ptr, length);
			}
			JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
			propertyName = escapedPropertyName.Slice(0, written);
		}
		WriteStringByOptionsPropertyName(propertyName);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringByOptionsPropertyName(ReadOnlySpan<char> propertyName)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndentedPropertyName(propertyName);
		}
		else
		{
			WriteStringMinimizedPropertyName(propertyName);
		}
	}

	private void WriteStringMinimizedPropertyName(ReadOnlySpan<char> escapedPropertyName)
	{
		Debug.Assert(escapedPropertyName.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < 715827881);
		int maxRequired = escapedPropertyName.Length * 3 + 4;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
	}

	private void WriteStringIndentedPropertyName(ReadOnlySpan<char> escapedPropertyName)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < (2147483642 - indent - s_newLineLength) / 3);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 5 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
	}

	public void WritePropertyName(ReadOnlySpan<byte> utf8PropertyName)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length && propertyIdx < 1073741823);
		if (propertyIdx != -1)
		{
			WriteStringEscapeProperty(utf8PropertyName, propertyIdx);
		}
		else
		{
			WriteStringByOptionsPropertyName(utf8PropertyName);
		}
		_currentDepth &= int.MaxValue;
		_tokenType = JsonTokenType.PropertyName;
	}

	private unsafe void WriteStringEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		byte[] propertyArray = null;
		if (firstEscapeIndexProp != -1)
		{
			int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
			Span<byte> escapedPropertyName;
			if (length > 256)
			{
				propertyArray = ArrayPool<byte>.Shared.Rent(length);
				escapedPropertyName = propertyArray;
			}
			else
			{
				byte* ptr = stackalloc byte[(int)(uint)length];
				escapedPropertyName = new Span<byte>(ptr, length);
			}
			JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
			utf8PropertyName = escapedPropertyName.Slice(0, written);
		}
		WriteStringByOptionsPropertyName(utf8PropertyName);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringByOptionsPropertyName(ReadOnlySpan<byte> utf8PropertyName)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndentedPropertyName(utf8PropertyName);
		}
		else
		{
			WriteStringMinimizedPropertyName(utf8PropertyName);
		}
	}

	private void WriteStringMinimizedPropertyName(ReadOnlySpan<byte> escapedPropertyName)
	{
		Debug.Assert(escapedPropertyName.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < 2147483643);
		int minRequired = escapedPropertyName.Length + 3;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
	}

	private void WriteStringIndentedPropertyName(ReadOnlySpan<byte> escapedPropertyName)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 5 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 4;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
	}

	public void WriteString(JsonEncodedText propertyName, JsonEncodedText value)
	{
		WriteStringHelper(propertyName.EncodedUtf8Bytes, value.EncodedUtf8Bytes);
	}

	private void WriteStringHelper(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666 && utf8Value.Length <= 166666666);
		WriteStringByOptions(utf8PropertyName, utf8Value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(string propertyName, JsonEncodedText value)
	{
		WriteString((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteString(string propertyName, string value)
	{
		if (propertyName == null)
		{
			throw new ArgumentNullException("propertyName");
		}
		if (value == null)
		{
			WriteNull(propertyName.AsSpan());
		}
		else
		{
			WriteString(propertyName.AsSpan(), value.AsSpan());
		}
	}

	public void WriteString(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
	{
		JsonWriterHelper.ValidatePropertyAndValue(propertyName, value);
		WriteStringEscape(propertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
	{
		JsonWriterHelper.ValidatePropertyAndValue(utf8PropertyName, utf8Value);
		WriteStringEscape(utf8PropertyName, utf8Value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(JsonEncodedText propertyName, string value)
	{
		if (value == null)
		{
			WriteNull(propertyName);
		}
		else
		{
			WriteString(propertyName, value.AsSpan());
		}
	}

	public void WriteString(JsonEncodedText propertyName, ReadOnlySpan<char> value)
	{
		WriteStringHelperEscapeValue(propertyName.EncodedUtf8Bytes, value);
	}

	private void WriteStringHelperEscapeValue(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<char> value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		JsonWriterHelper.ValidateValue(value);
		int valueIdx = JsonWriterHelper.NeedsEscaping(value, _options.Encoder);
		Debug.Assert(valueIdx >= -1 && valueIdx < value.Length && valueIdx < 1073741823);
		if (valueIdx != -1)
		{
			WriteStringEscapeValueOnly(utf8PropertyName, value, valueIdx);
		}
		else
		{
			WriteStringByOptions(utf8PropertyName, value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(string propertyName, ReadOnlySpan<char> value)
	{
		WriteString((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	public void WriteString(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<char> value)
	{
		JsonWriterHelper.ValidatePropertyAndValue(utf8PropertyName, value);
		WriteStringEscape(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(JsonEncodedText propertyName, ReadOnlySpan<byte> utf8Value)
	{
		WriteStringHelperEscapeValue(propertyName.EncodedUtf8Bytes, utf8Value);
	}

	private void WriteStringHelperEscapeValue(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		JsonWriterHelper.ValidateValue(utf8Value);
		int valueIdx = JsonWriterHelper.NeedsEscaping(utf8Value, _options.Encoder);
		Debug.Assert(valueIdx >= -1 && valueIdx < utf8Value.Length && valueIdx < 1073741823);
		if (valueIdx != -1)
		{
			WriteStringEscapeValueOnly(utf8PropertyName, utf8Value, valueIdx);
		}
		else
		{
			WriteStringByOptions(utf8PropertyName, utf8Value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(string propertyName, ReadOnlySpan<byte> utf8Value)
	{
		WriteString((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), utf8Value);
	}

	public void WriteString(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8Value)
	{
		JsonWriterHelper.ValidatePropertyAndValue(propertyName, utf8Value);
		WriteStringEscape(propertyName, utf8Value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(ReadOnlySpan<char> propertyName, JsonEncodedText value)
	{
		WriteStringHelperEscapeProperty(propertyName, value.EncodedUtf8Bytes);
	}

	private void WriteStringHelperEscapeProperty(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8Value)
	{
		Debug.Assert(utf8Value.Length <= 166666666);
		JsonWriterHelper.ValidateProperty(propertyName);
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length && propertyIdx < 1073741823);
		if (propertyIdx != -1)
		{
			WriteStringEscapePropertyOnly(propertyName, utf8Value, propertyIdx);
		}
		else
		{
			WriteStringByOptions(propertyName, utf8Value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(ReadOnlySpan<char> propertyName, string value)
	{
		if (value == null)
		{
			WriteNull(propertyName);
		}
		else
		{
			WriteString(propertyName, value.AsSpan());
		}
	}

	public void WriteString(ReadOnlySpan<byte> utf8PropertyName, JsonEncodedText value)
	{
		WriteStringHelperEscapeProperty(utf8PropertyName, value.EncodedUtf8Bytes);
	}

	private void WriteStringHelperEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
	{
		Debug.Assert(utf8Value.Length <= 166666666);
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length && propertyIdx < 1073741823);
		if (propertyIdx != -1)
		{
			WriteStringEscapePropertyOnly(utf8PropertyName, utf8Value, propertyIdx);
		}
		else
		{
			WriteStringByOptions(utf8PropertyName, utf8Value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteString(ReadOnlySpan<byte> utf8PropertyName, string value)
	{
		if (value == null)
		{
			WriteNull(utf8PropertyName);
		}
		else
		{
			WriteString(utf8PropertyName, value.AsSpan());
		}
	}

	private void WriteStringEscapeValueOnly(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> utf8Value, int firstEscapeIndex)
	{
		Debug.Assert(357913941 >= utf8Value.Length);
		Debug.Assert(firstEscapeIndex >= 0 && firstEscapeIndex < utf8Value.Length);
		byte[] valueArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndex);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(valueArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedValue = span;
		JsonWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndex, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName, escapedValue.Slice(0, written));
		if (valueArray != null)
		{
			ArrayPool<byte>.Shared.Return(valueArray);
		}
	}

	private void WriteStringEscapeValueOnly(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<char> value, int firstEscapeIndex)
	{
		Debug.Assert(357913941 >= value.Length);
		Debug.Assert(firstEscapeIndex >= 0 && firstEscapeIndex < value.Length);
		char[] valueArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(value.Length, firstEscapeIndex);
		Span<char> span = ((length > 256) ? ((Span<char>)(valueArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedValue = span;
		JsonWriterHelper.EscapeString(value, escapedValue, firstEscapeIndex, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName, escapedValue.Slice(0, written));
		if (valueArray != null)
		{
			ArrayPool<char>.Shared.Return(valueArray);
		}
	}

	private void WriteStringEscapePropertyOnly(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> escapedValue, int firstEscapeIndex)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndex >= 0 && firstEscapeIndex < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndex);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndex, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName.Slice(0, written), escapedValue);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringEscapePropertyOnly(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> escapedValue, int firstEscapeIndex)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndex >= 0 && firstEscapeIndex < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndex);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndex, _options.Encoder, out var written);
		WriteStringByOptions(escapedPropertyName.Slice(0, written), escapedValue);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringEscape(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
	{
		int valueIdx = JsonWriterHelper.NeedsEscaping(value, _options.Encoder);
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(valueIdx >= -1 && valueIdx < value.Length && valueIdx < 1073741823);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length && propertyIdx < 1073741823);
		if (valueIdx + propertyIdx != -2)
		{
			WriteStringEscapePropertyOrValue(propertyName, value, propertyIdx, valueIdx);
		}
		else
		{
			WriteStringByOptions(propertyName, value);
		}
	}

	private void WriteStringEscape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
	{
		int valueIdx = JsonWriterHelper.NeedsEscaping(utf8Value, _options.Encoder);
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(valueIdx >= -1 && valueIdx < utf8Value.Length && valueIdx < 1073741823);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length && propertyIdx < 1073741823);
		if (valueIdx + propertyIdx != -2)
		{
			WriteStringEscapePropertyOrValue(utf8PropertyName, utf8Value, propertyIdx, valueIdx);
		}
		else
		{
			WriteStringByOptions(utf8PropertyName, utf8Value);
		}
	}

	private void WriteStringEscape(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8Value)
	{
		int valueIdx = JsonWriterHelper.NeedsEscaping(utf8Value, _options.Encoder);
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(valueIdx >= -1 && valueIdx < utf8Value.Length && valueIdx < 1073741823);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length && propertyIdx < 1073741823);
		if (valueIdx + propertyIdx != -2)
		{
			WriteStringEscapePropertyOrValue(propertyName, utf8Value, propertyIdx, valueIdx);
		}
		else
		{
			WriteStringByOptions(propertyName, utf8Value);
		}
	}

	private void WriteStringEscape(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<char> value)
	{
		int valueIdx = JsonWriterHelper.NeedsEscaping(value, _options.Encoder);
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(valueIdx >= -1 && valueIdx < value.Length && valueIdx < 1073741823);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length && propertyIdx < 1073741823);
		if (valueIdx + propertyIdx != -2)
		{
			WriteStringEscapePropertyOrValue(utf8PropertyName, value, propertyIdx, valueIdx);
		}
		else
		{
			WriteStringByOptions(utf8PropertyName, value);
		}
	}

	private unsafe void WriteStringEscapePropertyOrValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value, int firstEscapeIndexProp, int firstEscapeIndexVal)
	{
		Debug.Assert(357913941 >= value.Length);
		Debug.Assert(357913941 >= propertyName.Length);
		char[] valueArray = null;
		char[] propertyArray = null;
		if (firstEscapeIndexVal != -1)
		{
			int length2 = JsonWriterHelper.GetMaxEscapedLength(value.Length, firstEscapeIndexVal);
			Span<char> escapedValue;
			if (length2 > 256)
			{
				valueArray = ArrayPool<char>.Shared.Rent(length2);
				escapedValue = valueArray;
			}
			else
			{
				char* ptr2 = stackalloc char[length2];
				escapedValue = new Span<char>(ptr2, length2);
			}
			JsonWriterHelper.EscapeString(value, escapedValue, firstEscapeIndexVal, _options.Encoder, out var written2);
			value = escapedValue.Slice(0, written2);
		}
		if (firstEscapeIndexProp != -1)
		{
			int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
			Span<char> escapedPropertyName;
			if (length > 256)
			{
				propertyArray = ArrayPool<char>.Shared.Rent(length);
				escapedPropertyName = propertyArray;
			}
			else
			{
				char* ptr = stackalloc char[length];
				escapedPropertyName = new Span<char>(ptr, length);
			}
			JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
			propertyName = escapedPropertyName.Slice(0, written);
		}
		WriteStringByOptions(propertyName, value);
		if (valueArray != null)
		{
			ArrayPool<char>.Shared.Return(valueArray);
		}
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private unsafe void WriteStringEscapePropertyOrValue(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value, int firstEscapeIndexProp, int firstEscapeIndexVal)
	{
		Debug.Assert(357913941 >= utf8Value.Length);
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		byte[] valueArray = null;
		byte[] propertyArray = null;
		if (firstEscapeIndexVal != -1)
		{
			int length2 = JsonWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);
			Span<byte> escapedValue;
			if (length2 > 256)
			{
				valueArray = ArrayPool<byte>.Shared.Rent(length2);
				escapedValue = valueArray;
			}
			else
			{
				byte* ptr2 = stackalloc byte[(int)(uint)length2];
				escapedValue = new Span<byte>(ptr2, length2);
			}
			JsonWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, _options.Encoder, out var written2);
			utf8Value = escapedValue.Slice(0, written2);
		}
		if (firstEscapeIndexProp != -1)
		{
			int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
			Span<byte> escapedPropertyName;
			if (length > 256)
			{
				propertyArray = ArrayPool<byte>.Shared.Rent(length);
				escapedPropertyName = propertyArray;
			}
			else
			{
				byte* ptr = stackalloc byte[(int)(uint)length];
				escapedPropertyName = new Span<byte>(ptr, length);
			}
			JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
			utf8PropertyName = escapedPropertyName.Slice(0, written);
		}
		WriteStringByOptions(utf8PropertyName, utf8Value);
		if (valueArray != null)
		{
			ArrayPool<byte>.Shared.Return(valueArray);
		}
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private unsafe void WriteStringEscapePropertyOrValue(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8Value, int firstEscapeIndexProp, int firstEscapeIndexVal)
	{
		Debug.Assert(357913941 >= utf8Value.Length);
		Debug.Assert(357913941 >= propertyName.Length);
		byte[] valueArray = null;
		char[] propertyArray = null;
		if (firstEscapeIndexVal != -1)
		{
			int length2 = JsonWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);
			Span<byte> escapedValue;
			if (length2 > 256)
			{
				valueArray = ArrayPool<byte>.Shared.Rent(length2);
				escapedValue = valueArray;
			}
			else
			{
				byte* ptr2 = stackalloc byte[(int)(uint)length2];
				escapedValue = new Span<byte>(ptr2, length2);
			}
			JsonWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, _options.Encoder, out var written2);
			utf8Value = escapedValue.Slice(0, written2);
		}
		if (firstEscapeIndexProp != -1)
		{
			int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
			Span<char> escapedPropertyName;
			if (length > 256)
			{
				propertyArray = ArrayPool<char>.Shared.Rent(length);
				escapedPropertyName = propertyArray;
			}
			else
			{
				char* ptr = stackalloc char[length];
				escapedPropertyName = new Span<char>(ptr, length);
			}
			JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
			propertyName = escapedPropertyName.Slice(0, written);
		}
		WriteStringByOptions(propertyName, utf8Value);
		if (valueArray != null)
		{
			ArrayPool<byte>.Shared.Return(valueArray);
		}
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private unsafe void WriteStringEscapePropertyOrValue(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<char> value, int firstEscapeIndexProp, int firstEscapeIndexVal)
	{
		Debug.Assert(357913941 >= value.Length);
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		char[] valueArray = null;
		byte[] propertyArray = null;
		if (firstEscapeIndexVal != -1)
		{
			int length2 = JsonWriterHelper.GetMaxEscapedLength(value.Length, firstEscapeIndexVal);
			Span<char> escapedValue;
			if (length2 > 256)
			{
				valueArray = ArrayPool<char>.Shared.Rent(length2);
				escapedValue = valueArray;
			}
			else
			{
				char* ptr2 = stackalloc char[length2];
				escapedValue = new Span<char>(ptr2, length2);
			}
			JsonWriterHelper.EscapeString(value, escapedValue, firstEscapeIndexVal, _options.Encoder, out var written2);
			value = escapedValue.Slice(0, written2);
		}
		if (firstEscapeIndexProp != -1)
		{
			int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
			Span<byte> escapedPropertyName;
			if (length > 256)
			{
				propertyArray = ArrayPool<byte>.Shared.Rent(length);
				escapedPropertyName = propertyArray;
			}
			else
			{
				byte* ptr = stackalloc byte[(int)(uint)length];
				escapedPropertyName = new Span<byte>(ptr, length);
			}
			JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
			utf8PropertyName = escapedPropertyName.Slice(0, written);
		}
		WriteStringByOptions(utf8PropertyName, value);
		if (valueArray != null)
		{
			ArrayPool<char>.Shared.Return(valueArray);
		}
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<char> value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(propertyName, value);
		}
		else
		{
			WriteStringMinimized(propertyName, value);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<byte> utf8Value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(utf8PropertyName, utf8Value);
		}
		else
		{
			WriteStringMinimized(utf8PropertyName, utf8Value);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<char> propertyName, ReadOnlySpan<byte> utf8Value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(propertyName, utf8Value);
		}
		else
		{
			WriteStringMinimized(propertyName, utf8Value);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<byte> utf8PropertyName, ReadOnlySpan<char> value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteStringIndented(utf8PropertyName, value);
		}
		else
		{
			WriteStringMinimized(utf8PropertyName, value);
		}
	}

	private void WriteStringMinimized(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<char> escapedValue)
	{
		Debug.Assert(escapedValue.Length <= 166666666);
		Debug.Assert(escapedPropertyName.Length < 715827880 - escapedValue.Length);
		int maxRequired = (escapedPropertyName.Length + escapedValue.Length) * 3 + 6;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedValue, output);
		output[BytesPending++] = 34;
	}

	private void WriteStringMinimized(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
	{
		Debug.Assert(escapedValue.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - escapedValue.Length - 6);
		int minRequired = escapedPropertyName.Length + escapedValue.Length + 5;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		escapedValue.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedValue.Length;
		output[BytesPending++] = 34;
	}

	private void WriteStringMinimized(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
	{
		Debug.Assert(escapedValue.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - escapedValue.Length - 6);
		int maxRequired = escapedPropertyName.Length * 3 + escapedValue.Length + 6;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		escapedValue.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedValue.Length;
		output[BytesPending++] = 34;
	}

	private void WriteStringMinimized(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<char> escapedValue)
	{
		Debug.Assert(escapedValue.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - escapedValue.Length - 6);
		int maxRequired = escapedValue.Length * 3 + escapedPropertyName.Length + 6;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedValue, output);
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<char> escapedValue)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedValue.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < (2147483640 - indent - s_newLineLength) / 3 - escapedValue.Length);
		int maxRequired = indent + (escapedPropertyName.Length + escapedValue.Length) * 3 + 7 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedValue, output);
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedValue.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - escapedValue.Length - 7 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + escapedValue.Length + 6;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		escapedValue.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedValue.Length;
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<char> escapedPropertyName, ReadOnlySpan<byte> escapedValue)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedValue.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - escapedValue.Length - 7 - indent - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + escapedValue.Length + 7 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		escapedValue.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedValue.Length;
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<byte> escapedPropertyName, ReadOnlySpan<char> escapedValue)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedValue.Length <= 1000000000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - escapedValue.Length - 7 - indent - s_newLineLength);
		int maxRequired = indent + escapedValue.Length * 3 + escapedPropertyName.Length + 7 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedValue, output);
		output[BytesPending++] = 34;
	}

	[CLSCompliant(false)]
	public void WriteNumber(JsonEncodedText propertyName, ulong value)
	{
		WriteNumberHelper(propertyName.EncodedUtf8Bytes, value);
	}

	private void WriteNumberHelper(ReadOnlySpan<byte> utf8PropertyName, ulong value)
	{
		Debug.Assert(utf8PropertyName.Length <= 166666666);
		WriteNumberByOptions(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	[CLSCompliant(false)]
	public void WriteNumber(string propertyName, ulong value)
	{
		WriteNumber((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), value);
	}

	[CLSCompliant(false)]
	public void WriteNumber(ReadOnlySpan<char> propertyName, ulong value)
	{
		JsonWriterHelper.ValidateProperty(propertyName);
		WriteNumberEscape(propertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	[CLSCompliant(false)]
	public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, ulong value)
	{
		JsonWriterHelper.ValidateProperty(utf8PropertyName);
		WriteNumberEscape(utf8PropertyName, value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	[CLSCompliant(false)]
	public void WriteNumber(JsonEncodedText propertyName, uint value)
	{
		WriteNumber(propertyName, (ulong)value);
	}

	[CLSCompliant(false)]
	public void WriteNumber(string propertyName, uint value)
	{
		WriteNumber((propertyName ?? throw new ArgumentNullException("propertyName")).AsSpan(), (ulong)value);
	}

	[CLSCompliant(false)]
	public void WriteNumber(ReadOnlySpan<char> propertyName, uint value)
	{
		WriteNumber(propertyName, (ulong)value);
	}

	[CLSCompliant(false)]
	public void WriteNumber(ReadOnlySpan<byte> utf8PropertyName, uint value)
	{
		WriteNumber(utf8PropertyName, (ulong)value);
	}

	private void WriteNumberEscape(ReadOnlySpan<char> propertyName, ulong value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(propertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < propertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(propertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(propertyName, value);
		}
	}

	private void WriteNumberEscape(ReadOnlySpan<byte> utf8PropertyName, ulong value)
	{
		int propertyIdx = JsonWriterHelper.NeedsEscaping(utf8PropertyName, _options.Encoder);
		Debug.Assert(propertyIdx >= -1 && propertyIdx < utf8PropertyName.Length);
		if (propertyIdx != -1)
		{
			WriteNumberEscapeProperty(utf8PropertyName, value, propertyIdx);
		}
		else
		{
			WriteNumberByOptions(utf8PropertyName, value);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<char> propertyName, ulong value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= propertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < propertyName.Length);
		char[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(propertyName.Length, firstEscapeIndexProp);
		Span<char> span = ((length > 256) ? ((Span<char>)(propertyArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(propertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<char>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberEscapeProperty(ReadOnlySpan<byte> utf8PropertyName, ulong value, int firstEscapeIndexProp)
	{
		Debug.Assert(357913941 >= utf8PropertyName.Length);
		Debug.Assert(firstEscapeIndexProp >= 0 && firstEscapeIndexProp < utf8PropertyName.Length);
		byte[] propertyArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8PropertyName.Length, firstEscapeIndexProp);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(propertyArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedPropertyName = span;
		JsonWriterHelper.EscapeString(utf8PropertyName, escapedPropertyName, firstEscapeIndexProp, _options.Encoder, out var written);
		WriteNumberByOptions(escapedPropertyName.Slice(0, written), value);
		if (propertyArray != null)
		{
			ArrayPool<byte>.Shared.Return(propertyArray);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<char> propertyName, ulong value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(propertyName, value);
		}
		else
		{
			WriteNumberMinimized(propertyName, value);
		}
	}

	private void WriteNumberByOptions(ReadOnlySpan<byte> utf8PropertyName, ulong value)
	{
		ValidateWritingProperty();
		if (_options.Indented)
		{
			WriteNumberIndented(utf8PropertyName, value);
		}
		else
		{
			WriteNumberMinimized(utf8PropertyName, value);
		}
	}

	private void WriteNumberMinimized(ReadOnlySpan<char> escapedPropertyName, ulong value)
	{
		Debug.Assert(escapedPropertyName.Length < 715827858);
		int maxRequired = escapedPropertyName.Length * 3 + 20 + 4;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberMinimized(ReadOnlySpan<byte> escapedPropertyName, ulong value)
	{
		Debug.Assert(escapedPropertyName.Length < 2147483623);
		int minRequired = escapedPropertyName.Length + 20 + 3;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<char> escapedPropertyName, ulong value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < 715827882 - indent - 20 - 5 - s_newLineLength);
		int maxRequired = indent + escapedPropertyName.Length * 3 + 20 + 5 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedPropertyName, output);
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberIndented(ReadOnlySpan<byte> escapedPropertyName, ulong value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedPropertyName.Length < int.MaxValue - indent - 20 - 5 - s_newLineLength);
		int minRequired = indent + escapedPropertyName.Length + 20 + 4;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		Debug.Assert(_options.SkipValidation || _tokenType != JsonTokenType.PropertyName);
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 34;
		escapedPropertyName.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedPropertyName.Length;
		output[BytesPending++] = 34;
		output[BytesPending++] = 58;
		output[BytesPending++] = 32;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	public void WriteBase64StringValue(ReadOnlySpan<byte> bytes)
	{
		JsonWriterHelper.ValidateBytes(bytes);
		WriteBase64ByOptions(bytes);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteBase64ByOptions(ReadOnlySpan<byte> bytes)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteBase64Indented(bytes);
		}
		else
		{
			WriteBase64Minimized(bytes);
		}
	}

	private void WriteBase64Minimized(ReadOnlySpan<byte> bytes)
	{
		int encodingLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
		Debug.Assert(encodingLength < 2147483644);
		int maxRequired = encodingLength + 3;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		Base64EncodeAndWrite(bytes, output, encodingLength);
		output[BytesPending++] = 34;
	}

	private void WriteBase64Indented(ReadOnlySpan<byte> bytes)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int encodingLength = Base64.GetMaxEncodedToUtf8Length(bytes.Length);
		Debug.Assert(encodingLength < int.MaxValue - indent - 3 - s_newLineLength);
		int maxRequired = indent + encodingLength + 3 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		output[BytesPending++] = 34;
		Base64EncodeAndWrite(bytes, output, encodingLength);
		output[BytesPending++] = 34;
	}

	public void WriteCommentValue(string value)
	{
		WriteCommentValue((value ?? throw new ArgumentNullException("value")).AsSpan());
	}

	public void WriteCommentValue(ReadOnlySpan<char> value)
	{
		JsonWriterHelper.ValidateValue(value);
		if (value.IndexOf(s_singleLineCommentDelimiter) != -1)
		{
			ThrowHelper.ThrowArgumentException_InvalidCommentValue();
		}
		WriteCommentByOptions(value);
	}

	private void WriteCommentByOptions(ReadOnlySpan<char> value)
	{
		if (_options.Indented)
		{
			WriteCommentIndented(value);
		}
		else
		{
			WriteCommentMinimized(value);
		}
	}

	private void WriteCommentMinimized(ReadOnlySpan<char> value)
	{
		Debug.Assert(value.Length < 715827878);
		int maxRequired = value.Length * 3 + 4;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		output[BytesPending++] = 47;
		int bytesConsumed = BytesPending++;
		output[bytesConsumed] = 42;
		ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(value);
		int written;
		OperationStatus status = JsonWriterHelper.ToUtf8(byteSpan, output.Slice(BytesPending), out bytesConsumed, out written);
		Debug.Assert(status != OperationStatus.DestinationTooSmall);
		BytesPending += written;
		output[BytesPending++] = 42;
		output[BytesPending++] = 47;
	}

	private void WriteCommentIndented(ReadOnlySpan<char> value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(value.Length < 715827882 - indent - 4 - s_newLineLength);
		int maxRequired = indent + value.Length * 3 + 4 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_tokenType != 0)
		{
			WriteNewLine(output);
		}
		JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
		BytesPending += indent;
		output[BytesPending++] = 47;
		int bytesConsumed = BytesPending++;
		output[bytesConsumed] = 42;
		ReadOnlySpan<byte> byteSpan = MemoryMarshal.AsBytes(value);
		int written;
		OperationStatus status = JsonWriterHelper.ToUtf8(byteSpan, output.Slice(BytesPending), out bytesConsumed, out written);
		Debug.Assert(status != OperationStatus.DestinationTooSmall);
		BytesPending += written;
		output[BytesPending++] = 42;
		output[BytesPending++] = 47;
	}

	public void WriteCommentValue(ReadOnlySpan<byte> utf8Value)
	{
		JsonWriterHelper.ValidateValue(utf8Value);
		if (utf8Value.IndexOf(SingleLineCommentDelimiterUtf8) != -1)
		{
			ThrowHelper.ThrowArgumentException_InvalidCommentValue();
		}
		WriteCommentByOptions(utf8Value);
	}

	private void WriteCommentByOptions(ReadOnlySpan<byte> utf8Value)
	{
		if (_options.Indented)
		{
			WriteCommentIndented(utf8Value);
		}
		else
		{
			WriteCommentMinimized(utf8Value);
		}
	}

	private void WriteCommentMinimized(ReadOnlySpan<byte> utf8Value)
	{
		Debug.Assert(utf8Value.Length < 2147483643);
		int maxRequired = utf8Value.Length + 4;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		output[BytesPending++] = 47;
		output[BytesPending++] = 42;
		utf8Value.CopyTo(output.Slice(BytesPending));
		BytesPending += utf8Value.Length;
		output[BytesPending++] = 42;
		output[BytesPending++] = 47;
	}

	private void WriteCommentIndented(ReadOnlySpan<byte> utf8Value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(utf8Value.Length < int.MaxValue - indent - 4 - s_newLineLength);
		int minRequired = indent + utf8Value.Length + 4;
		int maxRequired = minRequired + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		output[BytesPending++] = 47;
		output[BytesPending++] = 42;
		utf8Value.CopyTo(output.Slice(BytesPending));
		BytesPending += utf8Value.Length;
		output[BytesPending++] = 42;
		output[BytesPending++] = 47;
	}

	public void WriteStringValue(DateTime value)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteStringValueIndented(value);
		}
		else
		{
			WriteStringValueMinimized(value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteStringValueMinimized(DateTime value)
	{
		int maxRequired = 36;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringValueIndented(DateTime value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int maxRequired = indent + 33 + 3 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	public void WriteStringValue(DateTimeOffset value)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteStringValueIndented(value);
		}
		else
		{
			WriteStringValueMinimized(value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteStringValueMinimized(DateTimeOffset value)
	{
		int maxRequired = 36;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringValueIndented(DateTimeOffset value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int maxRequired = indent + 33 + 3 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		output[BytesPending++] = 34;
		Span<byte> tempSpan = stackalloc byte[33];
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, tempSpan, out bytesWritten, s_dateTimeStandardFormat);
		Debug.Assert(result);
		JsonWriterHelper.TrimDateTimeOffset(tempSpan.Slice(0, bytesWritten), out bytesWritten);
		tempSpan.Slice(0, bytesWritten).CopyTo(output.Slice(BytesPending));
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	public void WriteNumberValue(decimal value)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteNumberValueIndented(value);
		}
		else
		{
			WriteNumberValueMinimized(value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberValueMinimized(decimal value)
	{
		int maxRequired = 32;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberValueIndented(decimal value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int maxRequired = indent + 31 + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	public void WriteNumberValue(double value)
	{
		JsonWriterHelper.ValidateDouble(value);
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteNumberValueIndented(value);
		}
		else
		{
			WriteNumberValueMinimized(value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberValueMinimized(double value)
	{
		int maxRequired = 129;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		int bytesWritten;
		bool result = TryFormatDouble(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberValueIndented(double value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int maxRequired = indent + 128 + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		int bytesWritten;
		bool result = TryFormatDouble(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private static bool TryFormatDouble(double value, Span<byte> destination, out int bytesWritten)
	{
		string utf16Text = value.ToString("G17", CultureInfo.InvariantCulture);
		if (utf16Text.Length > destination.Length)
		{
			bytesWritten = 0;
			return false;
		}
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(utf16Text);
			if (bytes.Length > destination.Length)
			{
				bytesWritten = 0;
				return false;
			}
			bytes.CopyTo(destination);
			bytesWritten = bytes.Length;
			return true;
		}
		catch
		{
			bytesWritten = 0;
			return false;
		}
	}

	public void WriteNumberValue(float value)
	{
		JsonWriterHelper.ValidateSingle(value);
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteNumberValueIndented(value);
		}
		else
		{
			WriteNumberValueMinimized(value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberValueMinimized(float value)
	{
		int maxRequired = 129;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		int bytesWritten;
		bool result = TryFormatSingle(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberValueIndented(float value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int maxRequired = indent + 128 + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		int bytesWritten;
		bool result = TryFormatSingle(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private static bool TryFormatSingle(float value, Span<byte> destination, out int bytesWritten)
	{
		string utf16Text = value.ToString("G9", CultureInfo.InvariantCulture);
		if (utf16Text.Length > destination.Length)
		{
			bytesWritten = 0;
			return false;
		}
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(utf16Text);
			if (bytes.Length > destination.Length)
			{
				bytesWritten = 0;
				return false;
			}
			bytes.CopyTo(destination);
			bytesWritten = bytes.Length;
			return true;
		}
		catch
		{
			bytesWritten = 0;
			return false;
		}
	}

	internal void WriteNumberValue(ReadOnlySpan<byte> utf8FormattedNumber)
	{
		JsonWriterHelper.ValidateValue(utf8FormattedNumber);
		JsonWriterHelper.ValidateNumber(utf8FormattedNumber);
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteNumberValueIndented(utf8FormattedNumber);
		}
		else
		{
			WriteNumberValueMinimized(utf8FormattedNumber);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberValueMinimized(ReadOnlySpan<byte> utf8Value)
	{
		int maxRequired = utf8Value.Length + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		utf8Value.CopyTo(output.Slice(BytesPending));
		BytesPending += utf8Value.Length;
	}

	private void WriteNumberValueIndented(ReadOnlySpan<byte> utf8Value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(utf8Value.Length < int.MaxValue - indent - 1 - s_newLineLength);
		int maxRequired = indent + utf8Value.Length + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		utf8Value.CopyTo(output.Slice(BytesPending));
		BytesPending += utf8Value.Length;
	}

	public void WriteStringValue(Guid value)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteStringValueIndented(value);
		}
		else
		{
			WriteStringValueMinimized(value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteStringValueMinimized(Guid value)
	{
		int maxRequired = 39;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void WriteStringValueIndented(Guid value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int maxRequired = indent + 36 + 3 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		output[BytesPending++] = 34;
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
		output[BytesPending++] = 34;
	}

	private void ValidateWritingValue()
	{
		if (_options.SkipValidation)
		{
			return;
		}
		if (_inObject)
		{
			if (_tokenType != JsonTokenType.PropertyName)
			{
				Debug.Assert(_tokenType != 0 && _tokenType != JsonTokenType.StartArray);
				ThrowHelper.ThrowInvalidOperationException(ExceptionResource.CannotWriteValueWithinObject, 0, 0, _tokenType);
			}
		}
		else
		{
			Debug.Assert(_tokenType != JsonTokenType.PropertyName);
			if (CurrentDepth == 0 && _tokenType != 0)
			{
				ThrowHelper.ThrowInvalidOperationException(ExceptionResource.CannotWriteValueAfterPrimitiveOrClose, 0, 0, _tokenType);
			}
		}
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	private void Base64EncodeAndWrite(ReadOnlySpan<byte> bytes, Span<byte> output, int encodingLength)
	{
		byte[] outputText = null;
		Span<byte> span = ((encodingLength > 256) ? ((Span<byte>)(outputText = ArrayPool<byte>.Shared.Rent(encodingLength))) : stackalloc byte[encodingLength]);
		Span<byte> encodedBytes = span;
		int consumed;
		int written;
		OperationStatus status = Base64.EncodeToUtf8(bytes, encodedBytes, out consumed, out written);
		Debug.Assert(status == OperationStatus.Done);
		Debug.Assert(consumed == bytes.Length);
		encodedBytes = encodedBytes.Slice(0, written);
		Span<byte> destination = output.Slice(BytesPending);
		Debug.Assert(destination.Length >= written);
		encodedBytes.Slice(0, written).CopyTo(destination);
		BytesPending += written;
		if (outputText != null)
		{
			ArrayPool<byte>.Shared.Return(outputText);
		}
	}

	public void WriteNullValue()
	{
		WriteLiteralByOptions(JsonConstants.NullValue);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Null;
	}

	public void WriteBooleanValue(bool value)
	{
		if (value)
		{
			WriteLiteralByOptions(JsonConstants.TrueValue);
			_tokenType = JsonTokenType.True;
		}
		else
		{
			WriteLiteralByOptions(JsonConstants.FalseValue);
			_tokenType = JsonTokenType.False;
		}
		SetFlagToAddListSeparatorBeforeNextItem();
	}

	private void WriteLiteralByOptions(ReadOnlySpan<byte> utf8Value)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteLiteralIndented(utf8Value);
		}
		else
		{
			WriteLiteralMinimized(utf8Value);
		}
	}

	private void WriteLiteralMinimized(ReadOnlySpan<byte> utf8Value)
	{
		Debug.Assert(utf8Value.Length <= 5);
		int maxRequired = utf8Value.Length + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		utf8Value.CopyTo(output.Slice(BytesPending));
		BytesPending += utf8Value.Length;
	}

	private void WriteLiteralIndented(ReadOnlySpan<byte> utf8Value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(utf8Value.Length <= 5);
		int maxRequired = indent + utf8Value.Length + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		utf8Value.CopyTo(output.Slice(BytesPending));
		BytesPending += utf8Value.Length;
	}

	public void WriteNumberValue(int value)
	{
		WriteNumberValue((long)value);
	}

	public void WriteNumberValue(long value)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteNumberValueIndented(value);
		}
		else
		{
			WriteNumberValueMinimized(value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberValueMinimized(long value)
	{
		int maxRequired = 21;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberValueIndented(long value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int maxRequired = indent + 20 + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	public void WriteStringValue(JsonEncodedText value)
	{
		WriteStringValueHelper(value.EncodedUtf8Bytes);
	}

	private void WriteStringValueHelper(ReadOnlySpan<byte> utf8Value)
	{
		Debug.Assert(utf8Value.Length <= 166666666);
		WriteStringByOptions(utf8Value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	public void WriteStringValue(string value)
	{
		if (value == null)
		{
			WriteNullValue();
		}
		else
		{
			WriteStringValue(value.AsSpan());
		}
	}

	public void WriteStringValue(ReadOnlySpan<char> value)
	{
		JsonWriterHelper.ValidateValue(value);
		WriteStringEscape(value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteStringEscape(ReadOnlySpan<char> value)
	{
		int valueIdx = JsonWriterHelper.NeedsEscaping(value, _options.Encoder);
		Debug.Assert(valueIdx >= -1 && valueIdx < value.Length);
		if (valueIdx != -1)
		{
			WriteStringEscapeValue(value, valueIdx);
		}
		else
		{
			WriteStringByOptions(value);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<char> value)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteStringIndented(value);
		}
		else
		{
			WriteStringMinimized(value);
		}
	}

	private void WriteStringMinimized(ReadOnlySpan<char> escapedValue)
	{
		Debug.Assert(escapedValue.Length < 715827879);
		int maxRequired = escapedValue.Length * 3 + 3;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedValue, output);
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<char> escapedValue)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedValue.Length < 715827882 - indent - 3 - s_newLineLength);
		int maxRequired = indent + escapedValue.Length * 3 + 3 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		output[BytesPending++] = 34;
		TranscodeAndWrite(escapedValue, output);
		output[BytesPending++] = 34;
	}

	private void WriteStringEscapeValue(ReadOnlySpan<char> value, int firstEscapeIndexVal)
	{
		Debug.Assert(357913941 >= value.Length);
		Debug.Assert(firstEscapeIndexVal >= 0 && firstEscapeIndexVal < value.Length);
		char[] valueArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(value.Length, firstEscapeIndexVal);
		Span<char> span = ((length > 256) ? ((Span<char>)(valueArray = ArrayPool<char>.Shared.Rent(length))) : stackalloc char[length]);
		Span<char> escapedValue = span;
		JsonWriterHelper.EscapeString(value, escapedValue, firstEscapeIndexVal, _options.Encoder, out var written);
		WriteStringByOptions(escapedValue.Slice(0, written));
		if (valueArray != null)
		{
			ArrayPool<char>.Shared.Return(valueArray);
		}
	}

	public void WriteStringValue(ReadOnlySpan<byte> utf8Value)
	{
		JsonWriterHelper.ValidateValue(utf8Value);
		WriteStringEscape(utf8Value);
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.String;
	}

	private void WriteStringEscape(ReadOnlySpan<byte> utf8Value)
	{
		int valueIdx = JsonWriterHelper.NeedsEscaping(utf8Value, _options.Encoder);
		Debug.Assert(valueIdx >= -1 && valueIdx < utf8Value.Length);
		if (valueIdx != -1)
		{
			WriteStringEscapeValue(utf8Value, valueIdx);
		}
		else
		{
			WriteStringByOptions(utf8Value);
		}
	}

	private void WriteStringByOptions(ReadOnlySpan<byte> utf8Value)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteStringIndented(utf8Value);
		}
		else
		{
			WriteStringMinimized(utf8Value);
		}
	}

	private void WriteStringMinimized(ReadOnlySpan<byte> escapedValue)
	{
		Debug.Assert(escapedValue.Length < 2147483644);
		int minRequired = escapedValue.Length + 2;
		int maxRequired = minRequired + 1;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		output[BytesPending++] = 34;
		escapedValue.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedValue.Length;
		output[BytesPending++] = 34;
	}

	private void WriteStringIndented(ReadOnlySpan<byte> escapedValue)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		Debug.Assert(escapedValue.Length < int.MaxValue - indent - 3 - s_newLineLength);
		int minRequired = indent + escapedValue.Length + 2;
		int maxRequired = minRequired + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		output[BytesPending++] = 34;
		escapedValue.CopyTo(output.Slice(BytesPending));
		BytesPending += escapedValue.Length;
		output[BytesPending++] = 34;
	}

	private void WriteStringEscapeValue(ReadOnlySpan<byte> utf8Value, int firstEscapeIndexVal)
	{
		Debug.Assert(357913941 >= utf8Value.Length);
		Debug.Assert(firstEscapeIndexVal >= 0 && firstEscapeIndexVal < utf8Value.Length);
		byte[] valueArray = null;
		int length = JsonWriterHelper.GetMaxEscapedLength(utf8Value.Length, firstEscapeIndexVal);
		Span<byte> span = ((length > 256) ? ((Span<byte>)(valueArray = ArrayPool<byte>.Shared.Rent(length))) : stackalloc byte[length]);
		Span<byte> escapedValue = span;
		JsonWriterHelper.EscapeString(utf8Value, escapedValue, firstEscapeIndexVal, _options.Encoder, out var written);
		WriteStringByOptions(escapedValue.Slice(0, written));
		if (valueArray != null)
		{
			ArrayPool<byte>.Shared.Return(valueArray);
		}
	}

	[CLSCompliant(false)]
	public void WriteNumberValue(uint value)
	{
		WriteNumberValue((ulong)value);
	}

	[CLSCompliant(false)]
	public void WriteNumberValue(ulong value)
	{
		ValidateWritingValue();
		if (_options.Indented)
		{
			WriteNumberValueIndented(value);
		}
		else
		{
			WriteNumberValueMinimized(value);
		}
		SetFlagToAddListSeparatorBeforeNextItem();
		_tokenType = JsonTokenType.Number;
	}

	private void WriteNumberValueMinimized(ulong value)
	{
		int maxRequired = 21;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}

	private void WriteNumberValueIndented(ulong value)
	{
		int indent = Indentation;
		Debug.Assert(indent <= 2000);
		int maxRequired = indent + 20 + 1 + s_newLineLength;
		if (_memory.Length - BytesPending < maxRequired)
		{
			Grow(maxRequired);
		}
		Span<byte> output = _memory.Span;
		if (_currentDepth < 0)
		{
			output[BytesPending++] = 44;
		}
		if (_tokenType != JsonTokenType.PropertyName)
		{
			if (_tokenType != 0)
			{
				WriteNewLine(output);
			}
			JsonWriterHelper.WriteIndentation(output.Slice(BytesPending), indent);
			BytesPending += indent;
		}
		int bytesWritten;
		bool result = Utf8Formatter.TryFormat(value, output.Slice(BytesPending), out bytesWritten);
		Debug.Assert(result);
		BytesPending += bytesWritten;
	}
}
