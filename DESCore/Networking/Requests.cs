using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;

namespace TecWare.DE.Networking
{
	#region -- class MimeTypes ----------------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public static class MimeTypes
	{
		public static class Text
		{
			public const string Plain = "text/plain";
			public const string Xml = "text/xml";
			public const string JavaScript = "text/javascript";
			public const string Css = "text/css";
			public const string Html = "text/html";
			public const string Lua = "text/x-lua";
			public const string Json = "text/json";
		} // class Text

		public static class Image
		{
			public const string Png = "image/png";
			public const string Jpeg = "image/jpeg";
			public const string Gif = "image/gif";
			public const string Icon = "image/x-icon";
		} // class Image

		public static class Application
		{
			public const string Xaml = "application/xaml+xml";
			public const string OctetStream = "application/octet-stream";
		} // class Application
	} // class MimeTypes

	#endregion

	#region -- interface IDataReader ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataReader : IEnumerable<IDataRecord>
	{
		Type GetFieldType(int fieldIndex);
		string GetName(int fieldIndex);

		int FieldCount { get; }
	} // interface IDataReader

	#endregion

	#region -- interface IDataRecord ----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public interface IDataRecord
	{
		bool IsNull(int fieldIndex);
		Type GetFieldType(int fieldIndex);
		string GetName(int fieldIndex);

		int FieldCount { get; }

		object this[int fieldIndex] { get; }
		object this[string fieldName] { get; }
	} // interface IDataRecord

	#endregion
	
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

		public Uri GetFullUri(string path)
		{
			Uri uri;

			if (path == null)
				uri = baseUri;
			else
			{
				uri = new Uri(path, UriKind.RelativeOrAbsolute);
				if (!uri.IsAbsoluteUri && baseUri != null)
					uri = new Uri(baseUri, uri);
			}

			if (uri == null)
				throw new ArgumentNullException("Uri can not be null.");

			return uri;
		} // func GetFullUri

		/// <summary></summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public async Task<WebResponse> GetResponseAsync(string path)
		{
			var request = WebRequest.Create(GetFullUri(path));

			// we accept always gzip
			request.Headers[HttpRequestHeader.AcceptEncoding] = "gzip";

			return await request.GetResponseAsync();
		} // func GetResponseAsync

		/// <summary>Creates a plain Web-Request, special arguments are filled with IWebRequestCreate.</summary>
		/// <param name="path">Resource</param>
		/// <param name="acceptedMimeType">Optional.</param>
		/// <returns></returns>
		public async Task<Stream> GetStreamAsync(string path, string acceptedMimeType)
		{
			return GetStreamAsync(await GetResponseAsync(path), acceptedMimeType);
		} // func GetStreamAsync

		public Stream GetStreamAsync(WebResponse response, string acceptedMimeType)
		{
			CheckMimeType(response.ContentType, acceptedMimeType, false);
			if (IsCompressed(response.Headers["Content-Encoding"]))
				return new GZipStream(response.GetResponseStream(), CompressionMode.Decompress, false);
			else
				return response.GetResponseStream();
		} // func GetStreamAsync

		public async Task<TextReader> GetTextReaderAsync(string path, string acceptedMimeType)
		{
			return GetTextReaderAsync(await GetResponseAsync(path), acceptedMimeType);
		} // func GetTextReaderAsync

		public TextReader GetTextReaderAsync(WebResponse response, string acceptedMimeType)
		{
			var enc = CheckMimeType(response.ContentType, acceptedMimeType, true);
			if (IsCompressed(response.Headers["Content-Encoding"]))
				return new StreamReader(new GZipStream(response.GetResponseStream(), CompressionMode.Decompress), enc);
			else
				return new StreamReader(response.GetResponseStream(), enc);
		}// func GetTextReaderAsync

		public async Task<XmlReader> GetXmlStreamAsync(string path, string acceptedMimeType = MimeTypes.Text.Xml, XmlReaderSettings settings = null)
		{
			if (settings == null)
			{
				settings = new XmlReaderSettings();
				settings.IgnoreComments = acceptedMimeType != MimeTypes.Application.Xaml;
				settings.IgnoreWhitespace = acceptedMimeType != MimeTypes.Application.Xaml;
			}
			settings.CloseInput = true;

			var response = await GetResponseAsync(path);
			var baseUri = response.ResponseUri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.SafeUnescaped);
			var context = new XmlParserContext(null, null, null, null, null, null, baseUri, null, XmlSpace.Default);
			return XmlReader.Create(GetTextReaderAsync(response, acceptedMimeType), settings, context);
		} // func GetXmlStreamAsync

		public async Task<XElement> GetXmlAsync(string relativeUri, string acceptedMimeType = MimeTypes.Text.Xml, XName rootName = null)
		{
			var document = XDocument.Load(await GetXmlStreamAsync(relativeUri), LoadOptions.SetBaseUri);
			if (document == null)
				throw new ArgumentException("Keine Antwort vom Server.");

			// Wurzelelement prüfen
			if (rootName != null && document.Root.Name != rootName)
				throw new ArgumentException(String.Format("Wurzelelement erwartet '{0}', aber '{1}' vorgefunden.", document.Root.Name, rootName));

			return document.Root;
		} // func GetXmlAsync

		#region -- GetReaderAsync ---------------------------------------------------------

		#region -- class DataReaderRecord--------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class DataReaderRecord : IDataRecord
		{
			private DataReaderResult reader;
			private XElement record;

			public DataReaderRecord(DataReaderResult reader, XElement record)
			{
				this.reader = reader;
				this.record = record;
			} // ctor

			public Type GetFieldType(int fieldIndex)
			{
				return reader.GetField(fieldIndex).FieldType;
			} // func GetFieldType

			public string GetName(int fieldIndex)
			{
				return reader.GetField(fieldIndex).FieldName;
			} // func GetName

			public bool IsNull(int fieldIndex)
			{
				return GetValue(reader.GetField(fieldIndex), false) == null;
			} // func IsNull

			private object GetValue(DataReaderField f, bool convert)
			{
				var x = record.Element(f.FieldName);
				if (x == null)
					return null;

				var tmp = x.Value;
				if (!convert)
					return tmp;

				return Lua.RtConvertValue(tmp, f.FieldType);
			} // func GetValue

			public object this[string fieldName] => GetValue(reader.GetFieldByName(fieldName), true);
			public object this[int fieldIndex] => GetValue(reader.GetField(fieldIndex), true);

			public int FieldCount => reader.FieldCount;
		} // class DataReaderRecord

		#endregion

		#region -- class DataReaderField --------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class DataReaderField
		{
			public string FieldName { get; set; }
			public Type FieldType { get; set; }
		} // class DataReaderField

		#endregion

		#region -- class DataReaderResult -------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		private sealed class DataReaderResult : IDataReader
		{
			private XmlReader xml;
			private DataReaderField[] fields = null;

			public DataReaderResult(XmlReader xml)
			{
				this.xml = xml;
			} // ctor

			public Type GetFieldType(int fieldIndex)
				=> fields[fieldIndex].FieldType;

			public string GetName(int fieldIndex)
				=> fields[fieldIndex].FieldName;

			public IEnumerator<IDataRecord> GetEnumerator()
			{
				// Read over header
				xml.Read();
				if (xml.NodeType == XmlNodeType.XmlDeclaration)
					xml.Read();

				if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "datareader")
					xml.Read();
				else
					throw new ArgumentException(); // todo: Exception

				// Read column informations
				if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "columns")
				{
					var fieldList = new List<DataReaderField>();
					while (xml.Read() && xml.NodeType == XmlNodeType.Element && xml.LocalName == "column")
					{
						var name = xml.GetAttribute("name");
						var type = xml.GetAttribute("type");
						fieldList.Add(new DataReaderField() { FieldName = name, FieldType = LuaType.GetType(type) });
					}
					if (xml.NodeType != XmlNodeType.EndElement || xml.LocalName != "columns")
						throw new ArgumentException(); // todo: Exception
					else
					{
						xml.Read();
						fields = fieldList.ToArray();
					}
				}
				else
					throw new ArgumentException(); // todo: Exception

				// Read content
				if (xml.NodeType == XmlNodeType.Element && xml.LocalName == "items")
				{
					xml.Read();
          while (xml.NodeType == XmlNodeType.Element && xml.LocalName == "item")
						yield return new DataReaderRecord(this, (XElement)XElement.ReadFrom(xml));

					//if (xml.NodeType != XmlNodeType.EndElement || xml.LocalName != "items")
					//	throw new ArgumentException(); // todo: Exception
				}
				else
					throw new ArgumentException(); // todo: Exception
			} // func GetEnumerator

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			internal DataReaderField GetField(int fieldIndex)
				=> fields[fieldIndex];
			
			internal DataReaderField GetFieldByName(string fieldName)
				=> Array.Find(fields, c => String.Compare(c.FieldName, fieldName, StringComparison.OrdinalIgnoreCase) == 0);

			public int FieldCount => fields.Length;
		} // class DataReaderResult

		#endregion

		public async Task<IDataReader> GetReaderAsync(string relativeUri, string acceptedMimeType = MimeTypes.Text.Xml)
		{
			return new DataReaderResult(await GetXmlStreamAsync(relativeUri, acceptedMimeType));
		} // func GetReaderAsync

		#endregion

		public Uri BaseUri => baseUri;
	} // class BaseWebReqeust

	#endregion
}
