using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace System.Text.Json.Serialization.Converters;

internal sealed class JsonConverterEnum : JsonConverterFactory
{
	public override bool CanConvert(Type type)
	{
		return type.IsEnum;
	}

	public override JsonConverter CreateConverter(Type type, JsonSerializerOptions options)
	{
		return (JsonConverter)Activator.CreateInstance(typeof(JsonConverterEnum<>).MakeGenericType(type), BindingFlags.Instance | BindingFlags.Public, null, new object[1] { EnumConverterOptions.AllowNumbers }, null);
	}
}
internal class JsonConverterEnum<T> : JsonConverter<T> where T : struct, Enum
{
	private static readonly TypeCode s_enumTypeCode = Type.GetTypeCode(typeof(T));

	private static readonly string s_negativeSign = (((int)s_enumTypeCode % 2 == 0) ? null : NumberFormatInfo.CurrentInfo.NegativeSign);

	private readonly EnumConverterOptions _converterOptions;

	private readonly JsonNamingPolicy _namingPolicy;

	private readonly ConcurrentDictionary<string, string> _nameCache;

	public override bool CanConvert(Type type)
	{
		return type.IsEnum;
	}

	public JsonConverterEnum(EnumConverterOptions options)
		: this(options, (JsonNamingPolicy)null)
	{
	}

	public JsonConverterEnum(EnumConverterOptions options, JsonNamingPolicy namingPolicy)
	{
		_converterOptions = options;
		if (namingPolicy != null)
		{
			_nameCache = new ConcurrentDictionary<string, string>();
		}
		else
		{
			namingPolicy = JsonNamingPolicy.Default;
		}
		_namingPolicy = namingPolicy;
	}

	public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		switch (reader.TokenType)
		{
		case JsonTokenType.String:
		{
			if (!_converterOptions.HasFlag(EnumConverterOptions.AllowStrings))
			{
				ThrowHelper.ThrowJsonException();
				return default(T);
			}
			string enumString = reader.GetString();
			if (!Enum.TryParse<T>(enumString, out var value) && !Enum.TryParse<T>(enumString, ignoreCase: true, out value))
			{
				ThrowHelper.ThrowJsonException();
				return default(T);
			}
			return value;
		}
		case JsonTokenType.Number:
			if (_converterOptions.HasFlag(EnumConverterOptions.AllowNumbers))
			{
				switch (s_enumTypeCode)
				{
				case TypeCode.Int32:
				{
					if (reader.TryGetInt32(out var int17))
					{
						return Unsafe.As<int, T>(ref int17);
					}
					break;
				}
				case TypeCode.UInt32:
				{
					if (reader.TryGetUInt32(out var uint17))
					{
						return Unsafe.As<uint, T>(ref uint17);
					}
					break;
				}
				case TypeCode.UInt64:
				{
					if (reader.TryGetUInt64(out var uint18))
					{
						return Unsafe.As<ulong, T>(ref uint18);
					}
					break;
				}
				case TypeCode.Int64:
				{
					if (reader.TryGetInt64(out var int18))
					{
						return Unsafe.As<long, T>(ref int18);
					}
					break;
				}
				case TypeCode.SByte:
				{
					if (reader.TryGetInt32(out var byte8) && JsonHelpers.IsInRangeInclusive(byte8, -128, 127))
					{
						sbyte byte8Value = (sbyte)byte8;
						return Unsafe.As<sbyte, T>(ref byte8Value);
					}
					break;
				}
				case TypeCode.Byte:
				{
					if (reader.TryGetUInt32(out var ubyte8) && JsonHelpers.IsInRangeInclusive(ubyte8, 0u, 255u))
					{
						byte ubyte8Value = (byte)ubyte8;
						return Unsafe.As<byte, T>(ref ubyte8Value);
					}
					break;
				}
				case TypeCode.Int16:
				{
					if (reader.TryGetInt32(out var int16) && JsonHelpers.IsInRangeInclusive(int16, -32768, 32767))
					{
						short shortValue = (short)int16;
						return Unsafe.As<short, T>(ref shortValue);
					}
					break;
				}
				case TypeCode.UInt16:
				{
					if (reader.TryGetUInt32(out var uint16) && JsonHelpers.IsInRangeInclusive(uint16, 0u, 65535u))
					{
						ushort ushortValue = (ushort)uint16;
						return Unsafe.As<ushort, T>(ref ushortValue);
					}
					break;
				}
				}
				ThrowHelper.ThrowJsonException();
				return default(T);
			}
			goto default;
		default:
			ThrowHelper.ThrowJsonException();
			return default(T);
		}
	}

	private static bool IsValidIdentifier(string value)
	{
		return value[0] >= 'A' && (s_negativeSign == null || !value.StartsWith(s_negativeSign));
	}

	public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
	{
		if (_converterOptions.HasFlag(EnumConverterOptions.AllowStrings))
		{
			string original = value.ToString();
			if (_nameCache != null && _nameCache.TryGetValue(original, out var transformed))
			{
				writer.WriteStringValue(transformed);
				return;
			}
			if (IsValidIdentifier(original))
			{
				transformed = _namingPolicy.ConvertName(original);
				writer.WriteStringValue(transformed);
				if (_nameCache != null)
				{
					_nameCache.TryAdd(original, transformed);
				}
				return;
			}
		}
		if (!_converterOptions.HasFlag(EnumConverterOptions.AllowNumbers))
		{
			ThrowHelper.ThrowJsonException();
		}
		switch (s_enumTypeCode)
		{
		case TypeCode.Int32:
			writer.WriteNumberValue(Unsafe.As<T, int>(ref value));
			break;
		case TypeCode.UInt32:
			writer.WriteNumberValue(Unsafe.As<T, uint>(ref value));
			break;
		case TypeCode.UInt64:
			writer.WriteNumberValue(Unsafe.As<T, ulong>(ref value));
			break;
		case TypeCode.Int64:
			writer.WriteNumberValue(Unsafe.As<T, long>(ref value));
			break;
		case TypeCode.Int16:
			writer.WriteNumberValue(Unsafe.As<T, short>(ref value));
			break;
		case TypeCode.UInt16:
			writer.WriteNumberValue(Unsafe.As<T, ushort>(ref value));
			break;
		case TypeCode.Byte:
			writer.WriteNumberValue(Unsafe.As<T, byte>(ref value));
			break;
		case TypeCode.SByte:
			writer.WriteNumberValue(Unsafe.As<T, sbyte>(ref value));
			break;
		default:
			ThrowHelper.ThrowJsonException();
			break;
		}
	}
}
