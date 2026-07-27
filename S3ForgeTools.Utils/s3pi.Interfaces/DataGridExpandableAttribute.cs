using System;

namespace s3pi.Interfaces;

[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
public class DataGridExpandableAttribute : Attribute
{
	private bool dataGridExpandable;

	public bool DataGridExpandable
	{
		get
		{
			return dataGridExpandable;
		}
		set
		{
			dataGridExpandable = value;
		}
	}

	public DataGridExpandableAttribute()
	{
		dataGridExpandable = true;
	}

	public DataGridExpandableAttribute(bool value)
	{
		dataGridExpandable = value;
	}
}
