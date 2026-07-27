using System;

namespace s3pi.Interfaces;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ConstructorParametersAttribute : Attribute
{
	public readonly object[] parameters;

	public ConstructorParametersAttribute(object[] parameters)
	{
		this.parameters = parameters;
	}
}
