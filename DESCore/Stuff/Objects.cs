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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Neo.IronLua;

namespace TecWare.DE.Stuff
{
	#region -- class DisposableScope --------------------------------------------------

	/// <summary></summary>
	public sealed class DisposableScope : IDisposable
	{
		private readonly Action dispose;

		/// <summary></summary>
		/// <param name="dispose"></param>
		public DisposableScope(Action dispose)
			=> this.dispose = dispose;

		/// <summary></summary>
		public void Dispose()
			=> dispose();
	} // class class DisposableScope

	#endregion

	/// <summary></summary>
	public static partial class Procs
	{
		#region -- FreeAndNil ---------------------------------------------------------

		/// <summary>Ruft IDisposable.Dispose, wenn es implementiert wurde. Sonst wird die Variable auf <c>null</c> gesetzt.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="obj"></param>
		public static void FreeAndNil<T>(ref T obj)
			where T : class
		{
			// Optionaler Call von Dispose
			if (obj is IDisposable disp)
				disp.Dispose();

			// Setze die Referenz auf Null
			obj = null;
		} // proc FreeAndNil

		#endregion

		#region -- ChangeType ---------------------------------------------------------

		/// <summary>Konvertiert den Datentyp zum gewünschten Ziel-Datentyp.</summary>
		/// <param name="value"></param>
		/// <param name="typeTo"></param>
		/// <returns></returns>
		/// <remarks>Es wird der Lua Converter verwendet, da er Schnittstelle, Operatoren und allgemeine Regeln beherrscht ohne auf das TypeDescriptor-Framework zu verweisen.</remarks>
		public static object ChangeType(object value, Type typeTo)
		{
			if (typeTo == typeof(bool) && value is string)
			{
				if (Int64.TryParse((string)value, out var number))
					return number != 0;
				var t = (string)value;
				if (String.Compare(t, "t", StringComparison.OrdinalIgnoreCase) * String.Compare(t, "true", StringComparison.OrdinalIgnoreCase) * String.Compare(t, Boolean.TrueString, StringComparison.OrdinalIgnoreCase) == 0)
					return true;
				if (String.Compare(t, "f", StringComparison.OrdinalIgnoreCase) * String.Compare(t, "false", StringComparison.OrdinalIgnoreCase) * String.Compare(t, Boolean.FalseString, StringComparison.OrdinalIgnoreCase) == 0)
					return false;
				throw new ArgumentException(String.Format("Text '{0}' is neither False nor True.", t));
			}
			else if (typeTo == typeof(DateTimeOffset) && (value == null || value is string))
				return value == null ? DateTimeOffset.MinValue : DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture);
			else if (typeTo == typeof(DateTime) && (value == null || value is string))
				return value == null ? DateTime.MinValue : DateTime.Parse((string)value, CultureInfo.InvariantCulture);
			else if (typeTo == typeof(XDocument) && (value == null || value is string))
				return value == null ? null : XDocument.Parse((string)value);
			else if (typeTo == typeof(XElement) && (value == null || value is string))
				return value == null ? null : XElement.Parse((string)value);
			else if (typeTo == typeof(string) && (value == null || value is XDocument))
				return value?.ToString();
			else if (typeTo == typeof(string) && (value == null || value is XElement))
				return value?.ToString();

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

		#region -- GetService ---------------------------------------------------------
		
		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sp"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
		public static T GetService<T>(this IServiceProvider sp, bool throwException = false)
			where T : class
			=> GetService<T>(sp, typeof(T), throwException);
		
		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="sp"></param>
		/// <param name="serviceType"></param>
		/// <param name="throwException"></param>
		/// <returns></returns>
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

		#region -- CompareBytes -------------------------------------------------------

		/// <summary></summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static bool CompareBytes(byte[] a, byte[] b)
		{
			if (a == null || b == null)
				return false;
			if (a.Length != b.Length)
				return false;

			return CompareBytesIntern(a, 0, b, 0, a.Length);
		} // func CompareBytes

		/// <summary></summary>
		/// <param name="a"></param>
		/// <param name="aOffset"></param>
		/// <param name="b"></param>
		/// <param name="bOffset"></param>
		/// <param name="length"></param>
		/// <returns></returns>
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

		#region -- Convert Bytes ------------------------------------------------------

		/// <summary></summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		public static byte[] ConvertToBytes(string bytes)
			=> ConvertToBytes(bytes, 0, bytes.Length);

		/// <summary></summary>
		/// <param name="bytes"></param>
		/// <param name="ofs"></param>
		/// <param name="len"></param>
		/// <returns></returns>
		public static byte[] ConvertToBytes(string bytes, int ofs, int len)
		{
			if (ofs + len > bytes.Length)
				throw new ArgumentOutOfRangeException(nameof(len));

			// move ofs for 0x
			if (len > 2 && bytes[ofs] == '0' && (bytes[ofs + 1] == 'x' || bytes[ofs + 1] == 'X'))
			{
				ofs += 2;
				len -= 2;
			}

			if ((len & 1) != 0) // even number expected
				throw new ArgumentException("invalid bytes", nameof(bytes));

			var data = new byte[len >> 1];
			var j = 0;
			while (j < data.Length)
			{
				data[j] = Byte.Parse(bytes.Substring(ofs, 2), NumberStyles.AllowHexSpecifier);
				ofs += 2;
				j++;
			}

			return data;
		} // func ConvertToBytes

		/// <summary></summary>
		/// <param name="bytes"></param>
		/// <returns></returns>
		public static string ConvertToString(byte[] bytes)
			=> ConvertToString(bytes, 0, bytes.Length);

		/// <summary></summary>
		/// <param name="bytes"></param>
		/// <param name="ofs"></param>
		/// <param name="len"></param>
		/// <returns></returns>
		public static string ConvertToString(byte[] bytes, int ofs, int len)
		{
			var sb = new StringBuilder(len << 1);
			var end = ofs + len;
			while (ofs < end)
				sb.Append(bytes[ofs++].ToString("X2"));
			return sb.ToString();
		} // func ConvertToString

		#endregion

		#region -- CombineEnumerator --------------------------------------------------

		#region -- class ConnectedEnumerator ------------------------------------------

		private sealed class ConnectedEnumerator : IEnumerator, IDisposable
		{
			private readonly IEnumerator enumerators;
			private IEnumerator currentEnumerator = null;

			public ConnectedEnumerator(params IEnumerator[] enumerators)
			{
				this.enumerators = enumerators.GetEnumerator();
			} // ctor

			void IDisposable.Dispose()
			{
				// dispose enumerators
				if (currentEnumerator is IDisposable d)
					d.Dispose();

				while (enumerators.MoveNext())
				{
					if (enumerators.Current is IDisposable d2)
						d2.Dispose();
				}
			} // proc Dispose

			public bool MoveNext()
			{
				if (currentEnumerator == null || !currentEnumerator.MoveNext())
				{
					if (enumerators.MoveNext())
					{
						currentEnumerator = (IEnumerator)enumerators.Current;
						return MoveNext();
					}
					else
					{
						currentEnumerator = null;
						return false;
					}
				}
				else
					return true;
			} // func MoveNext

			public void Reset()
			{
				currentEnumerator = null;
				enumerators.Reset();
			} // proc Reset

			public object Current => currentEnumerator?.Current;
		} // class ConnectedEnumerator

		#endregion

		/// <summary></summary>
		/// <param name="enumerators"></param>
		/// <returns></returns>
		public static IEnumerator CombineEnumerator(params IEnumerator[] enumerators)
		{
			if (enumerators == null || enumerators.Length == 0)
				return Array.Empty<object>().GetEnumerator();
			else if (enumerators.Length == 1)
				return enumerators[1];
			else
				return new ConnectedEnumerator(enumerators);
		} // func Combine

		#endregion

		#region -- Range Enumerator ---------------------------------------------------

		private class RangeEnumeratorCore<ENUM> : IEnumerator
			where ENUM : IEnumerator
		{
			private readonly ENUM enumerator;
			private readonly int start;
			private readonly int end;
			private int position = 0;

			public RangeEnumeratorCore(ENUM enumerator, int start, int count)
			{
				if (enumerator == null)
					throw new ArgumentOutOfRangeException(nameof(enumerator));

				this.enumerator = enumerator;
				this.start = start;
				this.end = start > 0 ? start + count : count;
			} // ctor

			public void Reset()
			{
				position = 0;
				enumerator.Reset();
			} // proc Reset

			public bool MoveNext()
			{
				// skip part
				while (position < start)
				{
					if (!enumerator.MoveNext())
						return false;
					position++;
				}

				// count part
				if (position < end)
				{
					if (!enumerator.MoveNext())
						return false;
					position++;
					return true;
				}
				return false;
			} // func MoveNext

			public object Current => enumerator.Current;

			protected ENUM Enumerator => enumerator;
		} // class RangeEnumeratorCore

		private sealed class RangeEnumeratorUntyped : RangeEnumeratorCore<IEnumerator>
		{
			public RangeEnumeratorUntyped(IEnumerator enumerator, int start, int count) 
				: base(enumerator, start, count)
			{
			}
		} // class RangeEnumeratorUntyped

		private sealed class RangeEnumeratorTyped<T> : RangeEnumeratorCore<IEnumerator<T>>, IEnumerator<T>
		{
			public RangeEnumeratorTyped(IEnumerator<T> enumerator, int start, int count)
				: base(enumerator, start, count)
			{
			} // ctor

			public void Dispose()
				=> Enumerator.Dispose();

			T IEnumerator<T>.Current => Enumerator.Current;
		} // class RangeEnumeratorTyped

		/// <summary></summary>
		/// <param name="enumerator"></param>
		/// <param name="start"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public static IEnumerator GetRangeEnumerator(this IEnumerator enumerator, int start, int count)
			=> start == 0 && count == Int32.MaxValue
				? enumerator
				: new RangeEnumeratorUntyped(enumerator, start, count);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumerator"></param>
		/// <param name="start"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		public static IEnumerator<T> GetRangeEnumerator<T>(this IEnumerator<T> enumerator, int start, int count)
			=> start == 0 && count == Int32.MaxValue
				? enumerator
				: new RangeEnumeratorTyped<T>(enumerator, start, count);

		#endregion

		#region -- Typed Enumerator ---------------------------------------------------

		private sealed class TypedEnumerator<T> : IEnumerator<T>
		{
			private readonly IEnumerator enumerator;

			public TypedEnumerator(IEnumerator enumerator)
				=> this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));

			public void Dispose()
				=> (enumerator as IDisposable)?.Dispose();

			public void Reset()
				=> enumerator.Reset();

			public bool MoveNext()
				=> enumerator.MoveNext();

			object IEnumerator.Current => enumerator.Current;

			public T Current => (T)enumerator.Current;
		} // class TypedEnumerator

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="enumerator"></param>
		/// <returns></returns>
		public static IEnumerator<T> GetTypedEnumerator<T>(this IEnumerator enumerator)
			=> new TypedEnumerator<T>(enumerator);

		#endregion

		#region -- Disposable Enumerators ---------------------------------------------

		#region -- class DisposeEnumerator --------------------------------------------

		private sealed class DisposeEnumerator<T> : IEnumerator<T>
		{
			private readonly IDisposable disposable;
			private readonly IEnumerator<T> innerEnumerator;

			public DisposeEnumerator(IDisposable disposable, IEnumerator<T> innerEnumerator)
			{
				this.disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
				this.innerEnumerator = innerEnumerator ?? throw new ArgumentNullException(nameof(innerEnumerator));
			} // ctor

			public void Dispose()
			{
				innerEnumerator.Dispose();
				disposable.Dispose();
			} // proc Dispose

			public bool MoveNext()
				=> innerEnumerator.MoveNext();

			public void Reset()
				=> innerEnumerator.Reset();

			public T Current => innerEnumerator.Current;
			object IEnumerator.Current => innerEnumerator.Current;
		} // class DisposeEnumerator

		#endregion

		#region -- class DisposeEnumerable --------------------------------------------

		private sealed class DisposeEnumerable<T> : IEnumerable<T>
		{
			private readonly IDisposable disposable;
			private readonly IEnumerable<T> enumeratable;

			public DisposeEnumerable(IDisposable disposable, IEnumerable<T> enumeratable)
			{
				this.disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
				this.enumeratable = enumeratable ?? throw new ArgumentNullException(nameof(enumeratable));
			} // ctor

			public IEnumerator<T> GetEnumerator()
				=> enumeratable.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} // class DisposeEnumerable

		#endregion

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="e"></param>
		/// <param name="disposable"></param>
		/// <returns></returns>
		public static IEnumerable<T> Dispose<T>(this IEnumerable<T> e, IDisposable disposable)
			=> new DisposeEnumerable<T>(disposable, e);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="e"></param>
		/// <param name="disposable"></param>
		/// <returns></returns>
		public static IEnumerator<T> Dispose<T>(this IEnumerator<T> e, IDisposable disposable)
			=> new DisposeEnumerator<T>(disposable, e);

		#endregion
	} // class Procs
}
