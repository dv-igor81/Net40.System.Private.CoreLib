#define DEBUG
using System.Diagnostics;
using System.Reflection;

namespace System.Text.Json;

internal abstract class MemberAccessor
{
	public abstract JsonClassInfo.ConstructorDelegate CreateConstructor(Type classType);

	public abstract ImmutableCollectionCreator ImmutableCollectionCreateRange(Type constructingType, Type collectionType, Type elementType);

	public abstract ImmutableCollectionCreator ImmutableDictionaryCreateRange(Type constructingType, Type collectionType, Type elementType);

	protected MethodInfo ImmutableCollectionCreateRangeMethod(Type constructingType, Type elementType)
	{
		MethodInfo createRangeMethod = FindImmutableCreateRangeMethod(constructingType);
		if (createRangeMethod == null)
		{
			return null;
		}
		return createRangeMethod.MakeGenericMethod(elementType);
	}

	protected MethodInfo ImmutableDictionaryCreateRangeMethod(Type constructingType, Type elementType)
	{
		MethodInfo createRangeMethod = FindImmutableCreateRangeMethod(constructingType);
		if (createRangeMethod == null)
		{
			return null;
		}
		return createRangeMethod.MakeGenericMethod(typeof(string), elementType);
	}

	private MethodInfo FindImmutableCreateRangeMethod(Type constructingType)
	{
		MethodInfo[] constructingTypeMethods = constructingType.GetMethods();
		MethodInfo[] array = constructingTypeMethods;
		foreach (MethodInfo method in array)
		{
			if (method.Name == "CreateRange" && method.GetParameters().Length == 1)
			{
				return method;
			}
		}
		Debug.Fail("Could not create the appropriate CreateRange method.");
		return null;
	}

	public abstract Func<object, TProperty> CreatePropertyGetter<TClass, TProperty>(PropertyInfo propertyInfo);

	public abstract Action<object, TProperty> CreatePropertySetter<TClass, TProperty>(PropertyInfo propertyInfo);
}
