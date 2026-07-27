namespace System;

public class ArgumentLengthException : Exception
{
	public ArgumentLengthException()
	{
	}

	public ArgumentLengthException(string message)
		: base(message)
	{
	}

	public ArgumentLengthException(string argument, int length)
		: base($"{argument} length must be {length}.")
	{
	}

	public ArgumentLengthException(string format, params object[] formatparams)
		: base(string.Format(format, formatparams))
	{
	}
}
