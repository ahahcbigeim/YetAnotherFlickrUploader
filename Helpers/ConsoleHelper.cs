using System;
using System.Globalization;

namespace YetAnotherFlickrUploader.Helpers
{
	public static class ConsoleHelper
	{
		#region Console.Write variants

		public static void WriteDebug(string message)
		{
			Write(ConsoleColor.Gray, message);
		}

		public static void WriteDebug(string format, params object[] args)
		{
			Write(ConsoleColor.Gray, format, args);
		}

		public static void WriteInfo(string message)
		{
			Write(ConsoleColor.White, message);
		}

		public static void WriteInfo(string format, params object[] args)
		{
			Write(ConsoleColor.White, format, args);
		}

		public static void WriteWarning(string message)
		{
			Write(ConsoleColor.DarkYellow, message);
		}

		public static void WriteWarning(string format, params object[] args)
		{
			Write(ConsoleColor.DarkYellow, format, args);
		}

		public static void WriteError(string message)
		{
			Write(ConsoleColor.Red, message);
		}

		public static void WriteError(string format, params object[] args)
		{
			Write(ConsoleColor.Red, format, args);
		}

		#endregion

		#region Console.WriteLine variants

		public static void WriteDebugLine(string message)
		{
			WriteLine(ConsoleColor.Gray, message);
		}

		public static void WriteDebugLine(string format, params object[] args)
		{
			WriteLine(ConsoleColor.Gray, format, args);
		}

		public static void WriteInfoLine(string message)
		{
			WriteLine(ConsoleColor.White, message);
		}

		public static void WriteInfoLine(string format, params object[] args)
		{
			WriteLine(ConsoleColor.White, format, args);
		}

		public static void WriteWarningLine(string message)
		{
			WriteLine(ConsoleColor.DarkYellow, message);
		}

		public static void WriteWarningLine(string format, params object[] args)
		{
			WriteLine(ConsoleColor.DarkYellow, format, args);
		}

		public static void WriteErrorLine(string message)
		{
			WriteLine(ConsoleColor.Red, message);
		}

		public static void WriteErrorLine(string format, params object[] args)
		{
			WriteLine(ConsoleColor.Red, format, args);
		}

		#endregion

		public static void WriteException(Exception e)
		{
			var fc = Console.ForegroundColor;
			Console.ForegroundColor = ConsoleColor.Red;
			Console.WriteLine("Exception details:");
			Console.WriteLine(e.Message);
			Console.WriteLine(e.Source);
			if (e.InnerException != null)
			{
				Console.WriteLine("Inner exception:");
				Console.WriteLine(e.InnerException.Message);
				Console.WriteLine(e.InnerException.Source);
				Console.WriteLine("Stack:");
				Console.WriteLine(e.InnerException.StackTrace);
			}
			else
			{
				Console.WriteLine("Stack:");
				Console.WriteLine(e.StackTrace);
			}
			Console.ForegroundColor = fc;
		}

		public static bool ConfirmYesNo(string question)
		{
			WriteInfoLine(question);
			WriteInfo("Yes/No: ");
			var answer = Console.ReadKey();
			Console.WriteLine();
			return ("y" == answer.KeyChar.ToString(CultureInfo.InvariantCulture).ToLower());
		}

		#region Private methods

		private static void Write(ConsoleColor color, string message)
		{
			var fc = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.Write(message);
			Console.ForegroundColor = fc;
		}

		private static void Write(ConsoleColor color, string format, params object[] args)
		{
			var fc = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.Write(format, args);
			Console.ForegroundColor = fc;
		}

		private static void WriteLine(ConsoleColor color, string message)
		{
			var fc = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(message);
			Console.ForegroundColor = fc;
		}

		private static void WriteLine(ConsoleColor color, string format, params object[] args)
		{
			var fc = Console.ForegroundColor;
			Console.ForegroundColor = color;
			Console.WriteLine(format, args);
			Console.ForegroundColor = fc;
		}
 
		#endregion
	}
}
