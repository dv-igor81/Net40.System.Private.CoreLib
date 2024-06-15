#define DEBUG
using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.Json.Serialization;

internal sealed class JsonPropertyInfoNotNullableContravariant<TClass, TDeclaredProperty, TRuntimeProperty, TConverter> : JsonPropertyInfoCommon<TClass, TDeclaredProperty, TRuntimeProperty, TConverter> where TDeclaredProperty : TConverter
{
	protected override void OnRead(ref ReadStack state, ref Utf8JsonReader reader)
	{
		if (base.Converter == null)
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(base.RuntimePropertyType);
		}
		TConverter value = base.Converter.Read(ref reader, base.RuntimePropertyType, base.Options);
		if (state.Current.ReturnValue == null)
		{
			state.Current.ReturnValue = value;
		}
		else
		{
			base.Set(state.Current.ReturnValue, (TDeclaredProperty)(object)value);
		}
	}

	protected override void OnReadEnumerable(ref ReadStack state, ref Utf8JsonReader reader)
	{
		if (base.Converter == null)
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(base.RuntimePropertyType);
		}
		if (state.Current.KeyName == null && state.Current.IsProcessingDictionaryOrIDictionaryConstructible())
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(base.RuntimePropertyType);
			return;
		}
		if (state.Current.IsProcessingEnumerable() && state.Current.TempEnumerableValues == null && state.Current.ReturnValue == null)
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(base.RuntimePropertyType);
			return;
		}
		TConverter value = base.Converter.Read(ref reader, base.RuntimePropertyType, base.Options);
		JsonSerializer.ApplyValueToEnumerable(ref value, ref state);
	}

	protected override void OnWrite(ref WriteStackFrame current, Utf8JsonWriter writer)
	{
		TConverter value = ((!base.IsPropertyPolicy) ? ((TConverter)(object)base.Get(current.CurrentValue)) : ((TConverter)current.CurrentValue));
		if (value == null)
		{
			Debug.Assert(EscapedName.HasValue);
			if (!base.IgnoreNullValues)
			{
				writer.WriteNull(EscapedName.Value);
			}
		}
		else if (base.Converter != null)
		{
			if (EscapedName.HasValue)
			{
				writer.WritePropertyName(EscapedName.Value);
			}
			base.Converter.Write(writer, value, base.Options);
		}
	}

	protected override void OnWriteDictionary(ref WriteStackFrame current, Utf8JsonWriter writer)
	{
		JsonSerializer.WriteDictionary(base.Converter, base.Options, ref current, writer);
	}

	protected override void OnWriteEnumerable(ref WriteStackFrame current, Utf8JsonWriter writer)
	{
		if (base.Converter != null)
		{
			Debug.Assert(current.CollectionEnumerator != null);
			TConverter value = ((!(current.CollectionEnumerator is IEnumerator<TConverter> enumerator)) ? ((TConverter)current.CollectionEnumerator.Current) : enumerator.Current);
			if (value == null)
			{
				writer.WriteNullValue();
			}
			else
			{
				base.Converter.Write(writer, value, base.Options);
			}
		}
	}

	public override Type GetDictionaryConcreteType()
	{
		return typeof(Dictionary<string, TRuntimeProperty>);
	}

	public override void GetDictionaryKeyAndValueFromGenericDictionary(ref WriteStackFrame writeStackFrame, out string key, out object value)
	{
		if (writeStackFrame.CollectionEnumerator is IEnumerator<KeyValuePair<string, TDeclaredProperty>> genericEnumerator)
		{
			key = genericEnumerator.Current.Key;
			value = genericEnumerator.Current.Value;
			return;
		}
		throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(writeStackFrame.JsonPropertyInfo.DeclaredPropertyType, writeStackFrame.JsonPropertyInfo.ParentClassType, writeStackFrame.JsonPropertyInfo.PropertyInfo);
	}
}
