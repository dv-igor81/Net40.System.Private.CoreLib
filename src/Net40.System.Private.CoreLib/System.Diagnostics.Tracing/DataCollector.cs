#define DEBUG
using System.Runtime.InteropServices;

namespace System.Diagnostics.Tracing;

internal struct DataCollector
{
	[ThreadStatic]
	internal static System.Diagnostics.Tracing.DataCollector ThreadInstance;

	private unsafe byte* scratchEnd;

	private unsafe EventSource.EventData* datasEnd;

	private unsafe GCHandle* pinsEnd;

	private unsafe EventSource.EventData* datasStart;

	private unsafe byte* scratch;

	private unsafe EventSource.EventData* datas;

	private unsafe GCHandle* pins;

	private byte[]? buffer;

	private int bufferPos;

	private int bufferNesting;

	private bool writingScalars;

	internal unsafe void Enable(byte* scratch, int scratchSize, EventSource.EventData* datas, int dataCount, GCHandle* pins, int pinCount)
	{
		datasStart = datas;
		scratchEnd = scratch + scratchSize;
		datasEnd = datas + dataCount;
		pinsEnd = pins + pinCount;
		this.scratch = scratch;
		this.datas = datas;
		this.pins = pins;
		writingScalars = false;
	}

	internal void Disable()
	{
		this = default(System.Diagnostics.Tracing.DataCollector);
	}

	internal unsafe EventSource.EventData* Finish()
	{
		ScalarsEnd();
		return datas;
	}

	internal unsafe void AddScalar(void* value, int size)
	{
		if (bufferNesting == 0)
		{
			byte* scratchOld = scratch;
			byte* scratchNew = scratchOld + size;
			if (scratchEnd < scratchNew)
			{
				throw new IndexOutOfRangeException("SR.EventSource_AddScalarOutOfRange");
			}
			ScalarsBegin();
			scratch = scratchNew;
			for (int i = 0; i != size; i++)
			{
				scratchOld[i] = ((byte*)value)[i];
			}
		}
		else
		{
			int oldPos = bufferPos;
			int j;
			checked
			{
				bufferPos += size;
				EnsureBuffer();
				Debug.Assert(buffer != null);
				j = 0;
			}
			while (j != size)
			{
				buffer[oldPos] = ((byte*)value)[j];
				j++;
				oldPos++;
			}
		}
	}

	internal unsafe void AddBinary(string? value, int size)
	{
		if (size > 65535)
		{
			size = 65534;
		}
		if (bufferNesting != 0)
		{
			EnsureBuffer(size + 2);
		}
		AddScalar(&size, 2);
		if (size == 0)
		{
			return;
		}
		if (bufferNesting == 0)
		{
			ScalarsEnd();
			PinArray(value, size);
			return;
		}
		int oldPos = bufferPos;
		checked
		{
			bufferPos += size;
			EnsureBuffer();
			Debug.Assert(buffer != null);
			fixed (void* p = value)
			{
				Marshal.Copy((IntPtr)p, buffer, oldPos, size);
			}
		}
	}

	internal unsafe void AddNullTerminatedString(string? value)
	{
		if (value == null)
		{
			value = string.Empty;
		}
		int nullCharIndex = value.IndexOf('\0');
		if (nullCharIndex < 0)
		{
			nullCharIndex = value.Length;
		}
		int size = (nullCharIndex + 1) * 2;
		if (bufferNesting != 0)
		{
			EnsureBuffer(size);
		}
		if (bufferNesting == 0)
		{
			ScalarsEnd();
			PinArray(value, size);
			return;
		}
		int oldPos = bufferPos;
		checked
		{
			bufferPos += size;
			EnsureBuffer();
			Debug.Assert(buffer != null);
			fixed (void* p = value)
			{
				Marshal.Copy((IntPtr)p, buffer, oldPos, size);
			}
		}
	}

	internal void AddBinary(Array value, int size)
	{
		AddArray(value, size, 1);
	}

	internal unsafe void AddArray(Array? value, int length, int itemSize)
	{
		if (length > 65535)
		{
			length = 65535;
		}
		int size = length * itemSize;
		if (bufferNesting != 0)
		{
			EnsureBuffer(size + 2);
		}
		AddScalar(&length, 2);
		checked
		{
			if (length != 0)
			{
				if (bufferNesting == 0)
				{
					ScalarsEnd();
					PinArray(value, size);
					return;
				}
				int oldPos = bufferPos;
				bufferPos += size;
				EnsureBuffer();
				Debug.Assert(value != null && buffer != null);
				Buffer.BlockCopy(value, 0, buffer, oldPos, size);
			}
		}
	}

	internal int BeginBufferedArray()
	{
		BeginBuffered();
		bufferPos += 2;
		return bufferPos;
	}

	internal void EndBufferedArray(int bookmark, int count)
	{
		EnsureBuffer();
		Debug.Assert(buffer != null);
		buffer[bookmark - 2] = (byte)count;
		buffer[bookmark - 1] = (byte)(count >> 8);
		EndBuffered();
	}

	internal void BeginBuffered()
	{
		ScalarsEnd();
		bufferNesting++;
	}

	internal void EndBuffered()
	{
		bufferNesting--;
		if (bufferNesting == 0)
		{
			EnsureBuffer();
			Debug.Assert(buffer != null);
			PinArray(buffer, bufferPos);
			buffer = null;
			bufferPos = 0;
		}
	}

	private void EnsureBuffer()
	{
		int required = bufferPos;
		if (buffer == null || buffer.Length < required)
		{
			GrowBuffer(required);
		}
	}

	private void EnsureBuffer(int additionalSize)
	{
		int required = bufferPos + additionalSize;
		if (buffer == null || buffer.Length < required)
		{
			GrowBuffer(required);
		}
	}

	private void GrowBuffer(int required)
	{
		int newSize = ((buffer == null) ? 64 : buffer.Length);
		do
		{
			newSize *= 2;
		}
		while (newSize < required);
		Array.Resize(ref buffer, newSize);
	}

	private unsafe void PinArray(object? value, int size)
	{
		GCHandle* pinsTemp = pins;
		if (pinsEnd <= pinsTemp)
		{
			throw new IndexOutOfRangeException("SR.EventSource_PinArrayOutOfRange");
		}
		EventSource.EventData* datasTemp = datas;
		if (datasEnd <= datasTemp)
		{
			throw new IndexOutOfRangeException("SR.EventSource_DataDescriptorsOutOfRange");
		}
		pins = pinsTemp + 1;
		datas = datasTemp + 1;
		*pinsTemp = GCHandle.Alloc(value, GCHandleType.Pinned);
		datasTemp->DataPointer = pinsTemp->AddrOfPinnedObject();
		datasTemp->m_Size = size;
	}

	private unsafe void ScalarsBegin()
	{
		if (!writingScalars)
		{
			EventSource.EventData* datasTemp = datas;
			if (datasEnd <= datasTemp)
			{
				throw new IndexOutOfRangeException("SR.EventSource_DataDescriptorsOutOfRange");
			}
			datasTemp->DataPointer = (IntPtr)scratch;
			writingScalars = true;
		}
	}

	private unsafe void ScalarsEnd()
	{
		checked
		{
			if (writingScalars)
			{
				EventSource.EventData* datasTemp = datas;
				datasTemp->m_Size = (int)(scratch - unchecked((byte*)checked((nuint)datasTemp->m_Ptr)));
				datas = datasTemp + 1;
				writingScalars = false;
			}
		}
	}
}
