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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace TecWare.DE.Stuff
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static partial class Procs
	{
		#region -- ReaderSettings, WriterSettings ---------------------------------------

		/// <summary>Gibt Standard Settings zum Lesen von Xml-Dateien zurück</summary>
		public static XmlReaderSettings XmlReaderSettings
		{
			get
			{
				var settings = new XmlReaderSettings();
				settings.IgnoreComments = true;
				settings.IgnoreWhitespace = true;
				settings.CloseInput = true;
				return settings;
			}
		} // prop ReadSettings

		/// <summary>Gibt einen Standard Settings zum Schreiben von Xml-Dateien zurück.</summary>
		public static XmlWriterSettings XmlWriterSettings
		{
			get
			{
				XmlWriterSettings settings = new XmlWriterSettings();
				settings.CloseOutput = true;
				settings.CheckCharacters = true;
				settings.Encoding = Encoding.UTF8;
				settings.Indent = true;
				settings.IndentChars = "  ";
				settings.NewLineChars = Environment.NewLine;
				settings.NewLineHandling = NewLineHandling.Entitize;
				settings.NewLineOnAttributes = false;
				return settings;
			}
		} // prop WriterSettings

		#endregion

		#region -- GetAttribute, CreateAttribute ----------------------------------------

		public static string GetAttribute(this XmlReader xml, XName attributeName, string @default)
		{
			if (xml == null)
				return @default;


			return xml.GetAttribute(attributeName.LocalName) ?? @default;
		} // func GetAttribute

		public static T GetAttribute<T>(this XmlReader xml, XName attributeName, T @default)
		{
			try
			{
				var value = xml.GetAttribute(attributeName.LocalName);
				return value == null ? @default : value.ChangeType<T>();
			}
			catch
			{
				return @default;
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
		{
			try
			{
				string sValue = GetAttribute(x, attributeName, (string)null);
				if (sValue == null)
					return @default;

				return Procs.ChangeType<T>(sValue);
			}
			catch
			{
				return @default;
			}
		} // func GetAttribute

		/// <summary>Erzeugt ein Attribut für ein XElement.</summary>
		/// <typeparam name="T">Datentyp von dem Konvertiert werden soll.</typeparam>
		/// <param name="attributeName">Name des Attributes</param>
		/// <param name="value">Wert des Attributes</param>
		/// <returns>Attribut oder null.</returns>
		public static XAttribute XAttributeCreate<T>(string attributeName, T value)
		{
			return XAttributeCreate<T>(attributeName, value, default(T));
		} // func XAttributeCreate

		/// <summary>Erzeugt ein Attribut für ein XElement.</summary>
		/// <typeparam name="T">Datentyp von dem Konvertiert werden soll.</typeparam>
		/// <param name="attributeName">Name des Attributes</param>
		/// <param name="value">Wert des Attributes</param>
		/// <param name="default">Ist der Wert gleich dem Default-Wert, wird null zurückgegeben.</param>
		/// <returns>Attribut oder null.</returns>
		public static XAttribute XAttributeCreate<T>(string attributeName, T value, T @default)
		{
			if (Object.Equals(value, @default))
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
				object r = x.Annotation(typeAnnotation);
				if (r != null)
					return r;

				if (x.Parent == null)
					if (x is XDocument)
						break;
					else
						x = x.Document;
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

		#region -- GetNode --------------------------------------------------------------

		/// <summary></summary>
		/// <param name="x"></param>
		/// <param name="names"></param>
		/// <returns></returns>
		public static XElement GetElement(this XElement x, params XName[] names)
		{
			XElement r = x;
			for (int i = 0; i < names.Length; i++)
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
		/// <param name="sDefault">Wird das Element nicht gefunden, wird dieser Wert zurück gegeben.</param>
		/// <returns>Wert oder der default-Wert.</returns>
		public static string GetNode(this XElement x, XName elementName, string sDefault)
		{
			if (x == null)
				return sDefault;

			XElement attr = x.Element(elementName);
			if (attr == null)
				return sDefault;
			else
				return attr.Value;
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
				string sValue = GetNode(x, elementName, (string)null);
				if (sValue == null)
					return @default;
				return (T)Convert.ChangeType(sValue, typeof(T), CultureInfo.InvariantCulture);
			}
			catch
			{
				return @default;
			}
		} // func GetNode

		#endregion

		#region -- CompareNode ----------------------------------------------------------

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
				foreach (XAttribute attrA in a.Attributes())
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

		#region -- MergeAttributes ------------------------------------------------------

		/// <summary>Simple merge of two xml attributes.</summary>
		/// <param name="xTarget"></param>
		/// <param name="xSource"></param>
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
				else if (!Object.Equals(xSrcAttr.Value, xTargetAttr.Value))
				{
					xTargetAttr.Value = xSrcAttr.Value;
					isChanged = true;
				}
			}
		} // proc MergeNodes

		#endregion

		#region -- GetStrings -----------------------------------------------------------

		private static string[] EmptyArray(string[] a, bool emptyArrayToNull)
		{
			if (a == null)
				return emptyArrayToNull ? null : new string[0];
			else if (a.Length == 0)
				return emptyArrayToNull ? null : a;
			else
				return a;
		} // func EmptyArray

		public static string[] GetStrings(this XElement x, XName attribute, bool emptyArrayToNull = false)
		{
			var list = x?.GetAttribute(attribute, (string)null);
			if (String.IsNullOrEmpty(list))
				return EmptyArray(null, emptyArrayToNull);
			else
				return EmptyArray(list.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries), emptyArrayToNull);
		} // func GetStrings

		#endregion

		#region -- GetPaths -------------------------------------------------------------

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

		public static string[] GetPaths(this XElement x, XName attribute, bool emptyArrayToNull = false)
		{
			var value = x?.Attribute(attribute)?.Value;
			if (String.IsNullOrEmpty(value))
				return EmptyArray(null, emptyArrayToNull);
			else
				return EmptyArray(SplitPaths(value).ToArray(), emptyArrayToNull);
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
}
