namespace System.Collections.Generic;

public abstract class AHandlerList<T> : List<T> where T : IEquatable<T>
{
	protected EventHandler handler;

	protected long maxSize = -1L;

	public new virtual T this[int index]
	{
		get
		{
			return base[index];
		}
		set
		{
			if (!base[index].Equals(value))
			{
				base[index] = value;
				OnListChanged();
			}
		}
	}

	public long MaxSize => maxSize;

	protected AHandlerList(EventHandler handler)
	{
		this.handler = handler;
	}

	protected AHandlerList(EventHandler handler, IList<T> ilt)
		: base((IEnumerable<T>)ilt)
	{
		this.handler = handler;
	}

	protected AHandlerList(EventHandler handler, long size)
	{
		this.handler = handler;
		maxSize = size;
	}

	protected AHandlerList(EventHandler handler, long size, IList<T> ilt)
		: base((IEnumerable<T>)ilt)
	{
		this.handler = handler;
		maxSize = size;
	}

	public new virtual void AddRange(IEnumerable<T> collection)
	{
		int count = new List<T>(collection).Count;
		if (maxSize >= 0 && base.Count >= maxSize - count)
		{
			throw new InvalidOperationException();
		}
		EventHandler eventHandler = handler;
		handler = null;
		foreach (T item in collection)
		{
			Add(item);
		}
		handler = eventHandler;
		OnListChanged();
	}

	public new virtual void InsertRange(int index, IEnumerable<T> collection)
	{
		int count = new List<T>(collection).Count;
		if (maxSize >= 0 && base.Count >= maxSize - count)
		{
			throw new InvalidOperationException();
		}
		EventHandler eventHandler = handler;
		handler = null;
		foreach (T item in collection)
		{
			Insert(index++, item);
		}
		handler = eventHandler;
		OnListChanged();
	}

	public new virtual int RemoveAll(Predicate<T> match)
	{
		int num = base.RemoveAll(match);
		if (num != 0)
		{
			OnListChanged();
		}
		return num;
	}

	public new virtual void RemoveRange(int index, int count)
	{
		base.RemoveRange(index, count);
		OnListChanged();
	}

	public new virtual void Reverse()
	{
		base.Reverse();
		OnListChanged();
	}

	public new virtual void Reverse(int index, int count)
	{
		base.Reverse(index, count);
		OnListChanged();
	}

	public new virtual void Sort()
	{
		base.Sort();
		OnListChanged();
	}

	public new virtual void Sort(Comparison<T> comparison)
	{
		base.Sort(comparison);
		OnListChanged();
	}

	public new virtual void Sort(IComparer<T> comparer)
	{
		base.Sort(comparer);
		OnListChanged();
	}

	public new virtual void Sort(int index, int count, IComparer<T> comparer)
	{
		base.Sort(index, count, comparer);
		OnListChanged();
	}

	public new virtual void Insert(int index, T item)
	{
		if (maxSize >= 0 && base.Count == maxSize)
		{
			throw new InvalidOperationException();
		}
		base.Insert(index, item);
		OnListChanged();
	}

	public new virtual void RemoveAt(int index)
	{
		base.RemoveAt(index);
		OnListChanged();
	}

	public new virtual void Add(T item)
	{
		if (maxSize >= 0 && base.Count == maxSize)
		{
			throw new InvalidOperationException();
		}
		base.Add(item);
		OnListChanged();
	}

	public new virtual void Clear()
	{
		base.Clear();
		OnListChanged();
	}

	public new virtual bool Remove(T item)
	{
		bool flag = base.Remove(item);
		if (flag)
		{
			OnListChanged();
		}
		return flag;
	}

	protected void OnListChanged()
	{
		if (handler != null)
		{
			handler(this, EventArgs.Empty);
		}
	}
}
