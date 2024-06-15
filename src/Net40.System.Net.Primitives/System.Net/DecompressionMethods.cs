namespace System.Net.Net40;

[Flags]
public enum DecompressionMethods
{
	None = 0,
	GZip = 1,
	Deflate = 2,
	Brotli = 4,
	All = -1
}