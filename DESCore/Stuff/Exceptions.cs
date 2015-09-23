using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace TecWare.DE.Stuff
{
	#region -- class Procs --------------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static partial class Procs
	{
		/// <summary></summary>
		/// <param name="e"></param>
		/// <returns></returns>
		public static string GetMessageString(this Exception e)
		{
			return ExceptionFormatter.FormatPlainText(e);
		} // func GetMessageString

		/// <summary>Gibt die Nachricht via <c>Debug.Print</c> aus.</summary>
		/// <param name="e"></param>
		[Conditional("DEBUG")]
		public static void DebugOut(this Exception e)
		{
			Debug.WriteLine("Exception:\n{0}", e.GetMessageString());
		} // proc DebugOut
	} // class Procs

	#endregion

	#region -- class ExceptionFormatter -------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public abstract class ExceptionFormatter
	{
		#region -- class PlainTextExceptionFormatter --------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private class PlainTextExceptionFormatter : ExceptionFormatter
		{
			private StringBuilder sb;

			public PlainTextExceptionFormatter(StringBuilder sb)
			{
				this.sb = sb;
			} // ctor

			protected override void AppendSection(bool lFirst, string sSectionName)
			{
				sb.WriteSeperator(sSectionName);
			} // proc AppendSection

			protected override void AppendProperty(string sName, Type type, Func<object> value)
			{
				sb.Append((sName + ":").PadRight(20, ' ')).Append(' ');
				try
				{
					object v = value();

					if (v == null)
						sb.AppendLine("<null>");
					else
					{
						string sValue = Convert.ToString(v, CultureInfo.InvariantCulture);
						sValue = sValue.Replace("\n", "\\n")
							.Replace("\r", "\\r")
							.Replace("\t", "\\t");

						if (type == typeof(string))
						{
							sb.Append('\'').Append(sValue).Append('\'').AppendLine();
						}
						else if (type != typeof(sbyte) && type != typeof(byte) &&
								type != typeof(short) && type != typeof(ushort) &&
								type != typeof(int) && type != typeof(uint) &&
								type != typeof(long) && type != typeof(ulong) &&
								type != typeof(decimal) && type != typeof(DateTime) &&
								type != typeof(float) && type != typeof(double))
						{
							sb.Append('(').Append(type.Name).Append(')').AppendLine(sValue);
						}
						else
						{
							sb.AppendLine(sValue);
						}
					}
				}
				catch (Exception e)
				{
					sb.AppendLine(String.Format("Error[{0}] = '{1}'", e.GetType().Name, e.Message));
				}
			} // proc AppendProperty

			protected override object Compile()
			{
				return sb;
			} // func Compile
		} // class PlainTextExceptionFormatter

		#endregion

		/// <summary></summary>
		protected ExceptionFormatter()
		{
		} // ctor

		/// <summary>Wird geschrieben, wenn eine neue Exception begonnen wird.</summary>
		/// <param name="lFirst">Handelt es sich um die erste Sektion</param>
		/// <param name="sSectionName"></param>
		protected abstract void AppendSection(bool lFirst, string sSectionName);
		/// <summary>Schreibt eine Eigenschaft</summary>
		/// <param name="sName"></param>
		/// <param name="type"></param>
		/// <param name="value"></param>
		protected abstract void AppendProperty(string sName, Type type, Func<object> value);

		/// <summary>Schließt die Verarbeitung ab.</summary>
		/// <returns></returns>
		protected abstract object Compile();

		/// <summary>Formatiert die Fehlermeldung in das entsprechende Format</summary>
		/// <param name="e"></param>
		/// <returns></returns>
		public object Format(Exception e)
		{
			if (e == null)
				return null;

			var exceptions = new Stack<KeyValuePair<string, Exception>>();

			// Exception Titel
			AppendSection(true, e.GetType().Name);

			while (true)
			{
				// Eigenschaften ausgeben 
				foreach (PropertyInfo pi in e.GetType().GetRuntimeProperties())
					if (pi.Name == "StackTrace")
						continue;
					else if (pi.Name == "InnerException")
						continue;
					else if (pi.Name == "LoaderExceptions")
					{
						Exception[] le = (Exception[])pi.GetValue(e);
						if (le != null)
							for (int i = le.Length - 1; i >= 0; i--)
								exceptions.Push(new KeyValuePair<string, Exception>(String.Format("LoaderException[{0}]: ", i), le[i]));
					}
					else
						AppendProperty(pi.Name, pi.PropertyType, () => pi.GetValue(e));

				// Stacktrace
				AppendProperty("StackTrace", typeof(string), () => e.StackTrace);

				// InnerException
				if (e.InnerException != null)
					exceptions.Push(new KeyValuePair<string, Exception>("InnerException: ", e.InnerException));

				// Hole nächste Exception an
				if (exceptions.Count == 0)
					break;

				var n = exceptions.Pop();
				e = n.Value;
				AppendSection(false, n.Key + " " + n.Value.GetType().Name);
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
			ExceptionFormatter formatter = (ExceptionFormatter)Activator.CreateInstance(typeof(T));
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
}
