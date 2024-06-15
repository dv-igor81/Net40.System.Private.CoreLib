#define DEBUG
using System.Diagnostics;
using System.Reflection;

namespace System.Text.Json;

internal sealed class ReflectionMemberAccessor : MemberAccessor
{
	private delegate TProperty GetProperty<TClass, TProperty>(TClass obj);

	private delegate TProperty GetPropertyByRef<TClass, TProperty>(ref TClass obj);

	private delegate void SetProperty<TClass, TProperty>(TClass obj, TProperty value);

	private delegate void SetPropertyByRef<TClass, TProperty>(ref TClass obj, TProperty value);

	private delegate Func<object, TProperty> GetPropertyByRefFactory<TClass, TProperty>(GetPropertyByRef<TClass, TProperty> set);

	private delegate Action<object, TProperty> SetPropertyByRefFactory<TClass, TProperty>(SetPropertyByRef<TClass, TProperty> set);

	private static readonly MethodInfo s_createStructPropertyGetterMethod = new GetPropertyByRefFactory<int, int>(CreateStructPropertyGetter).Method.GetGenericMethodDefinition();

	private static readonly MethodInfo s_createStructPropertySetterMethod = new SetPropertyByRefFactory<int, int>(CreateStructPropertySetter).Method.GetGenericMethodDefinition();

	public override JsonClassInfo.ConstructorDelegate CreateConstructor(Type type)
	{
		Debug.Assert(type != null);
		ConstructorInfo realMethod = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
		if (type.IsAbstract)
		{
			return null;
		}
		if (realMethod == null && !type.IsValueType)
		{
			return null;
		}
		return () => Activator.CreateInstance(type);
	}

	public override ImmutableCollectionCreator ImmutableCollectionCreateRange(Type constructingType, Type collectionType, Type elementType)
	{
		MethodInfo createRange = ImmutableCollectionCreateRangeMethod(constructingType, elementType);
		if (createRange == null)
		{
			return null;
		}
		Type creatorType = typeof(ImmutableEnumerableCreator<, >).MakeGenericType(elementType, collectionType);
		ConstructorInfo constructor = creatorType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
		ImmutableCollectionCreator creator = (ImmutableCollectionCreator)constructor.Invoke(new object[0]);
		creator.RegisterCreatorDelegateFromMethod(createRange);
		return creator;
	}

	public override ImmutableCollectionCreator ImmutableDictionaryCreateRange(Type constructingType, Type collectionType, Type elementType)
	{
		MethodInfo createRange = ImmutableDictionaryCreateRangeMethod(constructingType, elementType);
		if (createRange == null)
		{
			return null;
		}
		Type creatorType = typeof(ImmutableDictionaryCreator<, >).MakeGenericType(elementType, collectionType);
		ConstructorInfo constructor = creatorType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
		ImmutableCollectionCreator creator = (ImmutableCollectionCreator)constructor.Invoke(new object[0]);
		creator.RegisterCreatorDelegateFromMethod(createRange);
		return creator;
	}

	public override Func<object, TProperty> CreatePropertyGetter<TClass, TProperty>(PropertyInfo propertyInfo)
	{
		MethodInfo getMethodInfo = propertyInfo.GetGetMethod();
		if (typeof(TClass).IsValueType)
		{
			GetPropertyByRefFactory<TClass, TProperty> factory = CreateDelegate<GetPropertyByRefFactory<TClass, TProperty>>(s_createStructPropertyGetterMethod.MakeGenericMethod(typeof(TClass), typeof(TProperty)));
			GetPropertyByRef<TClass, TProperty> propertyGetter2 = CreateDelegate<GetPropertyByRef<TClass, TProperty>>(getMethodInfo);
			return factory(propertyGetter2);
		}
		GetProperty<TClass, TProperty> propertyGetter = CreateDelegate<GetProperty<TClass, TProperty>>(getMethodInfo);
		return (object obj) => propertyGetter((TClass)obj);
	}

	public override Action<object, TProperty> CreatePropertySetter<TClass, TProperty>(PropertyInfo propertyInfo)
	{
		MethodInfo setMethodInfo = propertyInfo.GetSetMethod();
		if (typeof(TClass).IsValueType)
		{
			SetPropertyByRefFactory<TClass, TProperty> factory = CreateDelegate<SetPropertyByRefFactory<TClass, TProperty>>(s_createStructPropertySetterMethod.MakeGenericMethod(typeof(TClass), typeof(TProperty)));
			SetPropertyByRef<TClass, TProperty> propertySetter2 = CreateDelegate<SetPropertyByRef<TClass, TProperty>>(setMethodInfo);
			return factory(propertySetter2);
		}
		SetProperty<TClass, TProperty> propertySetter = CreateDelegate<SetProperty<TClass, TProperty>>(setMethodInfo);
		return delegate(object obj, TProperty value)
		{
			propertySetter((TClass)obj, value);
		};
	}

	private static TDelegate CreateDelegate<TDelegate>(MethodInfo methodInfo) where TDelegate : Delegate
	{
		return (TDelegate)Delegate.CreateDelegate(typeof(TDelegate), methodInfo);
	}

	private static Func<object, TProperty> CreateStructPropertyGetter<TClass, TProperty>(GetPropertyByRef<TClass, TProperty> get) where TClass : struct
	{
		return (object obj) => get(ref UnsafeEx.Unbox<TClass>(obj));
	}

	private static Action<object, TProperty> CreateStructPropertySetter<TClass, TProperty>(SetPropertyByRef<TClass, TProperty> set) where TClass : struct
	{
		return delegate(object obj, TProperty value)
		{
			set(ref UnsafeEx.Unbox<TClass>(obj), value);
		};
	}
}
