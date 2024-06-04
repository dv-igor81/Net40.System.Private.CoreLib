#define DEBUG
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.IO;

public class MemoryStreamEx : Stream
{
	private byte[] _buffer;

	private int _origin;

	private int _position;

	private int _length;

	private int _capacity;

	private bool _expandable;

	private bool _writable;

	private bool _exposable;

	private bool _isOpen;

	private Task<int>? _lastReadTask;

	private const int MemStreamMaxLength = int.MaxValue;

	public override bool CanRead => _isOpen;

	public override bool CanSeek => _isOpen;

	public override bool CanWrite => _writable;

	public virtual int Capacity
	{
		get
		{
			EnsureNotClosed();
			return _capacity - _origin;
		}
		set
		{
			if (value < Length)
			{
				throw new ArgumentOutOfRangeException("value", SR.ArgumentOutOfRange_SmallCapacity);
			}
			EnsureNotClosed();
			if (!_expandable && value != Capacity)
			{
				throw new NotSupportedException(SR.NotSupported_MemStreamNotExpandable);
			}
			if (!_expandable || value == _capacity)
			{
				return;
			}
			if (value > 0)
			{
				byte[] newBuffer = new byte[value];
				if (_length > 0)
				{
					Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _length);
				}
				_buffer = newBuffer;
			}
			else
			{
				_buffer = ArrayEx.Empty<byte>();
			}
			_capacity = value;
		}
	}

	public override long Length
	{
		get
		{
			EnsureNotClosed();
			return _length - _origin;
		}
	}

	public override long Position
	{
		get
		{
			EnsureNotClosed();
			return _position - _origin;
		}
		set
		{
			if (value < 0)
			{
				throw new ArgumentOutOfRangeException("value", SR.ArgumentOutOfRange_NeedNonNegNum);
			}
			EnsureNotClosed();
			if (value > int.MaxValue)
			{
				throw new ArgumentOutOfRangeException("value", SR.ArgumentOutOfRange_StreamLength);
			}
			_position = _origin + (int)value;
		}
	}

	public MemoryStreamEx()
		: this(0)
	{
	}

	public MemoryStreamEx(int capacity)
	{
		if (capacity < 0)
		{
			throw new ArgumentOutOfRangeException("capacity", "SR.ArgumentOutOfRange_NegativeCapacity");
		}
		_buffer = ((capacity != 0) ? new byte[capacity] : ArrayEx.Empty<byte>());
		_capacity = capacity;
		_expandable = true;
		_writable = true;
		_exposable = true;
		_origin = 0;
		_isOpen = true;
	}

	public MemoryStreamEx(byte[] buffer)
		: this(buffer, writable: true)
	{
	}

	public MemoryStreamEx(byte[] buffer, bool writable)
	{
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer", "SR.ArgumentNull_Buffer");
		}
		_buffer = buffer;
		_length = (_capacity = buffer.Length);
		_writable = writable;
		_exposable = false;
		_origin = 0;
		_isOpen = true;
	}

	public MemoryStreamEx(byte[] buffer, int index, int count)
		: this(buffer, index, count, writable: true, publiclyVisible: false)
	{
	}

	public MemoryStreamEx(byte[] buffer, int index, int count, bool writable)
		: this(buffer, index, count, writable, publiclyVisible: false)
	{
	}

	public MemoryStreamEx(byte[] buffer, int index, int count, bool writable, bool publiclyVisible)
	{
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer", "SR.ArgumentNull_Buffer");
		}
		if (index < 0)
		{
			throw new ArgumentOutOfRangeException("index", "SR.ArgumentOutOfRange_NeedNonNegNum");
		}
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count", "SR.ArgumentOutOfRange_NeedNonNegNum");
		}
		if (buffer.Length - index < count)
		{
			throw new ArgumentException("SR.Argument_InvalidOffLen");
		}
		_buffer = buffer;
		_origin = (_position = index);
		_length = (_capacity = index + count);
		_writable = writable;
		_exposable = publiclyVisible;
		_expandable = false;
		_isOpen = true;
	}

	private void EnsureNotClosed()
	{
		if (!_isOpen)
		{
			throw Error.GetStreamIsClosed();
		}
	}

	private void EnsureWriteable()
	{
		if (!CanWrite)
		{
			throw Error.GetWriteNotSupported();
		}
	}

	protected override void Dispose(bool disposing)
	{
		try
		{
			if (disposing)
			{
				_isOpen = false;
				_writable = false;
				_expandable = false;
				_lastReadTask = null;
			}
		}
		finally
		{
			base.Dispose(disposing);
		}
	}

	private bool EnsureCapacity(int value)
	{
		if (value < 0)
		{
			throw new IOException("SR.IO_StreamTooLong");
		}
		if (value > _capacity)
		{
			int newCapacity = Math.Max(value, 256);
			if (newCapacity < _capacity * 2)
			{
				newCapacity = _capacity * 2;
			}
			if ((uint)(_capacity * 2) > ArrayEx.MaxByteArrayLength)
			{
				newCapacity = Math.Max(value, ArrayEx.MaxByteArrayLength);
			}
			Capacity = newCapacity;
			return true;
		}
		return false;
	}

	public override void Flush()
	{
	}

	public new Task FlushAsync(CancellationToken cancellationToken)
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return TaskExEx.FromCanceled(cancellationToken);
		}
		try
		{
			Flush();
			return TaskExEx.CompletedTask;
		}
		catch (Exception ex)
		{
			return TaskExEx.FromException(ex);
		}
	}

	public virtual byte[] GetBuffer()
	{
		if (!_exposable)
		{
			throw new UnauthorizedAccessException("SR.UnauthorizedAccess_MemStreamBuffer");
		}
		return _buffer;
	}

	public virtual bool TryGetBuffer(out ArraySegment<byte> buffer)
	{
		if (!_exposable)
		{
			buffer = default(ArraySegment<byte>);
			return false;
		}
		buffer = new ArraySegment<byte>(_buffer, _origin, _length - _origin);
		return true;
	}

	internal byte[] InternalGetBuffer()
	{
		return _buffer;
	}

	internal int InternalGetPosition()
	{
		return _position;
	}

	[MethodImpl(MethodImplOptionsEx.AggressiveInlining)]
	internal ReadOnlySpan<byte> InternalReadSpan(int count)
	{
		EnsureNotClosed();
		int origPos = _position;
		int newPos = origPos + count;
		if ((uint)newPos > (uint)_length)
		{
			_position = _length;
			throw Error.GetEndOfFile();
		}
		ReadOnlySpan<byte> span = new ReadOnlySpan<byte>(_buffer, origPos, count);
		_position = newPos;
		return span;
	}

	internal int InternalEmulateRead(int count)
	{
		EnsureNotClosed();
		int i = _length - _position;
		if (i > count)
		{
			i = count;
		}
		if (i < 0)
		{
			i = 0;
		}
		Debug.Assert(_position + i >= 0, "_position + n >= 0");
		_position += i;
		return i;
	}

	public override int Read(byte[] buffer, int offset, int count)
	{
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer", SR.ArgumentNull_Buffer);
		}
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (buffer.Length - offset < count)
		{
			throw new ArgumentException(SR.Argument_InvalidOffLen);
		}
		EnsureNotClosed();
		int i = _length - _position;
		if (i > count)
		{
			i = count;
		}
		if (i <= 0)
		{
			return 0;
		}
		Debug.Assert(_position + i >= 0, "_position + n >= 0");
		if (i <= 8)
		{
			int byteCount = i;
			while (--byteCount >= 0)
			{
				buffer[offset + byteCount] = _buffer[_position + byteCount];
			}
		}
		else
		{
			Buffer.BlockCopy(_buffer, _position, buffer, offset, i);
		}
		_position += i;
		return i;
	}

	public int Read(Span<byte> buffer)
	{
		if (GetType() != typeof(MemoryStreamEx))
		{
			return StreamEx.Read(this, buffer);
		}
		EnsureNotClosed();
		int i = Math.Min(_length - _position, buffer.Length);
		if (i <= 0)
		{
			return 0;
		}
		new Span<byte>(_buffer, _position, i).CopyTo(buffer);
		_position += i;
		return i;
	}

	public new Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer", SR.ArgumentNull_Buffer);
		}
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (buffer.Length - offset < count)
		{
			throw new ArgumentException(SR.Argument_InvalidOffLen);
		}
		if (cancellationToken.IsCancellationRequested)
		{
			return TaskExEx.FromCanceled<int>(cancellationToken);
		}
		try
		{
			int i = Read(buffer, offset, count);
			Task<int> t = _lastReadTask;
			Debug.Assert(t == null || t.Status == TaskStatus.RanToCompletion, "Expected that a stored last task completed successfully");
			return (t != null && t.Result == i) ? t : (_lastReadTask = TaskExEx.FromResult(i));
		}
		catch (OperationCanceledException oce)
		{
			return TaskExEx.FromCanceled<int>(oce.CancellationToken);
		}
		catch (Exception exception)
		{
			return TaskExEx.FromException<int>(exception);
		}
	}

	public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return new ValueTask<int>(TaskExEx.FromCanceled<int>(cancellationToken));
		}
		try
		{
			ArraySegment<byte> destinationArray;
			return new ValueTask<int>(MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out destinationArray) ? Read(destinationArray.Array, destinationArray.Offset, destinationArray.Count) : Read(buffer.Span));
		}
		catch (OperationCanceledException oce)
		{
			return new ValueTask<int>(TaskExEx.FromCanceled<int>(oce.CancellationToken));
		}
		catch (Exception exception)
		{
			return new ValueTask<int>(TaskExEx.FromException<int>(exception));
		}
	}

	public override int ReadByte()
	{
		EnsureNotClosed();
		if (_position >= _length)
		{
			return -1;
		}
		return _buffer[_position++];
	}

	public new void CopyTo(Stream destination, int bufferSize)
	{
		StreamHelpers.ValidateCopyToArgs(this, destination, bufferSize);
		if (GetType() != typeof(MemoryStream))
		{
			base.CopyTo(destination, bufferSize);
			return;
		}
		int originalPosition = _position;
		int remaining = InternalEmulateRead(_length - originalPosition);
		if (remaining > 0)
		{
			destination.Write(_buffer, originalPosition, remaining);
		}
	}

	public new Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
	{
		StreamHelpers.ValidateCopyToArgs(this, destination, bufferSize);
		if (GetType() != typeof(MemoryStream))
		{
			return StreamTheraotExtensions.CopyToAsync(this, destination, bufferSize, cancellationToken);
		}
		if (cancellationToken.IsCancellationRequested)
		{
			return TaskExEx.FromCanceled(cancellationToken);
		}
		int pos = _position;
		int i = InternalEmulateRead(_length - _position);
		if (i == 0)
		{
			return TaskExEx.CompletedTask;
		}
		if (!(destination is MemoryStream memStrDest))
		{
			return StreamTheraotExtensions.WriteAsync(destination, _buffer, pos, i, cancellationToken);
		}
		try
		{
			memStrDest.Write(_buffer, pos, i);
			return TaskExEx.CompletedTask;
		}
		catch (Exception ex)
		{
			return TaskExEx.FromException(ex);
		}
	}

	public override long Seek(long offset, SeekOrigin loc)
	{
		EnsureNotClosed();
		if (offset > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_StreamLength);
		}
		switch (loc)
		{
		case SeekOrigin.Begin:
		{
			int tempPosition = _origin + (int)offset;
			if (offset < 0 || tempPosition < _origin)
			{
				throw new IOException(SR.IO_SeekBeforeBegin);
			}
			_position = tempPosition;
			break;
		}
		case SeekOrigin.Current:
		{
			int tempPosition3 = _position + (int)offset;
			if (_position + offset < _origin || tempPosition3 < _origin)
			{
				throw new IOException(SR.IO_SeekBeforeBegin);
			}
			_position = tempPosition3;
			break;
		}
		case SeekOrigin.End:
		{
			int tempPosition2 = _length + (int)offset;
			if (_length + offset < _origin || tempPosition2 < _origin)
			{
				throw new IOException(SR.IO_SeekBeforeBegin);
			}
			_position = tempPosition2;
			break;
		}
		default:
			throw new ArgumentException(SR.Argument_InvalidSeekOrigin);
		}
		Debug.Assert(_position >= 0, "_position >= 0");
		return _position;
	}

	public override void SetLength(long value)
	{
		if (value < 0 || value > int.MaxValue)
		{
			throw new ArgumentOutOfRangeException("value", SR.ArgumentOutOfRange_StreamLength);
		}
		EnsureWriteable();
		Debug.Assert(condition: true);
		if (value > int.MaxValue - _origin)
		{
			throw new ArgumentOutOfRangeException("value", SR.ArgumentOutOfRange_StreamLength);
		}
		int newLength = _origin + (int)value;
		if (!EnsureCapacity(newLength) && newLength > _length)
		{
			Array.Clear(_buffer, _length, newLength - _length);
		}
		_length = newLength;
		if (_position > newLength)
		{
			_position = newLength;
		}
	}

	public virtual byte[] ToArray()
	{
		int count = _length - _origin;
		if (count == 0)
		{
			return ArrayEx.Empty<byte>();
		}
		byte[] copy = new byte[count];
		Buffer.BlockCopy(_buffer, _origin, copy, 0, count);
		return copy;
	}

	public override void Write(byte[] buffer, int offset, int count)
	{
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer", SR.ArgumentNull_Buffer);
		}
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (buffer.Length - offset < count)
		{
			throw new ArgumentException(SR.Argument_InvalidOffLen);
		}
		EnsureNotClosed();
		EnsureWriteable();
		int i = _position + count;
		if (i < 0)
		{
			throw new IOException(SR.IO_StreamTooLong);
		}
		if (i > _length)
		{
			bool mustZero = _position > _length;
			if (i > _capacity && EnsureCapacity(i))
			{
				mustZero = false;
			}
			if (mustZero)
			{
				Array.Clear(_buffer, _length, i - _length);
			}
			_length = i;
		}
		if (count <= 8 && buffer != _buffer)
		{
			int byteCount = count;
			while (--byteCount >= 0)
			{
				_buffer[_position + byteCount] = buffer[offset + byteCount];
			}
		}
		else
		{
			Buffer.BlockCopy(buffer, offset, _buffer, _position, count);
		}
		_position = i;
	}

	public void Write(ReadOnlySpan<byte> buffer)
	{
		if (GetType() != typeof(MemoryStream))
		{
			StreamEx.Write(this, buffer);
			return;
		}
		EnsureNotClosed();
		EnsureWriteable();
		int i = _position + buffer.Length;
		if (i < 0)
		{
			throw new IOException(SR.IO_StreamTooLong);
		}
		if (i > _length)
		{
			bool mustZero = _position > _length;
			if (i > _capacity && EnsureCapacity(i))
			{
				mustZero = false;
			}
			if (mustZero)
			{
				Array.Clear(_buffer, _length, i - _length);
			}
			_length = i;
		}
		buffer.CopyTo(new Span<byte>(_buffer, _position, buffer.Length));
		_position = i;
	}

	public new Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
	{
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer", SR.ArgumentNull_Buffer);
		}
		if (offset < 0)
		{
			throw new ArgumentOutOfRangeException("offset", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count", SR.ArgumentOutOfRange_NeedNonNegNum);
		}
		if (buffer.Length - offset < count)
		{
			throw new ArgumentException(SR.Argument_InvalidOffLen);
		}
		if (cancellationToken.IsCancellationRequested)
		{
			return TaskExEx.FromCanceled(cancellationToken);
		}
		try
		{
			Write(buffer, offset, count);
			return TaskExEx.CompletedTask;
		}
		catch (OperationCanceledException oce)
		{
			return TaskExEx.FromCanceled(oce.CancellationToken);
		}
		catch (Exception exception)
		{
			return TaskExEx.FromException(exception);
		}
	}

	public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default(CancellationToken))
	{
		if (cancellationToken.IsCancellationRequested)
		{
			return new ValueTask(TaskExEx.FromCanceled(cancellationToken));
		}
		try
		{
			if (MemoryMarshal.TryGetArray(buffer, out var sourceArray))
			{
				Write(sourceArray.Array, sourceArray.Offset, sourceArray.Count);
			}
			else
			{
				Write(buffer.Span);
			}
			return default(ValueTask);
		}
		catch (OperationCanceledException oce)
		{
			return new ValueTask(TaskExEx.FromCanceled(oce.CancellationToken));
		}
		catch (Exception exception)
		{
			return new ValueTask(TaskExEx.FromException(exception));
		}
	}

	public override void WriteByte(byte value)
	{
		EnsureNotClosed();
		EnsureWriteable();
		if (_position >= _length)
		{
			int newLength = _position + 1;
			bool mustZero = _position > _length;
			if (newLength >= _capacity && EnsureCapacity(newLength))
			{
				mustZero = false;
			}
			if (mustZero)
			{
				Array.Clear(_buffer, _length, _position - _length);
			}
			_length = newLength;
		}
		_buffer[_position++] = value;
	}

	public virtual void WriteTo(Stream stream)
	{
		if (stream == null)
		{
			throw new ArgumentNullException("stream", SR.ArgumentNull_Stream);
		}
		EnsureNotClosed();
		stream.Write(_buffer, _origin, _length - _origin);
	}
}
