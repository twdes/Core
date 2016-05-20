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

            // set network login information
            request.Credentials = credentials;

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

		public async Task<XElement> GetXmlAsync(string path, string acceptedMimeType = MimeTypes.Text.Xml, XName rootName = null)
		{
            XDocument document;
            using (var xmlReader = await GetXmlStreamAsync(path))
                document = XDocument.Load(xmlReader, LoadOptions.SetBaseUri);
            if (document == null)
                throw new ArgumentException("Keine Antwort vom Server.");

            // Wurzelelement prüfen
            if (rootName != null && document.Root.Name != rootName)
                throw new ArgumentException(String.Format("Wurzelelement erwartet '{0}', aber '{1}' vorgefunden.", document.Root.Name, rootName));

            return document.Root;
        } // func GetXmlAsync

        #region -- CreateViewDataReader ---------------------------------------------------

        #region -- class ViewDataReader ---------------------------------------------------

        ///////////////////////////////////////////////////////////////////////////////
        /// <summary></summary>
        public class ViewDataReader : IEnumerable<IDataRow>
        {
            #region -- class ViewDataRow ------------------------------------------------------

            ///////////////////////////////////////////////////////////////////////////////
            /// <summary></summary>
            private sealed class ViewDataRow : IDataRow, IDynamicMetaObjectProvider
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

                public bool TryGetProperty(string name, out object value)
                {
                    value = null;

                    if (String.IsNullOrEmpty(name))
                        return false;

                    if (enumerator == null)
                        return false;

                    if (ColumnNames == null || ColumnNames.Length != ColumnCount)
                        return false;

                    if (columnValues == null || columnValues.Length != ColumnCount)
                        return false;

                    var index = Array.FindIndex(ColumnNames, c => String.Compare(c, name, StringComparison.Ordinal) == 0);
                    if (index == -1)
                        return false;

                    value = columnValues[index];
                    return true;
                } // func TryGetProperty

                public object this[int index] => columnValues[index];

                public string[] ColumnNames => enumerator.ColumnNames;
                public Type[] ColumnTypes => enumerator.ColumnTypes;
                public int ColumnCount => enumerator.ColumnCount;

                public object this[string columnName] => Array.Find(ColumnNames, c => String.Compare(c, columnName, StringComparison.OrdinalIgnoreCase) == 0);

                #endregion

                #region -- IDynamicMetaObjectProvider ---------------------------------------------

                public DynamicMetaObject GetMetaObject(Expression parameter)
                {
                    throw new NotImplementedException();
                } // func GetMetaObject

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
                    Unread,
                    Partly,
                    Complete,
                } // enum ReadingState

                #endregion

                private readonly XmlReader xml;
                private string[] columnNames;
                private Type[] columnTypes;
                private int columnCount;
                private ReadingState state;
                private ViewDataRow currentRow;

                #region -- Ctor/Dtor --------------------------------------------------------------

                public ViewDataEnumerator(BaseWebRequest owner, string path, string acceptedMimeType)
                {
                    xml = owner.GetXmlStreamAsync(path, acceptedMimeType).Result;
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

                public bool MoveNext()
                {
                    switch (state)
                    {
                        #region -- ReadingState.Unread --
                        case ReadingState.Unread:
                            xml.Read();
                            if (xml.NodeType == XmlNodeType.XmlDeclaration)
                                xml.Read();

                            if (xml.NodeType != XmlNodeType.Element || String.Compare(xml.LocalName, "view", StringComparison.Ordinal) != 0)
                                throw new InvalidDataException(string.Format("Expected \"view\" ({0}), read \"{1}\" ({2}).", XmlNodeType.Element, xml.LocalName, xml.NodeType));

                            xml.Read();
                            if (xml.NodeType != XmlNodeType.Element || String.Compare(xml.LocalName, "fields", StringComparison.Ordinal) != 0)
                                throw new InvalidDataException(string.Format("Expected \"view\" ({0}), read \"{1}\" ({2}).", XmlNodeType.Element, xml.LocalName, xml.NodeType));

                            var names = new List<string>();
                            var types = new List<Type>();
                            var fields = (XElement)XNode.ReadFrom(xml);
                            foreach (XElement field in fields.DescendantNodes())
                            {
                                names.Add(field.Name.LocalName);
                                types.Add(LuaType.GetType(field.Attribute("type").Value).Type);
                            }

                            if (names.Count < 1)
                                throw new InvalidDataException("No header found.");

                            columnNames = names.ToArray();
                            columnTypes = types.ToArray();
                            columnCount = columnNames.Length;

                            if (xml.NodeType != XmlNodeType.Element || String.Compare(xml.LocalName, "rows", StringComparison.Ordinal) != 0)
                                throw new InvalidDataException(string.Format("Expected \"rows\" ({0}), read \"{1}\" ({2}).", XmlNodeType.Element, xml.LocalName, xml.NodeType));

                            if (xml.IsEmptyElement)
                            {
                                xml.Read();
                                if (xml.NodeType != XmlNodeType.EndElement || String.Compare(xml.LocalName, "view", StringComparison.Ordinal) != 0)
                                    throw new InvalidDataException(string.Format("Expected \"view\" ({0}), read \"{1}\" ({2}).", XmlNodeType.EndElement, xml.LocalName, xml.NodeType));

                                xml.Read();
                                if (!xml.EOF)
                                    throw new InvalidDataException("Unexpected eof.");

                                state = ReadingState.Complete;
                                goto case ReadingState.Complete;
                            }
                            else
                            {
                                xml.Read();
                                state = ReadingState.Partly;
                                goto case ReadingState.Partly;
                            }
                        #endregion
                        #region -- ReadingState.Partly --
                        case ReadingState.Partly:
                            if (xml.NodeType != XmlNodeType.Element || String.Compare(xml.LocalName, "r", StringComparison.Ordinal) != 0)
                                throw new InvalidDataException(string.Format("Expected \"r\" ({0}), read \"{1}\" ({2}).", XmlNodeType.Element, xml.LocalName, xml.NodeType));

                            var values = new object[columnCount];

                            if (!xml.IsEmptyElement)
                            {
                                var rowData = (XElement)XNode.ReadFrom(xml);
                                foreach (var column in rowData.DescendantNodes())
                                {
                                    if (column.NodeType != XmlNodeType.Element)
                                        continue;

                                    var element = (XElement)column;
                                    if (element.IsEmpty)
                                        continue;

                                    var columnIndex = Array.FindIndex(columnNames, c => String.Compare(c, element.Name.LocalName, StringComparison.Ordinal) == 0);
                                    if (columnIndex != -1)
                                        values[columnIndex] = Procs.ChangeType(element.Value, columnTypes[columnIndex]);
                                }
                            } // if xml.IsEmptyElement
                            else
                                // Without a call to XNode.ReadFrom() it's necessary to read to the next node.
                                xml.Read();

                            currentRow = new ViewDataRow(this, values);

                            if (xml.NodeType == XmlNodeType.Element && String.Compare(xml.LocalName, "r", StringComparison.Ordinal) == 0)
                                return true;

                            if (xml.NodeType != XmlNodeType.EndElement || String.Compare(xml.LocalName, "rows", StringComparison.Ordinal) != 0)
                                throw new InvalidDataException(string.Format("Expected \"rows\" ({0}), read \"{1}\" ({2}).", XmlNodeType.EndElement, xml.LocalName, xml.NodeType));

                            xml.Read();
                            if (xml.NodeType != XmlNodeType.EndElement || String.Compare(xml.LocalName, "view", StringComparison.Ordinal) != 0)
                                throw new InvalidDataException(string.Format("Expected \"view\" ({0}), read \"{1}\" ({2}).", XmlNodeType.EndElement, xml.LocalName, xml.NodeType));

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

                void IEnumerator.Reset()
                {
                    if (state != ReadingState.Unread)
                        throw new InvalidOperationException("The state of the object forbids the calling of this method.");
                } // proc Reset

                public IDataRow Current => currentRow;
                object IEnumerator.Current => Current;

                #endregion

                #region -- IDataColumns -----------------------------------------------------------

                public string[] ColumnNames => columnNames;
                public Type[] ColumnTypes => columnTypes;
                public int ColumnCount => columnCount;

                #endregion
            } // class ViewDataEnumerator

            #endregion

            private readonly BaseWebRequest owner;
            private readonly string path;
            private readonly string acceptedMimeType;

            #region -- Ctor/Dtor --------------------------------------------------------------

            public ViewDataReader(BaseWebRequest owner, string path, string acceptedMimeType = MimeTypes.Text.Xml)
            {
                this.owner = owner;
                this.path = path;
                this.acceptedMimeType = acceptedMimeType;
            } // ctor

            #endregion

            #region -- IEnumerable ------------------------------------------------------------

            public IEnumerator<IDataRow> GetEnumerator()
                => new ViewDataEnumerator(owner, path, acceptedMimeType);

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
