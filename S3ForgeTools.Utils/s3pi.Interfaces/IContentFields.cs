using System.Collections.Generic;

namespace s3pi.Interfaces;

public interface IContentFields
{
	List<string> ContentFields { get; }

	TypedValue this[string index] { get; set; }
}
