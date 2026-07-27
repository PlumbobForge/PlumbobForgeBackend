using System.Collections;

namespace s3pi.Interfaces;

public interface IGenericAdd : IList, ICollection, IEnumerable
{
	void Add();

	bool Add(params object[] fields);
}
