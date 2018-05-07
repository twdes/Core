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
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TecWare.DE.Stuff
{
	#region -- class PropertyValue ----------------------------------------------------

	/// <summary></summary>
	public sealed class PropertyValue
	{
		private readonly string name;
		private readonly Type type;
		private readonly object value;

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		public PropertyValue(string name, object value)
			: this(name, value?.GetType(), value)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="type"></param>
		/// <param name="value"></param>
		public PropertyValue(string name, Type type, object value)
		{
			if (String.IsNullOrEmpty(name))
				throw new ArgumentNullException("name");

			this.name = name;
			this.type = type ?? typeof(object);
			this.value = Procs.ChangeType(value, this.type);
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> $"{name}:{type.Name} = {value}";

		/// <summary>Property name.</summary>
		public string Name => name;
		/// <summary>Type of the property value.</summary>
		public Type Type => type;
		/// <summary>Value</summary>
		public object Value => value;

		/// <summary></summary>
		public static PropertyValue[] EmptyArray { get; } = Array.Empty<PropertyValue>();
	} // class PropertyValue

	#endregion

	#region -- interface IPropertyReadOnlyDictionary ----------------------------------

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

	#region -- interface IPropertyEnumerableDictionary --------------------------------

	/// <summary></summary>
	public interface IPropertyEnumerableDictionary : IPropertyReadOnlyDictionary, IEnumerable<PropertyValue>
	{
	} // interface IPropertyEnumerableDictionary

	#endregion

	#region -- class PropertyDictionary -----------------------------------------------

	/// <summary></summary>
	public sealed class PropertyDictionary : IPropertyEnumerableDictionary, IPropertyReadOnlyDictionary, IEnumerable<PropertyValue>
	{
		#region -- class EmptyReadOnlyDictionary --------------------------------------

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

		#region -- class EmptyEnumerableDictionary ------------------------------------

		private sealed class EmptyEnumerableDictionary : IPropertyEnumerableDictionary
		{
			private readonly IPropertyReadOnlyDictionary properties;

			public EmptyEnumerableDictionary(IPropertyReadOnlyDictionary properties)
			{
				this.properties = properties ?? throw new ArgumentNullException(nameof(properties));
			} // ctor

			public IEnumerator<PropertyValue> GetEnumerator()
			{
				yield break;
			} // func GetEnumerator

			public bool TryGetProperty(string name, out object value)
				=> properties.TryGetProperty(name, out value);

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();
		} //class EmptyReadOnlyDictionary

		#endregion

		private PropertyDictionary parentDictionary = null;
		private Dictionary<string, PropertyValue> properties = new Dictionary<string, PropertyValue>(StringComparer.OrdinalIgnoreCase);

		#region -- Ctor ---------------------------------------------------------------

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
		/// <param name="parameterList">Parameterliste als Zeichenfolge konvertiert.</param>
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

		#region -- SetProperty, AddRange ----------------------------------------------

		/// <summary></summary>
		/// <param name="value"></param>
		public void SetProperty(PropertyValue value)
		{
			lock (properties)
				properties[value.Name] = value;
		} // proc SetProperty

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		public void SetProperty(string name, object value) 
			=> SetProperty(new PropertyValue(name, null, value));

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="type"></param>
		/// <param name="value"></param>
		public void SetProperty(string name, Type type, object value)
			=> SetProperty(new PropertyValue(name, type, value));

		/// <summary></summary>
		/// <param name="args"></param>
		public void AddRange(IEnumerable<KeyValuePair<string, string>> args)
		{
			if (args != null)
			{
				foreach (var c in args)
					SetProperty(c.Key, typeof(string), c.Value);
			}
		} // proc AddRange

		/// <summary></summary>
		/// <param name="args"></param>
		public void AddRange(IEnumerable<KeyValuePair<string, object>> args)
		{
			if (args != null)
			{
				foreach (var c in args)
					SetProperty(c.Key, null, c.Value);
			}
		} // proc AddRange

		/// <summary></summary>
		/// <param name="args"></param>
		public void AddRange(IEnumerable<PropertyValue> args)
		{
			if (args != null)
			{
				foreach (var c in args)
					SetProperty(c);
			}
		} // proc AddRange

		#endregion

		#region -- GetProperty --------------------------------------------------------

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public object GetProperty(string name, object @default)
			=> TryGetProperty(name, out object r) ? r : @default;

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public string GetProperty(string name, string @default)
			=> TryGetProperty(name, out string r) ? r : @default;

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <typeparam name="T">Rückgabewert.</typeparam>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public T GetProperty<T>(string name, T @default)
			=> TryGetProperty(name, out T r) ? r : @default;
		
		/// <summary>Versucht einen Paremter zurückzugeben.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="value">Wert der abgelegt wurde.</param>
		/// <param name="type">Orignal abgelegter Datentyp des Werts.</param>
		/// <returns><c>true</c>, wenn ein Wert gefunden wurde.</returns>
		public bool TryGetProperty(string name, out object value, out Type type)
		{
			if (!String.IsNullOrEmpty(name))
			{
				if (properties.TryGetValue(name, out var p))
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
			=> TryGetProperty(name, out value, out var type);
		
		/// <summary>Versucht einen Paremter zurückzugeben.</summary>
		/// <param name="name">Parametername.</param>
		/// <param name="value">Wert der abgelegt wurde.</param>
		/// <returns><c>true</c>, wenn ein Wert gefunden wurde.</returns>
		public bool TryGetProperty(string name, out string value)
		{
			if (TryGetProperty(name, out object ret) && ret != null)
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
			if (TryGetProperty(name, out object ret) && ret != null)
			{
				try
				{
					value = ret is T
						? (T)ret
						: (T)Procs.ChangeType(ret, typeof(T));
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

		#region -- Clear, Remove, Contains --------------------------------------------

		/// <summary>Löscht alle Parameter der aktuellen Ebene</summary>
		public void Clear()
			=> properties.Clear();

		/// <summary>Löscht den Parameter aus dem Dictionary</summary>
		/// <param name="name">Name des Parameters.</param>
		public void Remove(string name)
			=> properties.Remove(name);

		/// <summary>Existiert dieser Parameter</summary>
		/// <param name="name">Prüft, ob der Parameter vorhanden ist.</param>
		/// <returns><c>true</c>, wenn der Parameter gefunden wurde.</returns>
		public bool Contains(string name)
			=> Contains(name, true);

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
			get => GetProperty(name, null);
			set => SetProperty(name, value);
		} // prop this

		/// <summary></summary>
		public IEnumerable<KeyValuePair<string, object>> PropertyObjects => from c in properties select new KeyValuePair<string, object>(c.Key, c.Value.Value);
		/// <summary></summary>
		public IEnumerable<KeyValuePair<string, string>> PropertyStrings => from c in properties select new KeyValuePair<string, string>(c.Key, (string)Procs.ChangeType(c.Value.Value, typeof(string)));
		/// <summary>Gibt das Dictionary als Property-List zurück. Die Information zum Originaldatentyp geht dabei verloren.</summary>
		public string PropertyList => Procs.JoinPropertyList(PropertyStrings);

		/// <summary>Hinterlegter Parameter.</summary>
		public PropertyDictionary Parent { get => parentDictionary; set => parentDictionary = value; }

		/// <summary>Gibt die Anzahl der hinterlegten Parameter zurück.</summary>
		public int Count { get { return properties.Count; } }

		#region -- IEnumerable Member -------------------------------------------------

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

		/// <summary></summary>
		/// <param name="properties"></param>
		/// <returns></returns>
		public static IPropertyEnumerableDictionary ToEmptyEnumerator(IPropertyReadOnlyDictionary properties)
			=> new EmptyEnumerableDictionary(properties);

		/// <summary></summary>
		public static IPropertyEnumerableDictionary EmptyReadOnly { get; } = new EmptyReadOnlyDictionary();
	} // class PropertyDictionary

	#endregion

	#region -- class XAttributesPropertyDictionary ------------------------------------

	/// <summary>Property dictionary of a "attribute" xml-node.</summary>
	public sealed class XAttributesPropertyDictionary : IPropertyReadOnlyDictionary
	{
		private readonly XElement x;

		/// <summary></summary>
		/// <param name="x"></param>
		public XAttributesPropertyDictionary(XElement x)
			=> this.x = x;

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="value"></param>
		/// <returns></returns>
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

	#region -- class PropertyDictionaryExtensions -------------------------------------

	/// <summary></summary>
	public static class PropertyDictionaryExtensions
	{
		#region -- GetProperty --------------------------------------------------------

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <param name="propertyDictionary"></param>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public static object GetProperty(this IPropertyReadOnlyDictionary propertyDictionary, string name, object @default)
			=> propertyDictionary.TryGetProperty(name, out var r) ? r : @default;

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <param name="propertyDictionary"></param>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public static string GetProperty(this IPropertyReadOnlyDictionary propertyDictionary, string name, string @default)
			=> propertyDictionary.TryGetProperty(name, out string r) ? r : @default;

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <typeparam name="T">Rückgabewert.</typeparam>
		/// <param name="propertyDictionary"></param>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public static T GetProperty<T>(this IPropertyReadOnlyDictionary propertyDictionary, string name, T @default)
			=> propertyDictionary.TryGetProperty(name, out T r) ? r : @default;

		/// <summary>Gibt einen Parameter zurück.</summary>
		/// <typeparam name="T">Rückgabewert.</typeparam>
		/// <param name="propertyDictionary"></param>
		/// <param name="name">Parametername.</param>
		/// <param name="default">Defaultwert, falls der Wert nicht ermittelt werden konnte.</param>
		/// <returns>Abgelegter Wert oder der Default-Wert.</returns>
		public static T GetPropertyLate<T>(this IPropertyReadOnlyDictionary propertyDictionary, string name, Func<T> @default)
			=> propertyDictionary.TryGetProperty(name, out T r) ? r : @default();

		/// <summary>Versucht einen Paremter zurückzugeben.</summary>
		/// <param name="propertyDictionary"></param>
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
		/// <param name="propertyDictionary"></param>
		/// <param name="name">Parametername.</param>
		/// <param name="value">Wert der abgelegt wurde.</param>
		/// <returns><c>true</c>, wenn ein Wert gefunden wurde.</returns>
		public static bool TryGetProperty<T>(this IPropertyReadOnlyDictionary propertyDictionary, string name, out T value)
		{
			if (propertyDictionary.TryGetProperty(name, out var ret) && ret != null)
			{
				try
				{
					value = ret is T
						? (T)ret
						: (T)Procs.ChangeType(ret, typeof(T));
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

	#endregion

	#region -- class Procs ------------------------------------------------------------

	public partial class Procs
	{
		#region -- PropertyList -------------------------------------------------------

		private static string ReadStr(string cur, char terminator, ref int index)
		{
			var sb = new StringBuilder();
			var inQuote = false;
			while (index < cur.Length && (inQuote || cur[index] != terminator))
			{
				var c = cur[index];
				if (c == '"')
					if (inQuote)
					{
						c = cur[++index];
						if (c == terminator)
							break;
						else if (c == '"')
							sb.Append('"');
						else
						{
							sb.Append(c);
							inQuote = false;
						}
					}
					else
						inQuote = true;
				else
					sb.Append(c);
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
			=> SplitPropertyList(items, ';');
		
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
			var sb = new StringBuilder();
			foreach (var cur in items)
				sb.Append(GetValue(cur.Key)).Append('=').Append(GetValue(cur.Value)).Append(';');
			return sb.ToString();
		} // func JoinPropertyList

		/// <summary></summary>
		/// <param name="properties"></param>
		/// <returns></returns>
		public static IPropertyEnumerableDictionary ToProperties(object properties)
		{
			switch (properties)
			{
				case null:
					return PropertyDictionary.EmptyReadOnly;
				case Neo.IronLua.LuaTable t:
					return new LuaTableProperties(t);
				case IPropertyEnumerableDictionary f:
					return f;
				case IPropertyReadOnlyDictionary d:
					return PropertyDictionary.ToEmptyEnumerator(d);

				case string values2:
					return new PropertyDictionary(values2);
				case PropertyValue[] values1:
					return new PropertyDictionary(values1);
				case KeyValuePair<string, string> values3:
					return new PropertyDictionary(values3);
				case KeyValuePair<string, object> values4:
					return new PropertyDictionary(values4);
				default:
					throw new ArgumentException(nameof(properties));
			}
		} // func GetDictionaryProperties

		#endregion
	}

	#endregion
}
