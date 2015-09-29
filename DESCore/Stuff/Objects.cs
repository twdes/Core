using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
			return Lua.RtConvertValue(value, typeTo);
		} // func ChangeType

		/// <summary>Generische Implementierung von <c>ChangeType</c>.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value"></param>
		/// <returns></returns>
		public static T ChangeType<T>(this object value)
		{
			return (T)Procs.ChangeType(value, typeof(T));
		} // func ChangeType
		
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
