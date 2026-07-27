namespace S3ForgeTools.Utils.Logging;

public class Log : ILog
{
	public LogManager logManager { get; private set; }

	public string ModuleName { get; private set; }

	public Log(LogManager logManager, string ModuleName)
	{
		this.logManager = logManager;
		this.ModuleName = ModuleName;
	}

	public void Debug(string Message)
	{
		PostLog(LogManager.LogLevel.Debug, Message);
	}

	public void Info(string Message)
	{
		PostLog(LogManager.LogLevel.Info, Message);
	}

	public void Warn(string Message)
	{
		PostLog(LogManager.LogLevel.Warn, Message);
	}

	public void Error(string Message)
	{
		PostLog(LogManager.LogLevel.Error, Message);
	}

	public void Fatal(string Message)
	{
		PostLog(LogManager.LogLevel.Fatal, Message);
	}

	private void PostLog(LogManager.LogLevel Level, string Message)
	{
		logManager.PostEntry(Level, ModuleName, Message);
	}
}
