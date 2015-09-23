using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DES.Stuff
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
		Error
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
	} // interface ILogger2

	#endregion

	#region -- interface ILogMessageScope -----------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface ILogMessageScope : IDisposable
	{
		/// <summary>Writes text to the scope.</summary>
		/// <param name="text"></param>
		/// <returns></returns>
		ILogMessageScope Write(string text);
		/// <summary>Writes a new line to the scope.</summary>
		/// <param name="force"><c>true</c>, writes also a new, when the cursor is on the beginning of a line.</param>
		/// <returns></returns>
		ILogMessageScope WriteLine(bool force = true);

		/// <summary>Indents all follow up lines.</summary>
		/// <returns></returns>
		IDisposable Indent();

		/// <summary>Activates the automatisch flush on dispose.</summary>
		/// <returns></returns>
		ILogMessageScope AutoFlush();

		/// <summary></summary>
		/// <remarks>Upgrades the scope type to an higher level (info->warn->error)</remarks>
		LogMsgType Typ { get; set; }
  } // interface ILogMessageScope

	#endregion

	#region -- class LogMessageScopeProxy ------------------------------------------------

	public sealed class LogMessageScopeProxy : IDisposable
	{
		private ILogMessageScope scope;

		#region -- Ctor/Dtor --------------------------------------------------------------

		internal LogMessageScopeProxy(ILogMessageScope scope)
		{
			this.scope = scope;
		} // ctor

		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		private void Dispose(bool disposing)
		{
			if (disposing)
				scope?.Dispose();
		} // proc Dispose

		#endregion

		#region -- Write ------------------------------------------------------------------

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

		public LogMessageScopeProxy Write((string text)
		{
			scope?.Write(text);
			return this;
		} // proc Write

		public LogMessageScopeProxy SetType(LogMsgType typ)
		{
			if (scope != null)
			{
				scope.Typ = typ;
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

	#region -- class LoggerProxy --------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary>Extensions for the Logger-Interface.</summary>
	public sealed class LoggerProxy
	{
		private ILogger logger;

		internal LoggerProxy(ILogger logger)
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

		public void LogMsg(LogMsgType typ, string message) => logger?.LogMsg(typ, message);
		public void LogMsg(LogMsgType typ, string message, params object[] args) => logger?.LogMsg(typ, String.Format(message, args));

		public LogMessageScopeProxy GetScope(LogMsgType typ = LogMsgType.Information, bool autoFlush = true)
		{
			var logger2 = logger as ILogger2;
			if (logger2 != null)
				return new LogMessageScopeProxy(logger2.GetScope(typ, autoFlush));
			return LogMessageScopeProxy.Empty;
		} // func GetScope

		public static LoggerProxy Empty { get; } = new LoggerProxy(null);
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
		public static LoggerProxy LogProxy(this ILogger logger) => logger == null ? LoggerProxy.Empty : new LoggerProxy(logger);
		public static LoggerProxy LogProxy(this IServiceProvider sp) => LogProxy(sp?.GetService<ILogger>(false));
  } // class Procs

	#endregion
}
