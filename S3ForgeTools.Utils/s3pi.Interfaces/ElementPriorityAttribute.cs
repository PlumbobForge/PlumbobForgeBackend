using System;

namespace s3pi.Interfaces;

[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
public class ElementPriorityAttribute : Attribute
{
	private int priority;

	public int Priority
	{
		get
		{
			return priority;
		}
		set
		{
			priority = value;
		}
	}

	public ElementPriorityAttribute(int priority)
	{
		this.priority = priority;
	}
}
