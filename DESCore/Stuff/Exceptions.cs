﻿#region -- copyright --
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
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Neo.IronLua;
using System.Collections;

namespace TecWare.DE.Stuff
{
	#region -- class Procs ------------------------------------------------------------

	public static partial class Procs
	{
		/// <summary>Get a nice formatted exception.</summary>
		/// <param name="e"></param>
		/// <returns></returns>
		public static string GetMessageString(this Exception e)
			=> ExceptionFormatter.FormatPlainText(e);

		/// <summary>Gibt die Nachricht via <c>Debug.Print</c> aus.</summary>
		/// <param name="e"></param>
		[Conditional("DEBUG")]
		public static void DebugOut(this Exception e)
			=> Debug.WriteLine("Exception:\n{0}", e.GetMessageString());

		/// <summary>.</summary>
		/// <param name="condition"></param>
		/// <param name="message"></param>
		/// <param name="filePath"></param>
		/// <param name="lineNumber"></param>
		/// <param name="caller"></param>
		[Conditional("DEBUG")]
		public static void ThrowIf(bool condition, string message = null
#if DEBUG
			, [CallerFilePath] string filePath = null
			, [CallerLineNumber] int lineNumber = 0
			, [CallerMemberName] string caller = null
#endif
			)
		{
#if DEBUG
			Debug.WriteLineIf(condition, $"Assert: {message ?? "no description"}, {filePath}:{lineNumber:N0} from {caller}");
#endif
			if (condition)
				throw new ArgumentException(message);
		} // proc ThrowIf

		/// <summary></summary>
		/// <param name="condition"></param>
		/// <param name="message"></param>
		/// <param name="filePath"></param>
		/// <param name="lineNumber"></param>
		/// <param name="caller"></param>
		[Conditional("DEBUG")]
		public static void ThrowIfNot(bool condition, string message = null
#if DEBUG
			, [CallerFilePath] string filePath = null
			, [CallerLineNumber] int lineNumber = 0
			, [CallerMemberName] string caller = null
#endif
			)
			=> ThrowIf(!condition, message
#if DEBUG
				, filePath, lineNumber, caller
#endif
				);

		/// <summary></summary>
		/// <param name="e"></param>
		/// <returns></returns>
		public static Exception GetInnerException(this Exception e)
		{
			if (e is TargetInvocationException te)
				return te.InnerException.GetInnerException();
			else if (e is AggregateException ae && ae.InnerExceptions.Count == 1)
				return ae.InnerException.GetInnerException();
			else
				return e;
		} // func GetInnerException
	} // class Procs

	#endregion

	#region -- class ExceptionFormatter -----------------------------------------------

	/// <summary>Exception formatter.</summary>
	public abstract class ExceptionFormatter
	{
		#region -- class PlainTextExceptionFormatter ----------------------------------

		private class PlainTextExceptionFormatter : ExceptionFormatter
		{
			private readonly StringBuilder sb;

			public PlainTextExceptionFormatter(StringBuilder sb)
			{
				this.sb = sb ?? throw new ArgumentNullException(nameof(sb));
			} // ctor

			protected override void AppendSection(bool first, string sectionName, Exception ex)
				=> sb.WriteSeperator(sectionName + ": " + ex.GetType().Name);

			private static string ReplaceNoneVisibleChars(string value)
				=> value.Replace("\n", "\\n")
						.Replace("\r", "\\r")
						.Replace("\t", "\\t");

			protected override void AppendProperty(string name, Type type, Func<object> value)
			{
				sb.Append((name + ":").PadRight(20, ' ')).Append(' ');
				try
				{
					var v = value();
					if (v == null)
						sb.AppendLine("<null>");
					else
					{
						var stringValue = Convert.ToString(v, CultureInfo.InvariantCulture);


						if (type == typeof(string))
						{
							if (stringValue.Contains('\n'))
							{
								var lines = stringValue.Replace("\r", "").Split('\n');
								sb.AppendLine();
								foreach (var l in lines)
									sb.Append("    ").AppendLine(l);
							}
							else
								sb.Append('\'').Append(ReplaceNoneVisibleChars(stringValue)).Append('\'').AppendLine();
						}
						else if (type != typeof(sbyte) && type != typeof(byte) &&
								type != typeof(short) && type != typeof(ushort) &&
								type != typeof(int) && type != typeof(uint) &&
								type != typeof(long) && type != typeof(ulong) &&
								type != typeof(decimal) && type != typeof(DateTime) &&
								type != typeof(float) && type != typeof(double))
						{
							sb.Append('(').Append(type.Name).Append(')').AppendLine(ReplaceNoneVisibleChars(stringValue));
						}
						else
						{
							sb.AppendLine(stringValue);
						}
					}
				}
				catch (Exception e)
				{
					sb.AppendLine(String.Format("Error[{0}] = '{1}'", e.GetType().Name, e.Message));
				}
			} // proc AppendProperty

			protected override object Compile()
				=> sb;
		} // class PlainTextExceptionFormatter

		#endregion

		/// <summary></summary>
		protected ExceptionFormatter()
		{
		} // ctor

		/// <summary>Appends a new exception to the formatter.</summary>
		/// <param name="isFirst"><c>true</c>, if this is the first exception, added.</param>
		/// <param name="sectionName">Name of the section.</param>
		/// <param name="ex">Exception, that will be emitted next.</param>
		protected abstract void AppendSection(bool isFirst, string sectionName, Exception ex);

		/// <summary>Append a property.</summary>
		/// <param name="name">Name of the property.</param>
		/// <param name="type">Type of the property.</param>
		/// <param name="value">Value of the property.</param>
		protected abstract void AppendProperty(string name, Type type, Func<object> value);

		/// <summary>Finish the result.</summary>
		/// <returns></returns>
		protected abstract object Compile();

		/// <summary>Format a exception with this formatter.</summary>
		/// <param name="e">Exception to format.</param>
		/// <returns>Returns the result.</returns>
		public object Format(Exception e)
		{
			if (e == null)
				return null;

			var exceptions = new Stack<KeyValuePair<string, Exception>>();

			// Exception title
			AppendSection(true, e.GetType().Name, e);

			while (true)
			{
				// Eigenschaften ausgeben 
				foreach (var pi in e.GetType().GetRuntimeProperties())
					if (pi.Name == "StackTrace")
						continue;
					else if (pi.Name == "InnerException")
						continue;
					else if (pi.Name == "LoaderExceptions")
					{
						var le = (Exception[])pi.GetValue(e);
						if (le != null)
						{
							for (var i = le.Length - 1; i >= 0; i--)
								exceptions.Push(new KeyValuePair<string, Exception>($"LoaderException[{i}]", le[i]));
						}
					}
					else if (pi.Name == "Data")
					{
						var dict = (IDictionary)pi.GetValue(e);
						foreach (var key in dict.Keys)
						{
							var value = dict[key];
							if(value != null 
								&& !(value is ILuaExceptionData))
							{
								string GetNiceKeyName()
								{
									switch(key)
									{
										case int i:
											return $"Idx{i}";
										case string s:
											return s;
										case null:
											return "<null>";
										default:
											return key.ToString();
									}
								} // func GetNiceKeyName

								AppendProperty(GetNiceKeyName(), value.GetType(), () => value);
							}
						}
					}
					else
						AppendProperty(pi.Name, pi.PropertyType, () => pi.GetValue(e));

				// LuaStackFrame
				if (e.Data[LuaRuntimeException.ExceptionDataKey] is ILuaExceptionData data && data.Count > 0)
					AppendProperty("LuaStackTrace", typeof(string), () => data.FormatStackTrace(skipSClrFrame: false));

				// Stacktrace
				AppendProperty("StackTrace", typeof(string), () => e.StackTrace);

				// InnerException
				if (e.InnerException != null)
					exceptions.Push(new KeyValuePair<string, Exception>("InnerException", e.InnerException));

				// Hole nächste Exception an
				if (exceptions.Count == 0)
					break;

				var n = exceptions.Pop();
				e = n.Value;
				AppendSection(false, n.Key, n.Value);
			}

			return Compile();
		} // func Format

		/// <summary>Formats the exception with the given formatter.</summary>
		/// <param name="exception">Exception to format.</param>
		/// <typeparam name="T">Format</typeparam>
		/// <returns></returns>
		public static object Format<T>(Exception exception)
			where T : ExceptionFormatter
		{
			var formatter = (ExceptionFormatter)Activator.CreateInstance(typeof(T));
			return formatter.Format(exception);
		} // func Format

		/// <summary>Formats the exception to a text block.</summary>
		/// <param name="sb"></param>
		/// <param name="exception"></param>
		public static void FormatPlainText(StringBuilder sb, Exception exception)
		{
			new PlainTextExceptionFormatter(sb.WriteFreshLine()).Format(exception);
		} // func FormatPainText

		/// <summary>Formats the exception to a text block.</summary>
		/// <param name="exception"></param>
		/// <returns></returns>
		public static string FormatPlainText(Exception exception)
		{
			var sb = new StringBuilder();
			FormatPlainText(sb, exception);
			return sb.ToString();
		} // func FormatPainText
	} // class ExceptionFormatter

	#endregion

	#region -- class ILuaUserRuntimeException -----------------------------------------

	/// <summary>Marks a exception as a friendly exception for the user.</summary>
	public interface ILuaUserRuntimeException
	{
		/// <summary></summary>
		string Message { get; }
	} // interface ILuaUserRuntimeException

	#endregion

	#region -- class LuaUserRuntimeException ------------------------------------------

	/// <summary></summary>
	public sealed class LuaUserRuntimeException : LuaRuntimeException, ILuaUserRuntimeException
	{
		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		public LuaUserRuntimeException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="level"></param>
		/// <param name="skipClrFrames"></param>
		public LuaUserRuntimeException(string message, int level, bool skipClrFrames)
			: base(message, level, skipClrFrames)
		{
		}
	} // class LuaUserRuntimeException

	#endregion

	#region -- class LuaAssertRuntimeException ----------------------------------------

	/// <summary></summary>
	public sealed class LuaAssertRuntimeException : LuaRuntimeException
	{
		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		public LuaAssertRuntimeException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="level"></param>
		/// <param name="skipClrFrames"></param>
		public LuaAssertRuntimeException(string message, int level, bool skipClrFrames)
			: base(message, level, skipClrFrames)
		{
		}
	} // class LuaAssertRuntimeException

	#endregion
}
