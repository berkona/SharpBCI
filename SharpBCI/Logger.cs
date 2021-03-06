﻿using System;
using System.IO;
using System.Collections.Generic;

namespace SharpBCI {

	/**
	 * Internal enum to indicate log level
	 * INFO = normal running information
	 * WARNING = possible error, but not fatal
	 * ERROR = fatal error occurred
	 */
	public enum LogLevel {
		INFO,
		WARNING,
		ERROR,
	};

	public interface ILogOutput : IDisposable {
		void Log(LogLevel level, object message);
	}

	public class ConsoleLogger : ILogOutput {

		public void Dispose() { }
		public void Log(LogLevel level, object message) {
			Console.WriteLine(string.Format("{0} - [{1}]: {2}", DateTime.Now, level, message));
		}
	}

	public class FileLogger : ILogOutput {

		readonly AsyncStreamWriter outputStream;

		public FileLogger(string logName) {
			outputStream = new AsyncStreamWriter(logName, true);
			outputStream.WriteLine("\n\n\n");
		}

		public void Dispose() {
			outputStream.Close();
		}

		public void Log(LogLevel level, object message) {
			outputStream.WriteLine(string.Format("{0} - [{1}]: {2}", DateTime.Now, level, message));
		}
	}

	public static class Logger {
		
		readonly static List<ILogOutput> outputs = new List<ILogOutput>();

		public static void AddLogOutput(ILogOutput logOutput) {
			outputs.Add(logOutput);
		}

		public static void Dispose() {
			foreach (var o in outputs) {
				o.Dispose();
			}
		}

		public static void Log(object message, params object[] arguments) {
			_Log(LogLevel.INFO, message, arguments);
		}

		public static void Warning(object message, params object[] arguments) {
			_Log(LogLevel.WARNING, message, arguments);
		}

		public static void Error(object message, params object[] arguments) {
			_Log(LogLevel.ERROR, message, arguments);
		}

		static void _Log(LogLevel level, object message, object[] arguments) {
			if (message is string) message = string.Format(((string) message), arguments);
			foreach (var o in outputs) {
				o.Log(level, message);
			}
		}
	}
}
