using System.Collections;
using System.Reflection;

namespace System.Text.Json;

internal abstract class ImmutableCollectionCreator
{
	public abstract void RegisterCreatorDelegateFromMethod(MethodInfo creator);

	public abstract bool CreateImmutableEnumerable(IList items, out IEnumerable collection);

	public abstract bool CreateImmutableDictionary(IDictionary items, out IDictionary collection);
}
