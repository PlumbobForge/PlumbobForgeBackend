namespace S3ForgeTools.Utils.Logging;

public interface ILog
{
	void Debug(string Message);

	void Info(string Message);

	void Warn(string Message);

	void Error(string Message);

	void Fatal(string Message);
}
