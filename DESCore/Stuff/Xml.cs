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
using System.Xml;
using System.Xml.Linq;

namespace TecWare.DE.Stuff
{
	#region -- class Procs ------------------------------------------------------------

	/// <summary></summary>
	public static partial class Procs
	{
		#region -- ReaderSettings, WriterSettings -------------------------------------

		/// <summary>Gibt Standard Settings zum Lesen von Xml-Dateien zurück</summary>
		public static XmlReaderSettings XmlReaderSettings
		{
			get
			{
				var settings = new XmlReaderSettings()
				{
					IgnoreComments = true,
					IgnoreWhitespace = true,
					CloseInput = true
				};
				return settings;
			}
		} // prop ReadSettings

		/// <summary>Gibt einen Standard Settings zum Schreiben von Xml-Dateien zurück.</summary>
		public static XmlWriterSettings XmlWriterSettings
		{
			get
			{
				var settings = new XmlWriterSettings()
				{
					CloseOutput = true,
					CheckCharacters = true,
					Encoding = Encoding.UTF8,
					Indent = true,
					IndentChars = "  ",
					NewLineChars = Environment.NewLine,
					NewLineHandling = NewLineHandling.Entitize,
					NewLineOnAttributes = false
				};
				return settings;
			}
		} // prop WriterSettings

		#endregion

		#region -- GetAttribute, CreateAttribute --------------------------------------

		private static T ConvertXmlContent<T>(object value, T @default)
		{
			try
			{
				return value == null ? @default : value.ChangeType<T>();
			}
			catch
			{
				return @default;
			}
		} // func ConvertXmlContent

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public static string GetElementContent(this XmlReader xml, string @default)
			=> xml.HasValue
				? xml.ReadContentAsString()
				: @default;

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public static Task<string> GetElementContentAsync(this XmlReader xml, string @default)
			=> xml.HasValue
				? xml.ReadContentAsStringAsync()
				: Task.FromResult(@default);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xml"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public static T GetElementContent<T>(this XmlReader xml, T @default)
			=> ConvertXmlContent(GetElementContent(xml, (string)null), @default);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xml"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public static async Task<T> GetElementContentAsync<T>(this XmlReader xml, T @default)
			=> ConvertXmlContent(await GetElementContentAsync(xml, (string)null), @default);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xml"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public static T ReadElementContent<T>(this XmlReader xml, T @default)
			=> ConvertXmlContent(xml.ReadElementContentAsString(), @default);

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xml"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public static async Task<T> ReadElementContentAsync<T>(this XmlReader xml, T @default)
			=> ConvertXmlContent(await xml.ReadElementContentAsStringAsync(), @default);

		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		/// <param name="lineInfo"></param>
		/// <returns></returns>
		public static XmlException CreateXmlException(string message, Exception innerException, IXmlLineInfo lineInfo)
			=> new XmlException(message, innerException, lineInfo?.LineNumber ?? 0, lineInfo?.LinePosition ?? 0);

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="expectedNodeType"></param>
		/// <returns></returns>
		public static async Task MoveToContentAsync(this XmlReader xml, XmlNodeType expectedNodeType)
		{
			if (await xml.MoveToContentAsync() != expectedNodeType)
				throw CreateXmlException(String.Format("Invalid node type (expected: {0}, found: {1})", expectedNodeType, xml.NodeType), null, xml as IXmlLineInfo);
		} // proc MoveToContentAsync

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static async Task ReadStartElementAsync(this XmlReader xml, string name)
		{
			await MoveToContentAsync(xml, XmlNodeType.Element);

			if(xml.Name != name)
				throw CreateXmlException(String.Format("Element not found (expected: {0}, found: {1})", name, xml.Name), null, xml as IXmlLineInfo);

			await xml.ReadAsync();
		} // proc ReadStartElementAsync

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <returns></returns>
		public static async Task ReadEndElementAsync(this XmlReader xml)
		{
			await MoveToContentAsync(xml, XmlNodeType.EndElement);
			await xml.ReadAsync();
		} // proc ReadEndElementAsync

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="attributeName"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public static string GetAttribute(this XmlReader xml, XName attributeName, string @default)
		{
			if (xml == null)
				return @default;


			return xml.GetAttribute(attributeName.LocalName) ?? @default;
		} // func GetAttribute

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xml"></param>
		/// <param name="attributeName"></param>
		/// <param name="default"></param>
		/// <returns></returns>
		public static T GetAttribute<T>(this XmlReader xml, XName attributeName, T @default)
			=> TryGetAttribute<T>(xml, attributeName, out var t) ? t : @default;

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="xml"></param>
		/// <param name="attributeName"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool TryGetAttribute<T>(this XmlReader xml, XName attributeName, out T value)
		{
			try
			{
				var valueString = xml.GetAttribute(attributeName.LocalName);
				if (valueString == null)
				{
					value = default(T);
					return false;
				}
				value = valueString.ChangeType<T>();
				return true;
			}
			catch
			{
				value = default(T);
				return false;
			}
		} // func GetAttribute

		/// <summary>Gibt den Inhalt eines Attributes zurück.</summary>
		/// <param name="x">XElement, an dem das Attribut erwartet wird.</param>
		/// <param name="attributeName">Name des Attributes.</param>
		/// <param name="default">Wird das Attribut nicht gefunden, wird dieser Wert zurück gegeben.</param>
		/// <returns>Wert oder der default-Wert.</returns>
		public static string GetAttribute(this XElement x, XName attributeName, string @default)
		{
			if (x == null)
				return @default;

			XAttribute attr = x.Attribute(attributeName);
			if (attr == null)
				return @default;
			else
				return attr.Value;
		} // func GetAttribute

		/// <summary>Gibt den Inhalt eines Attributes typiriesiert zurück.</summary>
		/// <typeparam name="T">Datentyp der erwartet wird.</typeparam>
		/// <param name="x">XElement, an dem das Attribut erwartet wird.</param>
		/// <param name="attributeName">Name des Attributes.</param>
		/// <param name="default">Defaultwert, falls das Attribut nicht vorhanden ist oder der Wert nicht in den Typ konvertiert werden konnte.</param>
		/// <returns>Wert oder der default-Wert.</returns>
		public static T GetAttribute<T>(this XElement x, XName attributeName, T @default)
			=> TryGetAttribute<T>(x, attributeName, out var t) ? t : @default;

		/// <summary></summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="x"></param>
		/// <param name="attributeName"></param>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool TryGetAttribute<T>(this XElement x, XName attributeName, out T value)
		{
			try
			{
				var valueString = GetAttribute(x, attributeName, (string)null);
				if (valueString == null)
				{
					value = default(T);
					return false;
				}

				value = Procs.ChangeType<T>(valueString);
				return true;
			}
			catch
			{
				value = default(T);
				return false;
			}

		} // func TryGetAttribute

		/// <summary>Erzeugt ein Attribut für ein XElement.</summary>
		/// <typeparam name="T">Datentyp von dem Konvertiert werden soll.</typeparam>
		/// <param name="attributeName">Name des Attributes</param>
		/// <param name="value">Wert des Attributes</param>
		/// <returns>Attribut oder null.</returns>
		public static XAttribute XAttributeCreate<T>(string attributeName, T value)
			=> XAttributeCreate<T>(attributeName, value, default(T));
		
		/// <summary>Erzeugt ein Attribut für ein XElement.</summary>
		/// <typeparam name="T">Datentyp von dem Konvertiert werden soll.</typeparam>
		/// <param name="attributeName">Name des Attributes</param>
		/// <param name="value">Wert des Attributes</param>
		/// <param name="default">Ist der Wert gleich dem Default-Wert, wird null zurückgegeben.</param>
		/// <returns>Attribut oder null.</returns>
		public static XAttribute XAttributeCreate<T>(string attributeName, T value, T @default)
		{
			if (Equals(value, @default))
				return null;
			if (value == null)
				return null;

			return new XAttribute(attributeName, value);
		} // func XAttributeCreate

		/// <summary>Sucht die angegebene Annotation.</summary>
		/// <param name="x"></param>
		/// <param name="typeAnnotation"></param>
		/// <returns></returns>
		public static object FindAnnotation(this XObject x, Type typeAnnotation)
		{
			if (typeAnnotation == null || x == null)
				return null;

			while (x != null)
			{
				var r = x.Annotation(typeAnnotation);
				if (r != null)
					return r;

				if (x.Parent == null)
				{
					if (x is XDocument)
						break;
					else
						x = x.Document;
				}
				else
					x = x.Parent;
			}

			return null;
		} // func FindAnnotation

		/// <summary>Kopiert die Annotationen</summary>
		/// <param name="xSource"></param>
		/// <param name="xDestination"></param>
		public static void XCopyAnnotations(XElement xSource, XNode xDestination)
		{
			if (xSource == null || xDestination == null)
				return;

			XCopyAnnonation(xSource, xDestination, typeBaseUriAnnotation, true);
			if (xSource != xDestination)
			{
				XCopyAnnonation(xSource, xDestination, typeLineInfoAnnotation, false);
				XCopyAnnonation(xSource, xDestination, typeLineInfoEndElementAnnotation, false);
			}
		} // proc XCopyAnnotations

		private static void XCopyAnnonation(XElement xSource, XNode xDestination, Type typeAnnotation, bool recursive)
		{
			object baseUri = recursive ? xSource.FindAnnotation(typeAnnotation) : xSource.Annotation(typeAnnotation);
			if (baseUri != null)
			{
				xDestination.RemoveAnnotations(typeAnnotation);
				xDestination.AddAnnotation(baseUri);
			}
		} // func XCopyAnnonation

		#endregion

		#region -- GetNode ------------------------------------------------------------

		/// <summary></summary>
		/// <param name="x"></param>
		/// <param name="names"></param>
		/// <returns></returns>
		public static XElement GetElement(this XElement x, params XName[] names)
		{
			var r = x;
			for (var i = 0; i < names.Length; i++)
			{
				r = r.Element(names[i]);
				if (r == null)
					return null;
			}
			return r;
		} // func GetElement

		/// <summary>Gibt den Inhalt eines direkt untergeordneten Elementes zurück typiriesiert zurück.</summary>
		/// <param name="x">XElement, an dem das Attribut erwartet wird.</param>
		/// <param name="elementName">Name des Elementes.</param>
		/// <param name="default">Wird das Element nicht gefunden, wird dieser Wert zurück gegeben.</param>
		/// <returns>Wert oder der default-Wert.</returns>
		public static string GetNode(this XElement x, XName elementName, string @default)
		{
			if (x == null)
				return @default;

			var attr = x.Element(elementName);
			return attr?.Value ?? @default;
		} // func GetNode

		/// <summary>Gibt den Inhalt eines direkt untergeordneten Elementes zurück typiriesiert zurück.</summary>
		/// <typeparam name="T">Datentyp der erwartet wird.</typeparam>
		/// <param name="x">XElement, an dem das Element erwartet wird.</param>
		/// <param name="elementName">Name des Elementes.</param>
		/// <param name="default">Defaultwert, falls das Element nicht vorhanden ist oder der Wert nicht in den Typ konvertiert werden konnte.</param>
		/// <returns>Wert oder der default-Wert.</returns>
		public static T GetNode<T>(this XElement x, XName elementName, T @default)
		{
			try
			{
				var value = GetNode(x, elementName, (string)null);
				if (value == null)
					return @default;
				return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
			}
			catch
			{
				return @default;
			}
		} // func GetNode

		#endregion

		#region -- CompareNode --------------------------------------------------------

		/// <summary>Vergleicht zwei Xml-Knoten.</summary>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <returns></returns>
		public static bool CompareNode(XElement a, XElement b)
		{
			if (a == null && b == null)
				return true;
			else if (a == null && b != null)
				return false;
			else if (a != null && b == null)
				return false;
			else
			{
				// Name
				if (a.Name != b.Name)
					return false;
				if (a.Value != b.Value)
					return false;

				// Attribute
				foreach (var attrA in a.Attributes())
				{
					var attrB = b.Attribute(attrA.Name);
					if (attrB == null)
						return false;
					else if (attrA.Value != attrB.Value)
						return false;
				}

				// Elemente
				var elementsA = a.Elements().ToArray();
				var elementsB = b.Elements().ToArray();

				if (elementsA.Length != elementsB.Length)
					return false;

				for (var i = 0; i < elementsA.Length; i++)
				{
					if (!CompareNode(elementsA[i], elementsB[i]))
						return false;
				}

				return true;
			}
		} // func CompareNode

		#endregion

		#region -- MergeAttributes ----------------------------------------------------

		/// <summary>Simple merge of two xml attributes.</summary>
		/// <param name="xTarget"></param>
		/// <param name="xSource"></param>
		/// <param name="isChanged"></param>
		public static void MergeAttributes(XElement xTarget, XElement xSource, ref bool isChanged)
		{
			// merge attribues
			foreach (var xSrcAttr in xSource.Attributes())
			{
				// check if the attribute exists
				var xTargetAttr = xTarget.Attribute(xSrcAttr.Name);
				if (xTargetAttr == null)
				{
					xTarget.Add(new XAttribute(xSrcAttr.Name, xSrcAttr.Value));
					isChanged = true;
				}
				else if (!Equals(xSrcAttr.Value, xTargetAttr.Value))
				{
					xTargetAttr.Value = xSrcAttr.Value;
					isChanged = true;
				}
			}
		} // proc MergeNodes

		#endregion

		#region -- GetStrings ---------------------------------------------------------

		private static string[] EmptyArray(string[] a, bool emptyArrayToNull)
		{
			if (a == null)
				return emptyArrayToNull ? null : new string[0];
			else if (a.Length == 0)
				return emptyArrayToNull ? null : a;
			else
				return a;
		} // func EmptyArray

		/// <summary></summary>
		/// <param name="x"></param>
		/// <param name="attribute"></param>
		/// <param name="emptyArrayToNull"></param>
		/// <returns></returns>
		public static string[] GetStrings(this XElement x, XName attribute, bool emptyArrayToNull = false)
		{
			var list = x?.GetAttribute(attribute, (string)null);
			return String.IsNullOrEmpty(list)
				? EmptyArray(null, emptyArrayToNull)
				: EmptyArray(list.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries), emptyArrayToNull);
		} // func GetStrings

		#endregion

		#region -- GetPaths -----------------------------------------------------------

		/// <summary></summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static IEnumerable<string> SplitPaths(string value)
		{
			if (value == null)
				yield break;

			var startAt = 0;
			var pos = 0;
			var inQuote = false;
			while (pos < value.Length)
			{
				if (inQuote)
				{
					if (value[pos] == '"')
					{
						if (startAt < pos)
							yield return value.Substring(startAt, pos - startAt);
						startAt = pos + 1;
						inQuote = false;
					}
				}
				else
				{
					if (Char.IsWhiteSpace(value[pos]) || value[pos] == ';' || value[pos] == ',')
					{
						if (startAt < pos)
							yield return value.Substring(startAt, pos - startAt);
						startAt = pos + 1;
					}
					else if (value[pos] == '"')
					{
						if (startAt < pos)
							yield return value.Substring(startAt, pos - startAt);

						startAt = pos + 1;
						inQuote = true;
					}
				}
				pos++;
			}

			if (startAt < pos)
				yield return value.Substring(startAt, pos - startAt);
		} // func SplitPaths

		/// <summary></summary>
		/// <param name="values"></param>
		/// <returns></returns>
		public static string JoinPaths(IEnumerable<string> values)
		{
			var sb = new StringBuilder();

			var f = true;
			foreach (var c in values)
			{
				if (f)
					f = false;
				else
					sb.Append(' ');

				if (c.IndexOf(' ') >= 0)
					sb.Append('"').Append(c).Append('"');
				else
					sb.Append(c);
			}

			return sb.ToString();
		} // func JoinPaths

		/// <summary></summary>
		/// <param name="x"></param>
		/// <param name="attribute"></param>
		/// <param name="emptyArrayToNull"></param>
		/// <returns></returns>
		public static string[] GetPaths(this XElement x, XName attribute, bool emptyArrayToNull = false)
		{
			var value = x?.Attribute(attribute)?.Value;
			return String.IsNullOrEmpty(value)
				? EmptyArray(null, emptyArrayToNull)
				: EmptyArray(SplitPaths(value).ToArray(), emptyArrayToNull);
		} // func GetPaths

		#endregion

		// -- Ctor ---------------------------------------------------------------

		private static readonly Type typeBaseUriAnnotation;
		private static readonly Type typeLineInfoAnnotation;
		private static readonly Type typeLineInfoEndElementAnnotation;

		static Procs()
		{
			var xobjectTypeName = typeof(XObject).AssemblyQualifiedName;
			typeBaseUriAnnotation = Type.GetType(xobjectTypeName.Replace("XObject", "BaseUriAnnotation"), false);
			typeLineInfoAnnotation = Type.GetType(xobjectTypeName.Replace("XObject", "LineInfoAnnotation"), false);
			typeLineInfoEndElementAnnotation = Type.GetType(xobjectTypeName.Replace("XObject", "LineInfoEndElementAnnotation"), false);
		} // ctor
	} // class Procs

	#endregion
}
