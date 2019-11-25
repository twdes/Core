#region -- copyright --
//
// Licensed under the EUPL, Version 1.1 or - as soon they will be approved by the
// European Commission - subsequent versions of the EUPL(the "Licence"); You may
// not use this work except in compliance with the Licence.
//
// You may obtain a copy of the Licence at:
// http://ec.europa.eu/idabc/eupl
//
// Unless required by applicable law or agreed to in writing, software distributed
// under the Licence is distributed on an "AS IS" basis, WITHOUT WARRANTIES OR
// CONDITIONS OF ANY KIND, either express or implied. See the Licence for the
// specific language governing permissions and limitations under the Licence.
//
#endregion
using System;
using System.Diagnostics;
using System.Text;

namespace TecWare.DE.Stuff
{
	#region -- enum LogMsgType --------------------------------------------------------

	/// <summary>Art der Log-Nachricht</summary>
	public enum LogMsgType
	{
		/// <summary>Informativ message.</summary>
		Information,
		/// <summary>Warning message.</summary>
		Warning,
		/// <summary>Error message.</summary>
		Error,
		/// <summary>Debug message, will not be posted in the log.</summary>
		Debug
	} // enum LogMsgType

	#endregion

	#region -- interface ILogger ------------------------------------------------------

	/// <summary>Simple interface to the log-file.</summary>
	public interface ILogger
	{
		/// <summary>Appends a line to the log-file.</summary>
		/// <param name="typ">Severity of the logmessage.</param>
		/// <param name="message">Mesage.</param>
		void LogMsg(LogMsgType typ, string message);
	} // interface ILogger

	#endregion

	#region -- interface ILogger2 -----------------------------------------------------

	/// <summary></summary>
	public interface ILogger2
	{
		/// <summary>Creates a new log scope, or returns a existing scope. The scopes will be merged.</summary>
		/// <param name="typ"></param>
		/// <param name="autoFlush"></param>
		/// <returns></returns>
		ILogMessageScope GetScope(LogMsgType typ = LogMsgType.Information, bool autoFlush = true);
		/// <summary>Creates a new log scope.</summary>
		/// <param name="typ"></param>
		/// <param name="autoFlush"></param>
		/// <returns></returns>
		ILogMessageScope CreateScope(LogMsgType typ = LogMsgType.Information, bool autoFlush = true);
	} // interface ILogger2

	#endregion

	#region -- interface ILogMessageScope ---------------------------------------------

	/// <summary></summary>
	public interface ILogMessageScope : IDisposable
	{
		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="force"></param>
		ILogMessageScope SetType(LogMsgType value, bool force = false);

		/// <summary>Writes text to the scope.</summary>
		/// <param name="text"></param>
		/// <returns></returns>
		ILogMessageScope Write(string text);
		/// <summary>Writes a new line to the scope.</summary>
		/// <param name="force"><c>true</c>, writes also a new, when the cursor is on the beginning of a line.</param>
		/// <returns></returns>
		ILogMessageScope WriteLine(bool force = true);

		/// <summary>Indents all follow up lines.</summary>
		/// <param name="indentation"></param>
		/// <returns></returns>
		IDisposable Indent(string indentation = "  ");

		/// <summary>Activates the automatisch flush on dispose.</summary>
		/// <param name="autoFlush"></param>
		/// <returns></returns>
		ILogMessageScope AutoFlush(bool autoFlush = true);

		/// <summary></summary>
		/// <remarks>Upgrades the scope type to an higher level (info->warn->error)</remarks>
		LogMsgType Typ { get; }
	} // interface ILogMessageScope

	#endregion

	#region -- class LogMessageScopeProxy ---------------------------------------------

	/// <summary></summary>
	public sealed class LogMessageScopeProxy : IDisposable
	{
		private ILogMessageScope scope;
		private Stopwatch stopWatch;

		#region -- Ctor/Dtor ----------------------------------------------------------

		internal LogMessageScopeProxy(ILogMessageScope scope, bool stopTime = false)
		{
			this.scope = scope;
			this.stopWatch = stopTime ? Stopwatch.StartNew() : null;
		} // ctor

		/// <summary></summary>
		public void Dispose()
			=> Dispose(true);
		
		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (scope != null && stopWatch != null)
				{
					this.NewLine()
						.WriteLine("=== Duration = {0:N0}ms, {1:N0}ticks ===", stopWatch.ElapsedMilliseconds, stopWatch.ElapsedTicks);
				}
				scope?.Dispose();
			}
		} // proc Dispose

		#endregion

		#region -- Write --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="autoFlush"></param>
		/// <returns></returns>
		public LogMessageScopeProxy AutoFlush(bool autoFlush = true)
		{
			scope?.AutoFlush(autoFlush);
			return this;
		} // func AutoFlush

		/// <summary></summary>
		/// <returns></returns>
		public LogMessageScopeProxy NewLine()
		{
			scope?.WriteLine(false);
			return this;
		} // func NewLine

		/// <summary></summary>
		/// <returns></returns>
		public LogMessageScopeProxy WriteLine()
		{
			scope?.WriteLine(true);
			return this;
		} // func WriteLine

		/// <summary></summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public LogMessageScopeProxy Write(string text)
		{
			scope?.Write(text);
			return this;
		} // proc Write

		/// <summary></summary>
		/// <returns></returns>
		public LogMessageScopeProxy WriteStopWatch()
		{
			if (stopWatch != null)
				Write("[{0,7:N0}ms] ", stopWatch.ElapsedMilliseconds);
			return this;
		} // proc WriteStopWatch 

		/// <summary></summary>
		/// <param name="typ"></param>
		/// <param name="force"></param>
		/// <returns></returns>
		public LogMessageScopeProxy SetType(LogMsgType typ, bool force = false)
		{
			if (scope != null)
			{
				scope.SetType(typ, force);
				if (typ != LogMsgType.Information)
					scope.AutoFlush();
			}
			return this;
		} // proc SetType

		/// <summary></summary>
		/// <param name="text"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public LogMessageScopeProxy Write(string text, params object[] args)
			=> Write(String.Format(text, args));

		/// <summary></summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public LogMessageScopeProxy WriteLine(string text)
			=> Write(text).WriteLine();

		/// <summary></summary>
		/// <param name="text"></param>
		/// <param name="args"></param>
		/// <returns></returns>
		public LogMessageScopeProxy WriteLine(string text, params object[] args) 
			=> Write(text, args).WriteLine();

		/// <summary></summary>
		/// <param name="e"></param>
		/// <returns></returns>
		public LogMessageScopeProxy WriteWarning(Exception e) 
			=> NewLine().SetType(LogMsgType.Warning).WriteLine(e.GetMessageString());

		/// <summary></summary>
		/// <param name="e"></param>
		/// <returns></returns>
		public LogMessageScopeProxy WriteException(Exception e) 
			=> NewLine().SetType(LogMsgType.Error).WriteLine(e.GetMessageString());

		/// <summary></summary>
		/// <returns></returns>
		public IDisposable Indent() => scope?.Indent();

		#endregion

		/// <summary></summary>
		public LogMsgType Typ => scope?.Typ ?? LogMsgType.Information;

		/// <summary></summary>
		public static LogMessageScopeProxy Empty { get; } = new LogMessageScopeProxy(null);
	} // class LogMessageScopeProxy

	#endregion

	#region -- class LogMessageScope --------------------------------------------------

	/// <summary></summary>
	public sealed class LogMessageScope : ILogMessageScope
	{
		#region -- class IndentationScope ---------------------------------------------

		private class IndentationScope : IDisposable
		{
			private LogMessageScope owner;
			private IndentationScope parent;

			public IndentationScope(LogMessageScope owner, string indentation)
			{
				this.owner = owner;
				this.parent = owner.currentIndentation;
				this.Indentation = parent == null ? indentation : parent.Indentation + indentation;
			} // ctor

			public void Dispose()
			{
				if (owner.currentIndentation != this)
					throw new InvalidOperationException("Invalid indentation stack.");

				this.owner.currentIndentation = parent;
			} // proc Dispose

			public string Indentation { get; }
		} // class IndentationScope

		#endregion

		private readonly ILogger log;

		private LogMsgType typ;
		private bool autoFlush;
		private bool startIndent = false;
		private IndentationScope currentIndentation = null;
		private readonly StringBuilder sb = new StringBuilder();

		/// <summary></summary>
		/// <param name="log"></param>
		/// <param name="typ"></param>
		/// <param name="autoFlush"></param>
		public LogMessageScope(ILogger log, LogMsgType typ, bool autoFlush)
		{
			this.log = log;
			this.typ = typ;
			this.autoFlush = autoFlush;
		} // ctor

		// no dtor, only flush log in the calling thread
		/// <summary></summary>
		public void Dispose()
		{
			if (autoFlush)
				Flush();
		} // proc Dispose

		/// <summary></summary>
		public void Flush()
		{
			log?.LogMsg(typ, sb.ToString());
			sb.Length = 0;
		} // proc Flush

		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="force"></param>
		/// <returns></returns>
		public ILogMessageScope SetType(LogMsgType value, bool force = false)
		{
			if (force || typ < value)
				typ = value;
			return this;
		} // proc SetType

		/// <summary></summary>
		/// <param name="autoFlush"></param>
		/// <returns></returns>
		public ILogMessageScope AutoFlush(bool autoFlush = true)
		{
			this.autoFlush = autoFlush;
			return this;
		} // func AutoFlush

		/// <summary></summary>
		/// <param name="indentation"></param>
		/// <returns></returns>
		public IDisposable Indent(string indentation = "  ")
			=> currentIndentation = new IndentationScope(this, indentation);

		private void AppendLine(string text, int startAt, int endAt)
		{
			if (startIndent)
			{
				if (currentIndentation != null)
					sb.Append(currentIndentation.Indentation);
				startIndent = false;
			}
			sb.Append(text, startAt, endAt);
		} // proc AppendLine

		/// <summary></summary>
		/// <param name="text"></param>
		/// <returns></returns>
		public ILogMessageScope Write(string text)
		{
			if (currentIndentation != null)
			{
				var n2 = Environment.NewLine.Length;
				var pos = 0;
				var idx = text.IndexOf(Environment.NewLine);
				while (idx >= 0)
				{
					idx += n2;
					AppendLine(text, pos, idx - pos);
					startIndent = true;
					pos = idx;
					idx = text.IndexOf(Environment.NewLine, pos);
				}
				if (pos < text.Length)
					AppendLine(text, pos, text.Length - pos);
			}
			else
				sb.Append(text);
			return this;
		} // func Write

		/// <summary></summary>
		/// <param name="force"></param>
		/// <returns></returns>
		public ILogMessageScope WriteLine(bool force = true)
		{
			if (force || !startIndent)
			{
				sb.AppendLine();
				startIndent = true;
			}
			return this;
		} // func WriteLine

		/// <summary></summary>
		public LogMsgType Typ => typ;
	} // class LogMessageScope

	#endregion

	#region -- class LoggerProxy ------------------------------------------------------

	/// <summary>Extensions for the Logger-Interface.</summary>
	public abstract class LoggerProxy : ILogger
	{
		#region -- class LoggerProxySimple --------------------------------------------

		private sealed class LoggerProxySimple : LoggerProxy
		{
			internal LoggerProxySimple(ILogger logger)
				: base(logger)
			{
			} // ctor

			public sealed override void LogMsg(LogMsgType typ, string message) => Logger?.LogMsg(typ, message);
			public sealed override void LogMsg(LogMsgType typ, string message, params object[] args) => Logger?.LogMsg(typ, String.Format(message, args));
		} // class LoggerProxy

		#endregion

		#region -- class LoggerProxyPrefix --------------------------------------------

		private sealed class LoggerProxyPrefix : LoggerProxy
		{
			private readonly string prefix;

			internal LoggerProxyPrefix(ILogger logger, string prefix)
				: base(logger)
			{
				this.prefix = prefix;
			} // ctor

			private string GetMessage(string message)
				=> "[" + prefix + "] " + message;

			public sealed override void LogMsg(LogMsgType typ, string message) => Logger?.LogMsg(typ, GetMessage(message));
			public sealed override void LogMsg(LogMsgType typ, string message, params object[] args) => Logger?.LogMsg(typ, GetMessage(String.Format(message, args)));
		} // class LoggerProxyPrefix

		#endregion

		private readonly ILogger logger;

		/// <summary>Create a logger proxy</summary>
		/// <param name="logger">Optional logger.</param>
		protected LoggerProxy(ILogger logger)
		{
			this.logger = logger;
		} // ctor

		/// <summary>Write information message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		public void Info(string message) 
			=> LogMsg(LogMsgType.Information, message);
		/// <summary>Write information message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		/// <param name="args">Arguments to format.</param>
		public void Info(string message, params object[] args) 
			=> LogMsg(LogMsgType.Information, message, args);

		/// <summary>Write debug message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		public void Debug(string message)
			=> LogMsg(LogMsgType.Debug, message);

		/// <summary>Write debug message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		/// <param name="args">Arguments to format.</param>
		public void Debug(string message, params object[] args)
			=> LogMsg(LogMsgType.Debug, message, args);

		/// <summary>Write warning message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		public void Warn(string message) 
			=> LogMsg(LogMsgType.Warning, message);
		/// <summary>Write warning message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		/// <param name="args">Arguments to format.</param>
		public void Warn(string message, params object[] args) 
			=> LogMsg(LogMsgType.Warning, message, args);
		/// <summary>Write warning message to the log system.</summary>
		/// <param name="e">Exception, that will be written to log.</param>
		public void Warn(Exception e) 
			=> Procs.LogMsg(logger, LogMsgType.Warning, e);
		/// <summary>Write warning message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		/// <param name="e">Exception, that will be added to the message.</param>
		public void Warn(string message, Exception e) 
			=> Procs.LogMsg(logger, LogMsgType.Warning, message, e);

		/// <summary>Write error message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		public void Except(string message) 
			=> LogMsg(LogMsgType.Error, message);
		/// <summary>Write error message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		/// <param name="args">Arguments to format.</param>
		public void Except(string message, params object[] args) 
			=> LogMsg(LogMsgType.Error, message, args);
		/// <summary>Write error message to the log system.</summary>
		/// <param name="e">Exception, that will be written to log.</param>
		public void Except(Exception e) 
			=> Procs.LogMsg(logger, LogMsgType.Error, e);
		/// <summary>Write error message to the log system.</summary>
		/// <param name="message">Message, that will be written to log.</param>
		/// <param name="e">Exception, that will be added to the message.</param>
		public void Except(string message, Exception e) 
			=> Procs.LogMsg(logger, LogMsgType.Error, message, e);

		/// <summary>Write a message to the log system.</summary>
		/// <param name="typ">Type of the message.</param>
		/// <param name="message">Message, that will be written to log.</param>
		public abstract void LogMsg(LogMsgType typ, string message);
		/// <summary>Write a message to the log system.</summary>
		/// <param name="typ">Type of the message.</param>
		/// <param name="message">Message, that will be written to log.</param>
		/// <param name="args">Arguments to format.</param>
		public abstract void LogMsg(LogMsgType typ, string message, params object[] args);

		/// <summary>Get a current log scope or create a new one.</summary>
		/// <param name="typ">Type of the message.</param>
		/// <param name="autoFlush">Should the message flushed to log, on dispose.</param>
		/// <param name="stopTime">Should the scope stop the life time.</param>
		/// <returns>Return a log scope, that collects all log message, and write it in one block.</returns>
		public LogMessageScopeProxy GetScope(LogMsgType typ = LogMsgType.Information, bool autoFlush = true, bool stopTime = false)
		{
			return logger is ILogger2 logger2
				? new LogMessageScopeProxy(logger2.GetScope(typ, autoFlush), stopTime)
				: new LogMessageScopeProxy(new LogMessageScope(this, typ, autoFlush), stopTime);
		} // func GetScope

		/// <summary>Create a new log scope, that collections all messages.</summary>
		/// <param name="typ">Type of the message.</param>
		/// <param name="autoFlush">Should the message flushed to log, on dispose.</param>
		/// <param name="stopTime">Should the scope stop the life time.</param>
		/// <returns>Return a log scope, that collects all log messages, and write it in one block.</returns>
		public LogMessageScopeProxy CreateScope(LogMsgType typ = LogMsgType.Information, bool autoFlush = true, bool stopTime = false)
		{
			return logger is ILogger2 logger2
				? new LogMessageScopeProxy(logger2.CreateScope(typ, autoFlush), stopTime)
				: new LogMessageScopeProxy(new LogMessageScope(this, typ, autoFlush), stopTime);
		} // func CreateScopy

		/// <summary>Has this proxy a log-interface.</summary>
		public bool IsEmpty => logger == null;
		/// <summary>Log interface of this proxy.</summary>
		public ILogger Logger => logger;

		/// <summary>Create a new log-proxy for the logger.</summary>
		/// <param name="log">Log or <c>null</c>.</param>
		/// <returns>Log proxy for the logger.</returns>
		public static LoggerProxy Create(ILogger log)
			=> log == null ? Empty : new LoggerProxySimple(log);

		/// <summary>Create a new log-proxy for the logger.</summary>
		/// <param name="log">Log or <c>null</c>.</param>
		/// <param name="prefix">Prefix for the log messages.</param>
		/// <returns>Log proxy for the logger.</returns>
		public static LoggerProxy Create(ILogger log, string prefix)
		{
			if (String.IsNullOrEmpty(prefix))
				throw new ArgumentNullException("prefix");
			return log == null ? Empty : new LoggerProxyPrefix(log, prefix);
		} // func Create

		/// <summary>Instance of an empty log-proxy.</summary>
		public static LoggerProxy Empty { get; } = new LoggerProxySimple(null);
	} // class LoggerProxy

	#endregion

	#region -- class Procs ------------------------------------------------------------

	/// <summary></summary>
	public static partial class Procs
	{
		/// <summary></summary>
		/// <param name="logger"></param>
		/// <param name="typ"></param>
		/// <param name="message"></param>
		/// <param name="args"></param>
		public static void LogMsg(this ILogger logger, LogMsgType typ, string message, params object[] args) 
			=> logger?.LogMsg(typ, String.Format(message, args));
		/// <summary></summary>
		/// <param name="logger"></param>
		/// <param name="typ"></param>
		/// <param name="message"></param>
		/// <param name="e"></param>
		public static void LogMsg(this ILogger logger, LogMsgType typ, string message, Exception e) 
			=> logger?.LogMsg(typ, message + Environment.NewLine + Environment.NewLine + e.GetMessageString());
		/// <summary></summary>
		/// <param name="logger"></param>
		/// <param name="typ"></param>
		/// <param name="e"></param>
		public static void LogMsg(this ILogger logger, LogMsgType typ, Exception e) 
			=> LogMsg(logger, typ, e.Message, e);

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <returns></returns>
		public static LoggerProxy LogProxy(this IServiceProvider sp)
			=> LoggerProxy.Create(sp?.GetService<ILogger>(false));

		/// <summary></summary>
		/// <param name="sp"></param>
		/// <param name="prefix"></param>
		/// <returns></returns>
		public static LoggerProxy LogProxy(this IServiceProvider sp, string prefix) 
			=> LoggerProxy.Create(sp?.GetService<ILogger>(false), prefix);
	} // class Procs

	#endregion
}
