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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	#region -- enum LogMsgType ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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

	#region -- interface ILogger --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Simple interface to the log-file.</summary>
	public interface ILogger
	{
		/// <summary>Appends a line to the log-file.</summary>
		/// <param name="typ">Severity of the logmessage.</param>
		/// <param name="message">Mesage.</param>
		void LogMsg(LogMsgType typ, string message);
	} // interface ILogger

	#endregion

	#region -- interface ILogger2 -------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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

	#region -- interface ILogMessageScope -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
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
		/// <returns></returns>
		ILogMessageScope AutoFlush();

		/// <summary></summary>
		/// <remarks>Upgrades the scope type to an higher level (info->warn->error)</remarks>
		LogMsgType Typ { get; }
	} // interface ILogMessageScope

	#endregion

	#region -- class LogMessageScopeProxy ------------------------------------------------

	public sealed class LogMessageScopeProxy : IDisposable
	{
		private ILogMessageScope scope;
		private Stopwatch stopWatch;

		#region -- Ctor/Dtor --------------------------------------------------------------

		internal LogMessageScopeProxy(ILogMessageScope scope, bool stopTime = false)
		{
			this.scope = scope;
			this.stopWatch = stopTime ? Stopwatch.StartNew() : null;
		} // ctor

		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		private void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (scope != null && stopWatch != null)
				{
					this.NewLine()
						.WriteLine("=== Dauer = {0:N0}ms, {1:N0}ticks ===", stopWatch.ElapsedMilliseconds, stopWatch.ElapsedTicks);
				}
				scope?.Dispose();
			}
		} // proc Dispose

		#endregion

		#region -- Write ------------------------------------------------------------------

		public LogMessageScopeProxy AutoFlush()
		{
			scope?.AutoFlush();
			return this;
		} // func AutoFlush

		public LogMessageScopeProxy NewLine()
		{
			scope?.WriteLine(false);
			return this;
		} // func NewLine

		public LogMessageScopeProxy WriteLine()
		{
			scope?.WriteLine(true);
			return this;
		} // func WriteLine

		public LogMessageScopeProxy Write(string text)
		{
			scope?.Write(text);
			return this;
		} // proc Write

		public LogMessageScopeProxy WriteStopWatch()
		{
			if (stopWatch != null)
				Write("[{0,7:N0}ms] ", stopWatch.ElapsedMilliseconds);
			return this;
		} // proc WriteStopWatch 

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

		public LogMessageScopeProxy Write(string text, params object[] args) => Write(String.Format(text, args));

		public LogMessageScopeProxy WriteLine(string text) => Write(text).WriteLine();
		public LogMessageScopeProxy WriteLine(string text, params object[] args) => Write(text, args).WriteLine();

		public LogMessageScopeProxy WriteWarning(Exception e) => NewLine().SetType(LogMsgType.Warning).WriteLine(e.GetMessageString());
		public LogMessageScopeProxy WriteException(Exception e) => NewLine().SetType(LogMsgType.Error).WriteLine(e.GetMessageString());

		public IDisposable Indent() => scope?.Indent();

		#endregion

		public LogMsgType Typ => scope?.Typ ?? LogMsgType.Information;

		public static LogMessageScopeProxy Empty { get; } = new LogMessageScopeProxy(null);
	} // class LogMessageScopeProxy

	#endregion

	#region -- class LogMessageScope ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class LogMessageScope : ILogMessageScope
	{
		#region -- class IndentationScope -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
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

		public LogMessageScope(ILogger log, LogMsgType typ, bool autoFlush)
		{
			this.log = log;
			this.typ = typ;
			this.autoFlush = autoFlush;
		} // ctor

		// no dtor, only flush log in the calling thread
		public void Dispose()
		{
			if (autoFlush)
				Flush();
		} // proc Dispose

		public void Flush()
		{
			log?.LogMsg(typ, sb.ToString());
			sb.Length = 0;
		} // proc Flush

		public ILogMessageScope SetType(LogMsgType value, bool force = false)
		{
			if (force || typ < value)
				typ = value;
			return this;
		} // proc SetType

		public ILogMessageScope AutoFlush()
		{
			autoFlush = true;
			return this;
		} // func AutoFlush

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

		public ILogMessageScope WriteLine(bool force = true)
		{
			if (force || !startIndent)
			{
				sb.AppendLine();
				startIndent = true;
			}
			return this;
		} // func WriteLine

		public LogMsgType Typ => typ;
	} // class LogMessageScope

	#endregion

	#region -- class LoggerProxy --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Extensions for the Logger-Interface.</summary>
	public abstract class LoggerProxy : ILogger
	{
		#region -- class LoggerProxySimple ------------------------------------------------

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

		#region -- class LoggerProxyPrefix ------------------------------------------------

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

		private ILogger logger;

		protected LoggerProxy(ILogger logger)
		{
			this.logger = logger;
		} // ctor

		public void Info(string message) => LogMsg(LogMsgType.Information, message);
		public void Info(string message, params object[] args) => LogMsg(LogMsgType.Information, message, args);

		public void Warn(string message) => LogMsg(LogMsgType.Warning, message);
		public void Warn(string message, params object[] args) => LogMsg(LogMsgType.Warning, message, args);
		public void Warn(Exception e) => Procs.LogMsg(logger, LogMsgType.Warning, e);
		public void Warn(string message, Exception e) => Procs.LogMsg(logger, LogMsgType.Warning, message, e);

		public void Except(string message) => LogMsg(LogMsgType.Error, message);
		public void Except(string message, params object[] args) => LogMsg(LogMsgType.Error, message, args);
		public void Except(Exception e) => Procs.LogMsg(logger, LogMsgType.Error, e);
		public void Except(string message, Exception e) => Procs.LogMsg(logger, LogMsgType.Error, message, e);

		public abstract void LogMsg(LogMsgType typ, string message);
		public abstract void LogMsg(LogMsgType typ, string message, params object[] args);

		public LogMessageScopeProxy GetScope(LogMsgType typ = LogMsgType.Information, bool autoFlush = true, bool stopTime = false)
		{
			var logger2 = logger as ILogger2;
			if (logger2 != null)
				return new LogMessageScopeProxy(logger2.GetScope(typ, autoFlush), stopTime);

			return new LogMessageScopeProxy(new LogMessageScope(this, typ, autoFlush), stopTime);
		} // func GetScope

		public LogMessageScopeProxy CreateScope(LogMsgType typ = LogMsgType.Information, bool autoFlush = true, bool stopTime = false)
		{
			var logger2 = logger as ILogger2;
			if (logger2 != null)
				return new LogMessageScopeProxy(logger2.CreateScope(typ, autoFlush), stopTime);

			return new LogMessageScopeProxy(new LogMessageScope(this, typ, autoFlush), stopTime);
		} // func CreateScopy

		public bool IsEmpty => logger == null;
		public ILogger Logger => logger;

		public static LoggerProxy Create(ILogger log)
			=> log == null ? Empty : new LoggerProxySimple(log);

		public static LoggerProxy Create(ILogger log, string prefix)
		{
			if (String.IsNullOrEmpty(prefix))
				throw new ArgumentNullException("prefix");
			return log == null ? Empty : new LoggerProxyPrefix(log, prefix);
		} // func Create

		public static LoggerProxy Empty { get; } = new LoggerProxySimple(null);
	} // class LoggerProxy

	#endregion

	#region -- class Procs --------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static partial class Procs
	{
		public static void LogMsg(this ILogger logger, LogMsgType typ, string message, params object[] args) => logger?.LogMsg(typ, String.Format(message, args));
		public static void LogMsg(this ILogger logger, LogMsgType typ, string message, Exception e) => logger?.LogMsg(typ, message + Environment.NewLine + Environment.NewLine + e.GetMessageString());
		public static void LogMsg(this ILogger logger, LogMsgType typ, Exception e) => LogMsg(logger, typ, e.Message, e);
		public static LoggerProxy LogProxy(this IServiceProvider sp) => LoggerProxy.Create(sp?.GetService<ILogger>(false));

	} // class Procs

	#endregion
}
