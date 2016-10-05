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
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

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

	#region -- enum ClientAuthentificationType ------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public enum ClientAuthentificationType
	{
		/// <summary>Unkown type.</summary>
		Unknown = 0,
		/// <summary>Normal unsecure web authentification.</summary>
		Basic,
		/// <summary>Windows/Kerberos authentification.</summary>
		Ntlm
	} // enum ClientAuthentificationType

	#endregion

	#region -- class ClientAuthentificationInformation ----------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class ClientAuthentificationInformation
	{
		private const string basicRealmProperty = "Basic realm=";
		private const string integratedSecurity = "Integrated Security";

		private readonly ClientAuthentificationType type;
		private readonly string realm;
		
		private ClientAuthentificationInformation(string authenticate)
		{
			if (authenticate.StartsWith(basicRealmProperty, StringComparison.OrdinalIgnoreCase)) // basic network authentification
			{
				type = ClientAuthentificationType.Basic;
				realm = authenticate.Substring(basicRealmProperty.Length);
				if (!String.IsNullOrEmpty(realm) && realm[0] == '"')
					realm = realm.Substring(1, realm.Length - 2);
			}
			else if (authenticate.IndexOf("NTLM", StringComparison.OrdinalIgnoreCase) >= 0) // Windows authentification
			{
				type = ClientAuthentificationType.Ntlm;
				realm = integratedSecurity;
			}
			else
			{
				type = ClientAuthentificationType.Unknown;
				realm = "Unknown";
			}
		} // ctor

		private ClientAuthentificationInformation(ClientAuthentificationType type, string realm)
		{
			this.type = type;
			this.realm = realm;
		} // ctor

		public ClientAuthentificationType Type => type;
		public string Realm => realm;

		public static ClientAuthentificationInformation Unknown { get; } = new ClientAuthentificationInformation(ClientAuthentificationType.Unknown, "Unknown");
		public static ClientAuthentificationInformation Ntlm { get; } = new ClientAuthentificationInformation(ClientAuthentificationType.Ntlm, integratedSecurity);

		public static bool TryGet(WebException e, ref ClientAuthentificationInformation info, bool autoDisposeResponse = true)
		{
			var t = Get(e);
			if (t != null)
			{
				info = t;
				return true;
			}
			else
				return false;
		} // func TryGet

		public static ClientAuthentificationInformation Get(WebException e, bool autoDisposeResponse = true)
		{
			if (e.Response == null)
				return null;

			// get the response
			var r = (HttpWebResponse)e.Response;
			try
			{
				var code = r.StatusCode;

				if (code == HttpStatusCode.Unauthorized)
					return new ClientAuthentificationInformation(r.Headers["WWW-Authenticate"]);
				else
					return null;
			}
			finally
			{
				if (autoDisposeResponse)
					r.Dispose();
			}
		} // func Get
	} // class ClientAuthentificationInformation

	#endregion

	#region -- class BaseWebRequest -----------------------------------------------------

	///////////////////////////////////////////////////////////////////////////////
	/// <summary></summary>
	public sealed class BaseWebRequest
	{
		private Uri baseUri;
		private Encoding defaultEncoding;
		private ICredentials credentials;

		public BaseWebRequest(Uri baseUri, Encoding defaultEncoding, ICredentials credentials = null)
		{
			this.baseUri = baseUri;
			this.defaultEncoding = defaultEncoding;
			this.credentials = credentials;
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
			if (acceptedMimeType != null && !mimeType.StartsWith(acceptedMimeType))
				throw new ArgumentException($"Expected: {acceptedMimeType}; received: {mimeType}");

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

		private WebRequest GetWebRequest(string path)
		{
			var request = WebRequest.Create(GetFullUri(path));

			// we accept always gzip
			request.Headers[HttpRequestHeader.AcceptEncoding] = "gzip";

			// set network login information
			if (credentials != null)
				request.Credentials = credentials;

			return request;
		} // func GetWebRequest

		/// <summary></summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public async Task<WebResponse> GetResponseAsync(string path)
		{
			var request = GetWebRequest(path);

#if DEBUG
			Debug.WriteLine($"Request: {path}");
			var sw = Stopwatch.StartNew();
			try
			{
#endif
				return await request.GetResponseAsync();
#if DEBUG
			}
			finally
			{
				Debug.WriteLine("Request: {0}ms", sw.ElapsedMilliseconds);
			}
#endif
		} // func GetResponseAsync

		public async Task<WebResponse> PutStreamResponseAsync(string path, Action<WebRequest, Stream> writeRequest)
		{
			var request = GetWebRequest(path);

			// write request stream
			request.Method = HttpMethod.Put.Method;
			writeRequest(request, await request.GetRequestStreamAsync());

			// get the response
			return await request.GetResponseAsync();
		} // proc PutStreamResponseAsync

		public Task<WebResponse> PutTextResponseAsync(string path, string inputContentType, Action<TextWriter> writeRequest)
		{
			return PutStreamResponseAsync(path,
				(r, dst) =>
				{
					// set header
					r.ContentType = inputContentType + ";charset=" + defaultEncoding.WebName;
					r.Headers[HttpRequestHeader.ContentEncoding] = "gzip";

					// convert the text to bytes
					using (var zip = new GZipStream(dst, CompressionMode.Compress))
					using (var tw = new StreamWriter(zip, defaultEncoding))
						writeRequest(tw);
				});
		} // func PutTextResponseAsync

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
			var response = await GetResponseAsync(path); // todo: is disposed called?
			return GetXmlStreamAsync(response, acceptedMimeType, settings);
		} // func GetXmlStreamAsync

		public XmlReader GetXmlStreamAsync(WebResponse response, string acceptedMimeType = MimeTypes.Text.Xml, XmlReaderSettings settings = null)
		{
			if (settings == null)
			{
				settings = new XmlReaderSettings();
				settings.IgnoreComments = acceptedMimeType != MimeTypes.Application.Xaml;
				settings.IgnoreWhitespace = acceptedMimeType != MimeTypes.Application.Xaml;
			}
			settings.CloseInput = true;

			var baseUri = response.ResponseUri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.SafeUnescaped);
			var context = new XmlParserContext(null, null, null, null, null, null, baseUri, null, XmlSpace.Default);

			return XmlReader.Create(GetTextReaderAsync(response, acceptedMimeType), settings, context);
		} // func GetXmlStreamAsync

		public async Task<XElement> GetXmlAsync(string path, string acceptedMimeType = MimeTypes.Text.Xml, XName rootName = null)
		{
			XDocument document;
			using (var xmlReader = await GetXmlStreamAsync(path))
				document = XDocument.Load(xmlReader, LoadOptions.SetBaseUri);
			if (document == null)
				throw new ArgumentException("Keine Antwort vom Server.");

			CheckForExceptionResult(document.Root);

			// check root element
			if (rootName != null && document.Root.Name != rootName)
				throw new ArgumentException(String.Format("Wurzelelement erwartet '{0}', aber '{1}' vorgefunden.", document.Root.Name, rootName));

			return document.Root;
		} // func GetXmlAsync

		private XElement CheckForExceptionResult(XElement x)
		{
			var xStatus = x.Attribute("status");
			if (xStatus != null && xStatus.Value != "ok")
			{
				var xText = x.Attribute("text");
				throw new ArgumentException(String.Format("Server returns an error: {0}", xText?.Value ?? "unknown"));
			}
			return x;
		} // func CheckForExceptionResult

		public async Task<LuaTable> GetTableAsync(string path, XName rootName = null)
			=> Procs.CreateLuaTable(CheckForExceptionResult(await GetXmlAsync(path, rootName: (rootName ?? "return"))));

		#region -- CreateViewDataReader ---------------------------------------------------

		#region -- class ViewDataReader ---------------------------------------------------

		///////////////////////////////////////////////////////////////////////////////
		/// <summary></summary>
		public class ViewDataReader : IEnumerable<IDataRow>
		{
			#region -- class ViewDataColumn ---------------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private sealed class ViewDataColumn : IDataColumn
			{
				private readonly string name;
				private readonly Type dataType;
				private readonly PropertyDictionary attributes;

				#region -- Ctor/Dtor --------------------------------------------------------------

				public ViewDataColumn(string name, Type dataType, PropertyDictionary attributes)
				{
					this.name = name;
					this.dataType = dataType;
					this.attributes = attributes;
				} // ctor

				#endregion

				#region -- IDataColumn ------------------------------------------------------------

				public string Name => name;
				public Type DataType => dataType;
				public IPropertyEnumerableDictionary Attributes => attributes;

				#endregion
			} // class ViewDataColumn

			#endregion

			#region -- class ViewDataRow ------------------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private sealed class ViewDataRow : DynamicDataRow
			{
				private readonly ViewDataEnumerator enumerator;
				private readonly object[] columnValues;

				#region -- Ctor/Dtor --------------------------------------------------------------

				public ViewDataRow(ViewDataEnumerator enumerator, object[] columnValues)
				{
					this.enumerator = enumerator;
					this.columnValues = columnValues;
				} // ctor

				#endregion

				#region -- IDataRow ---------------------------------------------------------------

				public override IReadOnlyList<IDataColumn> Columns => enumerator.Columns;
				public override object this[int index] => columnValues[index];

				#endregion
			} // class ViewDataRow

			#endregion

			#region -- class ViewDataEnumerator -----------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private sealed class ViewDataEnumerator : IEnumerator<IDataRow>, IDataColumns
			{
				#region -- enum ReadingState ------------------------------------------------------

				///////////////////////////////////////////////////////////////////////////////
				/// <summary></summary>
				private enum ReadingState
				{
					/// <summary>Nothing read until now</summary>
					Unread,
					/// <summary>Read first row</summary>
					FetchFirstRow,
					/// <summary>Fetch more rows</summary>
					FetchRows,
					/// <summary>Done</summary>
					Complete,
				} // enum ReadingState

				#endregion

				private readonly XName xnView = "view";
				private readonly XName xnFields = "fields";
				private readonly XName xnRows = "rows";
				private readonly XName xnRow = "r";

				private readonly ViewDataReader owner;
				private XmlReader xml;
				private ViewDataColumn[] columns;
				private ReadingState state;
				private ViewDataRow currentRow;

				#region -- Ctor/Dtor --------------------------------------------------------------

				public ViewDataEnumerator(ViewDataReader owner)
				{
					this.owner = owner;
				} // ctor

				public void Dispose()
					=> Dispose(true);

				private void Dispose(bool disposing)
				{
					if (disposing)
						xml?.Dispose();
				} // proc Dispose

				#endregion

				#region -- IEnumerator ------------------------------------------------------------

				private bool MoveNext(bool headerOnly)
				{
					switch (state)
					{
						#region -- ReadingState.Unread --
						case ReadingState.Unread:
							// open the xml stream
							xml = owner.request.GetXmlStreamAsync(owner.path, owner.acceptedMimeType).Result;

							xml.Read();
							if (xml.NodeType == XmlNodeType.XmlDeclaration)
								xml.Read();

							if (xml.NodeType != XmlNodeType.Element || xml.LocalName != xnView.LocalName)
								throw new InvalidDataException($"Expected \"{xnView}\", read \"{xml.LocalName}\".");

							xml.Read();
							if (xml.NodeType != XmlNodeType.Element || xml.LocalName != xnFields.LocalName)
								throw new InvalidDataException($"Expected \"{xnFields}\", read \"{xml.LocalName}\".");

							var viewColumns = new List<ViewDataColumn>();
							var fields = (XElement)XNode.ReadFrom(xml);
							foreach (var field in fields.Elements())
							{
								var columnName = field.Name.LocalName;
								var columnDataType = LuaType.GetType(field.GetAttribute("type", "string"), lateAllowed: false).Type;
								var columnId = field.GetAttribute("field", String.Empty);

								var attributes = new PropertyDictionary();

								// add colum id
								if (!String.IsNullOrEmpty(columnId))
									attributes.SetProperty("field", typeof(string), columnId);

								foreach (var c in field.Elements("attribute"))
								{
									if (c.IsEmpty)
										continue;

									var attributeName = c.GetAttribute("name", String.Empty);
									if (String.IsNullOrEmpty(attributeName))
										continue;

									attributes.SetProperty(attributeName, LuaType.GetType(c.GetAttribute("type", "string"), lateAllowed: false).Type, c.Value);
								} // foreach c

								viewColumns.Add(new ViewDataColumn(columnName, columnDataType, attributes));
							} // foreach field

							if (viewColumns.Count < 1)
								throw new InvalidDataException("No header found.");
							columns = viewColumns.ToArray();

							state = ReadingState.FetchFirstRow;
							if (headerOnly)
								return true;
							else
								goto case ReadingState.FetchFirstRow;
						#endregion
						#region -- ReadingState.FetchFirstRow --
						case ReadingState.FetchFirstRow:
							if (xml.NodeType != XmlNodeType.Element || xml.LocalName != xnRows.LocalName)
								throw new InvalidDataException($"Expected \"{xnRows}\", read \"{xml.LocalName}\".");

							if (xml.IsEmptyElement)
							{
								xml.Read();
								if (xml.NodeType != XmlNodeType.EndElement || xml.LocalName != xnView.LocalName)
									throw new InvalidDataException($"Expected \"{xnView}\", read \"{xml.LocalName}\".");

								xml.Read();
								if (!xml.EOF)
									throw new InvalidDataException("Unexpected eof.");

								state = ReadingState.Complete;
								goto case ReadingState.Complete;
							} // if xml.IsEmptyElement
							else
							{
								xml.Read();
								state = ReadingState.FetchRows;
								goto case ReadingState.FetchRows;
							}
						#endregion
						#region -- ReadingState.FetchRows --
						case ReadingState.FetchRows:
							if (xml.NodeType != XmlNodeType.Element || xml.LocalName != xnRow.LocalName)
								throw new InvalidDataException($"Expected \"r\", read \"{xml.LocalName}\".");

							var values = new object[columns.Length];

							if (!xml.IsEmptyElement)
							{
								var rowData = (XElement)XNode.ReadFrom(xml);
								foreach (var column in rowData.Elements())
								{
									var columnIndex = Array.FindIndex(columns, c => String.Compare(c.Name, column.Name.LocalName, StringComparison.OrdinalIgnoreCase) == 0);
									if (columnIndex != -1)
										values[columnIndex] = Procs.ChangeType(column.Value, columns[columnIndex].DataType);
								}
							} // if xml.IsEmptyElement
							else
								// Without a call to XNode.ReadFrom() it's necessary to read to the next node.
								xml.Read();

							currentRow = new ViewDataRow(this, values);

							if (xml.NodeType == XmlNodeType.Element && xml.LocalName == xnRow.LocalName)
								return true;

							if (xml.NodeType != XmlNodeType.EndElement || xml.LocalName != xnRows.LocalName)
								throw new InvalidDataException($"Expected \"{xnRows}\", read \"{xml.LocalName}\".");

							xml.Read();
							if (xml.NodeType != XmlNodeType.EndElement || xml.LocalName != xnView.LocalName)
								throw new InvalidDataException($"Expected \"{xnView}\", read \"{xml.LocalName}\".");

							xml.Read();
							if (!xml.EOF)
								throw new InvalidDataException("Unexpected eof.");

							state = ReadingState.Complete;
							return true;
						#endregion
						case ReadingState.Complete:
							return false;
						default:
							throw new InvalidOperationException("The state of the object is invalid.");
					} // switch state
				} // func MoveNext 

				public bool MoveNext()
					=> MoveNext(false);

				void IEnumerator.Reset()
				{
					xml?.Dispose();
					xml = null;
					columns = null;
					currentRow = null;
					state = ReadingState.Unread;
				} // proc Reset

				public IDataRow Current => currentRow;
				object IEnumerator.Current => Current;

				#endregion

				#region -- IDataColumns -----------------------------------------------------------

				public IReadOnlyList<IDataColumn> Columns
				{
					get
					{
						if (state == ReadingState.Unread)
							MoveNext(true);
						return columns;
					}
				} // prop Columns

				#endregion
			} // class ViewDataEnumerator

			#endregion

			private readonly BaseWebRequest request;
			private readonly string path;
			private readonly string acceptedMimeType;

			#region -- Ctor/Dtor --------------------------------------------------------------

			public ViewDataReader(BaseWebRequest request, string path, string acceptedMimeType = MimeTypes.Text.Xml)
			{
				this.request = request;
				this.path = path;
				this.acceptedMimeType = acceptedMimeType;
			} // ctor

			#endregion

			#region -- IEnumerable ------------------------------------------------------------

			public IEnumerator<IDataRow> GetEnumerator()
				=> new ViewDataEnumerator(this);

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			#endregion
		} // class ViewDataReader

		#endregion

		public IEnumerable<IDataRow> CreateViewDataReader(string path, string acceptedMimeType = MimeTypes.Text.Xml)
			=> new ViewDataReader(this, path, acceptedMimeType);

		#endregion

		public Uri BaseUri => baseUri;
		public Encoding DefaultEncoding => defaultEncoding;
		public ICredentials Credentials => credentials;
	} // class BaseWebReqeust

	#endregion
}
