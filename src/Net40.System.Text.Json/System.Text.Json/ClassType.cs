namespace System.Text.Json;

internal enum ClassType : byte
{
	Unknown = 1,
	Object = 2,
	Value = 4,
	Enumerable = 8,
	Dictionary = 0x10,
	IDictionaryConstructible = 0x20
}
