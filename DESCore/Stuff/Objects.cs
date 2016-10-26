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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;

namespace TecWare.DE.Stuff
{
	#region -- class DisposableScope ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class DisposableScope : IDisposable
	{
		private Action dispose;

		public DisposableScope(Action dispose)
		{
			this.dispose = dispose;
		} // ctor

		public void Dispose()
		{
			dispose();
		} // proc Dispose
	} // class class DisposableScope

	#endregion

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static partial class Procs
	{
		#region -- FreeAndNil -------------------------------------------------------------

		/// <summary>Ruft IDisposable.Dispose, wenn es implementiert wurde. Sonst wird die Variable auf <c>null</c> gesetzt.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="obj"></param>
		public static void FreeAndNil<T>(ref T obj)
			where T : class
		{
			// Optionaler Call von Dispose
			IDisposable disp = obj as IDisposable;
			if (disp != null)
				disp.Dispose();

			// Setze die Referenz auf Null
			obj = null;
		} // proc FreeAndNil

		#endregion

		#region -- ChangeType -------------------------------------------------------------

		/// <summary>Konvertiert den Datentyp zum gewünschten Ziel-Datentyp.</summary>
		/// <param name="value"></param>
		/// <param name="typeTo"></param>
		/// <returns></returns>
		/// <remarks>Es wird der Lua Converter verwendet, da er Schnittstelle, Operatoren und allgemeine Regeln beherrscht ohne auf das TypeDescriptor-Framework zu verweisen.</remarks>
		public static object ChangeType(object value, Type typeTo)
		{
			if (typeTo == typeof(bool) && value is string)
			{
				var t = (string)value;
				return t == "1" || String.Compare(t, Boolean.TrueString, StringComparison.OrdinalIgnoreCase) == 0;
			}
			else if (typeTo == typeof(DateTimeOffset) && (value == null || value is string))
				return value == null ? DateTimeOffset.MinValue : DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture);

			else if (typeTo == typeof(XDocument) && (value == null || value is string))
				return value == null ? null : XDocument.Parse((string)value);
			else if (typeTo == typeof(string) && (value == null || value is XDocument))
				return value == null ? null : value.ToString();

			else if (typeTo == typeof(Type) && (value == null || value is string))
				return value == null ? null : LuaType.GetType((string)value, lateAllowed: false).Type;
			else if (typeTo == typeof(string) && value is Type)
				return LuaType.GetType((Type)value).AliasOrFullName;
			else
				return Lua.RtConvertValue(value, typeTo);
		} // func ChangeType

		/// <summary>Generische Implementierung von <c>ChangeType</c>.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <returns></returns>
		public static T ChangeType<T>(this object value)
			=> (T)Procs.ChangeType(value, typeof(T));
		
		#endregion

		#region -- GetService -------------------------------------------------------------
		
		public static T GetService<T>(this IServiceProvider sp, bool throwException = false)
			where T : class
		{
			return GetService<T>(sp, typeof(T), throwException);
		} // func GetService

		public static T GetService<T>(this IServiceProvider sp, Type serviceType, bool throwException = false)
			where T : class
		{
			T r = null;
			if (sp != null)
				r = sp.GetService(serviceType) as T;

			if (r == null && throwException)
				throw new ArgumentException(String.Format("Service {0} is not implemented by {1}.", serviceType.Name, typeof(T).Name));

			return r;
		} // func GetService

		#endregion

		#region -- CompareBytes -----------------------------------------------------------

		public static bool CompareBytes(byte[] a, byte[] b)
		{
			if (a == null || b == null)
				return false;
			if (a.Length != b.Length)
				return false;

			return CompareBytes(a, 0, b, 0, a.Length);
		} // func CompareBytes

		public static bool CompareBytes(byte[] a, int aOffset, byte[] b, int bOffset, int length)
		{
			if (a == null || b == null)
				return false;

			if (aOffset < 0 || bOffset < 0 || length < 0)
				throw new ArgumentOutOfRangeException();

			if (a.Length < aOffset + length)
				return false;
			if (b.Length < bOffset + length)
				return false;

			return CompareBytesIntern(a, aOffset, b, bOffset, length);
		} // proc CompareBytes

		private static bool CompareBytesIntern(byte[] a, int aOffset, byte[] b, int bOffset, int length)
		{
			for (var i = 0; i < length; i++)
			{
				if (a[aOffset + i] != b[bOffset + i])
					return false;
			}

			return true;
		} // func CompareBytesIntern

		#endregion

		public readonly static string[] EmptyStringArray = new string[0];
	} // class Procs
}
