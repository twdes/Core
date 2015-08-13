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

namespace TecWare.DES.Stuff
{
	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static partial class Procs
	{
		#region -- ReaderSettings, WriterSettings -----------------------------------------

		/// <summary>Gibt Standard Settings zum Lesen von Xml-Dateien zurück</summary>
		public static XmlReaderSettings XmlReaderSettings
		{
			get
			{
				XmlReaderSettings settings = new XmlReaderSettings();
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

		#region -- GetAttribute, CreateAttribute ------------------------------------------

		/// <summary>Gibt den Inhalt eines Attributes zurück.</summary>
		/// <param name="x">XElement, an dem das Attribut erwartet wird.</param>
		/// <param name="attributeName">Name des Attributes.</param>
		/// <param name="sDefault">Wird das Attribut nicht gefunden, wird dieser Wert zurück gegeben.</param>
		/// <returns>Wert oder der default-Wert.</returns>
		public static string GetAttribute(this XElement x, XName attributeName, string sDefault)
		{
			if (x == null)
				return sDefault;

			XAttribute attr = x.Attribute(attributeName);
			if (attr == null)
				return sDefault;
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
		} // func XObjectFindAnnotation

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

		private static void XCopyAnnonation(XElement xSource, XNode xDestination, Type typeAnnotation, bool lRecursive)
		{
			object baseUri = lRecursive ? xSource.FindAnnotation(typeAnnotation) : xSource.Annotation(typeAnnotation);
			if (baseUri != null)
			{
				xDestination.RemoveAnnotations(typeAnnotation);
				xDestination.AddAnnotation(baseUri);
			}
		} // func XCopyAnnonation

		#endregion

		#region -- GetNode ----------------------------------------------------------------

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

		#region -- CompareNode ------------------------------------------------------------

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
					XAttribute attrB = b.Attribute(attrA.Name);
					if (attrB == null)
						return false;
					else if (attrA.Value != attrB.Value)
						return false;
				}

				// Elemente
				XElement[] elementsA = a.Elements().ToArray();
				XElement[] elementsB = b.Elements().ToArray();

				if (elementsA.Length != elementsB.Length)
					return false;

				for (int i = 0; i < elementsA.Length; i++)
					if (!CompareNode(elementsA[i], elementsB[i]))
						return false;

				return true;
			}
		} // func CompareNode

		#endregion

		 // -- Ctor ---------------------------------------------------------------

		private static readonly Type typeBaseUriAnnotation;
		private static readonly Type typeLineInfoAnnotation;
		private static readonly Type typeLineInfoEndElementAnnotation;

		static Procs()
		{
			string sXObjectTypeName = typeof(XObject).AssemblyQualifiedName;
			typeBaseUriAnnotation = Type.GetType(sXObjectTypeName.Replace("XObject", "BaseUriAnnotation"), false);
			typeLineInfoAnnotation = Type.GetType(sXObjectTypeName.Replace("XObject", "LineInfoAnnotation"), false);
			typeLineInfoEndElementAnnotation = Type.GetType(sXObjectTypeName.Replace("XObject", "LineInfoEndElementAnnotation"), false);
		} // ctor
	} // class Procs

	public static class MimeTypes
	{
		public const string Text = "text/plain";
		public const string Xml = "text/xml";
		public const string Xaml = "application/xaml+xml";
	} // class MimeTypes

	#region -- class BaseWebReqeust -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class BaseWebReqeust
	{
		private Uri baseUri;
		private Encoding defaultEncoding;

		public BaseWebReqeust(Uri baseUri, Encoding defaultEncoding)
		{
			this.baseUri = baseUri;
			this.defaultEncoding = defaultEncoding;
		} // ctor

		private Encoding CheckMimeType(string contentType, string acceptedMimeType, bool charset)
		{
			string mimeType;

			// Lese den MimeType
			int iPos = contentType.IndexOf(';');
			if (iPos == -1)
				mimeType = contentType.Trim();
			else
				mimeType = contentType.Substring(0, iPos).Trim();

			// Prüfe den MimeType
			if (!mimeType.StartsWith(acceptedMimeType))
				throw new ArgumentException($"Inhalt entspricht nicht der Erwartung. (Erwartet: {acceptedMimeType}; Erhalten: {mimeType})");

			if (charset)
			{
				int startAt = contentType.IndexOf("charset=");
				if (startAt >= 0)
				{
					startAt += 8;
					int endAt = contentType.IndexOf(';', startAt);
					if (endAt == -1)
						endAt = contentType.Length;

					string charSet = contentType.Substring(startAt, endAt - startAt);
					return Encoding.GetEncoding(charSet);
				}
				else
					return defaultEncoding;
			}
			else
				return null;
		} // func CheckMimeType


		private bool IsCompressed(string contentEncoding)
		{
			return contentEncoding != null && contentEncoding.IndexOf("gzip") >= 0;
		} // func IsCompressed

		public async Task<WebResponse> GetResponseAsync(string relativeUri)
		{
			var request = WebRequest.Create(new Uri(baseUri, relativeUri));
			Task.Yield();
			return await request.GetResponseAsync();
		} // func GetResponseAsync

		/// <summary>Creates a plain Web-Request, special arguments are filled with IWebRequestCreate.</summary>
		/// <param name="uri">Resource</param>
		/// <param name="acceptedMimeType">Optional.</param>
		/// <returns></returns>
		public async Task<Stream> GetStreamAsync(string relativeUri, string acceptedMimeType)
		{
			return GetStreamAsync(await GetResponseAsync(relativeUri), acceptedMimeType);
		} // func GetStreamAsync

		public Stream GetStreamAsync(WebResponse response, string acceptedMimeType)
		{
			CheckMimeType(response.ContentType, acceptedMimeType, false);
			if (IsCompressed(response.Headers["Content-Encoding"]))
				return new GZipStream(response.GetResponseStream(), CompressionMode.Decompress, false);
			else
				return response.GetResponseStream();
		} // func GetStreamAsync

		public async Task<TextReader> GetTextReaderAsync(string relativeUri, string acceptedMimeType)
		{
			return GetTextReaderAsync(await GetResponseAsync(relativeUri), acceptedMimeType);
		} // func GetTextReaderAsync

		public TextReader GetTextReaderAsync(WebResponse response, string acceptedMimeType)
		{
			var enc = CheckMimeType(response.ContentType, acceptedMimeType, true);
			if (IsCompressed(response.Headers["Content-Encoding"]))
				return new StreamReader(new GZipStream(response.GetResponseStream(), CompressionMode.Decompress), enc);
			else
				return new StreamReader(response.GetResponseStream(), enc);
		}// func GetTextReaderAsync

		public async Task<XmlReader> GetXmlStreamAsync(string relativeUri, string acceptedMimeType = MimeTypes.Xml, XmlReaderSettings settings = null)
		{
			if (settings == null)
			{
				settings = new XmlReaderSettings();
				settings.IgnoreComments = acceptedMimeType != MimeTypes.Xml;
				settings.IgnoreWhitespace = acceptedMimeType != MimeTypes.Xml;
			}
			settings.CloseInput = true;

			var response = await GetResponseAsync(relativeUri);
			var baseUri = response.ResponseUri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.SafeUnescaped);
			var context = new XmlParserContext(null, null, null, null, null, null, baseUri, null, XmlSpace.Default);
			return XmlReader.Create(GetTextReaderAsync(response, acceptedMimeType), settings, context);
		} // func GetXmlStreamAsync

		public async Task<XElement> GetXmlAsync(string relativeUri, string acceptedMimeType = MimeTypes.Xml, XName rootName = null)
		{
			var document = XDocument.Load(await GetXmlStreamAsync(relativeUri), LoadOptions.SetBaseUri);
			if (document == null)
				throw new ArgumentException("Keine Antwort vom Server.");

			// Wurzelelement prüfen
			if (rootName != null && document.Root.Name != rootName)
				throw new ArgumentException(String.Format("Wurzelelement erwartet '{0}', aber '{1}' vorgefunden.", document.Root.Name, rootName));

			return document.Root;
		} // func GetXmlAsync

		public Uri BaseUri => baseUri;
	} // class BaseWebReqeust

	#endregion
}
