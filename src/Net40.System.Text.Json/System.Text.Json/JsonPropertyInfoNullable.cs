#define DEBUG
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace System.Text.Json;

internal sealed class JsonPropertyInfoNullable<TClass, TProperty> : JsonPropertyInfoCommon<TClass, TProperty?, TProperty, TProperty> where TProperty : struct
{
	private static readonly Type s_underlyingType = typeof(TProperty);

	protected override void OnRead(ref ReadStack state, ref Utf8JsonReader reader)
	{
		if (base.Converter == null)
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(base.RuntimePropertyType);
		}
		TProperty value = base.Converter.Read(ref reader, s_underlyingType, base.Options);
		if (state.Current.ReturnValue == null)
		{
			state.Current.ReturnValue = value;
		}
		else
		{
			base.Set(state.Current.ReturnValue, value);
		}
	}

	protected override void OnReadEnumerable(ref ReadStack state, ref Utf8JsonReader reader)
	{
		if (base.Converter == null)
		{
			ThrowHelper.ThrowJsonException_DeserializeUnableToConvertValue(base.RuntimePropertyType);
		}
		TProperty value = base.Converter.Read(ref reader, s_underlyingType, base.Options);
		TProperty? nullableValue = value;
		JsonSerializer.ApplyValueToEnumerable(ref nullableValue, ref state);
	}

	protected override void OnWrite(ref WriteStackFrame current, Utf8JsonWriter writer)
	{
		TProperty? value = ((!base.IsPropertyPolicy) ? base.Get(current.CurrentValue) : ((TProperty?)current.CurrentValue));
		if (!value.HasValue)
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
			base.Converter.Write(writer, value.GetValueOrDefault(), base.Options);
		}
	}

	protected override void OnWriteDictionary(ref WriteStackFrame current, Utf8JsonWriter writer)
	{
		Debug.Assert(base.Converter != null && current.CollectionEnumerator != null);
		string key = null;
		TProperty? value = null;
		if (current.CollectionEnumerator is IEnumerator<KeyValuePair<string, TProperty?>> { Current: var current2 } enumerator)
		{
			key = current2.Key;
			value = enumerator.Current.Value;
		}
		else if (current.IsIDictionaryConstructible || current.IsIDictionaryConstructibleProperty)
		{
			key = (string)((DictionaryEntry)current.CollectionEnumerator.Current).Key;
			value = (TProperty?)((DictionaryEntry)current.CollectionEnumerator.Current).Value;
		}
		Debug.Assert(key != null);
		if (base.Options.DictionaryKeyPolicy != null)
		{
			Debug.Assert(current.ExtensionDataStatus != ExtensionDataWriteStatus.Writing);
			key = base.Options.DictionaryKeyPolicy.ConvertName(key);
			if (key == null)
			{
				ThrowHelper.ThrowInvalidOperationException_SerializerDictionaryKeyNull(base.Options.DictionaryKeyPolicy.GetType());
			}
		}
		if (!value.HasValue)
		{
			writer.WriteNull(key);
			return;
		}
		writer.WritePropertyName(key);
		base.Converter.Write(writer, value.GetValueOrDefault(), base.Options);
	}

	protected override void OnWriteEnumerable(ref WriteStackFrame current, Utf8JsonWriter writer)
	{
		if (base.Converter != null)
		{
			Debug.Assert(current.CollectionEnumerator != null);
			TProperty? value = ((!(current.CollectionEnumerator is IEnumerator<TProperty?> enumerator)) ? ((TProperty?)current.CollectionEnumerator.Current) : enumerator.Current);
			if (!value.HasValue)
			{
				writer.WriteNullValue();
			}
			else
			{
				base.Converter.Write(writer, value.GetValueOrDefault(), base.Options);
			}
		}
	}

	public override Type GetDictionaryConcreteType()
	{
		return typeof(Dictionary<string, TProperty?>);
	}

	public override void GetDictionaryKeyAndValueFromGenericDictionary(ref WriteStackFrame writeStackFrame, out string key, out object value)
	{
		if (writeStackFrame.CollectionEnumerator is IEnumerator<KeyValuePair<string, TProperty?>> genericEnumerator)
		{
			key = genericEnumerator.Current.Key;
			value = genericEnumerator.Current.Value;
			return;
		}
		throw ThrowHelper.GetNotSupportedException_SerializationNotSupportedCollection(writeStackFrame.JsonPropertyInfo.DeclaredPropertyType, writeStackFrame.JsonPropertyInfo.ParentClassType, writeStackFrame.JsonPropertyInfo.PropertyInfo);
	}
}
