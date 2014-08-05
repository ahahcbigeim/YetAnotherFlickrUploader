using System;
using log4net;

namespace YetAnotherFlickrUploader.Helpers
{
	public static class Logger
	{
		private static ILog log = LogManager.GetLogger("app");

		public static void Debug(string message)
		{
			ConsoleHelper.WriteDebugLine(message);
			log.Debug(message);
		}

		public static void Debug(string format, params object[] args)
		{
			ConsoleHelper.WriteDebugLine(format, args);
			log.DebugFormat(format, args);
		}

		public static void Info(string message)
		{
			ConsoleHelper.WriteInfoLine(message);
			log.Info(message);
		}

		public static void Info(string format, params object[] args)
		{
			ConsoleHelper.WriteInfoLine(format, args);
			log.InfoFormat(format, args);
		}

		public static void Warning(string message)
		{
			ConsoleHelper.WriteWarningLine(message);
			log.Warn(message);
		}

		public static void Warning(string format, params object[] args)
		{
			ConsoleHelper.WriteWarningLine(format, args);
			log.WarnFormat(format, args);
		}

		public static void Error(string message)
		{
			ConsoleHelper.WriteErrorLine(message);
			log.Error(message);
		}

		public static void Error(string format, params object[] args)
		{
			ConsoleHelper.WriteErrorLine(format, args);
			log.ErrorFormat(format, args);
		}

		public static void Error(string message, Exception e)
		{
			ConsoleHelper.WriteErrorLine(message);
			ConsoleHelper.WriteException(e);
			log.Error(message, e);
		}
	}
}
