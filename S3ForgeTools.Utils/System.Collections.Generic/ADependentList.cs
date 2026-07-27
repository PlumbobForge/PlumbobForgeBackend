namespace System.Collections.Generic;

[Obsolete]
public abstract class ADependentList<T, U> : List<T>, IDependentList<T, U>, IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable, ICloneableWithParent, ICloneable
{
	protected U parent;

	protected long maxSize = -1L;

	public long MaxSize => maxSize;

	protected ADependentList(U parent)
	{
		this.parent = parent;
	}

	protected ADependentList(U parent, IList<T> lt)
		: base((IEnumerable<T>)lt)
	{
		this.parent = parent;
	}

	protected ADependentList(U parent, long size)
	{
		this.parent = parent;
		maxSize = size;
	}

	protected ADependentList(U parent, long size, IList<T> lt)
		: base((IEnumerable<T>)lt)
	{
		if (size >= 0 && lt.Count > size)
		{
			throw new ArgumentOutOfRangeException("lt", "Size of list supplied must not exceed maximum list size supplied.");
		}
		this.parent = parent;
		maxSize = size;
	}

	public object Clone(object newParent)
	{
		return Clone((U)newParent);
	}

	public abstract object Clone(U newParent);

	public object Clone()
	{
		return Clone(parent);
	}

	public new virtual void Insert(int index, T item)
	{
		if (maxSize >= 0 && base.Count == maxSize)
		{
			throw new InvalidOperationException();
		}
		base.Insert(index, item);
	}

	public new virtual void Add(T item)
	{
		if (maxSize >= 0 && base.Count == maxSize)
		{
			throw new InvalidOperationException();
		}
		base.Add(item);
	}
}
