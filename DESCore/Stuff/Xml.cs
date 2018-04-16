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
using System.Xml.Schema;

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

		#region -- XName, Name --------------------------------------------------------

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <returns></returns>
		public static XName GetName(this XmlReader xml)
			=> XName.Get(xml.Name, xml.NamespaceURI);

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="name"></param>
		/// <param name="comparison"></param>
		/// <returns></returns>
		public static bool IsName(this XmlReader xml, XName name, StringComparison comparison = StringComparison.Ordinal)
		{
			if (xml.NodeType == XmlNodeType.Element
				|| xml.NodeType == XmlNodeType.EndElement
				|| xml.NodeType == XmlNodeType.Attribute)
			{
				if (String.IsNullOrEmpty(name.NamespaceName))
					return String.Compare(xml.Name, name.LocalName, comparison) == 0;
				else
					return String.Compare(xml.Name, name.LocalName, comparison) == 0
						&& String.Compare(xml.NamespaceURI, name.NamespaceName, comparison) == 0;
			}
			else
				return false;
		} // func IsName

		#endregion

		#region -- Read, GetElementContent --------------------------------------------

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

		#endregion

		#region -- CreateXmlException -------------------------------------------------

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		/// <returns></returns>
		public static XmlException CreateXmlException(this XmlReader xml, string message, Exception innerException = null)
			=> CreateXmlException(xml as IXmlLineInfo, message, innerException);

		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		/// <param name="lineInfo"></param>
		/// <returns></returns>
		public static XmlException CreateXmlException(IXmlLineInfo lineInfo, string message, Exception innerException = null)
			=> new XmlException(message, innerException, lineInfo?.LineNumber ?? 0, lineInfo?.LinePosition ?? 0);

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="expectedNodeType"></param>
		/// <returns></returns>
		public static XmlException CreateXmlNodeException(this XmlReader xml, XmlNodeType expectedNodeType)
			=> CreateXmlException(xml, String.Format("Invalid node type (expected: {0}, found: {1})", expectedNodeType, xml.NodeType));

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="expectedName"></param>
		/// <returns></returns>
		public static XmlException CreateXmlElementException(this XmlReader xml, XName expectedName)
			=> CreateXmlException(xml, String.Format("Invalid node type (expected: {0}, found: {1})", expectedName, xml.GetName()));

		#endregion

		#region -- MoveToContent ------------------------------------------------------

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="expectedNodeType"></param>
		/// <returns></returns>
		public static void MoveToContent(this XmlReader xml, XmlNodeType expectedNodeType)
		{
			if (xml.MoveToContent() != expectedNodeType)
				throw CreateXmlNodeException(xml, expectedNodeType);
		} // proc MoveToContentAsync

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="expectedNodeType"></param>
		/// <returns></returns>
		public static async Task MoveToContentAsync(this XmlReader xml, XmlNodeType expectedNodeType)
		{
			if (await xml.MoveToContentAsync() != expectedNodeType)
				throw CreateXmlNodeException(xml, expectedNodeType);
		} // proc MoveToContentAsync

		#endregion

		#region -- ReadStartElement ---------------------------------------------------

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static bool ReadStartElement(this XmlReader xml, XName name)
		{
			MoveToContent(xml, XmlNodeType.Element);

			if (!IsName(xml, name))
				throw CreateXmlException(xml, String.Format("Element not found (expected: {0}, found: {1})", name, xml.Name));

			return xml.Read();
		} // proc ReadStartElementAsync

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static async Task<bool> ReadStartElementAsync(this XmlReader xml, XName name)
		{
			await MoveToContentAsync(xml, XmlNodeType.Element);

			if (!IsName(xml, name))
				throw CreateXmlException(xml, String.Format("Element not found (expected: {0}, found: {1})", name, xml.Name));

			return await xml.ReadAsync();
		} // proc ReadStartElementAsync

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static bool ReadOptionalStartElement(this XmlReader xml, XName name)
			=> xml.MoveToContent() == XmlNodeType.Element && xml.IsName(name) && xml.Read();

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="name"></param>
		/// <returns></returns>
		public static async Task<bool> ReadOptionalStartElementAsync(this XmlReader xml, XName name)
			=> await xml.MoveToContentAsync() == XmlNodeType.Element && xml.IsName(name) && await xml.ReadAsync();

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <returns></returns>
		public static async Task ReadEndElementAsync(this XmlReader xml)
		{
			await MoveToContentAsync(xml, XmlNodeType.EndElement);
			await xml.ReadAsync();
		} // proc ReadEndElementAsync

		#endregion

		#region -- Attributes ---------------------------------------------------------

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
				var valueString = xml.GetAttribute(attributeName.LocalName, attributeName.NamespaceName);
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

		/// <summary></summary>
		/// <returns></returns>
		public static IEnumerable<XAttribute> EnumerateAttributes(this XmlReader xml)
		{
			if (xml.MoveToFirstAttribute())
			{
				do
				{
					yield return new XAttribute(xml.GetName(), xml.Value);
				} while (xml.MoveToNextAttribute());
			}
		} // func EnumerateAttributes
		
		#endregion

		#region -- ReadElementAsSubTree -----------------------------------------------

		#region -- class XmlElementReader ---------------------------------------------

		/// <summary></summary>
		private sealed class XmlElementReader : XmlReader, IXmlNamespaceResolver, IXmlLineInfo
		{
			private const int maxState = 2;

			private readonly XmlReader xml;
			private readonly XmlNamespaceScope? namespaceScope;
			private readonly int recoveryLevel;

			private int state = 0;

			public XmlElementReader(XmlReader xml, XmlNamespaceScope? namespaceScope = null)
			{
				this.xml = xml;
				this.namespaceScope = namespaceScope;
				this.recoveryLevel = xml.Depth;
			} // ctor

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);
				if (xml.ReadState == ReadState.Interactive)
				{
					while (recoveryLevel < xml.Depth && xml.Read())
					{ }
				}
			} // proc Dispose

			public override bool Read()
			{
				switch (state)
				{
					case 0: // simulate element
						state = 1;
						return true;
					case 1: // read element
						if (xml.Read())
						{
							if (xml.NodeType == XmlNodeType.EndElement
								&& xml.Depth == recoveryLevel)
							{
								state = 2;
								return true;
							}
							return true;
						}
						else
							return false;
					case 2:
						xml.Read(); // eat end element
						state = 3;
						return false;
					default:
						return false;
				}
			} // func Read

			private XmlNamespaceScope GetNamespaceScope(XmlNamespaceScope scope)
			{
				if (namespaceScope.HasValue)
				{
					return scope < namespaceScope.Value
						? scope
						: namespaceScope.Value;
				}
				else
					return scope;
			} // func GetNamespaceScope

			IDictionary<string, string> IXmlNamespaceResolver.GetNamespacesInScope(XmlNamespaceScope scope)
				=> xml is IXmlNamespaceResolver resolver ? resolver.GetNamespacesInScope(GetNamespaceScope(scope)) : throw new NotSupportedException();
			string IXmlNamespaceResolver.LookupNamespace(string prefix)
				=> xml is IXmlNamespaceResolver resolver ? resolver.LookupNamespace(prefix) : throw new NotSupportedException();
			string IXmlNamespaceResolver.LookupPrefix(string namespaceName)
				=> xml is IXmlNamespaceResolver resolver ? resolver.LookupPrefix(namespaceName) : throw new NotSupportedException();

			#region -- overrides --

			public override object ReadContentAsObject() => xml.ReadContentAsObject();
			public override bool ReadContentAsBoolean() => xml.ReadContentAsBoolean();
			public override DateTime ReadContentAsDateTime() => xml.ReadContentAsDateTime();
			public override DateTimeOffset ReadContentAsDateTimeOffset() => xml.ReadContentAsDateTimeOffset();
			public override double ReadContentAsDouble() => xml.ReadContentAsDouble();
			public override float ReadContentAsFloat() => xml.ReadContentAsFloat();
			public override decimal ReadContentAsDecimal() => xml.ReadContentAsDecimal();
			public override int ReadContentAsInt() => xml.ReadContentAsInt();
			public override long ReadContentAsLong() => xml.ReadContentAsLong();
			public override string ReadContentAsString() => xml.ReadContentAsString();
			public override object ReadContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver) => xml.ReadContentAs(returnType, namespaceResolver);
			public override object ReadElementContentAsObject() => xml.ReadElementContentAsObject();
			public override object ReadElementContentAsObject(string localName, string namespaceURI) => xml.ReadElementContentAsObject(localName, namespaceURI);
			public override bool ReadElementContentAsBoolean() => xml.ReadElementContentAsBoolean();
			public override bool ReadElementContentAsBoolean(string localName, string namespaceURI) => xml.ReadElementContentAsBoolean(localName, namespaceURI);
			public override DateTime ReadElementContentAsDateTime() => xml.ReadElementContentAsDateTime();
			public override DateTime ReadElementContentAsDateTime(string localName, string namespaceURI) => xml.ReadElementContentAsDateTime(localName, namespaceURI);
			public override double ReadElementContentAsDouble() => xml.ReadElementContentAsDouble();
			public override double ReadElementContentAsDouble(string localName, string namespaceURI) => xml.ReadElementContentAsDouble(localName, namespaceURI);
			public override float ReadElementContentAsFloat() => xml.ReadElementContentAsFloat();
			public override float ReadElementContentAsFloat(string localName, string namespaceURI) => xml.ReadElementContentAsFloat(localName, namespaceURI);
			public override decimal ReadElementContentAsDecimal() => xml.ReadElementContentAsDecimal();
			public override decimal ReadElementContentAsDecimal(string localName, string namespaceURI) => xml.ReadElementContentAsDecimal(localName, namespaceURI);
			public override int ReadElementContentAsInt() => xml.ReadElementContentAsInt();
			public override int ReadElementContentAsInt(string localName, string namespaceURI) => xml.ReadElementContentAsInt(localName, namespaceURI);
			public override long ReadElementContentAsLong() => xml.ReadElementContentAsLong();
			public override long ReadElementContentAsLong(string localName, string namespaceURI) => xml.ReadElementContentAsLong(localName, namespaceURI);
			public override string ReadElementContentAsString() => xml.ReadElementContentAsString();
			public override string ReadElementContentAsString(string localName, string namespaceURI) => xml.ReadElementContentAsString(localName, namespaceURI);
			public override object ReadElementContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver) => xml.ReadElementContentAs(returnType, namespaceResolver);
			public override object ReadElementContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver, string localName, string namespaceURI) => xml.ReadElementContentAs(returnType, namespaceResolver, localName, namespaceURI);
			public override void MoveToAttribute(int i) => xml.MoveToAttribute(i);
			public override void Skip() => xml.Skip();
			public override int ReadContentAsBase64(byte[] buffer, int index, int count) => xml.ReadContentAsBase64(buffer, index, count);
			public override int ReadElementContentAsBase64(byte[] buffer, int index, int count) => xml.ReadElementContentAsBase64(buffer, index, count);
			public override int ReadContentAsBinHex(byte[] buffer, int index, int count) => xml.ReadContentAsBinHex(buffer, index, count);
			public override int ReadElementContentAsBinHex(byte[] buffer, int index, int count) => xml.ReadElementContentAsBinHex(buffer, index, count);
			public override int ReadValueChunk(char[] buffer, int index, int count) => xml.ReadValueChunk(buffer, index, count);
			public override string ReadString() => xml.ReadString();
			public override XmlNodeType MoveToContent() => xml.MoveToContent();
			public override void ReadStartElement() => xml.ReadStartElement();
			public override void ReadStartElement(string name) => xml.ReadStartElement(name);
			public override void ReadStartElement(string localname, string ns) => xml.ReadStartElement(localname, ns);
			public override string ReadElementString() => xml.ReadElementString();
			public override string ReadElementString(string name) => xml.ReadElementString(name);
			public override string ReadElementString(string localname, string ns) => xml.ReadElementString(localname, ns);
			public override void ReadEndElement() => xml.ReadEndElement();
			public override bool IsStartElement() => xml.IsStartElement();
			public override bool IsStartElement(string name) => xml.IsStartElement(name);
			public override bool IsStartElement(string localname, string ns) => xml.IsStartElement(localname, ns);
			public override bool ReadToFollowing(string name) => xml.ReadToFollowing(name);
			public override bool ReadToFollowing(string localName, string namespaceURI) => xml.ReadToFollowing(localName, namespaceURI);
			public override bool ReadToDescendant(string name) => xml.ReadToDescendant(name);
			public override bool ReadToDescendant(string localName, string namespaceURI) => xml.ReadToDescendant(localName, namespaceURI);
			public override bool ReadToNextSibling(string name) => xml.ReadToNextSibling(name);
			public override bool ReadToNextSibling(string localName, string namespaceURI) => xml.ReadToNextSibling(localName, namespaceURI);
			public override string ReadInnerXml() => xml.ReadInnerXml();
			public override string ReadOuterXml() => xml.ReadOuterXml();
			public override XmlReader ReadSubtree() => xml.ReadSubtree();
			public override Task<string> GetValueAsync() => xml.GetValueAsync();
			public override Task<object> ReadContentAsObjectAsync() => xml.ReadContentAsObjectAsync();
			public override Task<string> ReadContentAsStringAsync() => xml.ReadContentAsStringAsync();
			public override Task<object> ReadContentAsAsync(Type returnType, IXmlNamespaceResolver namespaceResolver) => xml.ReadContentAsAsync(returnType, namespaceResolver);
			public override Task<object> ReadElementContentAsObjectAsync() => xml.ReadElementContentAsObjectAsync();
			public override Task<string> ReadElementContentAsStringAsync() => xml.ReadElementContentAsStringAsync();
			public override Task<object> ReadElementContentAsAsync(Type returnType, IXmlNamespaceResolver namespaceResolver) => xml.ReadElementContentAsAsync(returnType, namespaceResolver);
			public override Task<bool> ReadAsync() => xml.ReadAsync();
			public override Task SkipAsync() => xml.SkipAsync();
			public override Task<int> ReadContentAsBase64Async(byte[] buffer, int index, int count) => xml.ReadContentAsBase64Async(buffer, index, count);
			public override Task<int> ReadElementContentAsBase64Async(byte[] buffer, int index, int count) => xml.ReadElementContentAsBase64Async(buffer, index, count);
			public override Task<int> ReadContentAsBinHexAsync(byte[] buffer, int index, int count) => xml.ReadContentAsBinHexAsync(buffer, index, count);
			public override Task<int> ReadElementContentAsBinHexAsync(byte[] buffer, int index, int count) => xml.ReadElementContentAsBinHexAsync(buffer, index, count);
			public override Task<int> ReadValueChunkAsync(char[] buffer, int index, int count) => xml.ReadValueChunkAsync(buffer, index, count);
			public override Task<XmlNodeType> MoveToContentAsync() => xml.MoveToContentAsync();
			public override Task<string> ReadInnerXmlAsync() => xml.ReadInnerXmlAsync();
			public override Task<string> ReadOuterXmlAsync() => xml.ReadOuterXmlAsync();
			public override string GetAttribute(int i) => xml.GetAttribute(i);
			public override string GetAttribute(string name) => xml.GetAttribute(name);
			public override string GetAttribute(string name, string namespaceURI) => xml.GetAttribute(name, namespaceURI);
			public override bool MoveToAttribute(string name) => xml.MoveToAttribute(name);
			public override bool MoveToAttribute(string name, string ns) => xml.MoveToAttribute(name, ns);
			public override string LookupNamespace(string prefix) => xml.LookupNamespace(prefix);
			public override bool ReadAttributeValue() => xml.ReadAttributeValue();
			public override void ResolveEntity() => xml.ResolveEntity();

			public override bool MoveToFirstAttribute() => xml.MoveToFirstAttribute();
			public override bool MoveToNextAttribute() => xml.MoveToNextAttribute();
			public override bool MoveToElement() => xml.MoveToElement();

			public override bool CanReadBinaryContent => xml.CanReadBinaryContent;
			public override bool CanReadValueChunk => xml.CanReadValueChunk;
			public override bool CanResolveEntity => xml.CanResolveEntity;
			public override int Depth => xml.Depth;
			public override string BaseURI => xml.BaseURI;
			public override bool IsEmptyElement => xml.IsEmptyElement;
			public override XmlNameTable NameTable => xml.NameTable;
			public override XmlReaderSettings Settings => xml.Settings;
			public override bool HasValue => xml.HasValue;
			public override bool IsDefault => xml.IsDefault;
			public override char QuoteChar => xml.QuoteChar;
			public override XmlSpace XmlSpace => xml.XmlSpace;
			public override string XmlLang => xml.XmlLang;
			public override IXmlSchemaInfo SchemaInfo => xml.SchemaInfo;
			public override Type ValueType => xml.ValueType;
			public override bool HasAttributes => xml.HasAttributes;
			public override string this[string name, string namespaceURI] => base[name, namespaceURI];
			public override string this[string name] => base[name];
			public override string this[int i] => base[i];

			bool IXmlLineInfo.HasLineInfo() => xml is IXmlLineInfo lineInfo ? lineInfo.HasLineInfo() : false;
			int IXmlLineInfo.LineNumber => xml is IXmlLineInfo lineInfo ? lineInfo.LineNumber : 0;
			int IXmlLineInfo.LinePosition => xml is IXmlLineInfo lineInfo ? lineInfo.LinePosition : 0;

			#endregion

			public override int AttributeCount => xml.AttributeCount;

			public override string Name => xml.Name;
			public override string Value => xml.Value;
			public override string NamespaceURI => xml.NamespaceURI;
			public override string LocalName => xml.LocalName;
			public override string Prefix => xml.Prefix;


			public override bool EOF => state > maxState;
			public override XmlNodeType NodeType => state <= maxState ? xml.NodeType : XmlNodeType.None;
			public override ReadState ReadState => state <= maxState ? xml.ReadState : ReadState.Closed;
		} // class XmlElementReader

		#endregion

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="namespaceScope"></param>
		/// <returns></returns>
		/// <remarks>Recoveries to the EndElement</remarks>
		public static XmlReader ReadElementAsSubTree(this XmlReader xml, XmlNamespaceScope? namespaceScope = null)
		{
			var recoveryLevel = xml.Depth;
			if (xml.MoveToContent() != XmlNodeType.Element)
				throw xml.CreateXmlException("Element expected.");

			return new XmlElementReader(xml, namespaceScope);
		} // func Create

		/// <summary></summary>
		/// <param name="xml"></param>
		/// <param name="namespaceScope"></param>
		/// <returns></returns>
		/// <remarks>Recoveries to the EndElement</remarks>
		public static async Task<XmlReader> ReadElementAsSubTreeAsync(this XmlReader xml, XmlNamespaceScope? namespaceScope = null)
		{
			var recoveryLevel = xml.Depth;
			if (await xml.MoveToContentAsync() != XmlNodeType.Element)
				throw xml.CreateXmlException("Element expected.");

			return new XmlElementReader(xml, namespaceScope);
		} // func Create

		#endregion

		#endregion

		#region -- XElement helper ----------------------------------------------------

		#region -- Attributes ---------------------------------------------------------

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

		#endregion

		#region -- Annotations --------------------------------------------------------

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
