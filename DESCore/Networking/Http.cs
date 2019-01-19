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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Data;
using TecWare.DE.Stuff;

namespace TecWare.DE.Networking
{
	#region -- enum DEHttpReturnState -------------------------------------------------

	/// <summary>State field of an request.</summary>
	public enum DEHttpReturnState
	{
		/// <summary>Unknown state.</summary>
		None,
		/// <summary>No errors. Action is commited.</summary>
		Ok,
		/// <summary>User friendly error. Action is rollbacked.</summary>
		User,
		/// <summary>Fatal error. Action is rollbacked.</summary>
		Error
	} // enum DEHttpReturnState

	#endregion

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
			public const string Lson = "text/x-lson";
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
		public static bool TryGet(HttpResponseException e, ref ClientAuthentificationInformation info, bool autoDisposeResponse = true)
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
		/// <returns></returns>
		public static ClientAuthentificationInformation Get(HttpResponseException e)
		{
			// get the response
			return e.StatusCode == HttpStatusCode.Unauthorized
				? new ClientAuthentificationInformation(
					e.Headers != null && e.Headers.TryGetValues("WWW-Authenticate", out var values)
						? String.Join(",", values)
						: null
				)
				: null;
		} // func Get


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
				return r.StatusCode == HttpStatusCode.Unauthorized
					? new ClientAuthentificationInformation(r.Headers["WWW-Authenticate"])
					: null;
			}
			finally
			{
				if (autoDisposeResponse)
					r.Dispose();
			}
		} // func Get
	} // class ClientAuthentificationInformation

	#endregion

	#region -- enum DEHttpTableFormat -------------------------------------------------

	/// <summary>Text format to represent a lua table.</summary>
	public enum DEHttpTableFormat
	{
		/// <summary>Use xml (can only used within de-server environment).</summary>
		Xml,
		/// <summary>Use lson (compatible to all lua dialects).</summary>
		Lson,
		/// <summary>Use json (compatible to json, but looses context)</summary>
		Json
	} // enum DEHttpTableFormat

	#endregion

	#region -- class DEHttpSocketBase -------------------------------------------------

	/// <summary>Socket communication base class for data exchange server sockets</summary>
	public abstract class DEHttpSocketBase : IDisposable
	{
		private readonly Uri serverUri;
		private readonly ICredentials credentials;
		private readonly CancellationTokenSource sessionDisposeSource;
		private bool isDisposing = false;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="serverUri"></param>
		/// <param name="credentials"></param>
		protected DEHttpSocketBase(Uri serverUri, ICredentials credentials)
		{
			var scheme = serverUri.Scheme;
			if (scheme == "http" || scheme == "https") // rewrite uri
				this.serverUri = new Uri((scheme == "https" ? "wss" : "ws") + "://" + serverUri.Host + ":" + serverUri.Port + "/" + serverUri.AbsolutePath);
			else // use uri
				this.serverUri = serverUri;

			this.credentials = credentials;
			this.sessionDisposeSource = new CancellationTokenSource();
		} // ctor

		/// <summary>Dispose current session.</summary>
		public void Dispose()
		{
			Dispose(true);
		} // proc Dispose

		/// <summary></summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				isDisposing = true;

				lock (socketLock)
				{
					if (clientSocket != null && clientSocket.State == WebSocketState.Open)
						Task.Run(() => clientSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None)).Wait();
				}

				if (!sessionDisposeSource.IsCancellationRequested)
					sessionDisposeSource.Cancel();
				sessionDisposeSource.Dispose();
			}
		} // proc Dispose

		#endregion

		#region -- Communication ------------------------------------------------------

		private readonly object socketLock = new object();
		private ClientWebSocket clientSocket = null;
		private CancellationToken currentConnectionToken = CancellationToken.None;

		/// <summary>Start socket.</summary>
		public void Start()
			=> Task.Run(() => RunProtocolAsync()).ContinueWith(t => OnCommunicationExceptionAsync(t.Exception).Wait(),TaskContinuationOptions.OnlyOnFaulted);

		/// <summary>Main loop for the debug session, that runs the protocol handlers.</summary>
		/// <returns></returns>
		public async Task RunProtocolAsync()
		{
			var recvOffset = 0;
			var recvBuffer = new byte[1 << 20];
			var lastNativeErrorCode = Int32.MinValue;
			var currentConnectionTokenSource = (CancellationTokenSource)null;
			var sessionDisposeToken = sessionDisposeSource.Token;

			while (!isDisposing && !sessionDisposeToken.IsCancellationRequested)
			{
				var connectionEstablished = false;

				// create the connection
				var socket = new ClientWebSocket();
				socket.Options.Credentials = credentials;
				socket.Options.SetRequestHeader("des-multiple-authentifications", "true");
				socket.Options.AddSubProtocol(SubProtocol);

				#region -- connect --
				try
				{
					await socket.ConnectAsync(serverUri, sessionDisposeToken);
					connectionEstablished = true;
					lock (socketLock)
					{
						clientSocket = socket;

						currentConnectionTokenSource = new CancellationTokenSource();
						currentConnectionToken = currentConnectionTokenSource.Token;
					}
					await OnConnectionEstablishedAsync();
				}
				catch (WebSocketException e)
				{
					if (lastNativeErrorCode != e.NativeErrorCode) // connect exception
					{
						if (!await OnConnectionFailureAsync(e))
							lastNativeErrorCode = e.NativeErrorCode;
					}
				}
				catch (TaskCanceledException)
				{
				}
				catch (Exception e)
				{
					lastNativeErrorCode = Int32.MinValue;
					await OnConnectionFailureAsync(e);
				}
				#endregion

				try
				{
					// reconnect set use
					if (socket.State == WebSocketState.Open)
						await OnConnectedAsync(socket, sessionDisposeToken);

					// wait for answers
					recvOffset = 0;
					while (socket.State == WebSocketState.Open && !sessionDisposeToken.IsCancellationRequested)
					{
						// check if the buffer is large enough
						var recvRest = recvBuffer.Length - recvOffset;
						if (recvRest == 0)
						{
							await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message to big.", sessionDisposeToken);
							break;
						}

						// receive the characters
						var r = await socket.ReceiveAsync(new ArraySegment<byte>(recvBuffer, recvOffset, recvRest), sessionDisposeToken);
						if (r.MessageType == WebSocketMessageType.Text)
						{
							recvOffset += r.Count;
							if (r.EndOfMessage)
							{
								try
								{
									await OnProcessMessageAsync(recvBuffer, 0, recvOffset);
								}
								catch (Exception e)
								{
									lastNativeErrorCode = Int32.MinValue;
									await OnCommunicationExceptionAsync(e);
								}
								recvOffset = 0;
							}
						}
					} // message loop
				}
				catch (WebSocketException e)
				{
					if (!isDisposing)
					{
						lastNativeErrorCode = e.NativeErrorCode;
						await OnCommunicationExceptionAsync(e);
					}
				}
				catch (TaskCanceledException)
				{
				}

				// close connection
				if (!isDisposing && connectionEstablished)
					await OnConnectionLostAsync();

				lock (socketLock)
				{
					clientSocket = null;

					// dispose current cancellation token
					if (currentConnectionTokenSource != null)
					{
						try { currentConnectionTokenSource.Cancel(); }
						catch { }
						currentConnectionTokenSource.Dispose();
						currentConnectionTokenSource = null;
					}

					currentConnectionToken = CancellationToken.None;
				}
				socket.Dispose();
			}
		} // func RunProtocolAsync

		/// <summary>Prints a message to the debug console.</summary>
		/// <param name="message"></param>
		protected virtual void DebugPrint(string message)
			=> Debug.Print(message);

		/// <summary>Gets called on connection, or reconnection</summary>
		protected virtual Task OnConnectedAsync(ClientWebSocket socket, CancellationToken cancellationToken)
			=> Task.CompletedTask;

		/// <summary>Gets called on communication exception.</summary>
		/// <param name="e"></param>
		protected virtual Task OnCommunicationExceptionAsync(Exception e)
		{
			DebugPrint($"Connection failed: {e}");
			return Task.CompletedTask;
		} // proc OnCommunicationExceptionAsync

		/// <summary>Event if the connection gets lost.</summary>
		protected virtual Task OnConnectionLostAsync()
		{
			DebugPrint("Connection lost.");
			return Task.CompletedTask;
		} // proc OnConnectionLostAsync 

		/// <summary>Event if the connection is established.</summary>
		protected virtual Task OnConnectionEstablishedAsync()
		{
			DebugPrint("Connection established.");
			return Task.CompletedTask;
		} // proc OnConnectionEstablishedAsync

		/// <summary>Connection failed.</summary>
		/// <param name="e"></param>
		/// <returns><c>true</c>, for exception handled.</returns>
		protected virtual Task<bool> OnConnectionFailureAsync(Exception e)
		{
			DebugPrint($"Connection failed: {e}");
			return Task.FromResult(false);
		} // proc OnConnectionFailureAsync

		/// <summary>Process incoming message</summary>
		/// <param name="recvBuffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected abstract Task OnProcessMessageAsync(byte[] recvBuffer, int offset, int count);

		/// <summary></summary>
		/// <param name="timeout"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		protected CancellationTokenSource GetSendCancellationTokenSource(int timeout, CancellationToken cancellationToken)
		{
			CancellationTokenSource cancellationTokenSource = null;
			if (cancellationToken == CancellationToken.None || cancellationToken == currentConnectionToken)
			{
				if (timeout > 0)
				{
					cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(currentConnectionToken);
					cancellationTokenSource.CancelAfter(timeout);

					cancellationToken = cancellationTokenSource.Token;
				}
				else
				{
					cancellationTokenSource = null;
					cancellationToken = currentConnectionToken;
				}
			}
			else
			{
				cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(currentConnectionToken, cancellationToken);
				cancellationToken = cancellationTokenSource.Token;
			}

			return cancellationTokenSource;
		} // func GetSendCancellationTokenSource

		/// <summary></summary>
		/// <param name="socket"></param>
		/// <returns></returns>
		protected bool TryGetSocket(out ClientWebSocket socket)
		{
			lock (socketLock)
			{
				socket = clientSocket;
				return socket != null && socket.State == WebSocketState.Open;
			}
		} // func TryGetSocket

		/// <summary></summary>
		/// <returns></returns>
		protected ClientWebSocket GetSocket()
			=> TryGetSocket(out var s) ? s : throw new ArgumentException("SocketSession is disconnected.");

		/// <summary>Return the sub-protocol for the socket.</summary>
		protected abstract string SubProtocol { get; }

		/// <summary>Is client socket connected.</summary>
		public bool IsConnected
		{
			get
			{
				lock (socketLock)
					return clientSocket != null && clientSocket.State == WebSocketState.Open;
			}
		} // prop IsConnected

		#endregion
	} // class DEHttpSocket

	#endregion

	#region -- class HttpResponseException --------------------------------------------

	/// <summary></summary>
	public class HttpResponseException : Exception
	{
		/// <summary></summary>
		/// <param name="statusCode"></param>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		public HttpResponseException(HttpStatusCode statusCode, string message, Exception innerException = null)
			: base(message, innerException)
		{
			Headers = null;
			StatusCode = statusCode;
		} // ctor

		/// <summary></summary>
		/// <param name="response"></param>
		public HttpResponseException(HttpResponseMessage response)
			: base((response ?? throw new ArgumentNullException(nameof(response))).ReasonPhrase, null)
		{
			Headers = response.Headers;
			StatusCode = response.StatusCode;
		} // ctor

		/// <summary></summary>
		public HttpStatusCode StatusCode { get; }
		/// <summary>Optional response headers</summary>
		public HttpHeaders Headers { get; }
	} // class HttpResponseException

	#endregion

	#region -- class HttpUserResponseException ----------------------------------------

	/// <summary>Exception, that is returned marked as user</summary>
	public class HttpUserResponseException : Exception, ILuaUserRuntimeException
	{
		/// <summary></summary>
		/// <param name="message"></param>
		/// <param name="innerException"></param>
		public HttpUserResponseException(string message, Exception innerException = null)
			: base(message, innerException)
		{
		}
	} // class HttpUserResponseException

	#endregion

	#region -- class DEHttpClient -----------------------------------------------------

	/// <summary>Extension of HttpClient</summary>
	public class DEHttpClient : HttpClient
	{
		#region -- class UnpackStreamContent ------------------------------------------

		private sealed class UnpackStreamContent : HttpContent
		{
			private readonly HttpContent innerContent;

			public UnpackStreamContent(HttpContent innerContent)
			{
				this.innerContent = innerContent ?? throw new ArgumentNullException(nameof(innerContent));

				foreach (var c in innerContent.Headers)
					Headers.Add(c.Key, c.Value);
			} // ctor

			protected override void Dispose(bool disposing)
			{
				innerContent.Dispose();
				base.Dispose(disposing);
			} // proc Dispose

			protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
			{
				using (var src = new GZipStream(await innerContent.ReadAsStreamAsync(), CompressionMode.Decompress))
					await src.CopyToAsync(stream);
			} // func SerializeToStreamAsync

			protected override bool TryComputeLength(out long length)
			{
				length = 0;
				return false;
			} // func TryComputeLength
		} // class UnpackStreamContent

		#endregion

		#region -- class DEClientHandler ----------------------------------------------

		private sealed class DEClientHandler : MessageProcessingHandler
		{
			public DEClientHandler(HttpMessageHandler innerHandler)
				: base(innerHandler)
			{
			}

			protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
				=> request;

			protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
			{
				if (response.Content.Headers.ContentEncoding.Contains("gzip")) // result is packed, unpack
					response.Content = new UnpackStreamContent(response.Content);
				return response;
			} // func ProcessResponse
		} // class DEClientHandler

		#endregion

		#region -- Ctor/Dtor ----------------------------------------------------------

		private DEHttpClient(DEClientHandler messageHandler, ICredentials credentials, Uri baseUri, Encoding defaultEncoding = null)
			: base(messageHandler, true)
		{
			this.Credentials = credentials;

			DefaultEncoding = defaultEncoding ?? Procs.Utf8Encoding;
			BaseAddress = CheckBaseUri(baseUri);

			// add add encoding
			DefaultRequestHeaders.AcceptCharset.Add(new StringWithQualityHeaderValue(DefaultEncoding.WebName));
			DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
			DefaultRequestHeaders.Add("des-multiple-authentifications", "true");
		} // ctor

		#endregion

		#region -- Helper -------------------------------------------------------------

		/// <summary></summary>
		/// <param name="path"></param>
		/// <returns></returns>
		public Uri CreateFullUri(string path)
		{
			Uri uri;

			if (path == null)
				uri = BaseAddress;
			else
			{
				uri = new Uri(path, UriKind.RelativeOrAbsolute);
				if (!uri.IsAbsoluteUri && BaseAddress != null)
					uri = new Uri(BaseAddress, uri);
			}

			if (uri == null)
				throw new ArgumentNullException(nameof(path), "BaseAddress and path can not be null.");

			return uri;
		} // func GetFullUri

		#endregion

		#region -- GetResponseAsync ---------------------------------------------------

		/// <summary>Core http request handler, that supports error handling on http-base.</summary>
		/// <param name="requestUri">Relative request uri.</param>
		/// <param name="acceptedMimeType">Accepted mime types, that will send to the server.</param>
		/// <param name="putContent">Content of a http put.</param>
		/// <returns></returns>
		public async Task<HttpResponseMessage> GetResponseAsync(string requestUri, string acceptedMimeType, HttpContent putContent = null)
		{
			var httpPut = putContent != null;
			var request = new HttpRequestMessage(httpPut ? HttpMethod.Put : HttpMethod.Get, requestUri);
			if (acceptedMimeType != null)
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptedMimeType));
			if (httpPut)
				request.Content = putContent;

			var response = await SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

			if (!response.IsSuccessStatusCode)
			{
				try
				{
					throw new HttpResponseException(response);
				}
				finally
				{
					response.Dispose();
				}
			}
			return response;
		} // func GetResponseAsync

		#endregion

		#region -- GetxxxxAsync -------------------------------------------------------

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <returns></returns>
		public Task<TextReader> GetTextReaderAsync(string requestUri)
			=> GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead).GetTextReaderAsync();

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <param name="acceptedMimeType"></param>
		/// <param name="settings"></param>
		/// <returns></returns>
		public Task<XmlReader> GetXmlReaderAsync(string requestUri, string acceptedMimeType = MimeTypes.Text.Xml, XmlReaderSettings settings = null)
			=> GetResponseAsync(requestUri, acceptedMimeType).GetXmlStreamAsync(acceptedMimeType, settings);

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <param name="acceptedMimeType"></param>
		/// <param name="rootName"></param>
		/// <returns></returns>
		public Task<XElement> GetXmlAsync(string requestUri, string acceptedMimeType = MimeTypes.Text.Xml, XName rootName = null)
			=> GetResponseAsync(requestUri, acceptedMimeType).GetXmlAsync(acceptedMimeType, rootName);

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <returns></returns>
		public Task<LuaTable> GetTableAsync(string requestUri)
			=> GetResponseAsync(requestUri, MimeTypes.Text.Lson).GetTableAsync();

		#endregion

		#region -- PutxxxxAsync -------------------------------------------------------

		/// <summary></summary>
		/// <param name="content"></param>
		/// <param name="inputMimeType"></param>
		/// <returns></returns>
		public HttpContent CreateStringContent(string content, string inputMimeType)
		{
			if (content.Length > 256)
			{
				using (var dst = new MemoryStream(content.Length / 2))
				using (var gz = new GZipStream(dst, CompressionMode.Compress))
				using (var sw = new StreamWriter(gz, DefaultEncoding))
				{
					sw.Write(content);
					sw.Close();
					gz.Close();
					var cnt = new ByteArrayContent(dst.ToArray());
					cnt.Headers.ContentType = new MediaTypeHeaderValue(inputMimeType ?? MimeTypes.Text.Plain) { CharSet = DefaultEncoding.WebName };
					cnt.Headers.ContentEncoding.Add("gzip");
					return cnt;
				}
			}
			else
				return new StringContent(content, DefaultEncoding, inputMimeType ?? MimeTypes.Text.Plain);
		} // proc CreateStringContent

		/// <summary>Put a xml writer.</summary>
		/// <param name="requesturi"></param>
		/// <param name="xmlWriter"></param>
		/// <param name="inputMimeType"></param>
		/// <param name="acceptedMimeType"></param>
		/// <returns></returns>
		public async Task<HttpResponseMessage> PutResponseXmlAsync(string requesturi, Action<XmlWriter> xmlWriter, string inputMimeType = null, string acceptedMimeType = null)
		{
			using (var sw = new StringWriter())
			using (var xw = XmlWriter.Create(sw))
			{
				await Task.Run(() => xmlWriter(xw));
				xw.Flush();
				sw.Flush();
				return await PutResponseTextAsync(requesturi, sw.GetStringBuilder().ToString(), inputMimeType ?? MimeTypes.Text.Xml, acceptedMimeType ?? MimeTypes.Text.Xml);
			}
		} // func PutXmlAsync

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <param name="x"></param>
		/// <param name="inputMimeType"></param>
		/// <param name="acceptedMimeType"></param>
		/// <returns></returns>
		public async Task<HttpResponseMessage> PutResponseXmlAsync(string requestUri, XDocument x, string inputMimeType = null, string acceptedMimeType = null)
		{
			using (var sw = new StringWriter())
			{
				await Task.Run(() => x.Save(sw));
				sw.Flush();
				return await PutResponseTextAsync(requestUri, sw.GetStringBuilder().ToString(), inputMimeType ?? MimeTypes.Text.Xml, acceptedMimeType ?? MimeTypes.Text.Xml);
			}
		} // func PutXmlAsync

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <param name="content"></param>
		/// <param name="inputMimeType"></param>
		/// <param name="acceptedMimeType"></param>
		/// /// <returns></returns>
		public Task<HttpResponseMessage> PutResponseTextAsync(string requestUri, string content, string inputMimeType = null, string acceptedMimeType = null)
			=> GetResponseAsync(requestUri, acceptedMimeType, CreateStringContent(content, inputMimeType));

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <param name="table"></param>
		/// <param name="tableFormat"></param>
		/// <returns></returns>
		public Task<HttpResponseMessage> PutResponseTableAsync(string requestUri, LuaTable table, DEHttpTableFormat tableFormat = DEHttpTableFormat.Lson)
		{
			switch (tableFormat)
			{
				case DEHttpTableFormat.Xml:
					return PutResponseXmlAsync(requestUri, new XDocument(table.ToXml()), MimeTypes.Text.Xml, MimeTypes.Text.Xml);
				case DEHttpTableFormat.Lson:
					return PutResponseTextAsync(requestUri, table.ToLson(false), MimeTypes.Text.Lson, MimeTypes.Text.Lson);
				case DEHttpTableFormat.Json:
					return PutResponseTextAsync(requestUri, table.ToJson(false), MimeTypes.Text.Json, MimeTypes.Text.Json);
				default:
					throw new ArgumentOutOfRangeException(nameof(tableFormat), tableFormat, "Invalid table format.");
			}
		} // func PutResponseTableAsync

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <param name="table"></param>
		/// <param name="tableFormat"></param>
		/// <returns></returns>
		public Task<LuaTable> PutTableAsync(string requestUri, LuaTable table, DEHttpTableFormat tableFormat = DEHttpTableFormat.Lson)
		{
			switch (tableFormat)
			{
				case DEHttpTableFormat.Xml:
					return PutResponseXmlAsync(requestUri, new XDocument(table.ToXml()), MimeTypes.Text.Xml, MimeTypes.Text.Xml).GetTableAsync();
				case DEHttpTableFormat.Lson:
					return PutResponseTextAsync(requestUri, table.ToLson(false), MimeTypes.Text.Lson, MimeTypes.Text.Lson).GetTableAsync();
				case DEHttpTableFormat.Json:
					return PutResponseTextAsync(requestUri, table.ToJson(false), MimeTypes.Text.Json, MimeTypes.Text.Json).GetTableAsync();
				default:
					throw new ArgumentOutOfRangeException(nameof(tableFormat), tableFormat, "Invalid table format.");
			}
		} // func PutResponseTableAsync

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
							xml = owner.request.GetXmlReaderAsync(owner.path, owner.acceptedMimeType).Result;

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

			private readonly DEHttpClient request;
			private readonly string path;
			private readonly string acceptedMimeType;

			#region -- Ctor/Dtor ------------------------------------------------------

			/// <summary></summary>
			/// <param name="request"></param>
			/// <param name="path"></param>
			/// <param name="acceptedMimeType"></param>
			public ViewDataReader(DEHttpClient request, string path, string acceptedMimeType = MimeTypes.Text.Xml)
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

		/// <summary>Default encoding, when no encoding is givven.</summary>
		public Encoding DefaultEncoding { get; }
		/// <summary></summary>
		public ICredentials Credentials { get; }

		/// <summary>Create new http client.</summary>
		/// <param name="baseUri">Base uri for the request.</param>
		/// <param name="credentials">Optional credentials</param>
		/// <param name="defaultEncoding">Default encoding.</param>
		/// <param name="httpHandler">Defines a http client handler.</param>
		public static DEHttpClient Create(Uri baseUri, ICredentials credentials = null, Encoding defaultEncoding = null, HttpClientHandler httpHandler = null)
		{
			if (httpHandler == null)
				httpHandler = GetDefaultMessageHandler();
			if (credentials != null)
				httpHandler.Credentials = credentials;

			try
			{
				return new DEHttpClient(new DEClientHandler(httpHandler), credentials, baseUri, defaultEncoding);
			}
			catch
			{
				httpHandler.Dispose();
				throw;
			}
		} // func Create

		private static Uri CheckBaseUri(Uri baseUri)
		{
			if (baseUri == null)
				throw new ArgumentNullException(nameof(baseUri));
			if (!baseUri.IsAbsoluteUri)
				throw new ArgumentException("Absolute uri expected.", nameof(baseUri));

			return baseUri;
		} // func CheckBaseUri

		/// <summary>Get default message handler.</summary>
		/// <returns></returns>
		public static HttpClientHandler GetDefaultMessageHandler()
			=> new HttpClientHandler();
	} // class DEHttpClient

	#endregion

	#region -- class HttpStuff --------------------------------------------------------

	/// <summary></summary>
	public static class HttpStuff
	{
		/// <summary>Check if the content.type is equal to the expected content-type</summary>
		/// <param name="response"></param>
		/// <param name="acceptedMimeType"></param>
		/// <returns></returns>
		public static HttpResponseMessage CheckMimeType(HttpResponseMessage response, string acceptedMimeType)
		{
			if (acceptedMimeType == null)
				return response;

			var mediaType = response.Content.Headers.ContentType?.MediaType;
			if (mediaType != acceptedMimeType)
				throw new ArgumentException($"Expected: {acceptedMimeType}; received: {mediaType}");

			return response;
		} // func CheckMimeType

		private static Encoding GetEncodingFromCharset(string charSet)
		{
			if (String.IsNullOrEmpty(charSet))
				return null;

			return Encoding.GetEncoding(charSet);
		} //func GetEncodingFromCharset

		/// <summary></summary>
		/// <param name="value"></param>
		/// <param name="state"></param>
		/// <returns></returns>
		public static bool TryParseReturnState(string value, out DEHttpReturnState state)
		{
			if (value == null || String.Compare(value, "ok", StringComparison.OrdinalIgnoreCase) == 0)
			{
				state = DEHttpReturnState.Ok;
				return true;
			}
			else if (String.Compare(value, "user", StringComparison.OrdinalIgnoreCase) == 0)
			{
				state = DEHttpReturnState.User;
				return true;
			}
			else if (String.Compare(value, "error", StringComparison.OrdinalIgnoreCase) == 0)
			{
				state = DEHttpReturnState.Error;
				return true;
			}
			else
			{
				state = DEHttpReturnState.Error;
				return false;
			}
		} // func TryParseReturnState

		/// <summary></summary>
		/// <param name="state"></param>
		/// <returns></returns>
		public static string FormatReturnState(DEHttpReturnState state)
		{
			switch(state)
			{
				case DEHttpReturnState.Ok:
					return "ok";
				case DEHttpReturnState.User:
					return "user";
				case DEHttpReturnState.Error:
					return "error";
				default:
					throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown state value.");
			}
		} // func FormatReturnState

		private static void CheckForExceptionResult(string stateValue, string text)
		{
			if (TryParseReturnState(stateValue, out var state) && state != DEHttpReturnState.Ok)
			{
				if (String.IsNullOrEmpty(text))
					text = "unknown message";

				if (state == DEHttpReturnState.User)
					throw new HttpUserResponseException(text);
				else
					throw new HttpResponseException(HttpStatusCode.InternalServerError, text);
			}
		} // proc CheckForExceptionResult

		/// <summary></summary>
		/// <param name="x"></param>
		/// <param name="rootName"></param>
		/// <returns></returns>
		public static XElement CheckForExceptionResult(XElement x, XName rootName = null)
		{
			if (x == null)
				throw new ArgumentNullException(nameof(x), "No result parsed.");

			CheckForExceptionResult(x.Attribute("status")?.Value, x.Attribute("text")?.Value);

			if (rootName != null && x.Name != rootName)
				throw new ArgumentOutOfRangeException(nameof(rootName), x.Name, $"Root element expected '{rootName}', but '{x.Name}' found.");

			return x;
		} // func CheckForExceptionResult

		/// <summary></summary>
		/// <param name="t"></param>
		/// <returns></returns>
		public static LuaTable CheckForExceptionResult(LuaTable t)
		{
			if (t == null)
				throw new ArgumentNullException(nameof(t), "No result parsed.");

			CheckForExceptionResult(t.GetMemberValue("status", rawGet: true) as string, t.GetMemberValue("text", rawGet: true) as string);

			return t;
		} // func CheckForExceptionResult

		private static async Task<TextReader> GetTextReaderAsync(HttpResponseMessage response)
		{
			var enc = GetEncodingFromCharset(response.Content.Headers.ContentType?.CharSet);
			return new StreamReader(await response.Content.ReadAsStreamAsync(), enc ?? Encoding.UTF8, enc != null, 1024, false);
		} // func GetTextReaderAsync

		private static async Task<XmlReader> GetXmlStreamAsync(HttpResponseMessage response, string acceptedMimeType, XmlReaderSettings settings)
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

			var baseUri = response.RequestMessage.RequestUri.GetComponents(UriComponents.Scheme | UriComponents.Host | UriComponents.Port | UriComponents.Path, UriFormat.SafeUnescaped);
			var context = new XmlParserContext(null, null, null, null, null, null, baseUri, null, XmlSpace.Default);

			return XmlReader.Create(await GetTextReaderAsync(CheckMimeType(response, acceptedMimeType)), settings, context);
		} // func GetXmlStreamAsync

		private static async Task<XElement> GetXmlAsync(HttpResponseMessage response, string acceptedMimeType, XName rootName)
		{
			using (var xml = await GetXmlStreamAsync(response, acceptedMimeType, null))
			{
				var xDoc = await Task.Run(() => XDocument.Load(xml, LoadOptions.SetBaseUri));
				return CheckForExceptionResult(xDoc?.Root, rootName);
			}
		} // func GetXmlAsync

		private static async Task<LuaTable> GetTableAsync(HttpResponseMessage response)
		{
			using (var tr = await GetTextReaderAsync(response))
				return CheckForExceptionResult(LuaTable.FromLson(tr));

		} // func GetTableAsync

		private static async Task<LuaTable> GetJsonTableAsync(HttpResponseMessage response)
		{
			using (var tr = await GetTextReaderAsync(response))
				return CheckForExceptionResult(LuaTable.FromJson(tr));
		} // func GetJsonTableAsync

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="acceptedMimeType"></param>
		/// <returns></returns>
		public static async Task<TextReader> GetTextReaderAsync(this Task<HttpResponseMessage> t, string acceptedMimeType = null)
			=> await GetTextReaderAsync(CheckMimeType(await t, acceptedMimeType));

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="rootName"></param>
		/// <returns></returns>
		public static async Task<LuaTable> GetTableAsync(this Task<HttpResponseMessage> t, XName rootName = null)
		{
			var r = await t;
			if (r.Content.Headers.ContentType?.MediaType == MimeTypes.Text.Lson)
				return await GetTableAsync(r);
			else if (r.Content.Headers.ContentType?.MediaType == MimeTypes.Text.Json)
				return await GetJsonTableAsync(r);
			else if (r.Content.Headers.ContentType?.MediaType == MimeTypes.Text.Xml)
				return Procs.CreateLuaTable(await GetXmlAsync(r, MimeTypes.Text.Xml, rootName));
			else
				throw new ArgumentException();
		} // func GetTableAsync

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="acceptedMimeType"></param>
		/// <param name="settings"></param>
		/// <returns></returns>
		public static async Task<XmlReader> GetXmlStreamAsync(this Task<HttpResponseMessage> t, string acceptedMimeType = MimeTypes.Text.Xml, XmlReaderSettings settings = null)
		{
			using (var r = await t)
				return await GetXmlStreamAsync(r, acceptedMimeType, settings);
		} // func GetXmlStreamAsync

		/// <summary></summary>
		/// <param name="t"></param>
		/// <param name="acceptedMimeType"></param>
		/// <param name="rootName"></param>
		/// <returns></returns>
		public static async Task<XElement> GetXmlAsync(this Task<HttpResponseMessage> t, string acceptedMimeType = MimeTypes.Text.Xml, XName rootName = null)
		{
			using (var r = await t)
				return await GetXmlAsync(r, acceptedMimeType, rootName);
		} // func GetXmlAsync

		/// <summary></summary>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public static string MakeRelativeUri(params PropertyValue[] arguments)
			=> MakeRelativeUri(String.Empty, (IEnumerable<PropertyValue>)arguments);

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public static string MakeRelativeUri(string requestUri, params PropertyValue[] arguments)
			=> MakeRelativeUri(requestUri, (IEnumerable<PropertyValue>)arguments);

		/// <summary></summary>
		/// <param name="requestUri"></param>
		/// <param name="arguments"></param>
		/// <returns></returns>
		public static string MakeRelativeUri(string requestUri, IEnumerable<PropertyValue> arguments)
		{
			var sb = new StringBuilder();
			if (!String.IsNullOrEmpty(requestUri))
				sb.Append(requestUri);

			var firstAdded = requestUri.Contains('?');

			foreach(var a in arguments)
			{
				if (firstAdded)
					sb.Append('&');
				else
				{
					sb.Append('?');
					firstAdded = true;
				}

				if (a.Value == null)
					continue;
				sb.Append(Uri.EscapeUriString(a.Name))
					.Append('=');

				sb.Append(a.Value.ChangeType<string>());
			}

			return sb.ToString();
		} // func MakeRelativeUri
	} // func HttpStuff

	#endregion
}