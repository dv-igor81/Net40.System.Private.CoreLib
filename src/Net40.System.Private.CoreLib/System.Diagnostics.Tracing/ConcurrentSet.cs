using System.Threading;

namespace System.Diagnostics.Tracing;

internal struct ConcurrentSet<KeyType, ItemType> where ItemType : System.Diagnostics.Tracing.ConcurrentSetItem<KeyType, ItemType>
{
	private ItemType[]? items;

	public ItemType? TryGet(KeyType key)
	{
		ItemType[] oldItems = items;
		if (oldItems == null)
		{
			goto IL_0070;
		}
		int lo = 0;
		int hi = oldItems.Length;
		ItemType item;
		while (true)
		{
			int i = (lo + hi) / 2;
			item = oldItems[i];
			int cmp = item.Compare(key);
			if (cmp == 0)
			{
				break;
			}
			if (cmp < 0)
			{
				lo = i + 1;
			}
			else
			{
				hi = i;
			}
			if (lo != hi)
			{
				continue;
			}
			goto IL_0070;
		}
		goto IL_0078;
		IL_0078:
		return item;
		IL_0070:
		item = null;
		goto IL_0078;
	}

	public ItemType GetOrAdd(ItemType newItem)
	{
		ItemType[] oldItems = items;
		ItemType item;
		while (true)
		{
			ItemType[] newItems;
			if (oldItems == null)
			{
				newItems = new ItemType[1] { newItem };
				goto IL_00c5;
			}
			int lo = 0;
			int hi = oldItems.Length;
			while (true)
			{
				int i = (lo + hi) / 2;
				item = oldItems[i];
				int cmp = item.Compare(newItem);
				if (cmp == 0)
				{
					break;
				}
				if (cmp < 0)
				{
					lo = i + 1;
				}
				else
				{
					hi = i;
				}
				if (lo != hi)
				{
					continue;
				}
				goto IL_008d;
			}
			break;
			IL_00c5:
			newItems = Interlocked.CompareExchange(ref items, newItems, oldItems);
			if (oldItems != newItems)
			{
				oldItems = newItems;
				continue;
			}
			item = newItem;
			break;
			IL_008d:
			int oldLength = oldItems.Length;
			newItems = new ItemType[oldLength + 1];
			Array.Copy(oldItems, 0, newItems, 0, lo);
			newItems[lo] = newItem;
			Array.Copy(oldItems, lo, newItems, lo + 1, oldLength - lo);
			goto IL_00c5;
		}
		return item;
	}
}
