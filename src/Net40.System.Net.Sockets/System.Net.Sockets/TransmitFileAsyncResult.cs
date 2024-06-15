using System.IO;
using System.Net.Sockets.Net40;

namespace System.Net.Sockets;

internal sealed class TransmitFileAsyncResult : BaseOverlappedAsyncResult
{
	private FileStream _fileStream;

	private bool _doDisconnect;

	internal bool DoDisconnect => _doDisconnect;

	internal TransmitFileAsyncResult(Net40.Socket socket, object asyncState, AsyncCallback asyncCallback)
		: base(socket, asyncState, asyncCallback)
	{
		}

	internal void SetUnmanagedStructures(FileStream fileStream, byte[] preBuffer, byte[] postBuffer, bool doDisconnect)
	{
			_fileStream = fileStream;
			_doDisconnect = doDisconnect;
			int num = 0;
			if (preBuffer != null && preBuffer.Length != 0)
			{
				num++;
			}
			if (postBuffer != null && postBuffer.Length != 0)
			{
				num++;
			}
			object[] array = null;
			if (num != 0)
			{
				array = new object[num];
				if (preBuffer != null && preBuffer.Length != 0)
				{
					array[--num] = preBuffer;
				}
				if (postBuffer != null && postBuffer.Length != 0)
				{
					array[--num] = postBuffer;
				}
			}
			SetUnmanagedStructures(array);
		}

	protected override void ForceReleaseUnmanagedStructures()
	{
			if (_fileStream != null)
			{
				_fileStream.Dispose();
				_fileStream = null;
			}
			base.ForceReleaseUnmanagedStructures();
		}
}