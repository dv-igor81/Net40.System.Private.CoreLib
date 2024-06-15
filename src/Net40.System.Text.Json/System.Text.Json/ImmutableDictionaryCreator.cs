#define DEBUG
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace System.Text.Json;

internal sealed class ImmutableDictionaryCreator<TElement, TCollection> : ImmutableCollectionCreator where TCollection : IReadOnlyDictionary<string, TElement>
{
	private Func<IEnumerable<KeyValuePair<string, TElement>>, TCollection> _creatorDelegate;

	public override void RegisterCreatorDelegateFromMethod(MethodInfo creator)
	{
		Debug.Assert(_creatorDelegate == null);
		_creatorDelegate = (Func<IEnumerable<KeyValuePair<string, TElement>>, TCollection>)MethodInfoTheraotExtensions.CreateDelegate(creator, typeof(Func<IEnumerable<KeyValuePair<string, TElement>>, TCollection>));
	}

	public override bool CreateImmutableEnumerable(IList items, out IEnumerable collection)
	{
		collection = null;
		return false;
	}

	public override bool CreateImmutableDictionary(IDictionary items, out IDictionary collection)
	{
		Debug.Assert(_creatorDelegate != null);
		collection = (IDictionary)(object)_creatorDelegate(CreateGenericTElementIDictionary(items));
		return true;
	}

	private IEnumerable<KeyValuePair<string, TElement>> CreateGenericTElementIDictionary(IDictionary sourceDictionary)
	{
		foreach (DictionaryEntry item in sourceDictionary)
		{
			yield return new KeyValuePair<string, TElement>((string)item.Key, (TElement)item.Value);
		}
	}
}
