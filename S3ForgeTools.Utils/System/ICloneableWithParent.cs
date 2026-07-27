namespace System;

public interface ICloneableWithParent : ICloneable
{
	object Clone(object newParent);
}
