using System.Collections.Generic;
using System.Diagnostics;

namespace System.Threading;

//
// Utility functions for getting/creating instances of IAsyncLocalValueMap
//
internal static class AsyncLocalValueMap
{
    public static IAsyncLocalValueMap Empty { get; } = new EmptyAsyncLocalValueMap();

    public static bool IsEmpty(IAsyncLocalValueMap asyncLocalValueMap)
    {
        Debug.Assert(asyncLocalValueMap != null);
        Debug.Assert(asyncLocalValueMap == Empty || asyncLocalValueMap.GetType() != typeof(EmptyAsyncLocalValueMap));

        return asyncLocalValueMap == Empty;
    }

    public static IAsyncLocalValueMap Create(IAsyncLocal key, object? value, bool treatNullValueAsNonexistent)
    {
        // If the value isn't null or a null value may not be treated as nonexistent, then create a new one-element map
        // to store the key/value pair.  Otherwise, use the empty map.
        return value != null || !treatNullValueAsNonexistent ? new OneElementAsyncLocalValueMap(key, value) : Empty;
    }

    // Instance without any key/value pairs.  Used as a singleton/
    private sealed class EmptyAsyncLocalValueMap : IAsyncLocalValueMap
    {
        public IAsyncLocalValueMap Set(IAsyncLocal key, object? value, bool treatNullValueAsNonexistent)
        {
            // If the value isn't null or a null value may not be treated as nonexistent, then create a new one-element map
            // to store the key/value pair.  Otherwise, use the empty map.
            return value != null || !treatNullValueAsNonexistent
                ? new OneElementAsyncLocalValueMap(key, value)
                : (IAsyncLocalValueMap)this;
        }

        public bool TryGetValue(IAsyncLocal key, out object? value)
        {
            value = null;
            return false;
        }
    }

    // Instance with one key/value pair.
    private sealed class OneElementAsyncLocalValueMap : IAsyncLocalValueMap
    {
        private readonly IAsyncLocal _key1;
        private readonly object? _value1;

        public OneElementAsyncLocalValueMap(IAsyncLocal key, object? value)
        {
            _key1 = key;
            _value1 = value;
        }

        public IAsyncLocalValueMap Set(IAsyncLocal key, object? value, bool treatNullValueAsNonexistent)
        {
            if (value != null || !treatNullValueAsNonexistent)
            {
                // If the key matches one already contained in this map, then create a new one-element map with the updated
                // value, otherwise create a two-element map with the additional key/value.
                return ReferenceEquals(key, _key1)
                    ? new OneElementAsyncLocalValueMap(key, value)
                    : (IAsyncLocalValueMap)new TwoElementAsyncLocalValueMap(_key1, _value1, key, value);
            }
            else
            {
                // If the key exists in this map, remove it by downgrading to an empty map.  Otherwise, there's nothing to
                // add or remove, so just return this map.
                return ReferenceEquals(key, _key1) ? Empty : (IAsyncLocalValueMap)this;
            }
        }

        public bool TryGetValue(IAsyncLocal key, out object? value)
        {
            if (ReferenceEquals(key, _key1))
            {
                value = _value1;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }

    // Instance with two key/value pairs.
    private sealed class TwoElementAsyncLocalValueMap : IAsyncLocalValueMap
    {
        private readonly IAsyncLocal _key1, _key2;
        private readonly object? _value1, _value2;

        public TwoElementAsyncLocalValueMap(IAsyncLocal key1, object? value1, IAsyncLocal key2, object? value2)
        {
            _key1 = key1;
            _value1 = value1;
            _key2 = key2;
            _value2 = value2;
        }

        public IAsyncLocalValueMap Set(IAsyncLocal key, object? value, bool treatNullValueAsNonexistent)
        {
            if (value != null || !treatNullValueAsNonexistent)
            {
                // If the key matches one already contained in this map, then create a new two-element map with the updated
                // value, otherwise create a three-element map with the additional key/value.
                return
                    ReferenceEquals(key, _key1) ? new TwoElementAsyncLocalValueMap(key, value, _key2, _value2) :
                    ReferenceEquals(key, _key2) ? new TwoElementAsyncLocalValueMap(_key1, _value1, key, value) :
                    (IAsyncLocalValueMap)new ThreeElementAsyncLocalValueMap(_key1, _value1, _key2, _value2, key, value);
            }
            else
            {
                // If the key exists in this map, remove it by downgrading to a one-element map without the key.  Otherwise,
                // there's nothing to add or remove, so just return this map.
                return
                    ReferenceEquals(key, _key1) ? new OneElementAsyncLocalValueMap(_key2, _value2) :
                    ReferenceEquals(key, _key2) ? new OneElementAsyncLocalValueMap(_key1, _value1) :
                    (IAsyncLocalValueMap)this;
            }
        }

        public bool TryGetValue(IAsyncLocal key, out object? value)
        {
            if (ReferenceEquals(key, _key1))
            {
                value = _value1;
                return true;
            }
            else if (ReferenceEquals(key, _key2))
            {
                value = _value2;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }

    // Instance with three key/value pairs.
    private sealed class ThreeElementAsyncLocalValueMap : IAsyncLocalValueMap
    {
        private readonly IAsyncLocal _key1, _key2, _key3;
        private readonly object? _value1, _value2, _value3;

        public ThreeElementAsyncLocalValueMap(IAsyncLocal key1, object? value1, IAsyncLocal key2, object? value2,
            IAsyncLocal key3, object? value3)
        {
            _key1 = key1;
            _value1 = value1;
            _key2 = key2;
            _value2 = value2;
            _key3 = key3;
            _value3 = value3;
        }

        public IAsyncLocalValueMap Set(IAsyncLocal key, object? value, bool treatNullValueAsNonexistent)
        {
            if (value != null || !treatNullValueAsNonexistent)
            {
                // If the key matches one already contained in this map, then create a new three-element map with the
                // updated value.
                if (ReferenceEquals(key, _key1))
                    return new ThreeElementAsyncLocalValueMap(key, value, _key2, _value2, _key3, _value3);
                if (ReferenceEquals(key, _key2))
                    return new ThreeElementAsyncLocalValueMap(_key1, _value1, key, value, _key3, _value3);
                if (ReferenceEquals(key, _key3))
                    return new ThreeElementAsyncLocalValueMap(_key1, _value1, _key2, _value2, key, value);

                // The key doesn't exist in this map, so upgrade to a multi map that contains
                // the additional key/value pair.
                var multi = new MultiElementAsyncLocalValueMap(4);
                multi.UnsafeStore(0, _key1, _value1);
                multi.UnsafeStore(1, _key2, _value2);
                multi.UnsafeStore(2, _key3, _value3);
                multi.UnsafeStore(3, key, value);
                return multi;
            }
            else
            {
                // If the key exists in this map, remove it by downgrading to a two-element map without the key.  Otherwise,
                // there's nothing to add or remove, so just return this map.
                return
                    ReferenceEquals(key, _key1) ? new TwoElementAsyncLocalValueMap(_key2, _value2, _key3, _value3) :
                    ReferenceEquals(key, _key2) ? new TwoElementAsyncLocalValueMap(_key1, _value1, _key3, _value3) :
                    ReferenceEquals(key, _key3) ? new TwoElementAsyncLocalValueMap(_key1, _value1, _key2, _value2) :
                    (IAsyncLocalValueMap)this;
            }
        }

        public bool TryGetValue(IAsyncLocal key, out object? value)
        {
            if (ReferenceEquals(key, _key1))
            {
                value = _value1;
                return true;
            }
            else if (ReferenceEquals(key, _key2))
            {
                value = _value2;
                return true;
            }
            else if (ReferenceEquals(key, _key3))
            {
                value = _value3;
                return true;
            }
            else
            {
                value = null;
                return false;
            }
        }
    }

    // Instance with up to 16 key/value pairs.
    private sealed class MultiElementAsyncLocalValueMap : IAsyncLocalValueMap
    {
        internal const int MaxMultiElements = 16;
        private readonly KeyValuePair<IAsyncLocal, object?>[] _keyValues;

        internal MultiElementAsyncLocalValueMap(int count)
        {
            Debug.Assert(count <= MaxMultiElements);
            _keyValues = new KeyValuePair<IAsyncLocal, object?>[count];
        }

        internal void UnsafeStore(int index, IAsyncLocal key, object? value)
        {
            Debug.Assert(index < _keyValues.Length);
            _keyValues[index] = new KeyValuePair<IAsyncLocal, object?>(key, value);
        }

        public IAsyncLocalValueMap Set(IAsyncLocal key, object? value, bool treatNullValueAsNonexistent)
        {
            // Find the key in this map.
            for (int i = 0; i < _keyValues.Length; i++)
            {
                if (ReferenceEquals(key, _keyValues[i].Key))
                {
                    // The key is in the map.
                    if (value != null || !treatNullValueAsNonexistent)
                    {
                        // Create a new map of the same size that has all of the same pairs, with this new key/value pair
                        // overwriting the old.
                        var multi = new MultiElementAsyncLocalValueMap(_keyValues.Length);
                        Array.Copy(_keyValues, 0, multi._keyValues, 0, _keyValues.Length);
                        multi._keyValues[i] = new KeyValuePair<IAsyncLocal, object?>(key, value);
                        return multi;
                    }
                    else if (_keyValues.Length == 4)
                    {
                        // We only have four elements, one of which we're removing, so downgrade to a three-element map,
                        // without the matching element.
                        return
                            i == 0 ? new ThreeElementAsyncLocalValueMap(_keyValues[1].Key, _keyValues[1].Value,
                                _keyValues[2].Key, _keyValues[2].Value, _keyValues[3].Key, _keyValues[3].Value) :
                            i == 1 ? new ThreeElementAsyncLocalValueMap(_keyValues[0].Key, _keyValues[0].Value,
                                _keyValues[2].Key, _keyValues[2].Value, _keyValues[3].Key, _keyValues[3].Value) :
                            i == 2 ? new ThreeElementAsyncLocalValueMap(_keyValues[0].Key, _keyValues[0].Value,
                                _keyValues[1].Key, _keyValues[1].Value, _keyValues[3].Key, _keyValues[3].Value) :
                            (IAsyncLocalValueMap)new ThreeElementAsyncLocalValueMap(_keyValues[0].Key,
                                _keyValues[0].Value, _keyValues[1].Key, _keyValues[1].Value, _keyValues[2].Key,
                                _keyValues[2].Value);
                    }
                    else
                    {
                        // We have enough elements remaining to warrant a multi map.  Create a new one and copy all of the
                        // elements from this one, except the one to be removed.
                        var multi = new MultiElementAsyncLocalValueMap(_keyValues.Length - 1);
                        if (i != 0) Array.Copy(_keyValues, 0, multi._keyValues, 0, i);
                        if (i != _keyValues.Length - 1)
                            Array.Copy(_keyValues, i + 1, multi._keyValues, i, _keyValues.Length - i - 1);
                        return multi;
                    }
                }
            }

            // The key does not already exist in this map.

            if (value == null && treatNullValueAsNonexistent)
            {
                // We can simply return this same map, as there's nothing to add or remove.
                return this;
            }

            // We need to create a new map that has the additional key/value pair.
            // If with the addition we can still fit in a multi map, create one.
            if (_keyValues.Length < MaxMultiElements)
            {
                var multi = new MultiElementAsyncLocalValueMap(_keyValues.Length + 1);
                Array.Copy(_keyValues, 0, multi._keyValues, 0, _keyValues.Length);
                multi._keyValues[_keyValues.Length] = new KeyValuePair<IAsyncLocal, object?>(key, value);
                return multi;
            }

            // Otherwise, upgrade to a many map.
            var many = new ManyElementAsyncLocalValueMap(MaxMultiElements + 1);
            foreach (KeyValuePair<IAsyncLocal, object?> pair in _keyValues)
            {
                many[pair.Key] = pair.Value;
            }

            many[key] = value;
            return many;
        }

        public bool TryGetValue(IAsyncLocal key, out object? value)
        {
            foreach (KeyValuePair<IAsyncLocal, object?> pair in _keyValues)
            {
                if (ReferenceEquals(key, pair.Key))
                {
                    value = pair.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }
    }

    // Instance with any number of key/value pairs.
    private sealed class ManyElementAsyncLocalValueMap : Dictionary<IAsyncLocal, object?>, IAsyncLocalValueMap
    {
        public ManyElementAsyncLocalValueMap(int capacity) : base(capacity)
        {
        }

        public IAsyncLocalValueMap Set(IAsyncLocal key, object? value, bool treatNullValueAsNonexistent)
        {
            int count = Count;
            bool containsKey = ContainsKey(key);

            // If the value being set exists, create a new many map, copy all of the elements from this one,
            // and then store the new key/value pair into it.  This is the most common case.
            if (value != null || !treatNullValueAsNonexistent)
            {
                var map = new ManyElementAsyncLocalValueMap(count + (containsKey ? 0 : 1));
                foreach (KeyValuePair<IAsyncLocal, object?> pair in this)
                {
                    map[pair.Key] = pair.Value;
                }

                map[key] = value;
                return map;
            }

            // Otherwise, the value is null and a null value may be treated as nonexistent. We can downgrade to a smaller
            // map rather than storing null.

            // If the key is contained in this map, we're going to create a new map that's one pair smaller.
            if (containsKey)
            {
                // If the new count would be within range of a multi map instead of a many map,
                // downgrade to the multi map, which uses less memory and is faster to access.
                // Otherwise, just create a new many map that's missing this key.
                if (count == MultiElementAsyncLocalValueMap.MaxMultiElements + 1)
                {
                    var multi = new MultiElementAsyncLocalValueMap(MultiElementAsyncLocalValueMap.MaxMultiElements);
                    int index = 0;
                    foreach (KeyValuePair<IAsyncLocal, object?> pair in this)
                    {
                        if (!ReferenceEquals(key, pair.Key))
                        {
                            multi.UnsafeStore(index++, pair.Key, pair.Value);
                        }
                    }

                    Debug.Assert(index == MultiElementAsyncLocalValueMap.MaxMultiElements);
                    return multi;
                }
                else
                {
                    var map = new ManyElementAsyncLocalValueMap(count - 1);
                    foreach (KeyValuePair<IAsyncLocal, object?> pair in this)
                    {
                        if (!ReferenceEquals(key, pair.Key))
                        {
                            map[pair.Key] = pair.Value;
                        }
                    }

                    Debug.Assert(map.Count == count - 1);
                    return map;
                }
            }

            // We were storing null and a null value may be treated as nonexistent, but the key wasn't in the map, so
            // there's nothing to change.  Just return this instance.
            return this;
        }
    }
}