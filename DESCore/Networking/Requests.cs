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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.DE.Networking
{
	#region -- class MimeTypeInfoAttribute --------------------------------------------

	[AttributeUsage(AttributeTargets.Field)]
	internal class MimeTypeInfoAttribute : Attribute
	{
		public MimeTypeInfoAttribute(bool isPackedContent, params string[] extensions)
		{
			IsPackedContent = isPackedContent;
			Extensions = extensions;
		} // ctor

		public bool IsPackedContent { get; }
		public string[] Extensions { get; }
	} // class MimeTypeInfoAttribute

	#endregion

	#region -- class MimeTypes --------------------------------------------------------

	/// <summary>Static mime type definitons.</summary>
	public static class MimeTypes
	{
		/// <summary>All text mime types</summary>
		public static class Text
		{
			/// <summary></summary>
			[MimeTypeInfo(false, ".txt", ".ts")]
			public const string Plain = "text/plain";
			/// <summary></summary>
			[MimeTypeInfo(false, ".xml")]
			public const string Xml = "text/xml";
			/// <summary></summary>
			[MimeTypeInfo(false, ".js")]
			public const string JavaScript = "application/javascript";
			/// <summary></summary>
			[MimeTypeInfo(false, ".css")]
			public const string Css = "text/css";
			/// <summary></summary>
			[MimeTypeInfo(false, ".html", ".htm")]
			public const string Html = "text/html";
			/// <summary></summary>
			[MimeTypeInfo(false, ".lua")]
			public const string Lua = "text/x-lua";
			/// <summary></summary>
			[MimeTypeInfo(false, ".json", ".map")]
			public const string Json = "text/json";
			/// <summary></summary>
			[MimeTypeInfo(false, ".lson")]
			public const string Lson = "text/lson";
			/// <summary></summary>
			[MimeTypeInfo(false)]
			public const string DataSet = "text/dataset";
		} // class Text

		/// <summary>All image mime types</summary>
		public static class Image
		{
			/// <summary></summary>
			[MimeTypeInfo(false, ".bmp")]
			public const string Bmp = "image/bmp";
			/// <summary></summary>
			[MimeTypeInfo(true, ".png")]
			public const string Png = "image/png";
			/// <summary></summary>
			[MimeTypeInfo(true, ".jpg", ".jpeg", ".jpe")]
			public const string Jpeg = "image/jpeg";
			/// <summary></summary>
			[MimeTypeInfo(true, ".gif")]
			public const string Gif = "image/gif";
			/// <summary></summary>
			[MimeTypeInfo(false, ".ico")]
			public const string Icon = "image/x-icon";
		} // class Image

		/// <summary>All application mime types.</summary>
		public static class Application
		{
			/// <summary>Pdf files.</summary>
			[MimeTypeInfo(false, ".pdf")]
			public const string Pdf = "application/pdf";
			/// <summary></summary>
			[MimeTypeInfo(false, ".xaml")]
			public const string Xaml = "application/xaml+xml";
			/// <summary></summary>
			[MimeTypeInfo(true, ".dat")]
			public const string OctetStream = "application/octet-stream";
		} // class Application
	} // class MimeTypes

	#endregion

	#region -- class MimeTypeMapping --------------------------------------------------

	/// <summary>Mime type mapping class</summary>
	public sealed class MimeTypeMapping
	{
		private MimeTypeMapping(string mimeType, bool isCompressedContent, string[] extensions)
		{
			MimeType = mimeType;
			Extensions = extensions;
			IsCompressedContent = isCompressedContent;
		} // ctor

		/// <summary></summary>
		/// <returns></returns>
		public override int GetHashCode()
			=> MimeType.GetHashCode();

		/// <summary></summary>
		/// <param name="obj"></param>
		/// <returns></returns>
		public override bool Equals(object obj)
			=> obj is MimeTypeMapping m ? MimeType.Equals(m.MimeType) : base.Equals(obj);

		/// <summary></summary>
		/// <returns></returns>
		public override string ToString()
			=> "Mapping: " + MimeType;

		/// <summary>Mime type.</summary>
		public string MimeType { get; }
		/// <summary>Return posible extensions of the mime type.</summary>
		public string[] Extensions { get; }
		/// <summary>Is the content of this mimetype a compress data format.</summary>
		public bool IsCompressedContent { get; }

		private static readonly Regex extensionRegEx = new Regex(@"^\.\w+$", RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.IgnoreCase);
		private static readonly List<MimeTypeMapping> mimeTypeMappings = new List<MimeTypeMapping>();
		private static readonly Dictionary<string, int> mimeTypeIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
		private static readonly Dictionary<string, int> extensionIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

		static MimeTypeMapping()
		{
			foreach (var t in typeof(MimeTypes).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
			{
				foreach (var fi in t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.GetField))
				{
					var attr = fi.GetCustomAttribute<MimeTypeInfoAttribute>();
					if (attr != null)
						UpdateIntern((string)fi.GetValue(null), attr.IsPackedContent, true, attr.Extensions);
				}
			}
		} // sctor

		private static void UpdateIntern(string mimeType, bool isCompressedContent, bool replace, string[] extensions)
		{
			if (String.IsNullOrEmpty(mimeType))
				throw new ArgumentNullException(nameof(mimeType));

			// clean extensions
			var cleanExtensions = GetCleanExtensions(extensions);

			// check if mime type exists
			if (mimeTypeIndex.TryGetValue(mimeType, out var idx))
			{
				var oldMapping = mimeTypeMappings[idx];
				if (replace)
				{
					ClearExtensionIndex(oldMapping.Extensions, idx);
					extensions = cleanExtensions.ToArray();
				}
				else
					extensions = oldMapping.Extensions.Union(cleanExtensions).ToArray();

				mimeTypeMappings[idx] = new MimeTypeMapping(mimeType, isCompressedContent, extensions);

				UpdateExtensionIndex(extensions, idx);
			}
			else // add new
			{
				extensions = cleanExtensions.ToArray();

				idx = mimeTypeMappings.Count;

				// add mapping
				var mapping = new MimeTypeMapping(mimeType, isCompressedContent, extensions);
				mimeTypeMappings.Add(mapping);

				mimeTypeIndex[mimeType] = idx;

				UpdateExtensionIndex(extensions, idx);
			}
		} // UpdateIntern

		private static IEnumerable<string> GetCleanExtensions(string[] extensions)
		{
			foreach(var c in extensions)
			{
				// check syntax
				if (!extensionRegEx.IsMatch(c))
					throw new ArgumentOutOfRangeException("ext", c, $"Extension '{c}' is invalid.");
				yield return c;

			}
		} // func GetCleanExtension

		private static void ClearExtensionIndex(string[] extensions, int idx)
		{
			for (var i = 0; i < extensions.Length; i++)
			{
				var key = extensions[i];
				if (extensionIndex.TryGetValue(key, out var currentIdx) && currentIdx == idx)
					extensionIndex.Remove(key);
			}
		} // proc ClearExtensionIndex 

		private static void UpdateExtensionIndex(string[] extensions, int idx)
		{
			for (var i = 0; i < extensions.Length; i++)
			{
				var key = extensions[i];
				if (!extensionIndex.ContainsKey(key))
					extensionIndex[key] = idx;
			}
		} // proc UpdateExtensionIndex

		/// <summary></summary>
		/// <param name="mimeType"></param>
		/// <param name="replace"></param>
		/// <param name="isPackedContent"></param>
		/// <param name="extensions"></param>
		public static void Update(string mimeType, bool isPackedContent, bool replace, params string[] extensions)
		{
			lock (mimeTypeMappings)
				UpdateIntern(mimeType, isPackedContent, replace, extensions);
		} // proc Update

		/// <summary>Find mapping information for a mimeType.</summary>
		/// <param name="mimeType"></param>
		/// <param name="mapping"></param>
		/// <returns></returns>
		public static bool TryGetMapping(string mimeType, out MimeTypeMapping mapping)
		{
			if (mimeTypeIndex.TryGetValue(mimeType, out var idx))
			{
				mapping = mimeTypeMappings[idx];
				return true;
			}
			else
			{
				mapping = null;
				return false;
			}
		} // func TryGetMapping

		/// <summary>Translate a extension to a mime type.</summary>
		/// <param name="extension"></param>
		/// <returns></returns>
		public static string GetMimeTypeFromExtension(string extension)
			=> TryGetMimeTypeFromExtension(extension, out var mimeType) ? mimeType : MimeTypes.Application.OctetStream;

		/// <summary>Translate a extension to a mime type.</summary>
		/// <param name="extension"></param>
		/// <param name="mimeType"></param>
		/// <returns></returns>
		public static bool TryGetMimeTypeFromExtension(string extension, out string mimeType)
		{
			// test for filename
			if (!extensionRegEx.IsMatch(extension))
				extension = Path.GetExtension(extension);

			// search extension
			if (extensionIndex.TryGetValue(extension, out var idx))
			{
				mimeType = mimeTypeMappings[idx].MimeType;
				return true;
			}
			else
			{
				mimeType = null;
				return false;
			}
		} // func TryGetMimeTypeFromExtension

		/// <summary>Translate a mime type to extension.</summary>
		/// <param name="mimeType"></param>
		/// <returns></returns>
		public static string GetExtensionFromMimeType(string mimeType)
			=> TryGetExtensionFromMimeType(mimeType, out var ext) ? ext : ".dat";

		/// <summary>Translate a mime type to extension.</summary>
		/// <param name="mimeType"></param>
		/// <param name="extension"></param>
		/// <returns></returns>
		public static bool TryGetExtensionFromMimeType(string mimeType, out string extension)
		{
			if (TryGetMapping(mimeType, out var mapping) && mapping.Extensions.Length > 0)
			{
				extension = mapping.Extensions.First();
				return true;
			}
			else
			{
				extension = null;
				return false;
			}
		} // func TryGetExtensionFromMimeType

		  /// <summary>Is the content of the mime type text based.</summary>
		  /// <param name="mimeType"></param>
		  /// <returns></returns>
		public static bool GetIsCompressedContent(string mimeType)
			=> TryGetMapping(mimeType, out var mapping) ? mapping.IsCompressedContent : false;

		/// <summary>Return all mappings.</summary>
		public static IEnumerable<MimeTypeMapping> Mappings
		{
			get
			{
				lock(mimeTypeMappings)
				{
					foreach (var m in mimeTypeMappings)
						yield return m;
				}
			}
		} // prop Mappings
	} // class MimeTypeMapping

	#endregion

	#region -- enum ClientAuthentificationType ----------------------------------------

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

	#region -- class ClientAuthentificationInformation --------------------------------

	/// <summary>Parse the client authentificate property.</summary>
	public sealed class ClientAuthentificationInformation
	{
		private const string basicRealmProperty = "Basic realm=";
		private const string integratedSecurity = "Integrated Security";

		private readonly ClientAuthentificationType type;
		private readonly string realm;

		private ClientAuthentificationInformation(string authenticate)
		{
			if (authenticate == null)
			{
				type = ClientAuthentificationType.Unknown;
				realm = "None";
			}
			else if (authenticate.StartsWith(basicRealmProperty, StringComparison.OrdinalIgnoreCase)) // basic network authentification
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

		/// <summary></summary>
		public ClientAuthentificationType Type => type;
		/// <summary></summary>
		public string Realm => realm;

		/// <summary></summary>
		public static ClientAuthentificationInformation Unknown { get; } = new ClientAuthentificationInformation(ClientAuthentificationType.Unknown, "Unknown");
		/// <summary></summary>
		public static ClientAuthentificationInformation Ntlm { get; } = new ClientAuthentificationInformation(ClientAuthentificationType.Ntlm, integratedSecurity);

		/// <summary></summary>
		/// <param name="e"></param>
		/// <param name="info"></param>
		/// <param name="autoDisposeResponse"></param>
		/// <returns></returns>
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

		/// <summary></summary>
		/// <param name="e"></param>
		/// <param name="autoDisposeResponse"></param>
		/// <returns></returns>
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

	#region -- class BaseWebRequest ---------------------------------------------------

	/// <summary></summary>
	public sealed class BaseWebRequest
	{
		private readonly Uri baseUri;
		private readonly Encoding defaultEncoding;
		private readonly ICredentials credentials;

		/// <summary></summary>
		/// <param name="baseUri"></param>
		/// <param name="defaultEncoding"></param>
		/// <param name="credentials"></param>
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
			var pos = contentType.IndexOf(';');
			if (pos == -1)
				mimeType = contentType.Trim();
			else
				mimeType = contentType.Substring(0, pos).Trim();

			// Prüfe den MimeType
			if (acceptedMimeType != null && !mimeType.StartsWith(acceptedMimeType))
				throw new ArgumentException($"Expected: {acceptedMimeType}; received: {mimeType}");

			if (charset)
			{
				var startAt = contentType.IndexOf("charset=");
				if (startAt >= 0)
				{
					startAt += 8;
					var endAt = contentType.IndexOf(';', startAt);
					if (endAt == -1)
						endAt = contentType.Length;

					var charSet = contentType.Substring(startAt, endAt - startAt);
					return Encoding.GetEncoding(charSet);
				}
				else
					return defaultEncoding;
			}
			else
				return null;
		} // func CheckMimeType

		private bool IsCompressed(string contentEncoding)
			=> contentEncoding != null && contentEncoding.IndexOf("gzip") >= 0;

		/// <summary></summary>
		/// <param name="path"></param>
		/// <returns></returns>
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

		#region -- Putxxxx ------------------------------------------------------------

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="writeRequest"></param>
		/// <returns></returns>
		public async Task<WebResponse> PutStreamResponseAsync(string path, Action<WebRequest, Stream> writeRequest)
		{
			var request = GetWebRequest(path);

			// write request stream
			request.Method = HttpMethod.Put.Method;
			writeRequest(request, await request.GetRequestStreamAsync());

			// get the response
			return await request.GetResponseAsync();
		} // proc PutStreamResponseAsync

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="inputContentType"></param>
		/// <param name="writeRequest"></param>
		/// <returns></returns>
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

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="inputContentType"></param>
		/// <param name="writeRequest"></param>
		/// <returns></returns>
		public Task<WebResponse> PutXmlResponseAsync(string path, string inputContentType, Action<XmlWriter> writeRequest)
			=> PutTextResponseAsync(path, inputContentType ?? MimeTypes.Text.Xml,
				tw =>
				{
					using (var xml = XmlWriter.Create(tw, Procs.XmlWriterSettings))
						writeRequest(xml);
				});

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="table"></param>
		/// <returns></returns>
		public Task<WebResponse> PutTableResponseAsync(string path, LuaTable table)
			=> PutXmlResponseAsync(path, MimeTypes.Text.Xml, xml => table.ToXml().WriteTo(xml));

		#endregion

		#region -- GetStream ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="response"></param>
		/// <param name="acceptedMimeType"></param>
		/// <returns></returns>
		public Stream GetStream(WebResponse response, string acceptedMimeType = null)
		{
			CheckMimeType(response.ContentType, acceptedMimeType, false);
			return IsCompressed(response.Headers["Content-Encoding"])
				? new GZipStream(response.GetResponseStream(), CompressionMode.Decompress, false)
				: response.GetResponseStream();
		} // func GetStreamAsync

		/// <summary>Creates a plain Web-Request, special arguments are filled with IWebRequestCreate.</summary>
		/// <param name="path">Resource</param>
		/// <param name="acceptedMimeType">Optional.</param>
		/// <returns></returns>
		public async Task<Stream> GetStreamAsync(string path, string acceptedMimeType = null)
			=> GetStream(await GetResponseAsync(path), acceptedMimeType);

		#endregion

		#region -- GetTextReader ------------------------------------------------------

		/// <summary></summary>
		/// <param name="response"></param>
		/// <param name="acceptedMimeType"></param>
		/// <returns></returns>
		public TextReader GetTextReader(WebResponse response, string acceptedMimeType)
		{
			var enc = CheckMimeType(response.ContentType, acceptedMimeType, true);
			if (IsCompressed(response.Headers["Content-Encoding"]))
				return new StreamReader(new GZipStream(response.GetResponseStream(), CompressionMode.Decompress), enc);
			else
				return new StreamReader(response.GetResponseStream(), enc);
		}// func GetTextReaderAsync

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="acceptedMimeType"></param>
		/// <returns></returns>
		public async Task<TextReader> GetTextReaderAsync(string path, string acceptedMimeType)
			=> GetTextReader(await GetResponseAsync(path), acceptedMimeType);

		#endregion

		#region -- GetXmlStream -------------------------------------------------------

		/// <summary></summary>
		/// <param name="response"></param>
		/// <param name="acceptedMimeType"></param>
		/// <param name="settings"></param>
		/// <returns></returns>
		public XmlReader GetXmlStream(WebResponse response, string acceptedMimeType = MimeTypes.Text.Xml, XmlReaderSettings settings = null)
		{
			if (settings == null)
			{
				settings = new XmlReaderSettings()
				{
					IgnoreComments = acceptedMimeType != MimeTypes.Application.Xaml,
					IgnoreWhitespace = acceptedMimeType != MimeTypes.Application.Xaml
				};
			}
			settings.CloseInput = true;

			var baseUri = response.ResponseUri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.SafeUnescaped);
			var context = new XmlParserContext(null, null, null, null, null, null, baseUri, null, XmlSpace.Default);

			return XmlReader.Create(GetTextReader(response, acceptedMimeType), settings, context);
		} // func GetXmlStream

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="acceptedMimeType"></param>
		/// <param name="settings"></param>
		/// <returns></returns>
		public async Task<XmlReader> GetXmlStreamAsync(string path, string acceptedMimeType = MimeTypes.Text.Xml, XmlReaderSettings settings = null)
		{
			var response = await GetResponseAsync(path); // todo: is disposed called?
			return GetXmlStream(response, acceptedMimeType, settings);
		} // func GetXmlStreamAsync

		#endregion

		#region -- GetXml -------------------------------------------------------------

		/// <summary></summary>
		/// <param name="x"></param>
		/// <returns></returns>
		public XElement CheckForExceptionResult(XElement x)
		{
			var xStatus = x.Attribute("status");
			if (xStatus != null && xStatus.Value != "ok")
			{
				var xText = x.Attribute("text");
				throw new ArgumentException(String.Format("Server returns an error: {0}", xText?.Value ?? "unknown"));
			}
			return x;
		} // func CheckForExceptionResult

		/// <summary></summary>
		/// <param name="response"></param>
		/// <param name="acceptedMimeType"></param>
		/// <param name="rootName"></param>
		/// <returns></returns>
		public XElement GetXml(WebResponse response, string acceptedMimeType = MimeTypes.Text.Xml, XName rootName = null)
		{
			XDocument document;
			using (var xml = GetXmlStream(response, acceptedMimeType, null))
				document = XDocument.Load(xml, LoadOptions.SetBaseUri);
			if (document == null)
				throw new ArgumentException("Keine Antwort vom Server.");

			CheckForExceptionResult(document.Root);

			if (rootName != null && document.Root.Name != rootName)
				throw new ArgumentException(String.Format("Wurzelelement erwartet '{0}', aber '{1}' vorgefunden.", document.Root.Name, rootName));

			return document.Root;
		} // func GetXml

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="acceptedMimeType"></param>
		/// <param name="rootName"></param>
		/// <returns></returns>
		public async Task<XElement> GetXmlAsync(string path, string acceptedMimeType = MimeTypes.Text.Xml, XName rootName = null)
			=> GetXml(await GetResponseAsync(path), acceptedMimeType, rootName);

		#endregion

		#region -- GetTable -----------------------------------------------------------

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="rootName"></param>
		/// <returns></returns>
		public async Task<LuaTable> GetTableAsync(string path, XName rootName = null)
			=> GetTable(await GetResponseAsync(path), rootName);

		/// <summary></summary>
		/// <param name="response"></param>
		/// <param name="rootName"></param>
		/// <returns></returns>
		public LuaTable GetTable(WebResponse response, XName rootName = null)
			=> Procs.CreateLuaTable(CheckForExceptionResult(GetXml(response, rootName: (rootName ?? "table"))));

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="table"></param>
		/// <param name="rootName"></param>
		/// <returns></returns>
		public async Task<LuaTable> PutTableAsync(string path, LuaTable table, XName rootName = null)
			=> GetTable(await PutTableResponseAsync(path, table), rootName);

		#endregion

		#region -- CreateViewDataReader -----------------------------------------------

		#region -- class ViewDataReader -----------------------------------------------

		/// <summary></summary>
		public class ViewDataReader : IEnumerable<IDataRow>
		{
			#region -- class ViewDataColumn -------------------------------------------

			/// <summary></summary>
			private sealed class ViewDataColumn : IDataColumn
			{
				private readonly string name;
				private readonly Type dataType;
				private readonly PropertyDictionary attributes;

				#region -- Ctor/Dtor --------------------------------------------------

				public ViewDataColumn(string name, Type dataType, PropertyDictionary attributes)
				{
					this.name = name;
					this.dataType = dataType;
					this.attributes = attributes;
				} // ctor

				#endregion

				#region -- IDataColumn ------------------------------------------------

				public string Name => name;
				public Type DataType => dataType;
				public IPropertyEnumerableDictionary Attributes => attributes;

				#endregion
			} // class ViewDataColumn

			#endregion

			#region -- class ViewDataRow ----------------------------------------------

			///////////////////////////////////////////////////////////////////////////////
			/// <summary></summary>
			private sealed class ViewDataRow : DynamicDataRow
			{
				private readonly ViewDataEnumerator enumerator;
				private readonly object[] columnValues;

				#region -- Ctor/Dtor --------------------------------------------------

				public ViewDataRow(ViewDataEnumerator enumerator, object[] columnValues)
				{
					this.enumerator = enumerator;
					this.columnValues = columnValues;
				} // ctor

				#endregion

				#region -- IDataRow ---------------------------------------------------

				public override IReadOnlyList<IDataColumn> Columns => enumerator.Columns;
				public override object this[int index] => columnValues[index];

				public override bool IsDataOwner => true;

				#endregion
			} // class ViewDataRow

			#endregion

			#region -- class ViewDataEnumerator ---------------------------------------

			/// <summary></summary>
			private sealed class ViewDataEnumerator : IEnumerator<IDataRow>, IDataColumns
			{
				#region -- enum ReadingState ------------------------------------------

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

				#region -- Ctor/Dtor --------------------------------------------------

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

				#region -- IEnumerator ------------------------------------------------

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

				#region -- IDataColumns -----------------------------------------------

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

			#region -- Ctor/Dtor ------------------------------------------------------

			/// <summary></summary>
			/// <param name="request"></param>
			/// <param name="path"></param>
			/// <param name="acceptedMimeType"></param>
			public ViewDataReader(BaseWebRequest request, string path, string acceptedMimeType = MimeTypes.Text.Xml)
			{
				this.request = request;
				this.path = path;
				this.acceptedMimeType = acceptedMimeType;
			} // ctor

			#endregion

			#region -- IEnumerable ----------------------------------------------------

			/// <summary></summary>
			/// <returns></returns>
			public IEnumerator<IDataRow> GetEnumerator()
				=> new ViewDataEnumerator(this);

			IEnumerator IEnumerable.GetEnumerator()
				=> GetEnumerator();

			#endregion
		} // class ViewDataReader

		#endregion

		/// <summary></summary>
		/// <param name="path"></param>
		/// <param name="acceptedMimeType"></param>
		/// <returns></returns>
		public IEnumerable<IDataRow> CreateViewDataReader(string path, string acceptedMimeType = MimeTypes.Text.Xml)
			=> new ViewDataReader(this, path, acceptedMimeType);

		#endregion

		/// <summary></summary>
		public Uri BaseUri => baseUri;
		/// <summary></summary>
		public Encoding DefaultEncoding => defaultEncoding;
		/// <summary></summary>
		public ICredentials Credentials => credentials;
	} // class BaseWebReqeust

	#endregion
}
