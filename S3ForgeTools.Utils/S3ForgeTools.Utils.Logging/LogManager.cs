using System;
using System.Collections.Generic;
using System.IO;

namespace S3ForgeTools.Utils.Logging;

public sealed class LogManager
{
	public enum LogLevel
	{
		Off,
		Fatal,
		Error,
		Warn,
		Info,
		Debug
	}

	private static readonly LogManager instance = new LogManager();

	private LogLevel _Level;

	private bool _Enabled;

	private string _LogFilename;

	private StreamWriter _LogWriter;

	private Dictionary<string, ILog> LogModules;

	private List<string> _Buffer;

	public LogManager Singleton => instance;

	public static LogLevel Level
	{
		get
		{
			return instance._Level;
		}
		set
		{
			instance._Level = value;
		}
	}

	public static bool IsEnabled
	{
		get
		{
			return instance._Enabled;
		}
		set
		{
			Set_Enabled(value);
		}
	}

	public static string LogFilename => instance._LogFilename;

	private LogManager()
	{
		_LogFilename = "";
		_Buffer = new List<string>();
		LogModules = new Dictionary<string, ILog>();
	}

	public static void SetLevel(LogLevel level)
	{
		instance._Level = level;
	}

	public static void Enable()
	{
		IsEnabled = true;
	}

	public static void Disable()
	{
		IsEnabled = false;
	}

	public static ILog GetLogger(string ModuleName)
	{
		return instance.GetLogModule(ModuleName);
	}

	private ILog GetLogModule(string ModuleName)
	{
		if (LogModules.ContainsKey(ModuleName))
		{
			return LogModules[ModuleName];
		}
		ILog log = new Log(this, ModuleName);
		LogModules.Add(ModuleName, log);
		return log;
	}

	private static void Set_Enabled(bool value)
	{
		instance.SetEnabled(value);
	}

	public static void SetFilename(string Filename)
	{
		instance.Set_Filename(Filename);
	}

	private string NumberedLogFile(string Filename, int Number)
	{
		return Path.ChangeExtension(Filename, $".{Number}.log");
	}

	private void RotateLogFiles(string Filename)
	{
		if (File.Exists(NumberedLogFile(Filename, 5)))
		{
			File.Delete(NumberedLogFile(Filename, 5));
		}
		for (int num = 4; num > 0; num--)
		{
			if (File.Exists(NumberedLogFile(Filename, num)))
			{
				File.Move(NumberedLogFile(Filename, num), NumberedLogFile(Filename, num + 1));
			}
		}
		if (File.Exists(Filename))
		{
			File.Move(Filename, NumberedLogFile(Filename, 1));
		}
	}

	private void Set_Filename(string Filename)
	{
		if (instance._LogFilename != "")
		{
			throw new ArgumentException("Cannot set Filename twice");
		}
		_LogFilename = Filename;
		Directory.CreateDirectory(Path.GetDirectoryName(_LogFilename));
		RotateLogFiles(_LogFilename);
		_LogWriter = File.CreateText(_LogFilename);
		foreach (string item in _Buffer)
		{
			_LogWriter.WriteLine(item);
		}
		_Buffer.Clear();
		_LogWriter.Flush();
	}

	private void SetEnabled(bool value)
	{
		if (value)
		{
			if (!_Enabled)
			{
				_Enabled = true;
			}
		}
		else if (_Enabled && _LogFilename != "")
		{
			_LogWriter.Close();
			_Enabled = false;
			_LogFilename = "";
		}
	}

	internal void PostEntry(LogLevel Level, string LogModule, string Message)
	{
		if (_Enabled && Level <= _Level)
		{
			if (_LogFilename != "" && _LogWriter.BaseStream.CanWrite)
			{
				_LogWriter.WriteLine(string.Format("{0}| {1} | [{2}] -- {3}", DateTime.Now.ToString("u"), Level.ToString(), LogModule, Message));
				_LogWriter.Flush();
			}
			else
			{
				_Buffer.Add(string.Format("{0}| {1} | [{2}] -- {3}", DateTime.Now.ToString("u"), Level.ToString(), LogModule, Message));
			}
		}
	}
}
