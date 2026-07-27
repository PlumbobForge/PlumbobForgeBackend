namespace System.Collections.Generic;

public interface IDependentList<T, U> : IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable, ICloneableWithParent, ICloneable
{
}
