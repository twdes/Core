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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace TecWare.DE.Stuff
{
	#region -- class PropertyValue ------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class PropertyValue
	{
		private readonly string name;
		private readonly Type type;
		private readonly object value;

		public PropertyValue(string name, object value)
			: this(name, value?.GetType(), value)
		{
		} // ctor

		public PropertyValue(string name, Type type, object value)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			this.name = name;
			this.type = type ?? typeof(object);
			this.value = Procs.ChangeType(value, this.type);
		} // ctor

		public override string ToString()
			=> $"{name}:{type.Name} = {value}";

		public string Name => name;
		public Type Type => type;
		public object Value => value;

		public static PropertyValue[] EmptyArray { get; } = new PropertyValue[0];
	} // class PropertyValue

	#endregion

	#region -- interface IPropertyReadOnlyDictionary ------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IPropertyReadOnlyDictionary
	{
		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		bool TryGetProperty(string name, out object value);
	} //  interface IPropertyReadOnlyDictionary

	#endregion

	#region -- interface IPropertyEnumerableDictionary ----------------------------------

	public interface IPropertyEnumerableDictionary : IPropertyReadOnlyDictionary, IEnumerable<PropertyValue>
	{
	} // interface IPropertyEnumerableDictionary

	#endregion

	#region -- class PropertyDictionary -------------------------------------------------

	public sealed class PropertyDictionary : IPropertyEnumerableDictionary, IPropertyReadOnlyDictionary, IEnumerable<PropertyValue>
	{
		#region -- class EmptyReadOnlyDictionary ------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class EmptyReadOnlyDictionary : IPropertyEnumerableDictionary
		{
			public EmptyReadOnlyDictionary()
			{
			} // ctor

			public IEnumerator<PropertyValue> GetEnumerator()
			{
				yield break;
			} // func GetEnumerator

			public bool TryGetProperty(string name, out object value)
			{
				value = null;
				return false;
			} // func TryGetProperty

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} //class EmptyReadOnlyDictionary

		#endregion

		private PropertyDictionary parentDictionary = null;
		private Dictionary<string, PropertyValue> properties = new Dictionary<string, PropertyValue>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor -------------------------------------------------------------------

		/// <summary>Erzeugt ein leeres Dictionary</summary>
		public PropertyDictionary()
		{
		} // ctor

		/// <summary>Erzeugt ein leeres Dictionary</summary>
		/// <param name="parent">Mit dem angegebenen ParameterDictionary</param>
		public PropertyDictionary(PropertyDictionary parent)
		{
			this.parentDictionary = parent;
		} // ctor

		/// <summary>Erzeugt ein Dictionary.</summary>
		/// <param name="sParameterList">Parameterliste als Zeichenfolge konvertiert.</param>
		public PropertyDictionary(string parameterList)
		{
			AddRange(Procs.SplitPropertyList(parameterList));
		} // ctor

		/// <summary>Erzeugt ein Dictionary.</summary>
		/// <param name="args">Liste mit KeyValue-Paaren.</param>
		public PropertyDictionary(params KeyValuePair<string, object>[] args)
		{
			AddRange(args);
		} // ctor

		/// <summary>Erzeugt ein Dictionary.</summary>
		/// <param name="args">Liste mit KeyValue-Paaren.</param>
		public PropertyDictionary(params KeyValuePair<string, string>[] args)
		{
			AddRange(args);
		} // ctor

		/// <summary>Erzeugt ein Dictionary.</summary>
		/// <param name="args">Liste mit KeyValue-Paaren.</param>
		public PropertyDictionary(params PropertyValue[] args)
		{
			AddRange(args);
		} // ctor

		#endregion

		#region -- SetProperty, AddRange --------------------------------------------------

		public void SetProperty(PropertyValue value)
		{
			lock (properties)
				properties[value.Name] = value;
		} // proc SetProperty

		public void SetProperty(string name, object value) => SetProperty(new PropertyValue(name, null, value));
		public void SetProperty(string name, Type type, object value) => SetProperty(new PropertyValue(name, type, value));

		public void AddRange(IEnumerable<KeyValuePair<string, string>> args)
		{
			if (args != null)
			{
				foreach (var c in args)
					SetProperty(c.Key, typeof(string), c.Value);
			}
		} // proc AddRange

		public void AddRange(IEnumerable<KeyValuePair<string, object>> args)
		{
			if (args != null)
			{
				foreach (var c in args)
					SetProperty(c.Key, null, c.Value);
			}
		} // proc AddRange

		public void AddRange(IEnumerable<PropertyValue> args)
		{
			if (args != null)
			{
				foreach (var c in args)
					SetProperty(c);
			}
		} // proc AddRange

		#endregion

		#region -- GetProperty ------------------------------------------------------------

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public object GetProperty(string name, object @default)
		{
			object r;
			return TryGetProperty(name, out r) ? r : @default;
		} // func GetProperty

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public string GetProperty(string name, string @default)
		{
			string r;
			return TryGetProperty(name, out r) ? r : @default;
		} // func GetProperty

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <typeparam name="T">Rückgabewert.</typeparam>
		/// <param name="name">Parametername.</param>
		/// <param name="def">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public T GetProperty<T>(string name, T @default)
		{
			T r;
			return TryGetProperty(name, out r) ? r : @default;
		} // func GetProperty

		/// <summary>Versucht einen Paremter zurückzugeben.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="value">Wert der abgelegt wurde.</param>
		/// <param name="type">Orignal abgelegter Datentyp des Werts.</param>
		/// <returns><c>true</c>, wenn ein Wert gefunden wurde.</returns>
		public bool TryGetProperty(string name, out object value, out Type type)
		{
			PropertyValue p;
			if (!String.IsNullOrEmpty(name))
			{
				if (properties.TryGetValue(name, out p))
				{
					value = p.Value;
					type = p.Type;
					return true;
				}
				else if (parentDictionary != null)
				{
					return parentDictionary.TryGetProperty(name, out value, out type);
				}
			}

			value = null;
			type = null;
			return false;
		} // func TryGetProperty

		/// <summary>Versucht einen Paremter zurückzugeben.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="value">Wert der abgelegt wurde.</param>
		/// <returns><c>true</c>, wenn ein Wert gefunden wurde.</returns>
		public bool TryGetProperty(string name, out object value)
		{
			Type type;
			return TryGetProperty(name, out value, out type);
		} // func TryGetProperty

		/// <summary>Versucht einen Paremter zurückzugeben.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="value">Wert der abgelegt wurde.</param>
		/// <returns><c>true</c>, wenn ein Wert gefunden wurde.</returns>
		public bool TryGetProperty(string name, out string value)
		{
			object ret;
			if (TryGetProperty(name, out ret) && ret != null)
			{
				value = ret.ToString();
				return true;
			}
			else
			{
				value = String.Empty;
				return false;
			}
		} // func TryGetProperty

		/// <summary>Versucht einen Paremter zurückzugeben.</summary>
		/// <typeparam name="T">Rückgabewert.</typeparam>
		/// <param name="name">Parametername.</param>
		/// <param name="value">Wert der abgelegt wurde.</param>
		/// <returns><c>true</c>, wenn ein Wert gefunden wurde.</returns>
		public bool TryGetProperty<T>(string name, out T value)
		{
			object ret;
			if (TryGetProperty(name, out ret) && ret != null)
			{
				try
				{
					if (ret is T)
						value = (T)ret;
					else
						value = (T)Procs.ChangeType(ret, typeof(T));
					return true;
				}
				catch (FormatException)
				{
				}
			}
			value = default(T);
			return false;
		} // func TryGetProperty

		#endregion

		#region -- Clear, Remove, Contains ------------------------------------------------

		/// <summary>Löscht alle Parameter der aktuellen Ebene</summary>
		public void Clear()
		{
			properties.Clear();
		} // proc Clear

		/// <summary>Löscht den Parameter aus dem Dictionary</summary>
		/// <param name="name">Name des Parameters.</param>
		public void Remove(string name)
		{
			properties.Remove(name);
		} // proc Remove

		/// <summary>Existiert dieser Parameter</summary>
		/// <param name="name">Prüft, ob der Parameter vorhanden ist.</param>
		/// <returns><c>true</c>, wenn der Parameter gefunden wurde.</returns>
		public bool Contains(string name)
		{
			return Contains(name, true);
		} // func Contains

		/// <summary>Existiert dieser Parameter</summary>
		/// <param name="name">Prüft, ob der Parameter vorhanden ist.</param>
		/// <param name="fullHierachy"><c>true</c>, wenn auch in den Parent geprüft werden soll.</param>
		/// <returns><c>true</c>, wenn der Parameter gefunden wurde.</returns>
		public bool Contains(string name, bool fullHierachy)
		{
			var l = properties.ContainsKey(name);
			if (!l && parentDictionary != null && fullHierachy)
				return parentDictionary.Contains(name, true);
			return l;
		} // func Contains

		#endregion

		/// <summary>Setzt/Gibt einen Parameter zurück.</summary>
		/// <param name="name">Name des Parameters.</param>
		/// <returns>Wert</returns>
		public object this[string name]
		{
			get { return GetProperty(name, null); }
			set { SetProperty(name, value); }
		} // prop this

		/// <summary></summary>
		public IEnumerable<KeyValuePair<string, object>> PropertyObjects => from c in properties select new KeyValuePair<string, object>(c.Key, c.Value.Value);
		/// <summary></summary>
		public IEnumerable<KeyValuePair<string, string>> PropertyStrings => from c in properties select new KeyValuePair<string, string>(c.Key, (string)Procs.ChangeType(c.Value.Value, typeof(string)));
		/// <summary>Gibt das Dictionary als Property-List zurück. Die Information zum Originaldatentyp geht dabei verloren.</summary>
		public string PropertyList => Procs.JoinPropertyList(PropertyStrings);

		/// <summary>Hinterlegter Parameter.</summary>
		public PropertyDictionary Parent { get { return parentDictionary; } set { parentDictionary = value; } }

		/// <summary>Gibt die Anzahl der hinterlegten Parameter zurück.</summary>
		public int Count { get { return properties.Count; } }

		#region -- IEnumerable Member -----------------------------------------------------

		IEnumerator<PropertyValue> IEnumerable<PropertyValue>.GetEnumerator()
		{
			foreach (var c in properties.Values)
				yield return c;

			if (parentDictionary != null)
			{
				foreach (var c in parentDictionary)
					yield return c;
			}
		} // func GetEnumerator

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
			=> ((IEnumerable<PropertyValue>)this).GetEnumerator();

		#endregion

		public static IPropertyEnumerableDictionary EmptyReadOnly { get; } = new EmptyReadOnlyDictionary();
	} // class PropertyDictionary

	#endregion

	#region -- class XAttributesPropertyDictionary --------------------------------------

	public sealed class XAttributesPropertyDictionary : IPropertyReadOnlyDictionary
	{
		private readonly XElement x;

		public XAttributesPropertyDictionary(XElement x)
			=> this.x = x;

		public bool TryGetProperty(string name, out object value)
		{
			var a = x.Attribute(name);
			if(a != null && a.Value != null)
			{
				value = a.Value;
				return true;
			}
			else
			{
				value = null;
				return false;
			}
		} // func TryGetProperty
	} // class XAttributesPropertyDictionary

	#endregion

	public static class PropertyDictionaryExtensions
	{
		#region -- GetProperty ------------------------------------------------------------

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public static object GetProperty(this IPropertyReadOnlyDictionary propertyDictionary, string name, object @default)
			=> propertyDictionary.TryGetProperty(name, out var r) ? r : @default;

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public static string GetProperty(this IPropertyReadOnlyDictionary propertyDictionary, string name, string @default)
			=> propertyDictionary.TryGetProperty(name, out string r) ? r : @default;

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <typeparam name="T">Rückgabewert.</typeparam>
		/// <param name="name">Parametername.</param>
		/// <param name="def">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public static T GetProperty<T>(this IPropertyReadOnlyDictionary propertyDictionary, string name, T @default)
			=> propertyDictionary.TryGetProperty(name, out T r) ? r : @default;

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <typeparam name="T">Rückgabewert.</typeparam>
		/// <param name="name">Parametername.</param>
		/// <param name="def">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public static T GetPropertyLate<T>(this IPropertyReadOnlyDictionary propertyDictionary, string name, Func<T> @default)
			=> propertyDictionary.TryGetProperty(name, out T r) ? r : @default();

		/// <summary>Versucht einen Paremter zurückzugeben.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="value">Wert der abgelegt wurde.</param>
		/// <returns><c>true</c>, wenn ein Wert gefunden wurde.</returns>
		public static bool TryGetProperty(this IPropertyReadOnlyDictionary propertyDictionary, string name, out string value)
		{
			if (propertyDictionary.TryGetProperty(name, out var ret) && ret != null)
			{
				value = ret.ToString();
				return true;
			}
			else
			{
				value = String.Empty;
				return false;
			}
		} // func TryGetProperty

		/// <summary>Versucht einen Paremter zurückzugeben.</summary>
		/// <typeparam name="T">Rückgabewert.</typeparam>
		/// <param name="name">Parametername.</param>
		/// <param name="value">Wert der abgelegt wurde.</param>
		/// <returns><c>true</c>, wenn ein Wert gefunden wurde.</returns>
		public static bool TryGetProperty<T>(this IPropertyReadOnlyDictionary propertyDictionary, string name, out T value)
		{
			if (propertyDictionary.TryGetProperty(name, out var ret) && ret != null)
			{
				try
				{
					if (ret is T)
						value = (T)ret;
					else
						value = (T)Procs.ChangeType(ret, typeof(T));
					return true;
				}
				catch (FormatException)
				{
				}
			}
			value = default(T);
			return false;
		} // func TryGetProperty

		#endregion
	} // class PropertyDictionaryExtensions

	public partial class Procs
	{
		#region -- PropertyList -----------------------------------------------------------

		private static string ReadStr(string cur, char terminator, ref int index)
		{
			StringBuilder sb = new StringBuilder();
			bool lInQuote = false;
			while (index < cur.Length && (lInQuote || cur[index] != terminator))
			{
				char cCur = cur[index];
				if (cCur == '"')
					if (lInQuote)
					{
						cCur = cur[++index];
						if (cCur == terminator)
							break;
						else if (cCur == '"')
							sb.Append('"');
						else
						{
							sb.Append(cCur);
							lInQuote = false;
						}
					}
					else
						lInQuote = true;
				else
					sb.Append(cCur);
				index++;
			}
			index++;
			return sb.ToString();
		} // func ReadStr

		/// <overloads>Splits a list of properties.</overloads>
		/// <summary>Splits a list of properties with the format <c>Param1=Value1;Param2=Value2</c>.</summary>
		/// <param name="items"></param>
		/// <returns></returns>
		public static IEnumerable<KeyValuePair<string, string>> SplitPropertyList(string items)
		{
			return SplitPropertyList(items, ';');
		} // func SplitPropertyList

		/// <summary></summary>
		/// <param name="items"></param>
		/// <param name="sepChar"></param>
		/// <returns></returns>
		public static IEnumerable<KeyValuePair<string, string>> SplitPropertyList(string items, char sepChar)
		{
			var index = 0;
			if (items != null)
			{
				items = items.Replace("\\n", Environment.NewLine);
				while (index < items.Length)
					yield return new KeyValuePair<string, string>(ReadStr(items, '=', ref index), ReadStr(items, sepChar, ref index));
			}
		} // func SplitPropertyList

		private static string GetValue(string value)
		{
			value = value.Replace("\n", "\\n").Replace("\r", "");
			if (value.IndexOf('"') >= 0)
				value = '"' + value.Replace("\"", "\"\"") + '"';
			return value;
		} // func GetValue

		/// <summary></summary>
		/// <param name="items"></param>
		/// <returns></returns>
		public static string JoinPropertyList(IEnumerable<KeyValuePair<string, string>> items)
		{
			StringBuilder sb = new StringBuilder();
			foreach (KeyValuePair<string, string> oCur in items)
				sb.Append(GetValue(oCur.Key)).Append('=').Append(GetValue(oCur.Value)).Append(';');
			return sb.ToString();
		} // func JoinPropertyList

		#endregion
	}
}
