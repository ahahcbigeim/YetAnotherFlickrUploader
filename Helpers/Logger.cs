using System;
using log4net;

namespace YetAnotherFlickrUploader.Helpers
{
	public static class Logger
	{
		private static readonly ILog log = LogManager.GetLogger("app");

		public static void Debug(string message)
		{
			ConsoleHelper.WriteDebugLine(message);
			log.Debug(FormatMessage(message));
		}

		public static void Debug(string format, params object[] args)
		{
			ConsoleHelper.WriteDebugLine(format, args);
			log.Debug(FormatMessage(format, args));
		}

		public static void Info(string message)
		{
			ConsoleHelper.WriteInfoLine(message);
			log.Info(FormatMessage(message));
		}

		public static void Info(string format, params object[] args)
		{
			ConsoleHelper.WriteInfoLine(format, args);
			log.Info(FormatMessage(format, args));
		}

		public static void Warning(string message)
		{
			ConsoleHelper.WriteWarningLine(message);
			log.Warn(FormatMessage(message));
		}

		public static void Warning(string format, params object[] args)
		{
			ConsoleHelper.WriteWarningLine(format, args);
			log.Warn(FormatMessage(format, args));
		}

		public static void Error(string message)
		{
			ConsoleHelper.WriteErrorLine(message);
			log.Error(FormatMessage(message));
		}

		public static void Error(string format, params object[] args)
		{
			ConsoleHelper.WriteErrorLine(format, args);
			log.Error(FormatMessage(format, args));
		}

		public static void Error(string message, Exception e)
		{
			ConsoleHelper.WriteErrorLine(message);
			ConsoleHelper.WriteException(e);
			log.Error(FormatMessage(message), e);
		}

		private static string FormatMessage(string format, params object[] args)
		{
			string result = args != null && args.Length > 0 ? string.Format(format, args) : format;
			return result.TrimEnd(' ');
		}
	}
}
