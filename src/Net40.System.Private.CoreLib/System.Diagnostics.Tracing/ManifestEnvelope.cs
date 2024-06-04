using System.Runtime.InteropServices;

namespace System.Diagnostics.Tracing;

[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct ManifestEnvelope
{
	public enum ManifestFormats : byte
	{
		SimpleXmlFormat = 1
	}

	public const int MaxChunkSize = 65280;
}
