#define DEBUG
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace System.Text.Json;

internal sealed class ImmutableEnumerableCreator<TElement, TCollection> : ImmutableCollectionCreator where TCollection : IEnumerable<TElement>
{
	private Func<IEnumerable<TElement>, TCollection> _creatorDelegate;

	public override void RegisterCreatorDelegateFromMethod(MethodInfo creator)
	{
		Debug.Assert(_creatorDelegate == null);
		_creatorDelegate = (Func<IEnumerable<TElement>, TCollection>)MethodInfoTheraotExtensions.CreateDelegate(creator, typeof(Func<IEnumerable<TElement>, TCollection>));
	}

	public override bool CreateImmutableEnumerable(IList items, out IEnumerable collection)
	{
		Debug.Assert(_creatorDelegate != null);
		collection = _creatorDelegate(CreateGenericTElementIEnumerable(items));
		return true;
	}

	public override bool CreateImmutableDictionary(IDictionary items, out IDictionary collection)
	{
		collection = null;
		return false;
	}

	private IEnumerable<TElement> CreateGenericTElementIEnumerable(IList sourceList)
	{
		foreach (object item in sourceList)
		{
			yield return (TElement)item;
		}
	}
}
