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
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Neo.IronLua;
using TecWare.DE.Stuff;

namespace TecWare.DE.Networking
{
	#region -- class DebugMemberValue -------------------------------------------------

	/// <summary></summary>
	public sealed class DebugMemberValue
	{
		private readonly string name;
		private readonly string typeName;
		private readonly Type type; // is null if the value is not converted
		private readonly object coreValue;
		private readonly Lazy<object> value;
		
		internal DebugMemberValue(string name, string typeName, Type type, object coreValue)
		{
			this.name = name;
			this.typeName = typeName;
			this.type = type;
			this.coreValue = coreValue;
			this.value = new Lazy<object>(ConvertType);
		} // ctor

		private object ConvertType()
		{
			if (IsValueArray || IsValueList)
				return coreValue;
			else if (type == null || coreValue == null)
				return null;

			try
			{
				return Procs.ChangeType(coreValue, type);
			}
			catch
			{
				return null;
			}
		} // func ConvertType


		/// <summary>Name of the member</summary>
		public string Name => name;
		/// <summary>Type name of the member.</summary>
		public string TypeName => typeName;

		/// <summary>Is type converted to client type.</summary>
		public bool IsConverted => type != null;

		/// <summary>Type of the member</summary>
		public Type Type => type ?? typeof(string);

		/// <summary>Value</summary>
		public object Value => value.Value;
		/// <summary>Value as string</summary>
		public string ValueAsString => coreValue == null ? "-NULL-" : (coreValue is string s ? s : "-VALUE-");

		/// <summary>Is value a list.</summary>
		public bool IsValueList => coreValue is DebugMemberValue[][];
		/// <summary>Is value a array.</summary>
		public bool IsValueArray => coreValue is DebugMemberValue[];
		
		internal static Type GetType(string typeString)
		{
			var lastIndex = typeString.LastIndexOfAny(new char[] { ']', ',' });
			if (lastIndex == -1 || typeString[lastIndex] == ']')
				return LuaType.GetType(typeString);
			else
			{
				return Type.GetType(typeString,
					name =>
					{
						// do not load new assemblies
						var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(c => c.FullName == name.FullName);
						if (asm == null)
							throw new TypeLoadException("Assembly is not loaded.");
						return asm;
					},
					(asm, name, ignorecase) => LuaType.GetType(typeString).Type,
					false
				);
			}
		} // func GetType

		/// <summary></summary>
		/// <param name="name"></param>
		/// <param name="typeName"></param>
		/// <param name="coreValue"></param>
		/// <returns></returns>
		public static DebugMemberValue Create(string name, string typeName, object coreValue)
			=> new DebugMemberValue(name, typeName, GetType(typeName), coreValue);
	} // class DebugMemberValue

	#endregion

	#region -- class DebugSocketException ---------------------------------------------

	/// <summary>Debug socket exception, that was catched on the server site.</summary>
	public sealed class DebugSocketException : Exception
	{
		private readonly string remoteStackTrace;
		private readonly string exceptionType;
		private readonly DebugSocketException[] innerExceptions;

		/// <summary></summary>
		/// <param name="x"></param>
		internal DebugSocketException(XElement x)
			: base(x.GetAttribute("message", "No message"))
		{
			this.exceptionType = x.GetAttribute("type", "Exception");
			this.remoteStackTrace = x.Element("stackTrace")?.Value;

			this.innerExceptions =
			(
				from c in x.Elements("innerException")
				select new DebugSocketException(c)
			).ToArray();
		} // ctor

		/// <summary>Type of the exception.</summary>
		public string ExceptionType => exceptionType;
		/// <summary>Stacktrace of the exception</summary>
		public override string StackTrace => remoteStackTrace;
		/// <summary>List of inner exceptions</summary>
		public IReadOnlyList<DebugSocketException> InnerExceptions => innerExceptions;
	} // class DebugSocketException

	#endregion

	#region -- class DebugExecuteResult -----------------------------------------------

	/// <summary>Result of a executed script.</summary>
	public sealed class DebugExecuteResult : IEnumerable<DebugMemberValue>
	{
		private readonly long compileTime;
		private readonly long runTime;
		private readonly IEnumerable<DebugMemberValue> result;

		internal DebugExecuteResult(long compileTime, long runTime, IEnumerable<DebugMemberValue> result)
		{
			this.compileTime = compileTime;
			this.runTime = runTime;
			this.result = result ?? throw new ArgumentNullException(nameof(result));
		} // ctor

		/// <summary>Result of the script</summary>
		/// <returns></returns>
		public IEnumerator<DebugMemberValue> GetEnumerator()
			=> result.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator()
			=> result.GetEnumerator();

		/// <summary>Compile time of script</summary>
		public long CompileTime => compileTime;
		/// <summary>Run time of the script</summary>
		public long RunTime => runTime;
	} // class DebugExecuteResult

	#endregion

	#region -- class DebugRunScriptResult ---------------------------------------------

	/// <summary>Run multiple scripts result.</summary>
	public sealed class DebugRunScriptResult
	{
		#region -- class Test -----------------------------------------------------------

		/// <summary>Test result</summary>
		public sealed class Test
		{
			private readonly Script script;
			private readonly string test;
			private readonly long duration;
			private readonly bool success;
			private readonly DebugSocketException exception;

			internal Test(Script script, XElement x, bool withException)
			{
				this.script = script;

				this.test = x.GetAttribute("name", "<noname>");
				this.success = x.GetAttribute("success", true);
				this.duration = x.GetAttribute("time", -1L);

				this.exception = withException && !success ? new DebugSocketException(x) : null;
			} // ctor

			/// <summary>Format the test-result.</summary>
			/// <returns></returns>
			public DebugMemberValue[] Format()
			{
				return new DebugMemberValue[]
				  {
					new DebugMemberValue("Script", "string", typeof(string), script?.ScriptId),
					new DebugMemberValue("Test", "string", typeof(string), test),
					new DebugMemberValue("Success", "bool", typeof(bool), success),
					new DebugMemberValue("Duration", "long", typeof(long), duration),
					new DebugMemberValue("Message", "string", typeof(string), exception?.Message),
				  };
			} // func Format
			
			/// <summary>Script of the test.</summary>
			public Script Script => script;

			/// <summary>Name of the test.</summary>
			public string Name => test;
			/// <summary>Run time.</summary>
			public long Duration => duration;
			/// <summary>Basic result</summary>
			public bool Success => success;

			/// <summary>Exception on fail.</summary>
			public DebugSocketException Exception => exception;
		} // class Test

		#endregion

		#region -- class Script ---------------------------------------------------------

		/// <summary>Executed test-script</summary>
		public sealed class Script
		{
			private readonly string scriptId;
			private readonly bool success;
			private readonly long compileTime;
			private readonly long runTime;

			private readonly Lazy<int> passedTests;

			private readonly DebugSocketException exception;

			private readonly Test[] tests;

			internal Script(XElement x, bool withException)
			{
				this.scriptId = x.GetAttribute("id", "<noname>");
				this.success = x.GetAttribute("success", true);
				this.compileTime = x.GetAttribute("compileTime", -1L);
				this.runTime = x.GetAttribute("runTime", -1L);

				this.exception = withException && !success ? new DebugSocketException(x) : null;

				tests = (
					from c in x.Elements("test")
					select new Test(this, c, withException)
				).ToArray();

				passedTests = new Lazy<int>(() => tests.Sum(c => c.Success ? 1 : 0));
			} // ctor

			/// <summary>Format the script-result.</summary>
			/// <returns></returns>
			public DebugMemberValue[] Format()
			{
				return new DebugMemberValue[]
				  {
					new DebugMemberValue("Script", "string", typeof(string), scriptId),
					new DebugMemberValue("Success", "bool", typeof(bool), success),
					new DebugMemberValue("Passed", "int", typeof(int), Passed),
					new DebugMemberValue("Failed", "int", typeof(int), Failed),
					new DebugMemberValue("Message", "string", typeof(string), exception?.Message),
				  };
			} // func Format

			/// <summary>Tests in the script</summary>
			public IEnumerable<Test> Tests => tests;

			/// <summary>Id of the script</summary>
			public string ScriptId => scriptId;
			/// <summary>Compile time</summary>
			public long CompileTime => compileTime;
			/// <summary>Total run time.</summary>
			public long RunTime => runTime;
			/// <summary>All test passed.</summary>
			public bool Success => success;

			/// <summary>Number of successful tests.</summary>
			public int Passed => passedTests.Value;
			/// <summary>Number of failed tests.</summary>
			public int Failed => tests.Length - Passed;

			/// <summary>Exception (e.g. compile)</summary>
			public DebugSocketException Exception => exception;
		} // class Script

		#endregion

		private readonly Script[] scripts;

		internal DebugRunScriptResult(XElement xReturn)
		{
			scripts =
			(
				from x in xReturn.Elements("script")
				select new Script(x, true)
			).ToArray();
		} // ctor

		/// <summary>Return all tests of all scripts.</summary>
		public IEnumerable<Test> AllTests
		{
			get
			{
				foreach (var s in scripts)
					foreach (var t in s.Tests)
						yield return t;
			}
		} // prop AllTests

		/// <summary>Test-scripts executed</summary>
		public IEnumerable<Script> Scripts => scripts;
	} // class DebugRunScriptResult

	#endregion

	#region -- DebugSocket ------------------------------------------------------------

	/// <summary></summary>
	public abstract class DebugSocket : DEHttpSocketBase
	{
		#region -- class ReturnWait ---------------------------------------------------

		private sealed class ReturnWait
		{
			private readonly int token;
			private readonly TaskCompletionSource<XElement> source;

			public ReturnWait(int token, CancellationToken cancellationToken)
			{
				this.token = token;
				this.source = new TaskCompletionSource<XElement>();
				cancellationToken.Register(() => source.TrySetCanceled());
			} // ctor

			public int Token => token;
			public TaskCompletionSource<XElement> Source => source;
		} // class ReturnWait

		#endregion

		/// <summary>Notify use path change.</summary>
		public event EventHandler CurrentUsePathChanged;

		private readonly Random random = new Random(Environment.TickCount);

		private string currentUsePath = null;
		private int currentUseToken = -1;

		private int defaultTimeout = 0;

		#region -- Ctor/Dtor ----------------------------------------------------------

		/// <summary></summary>
		/// <param name="http"></param>
		protected DebugSocket(DEHttpClient http)
			: base(new Uri(http.BaseAddress, "luaengine"), http.Credentials)
		{
		} // ctor

		/// <summary></summary>
		/// <param name="serverUri"></param>
		/// <param name="credentials"></param>
		protected DebugSocket(Uri serverUri, ICredentials credentials)
			: base(serverUri, credentials)
		{
		} // ctor

		#endregion

		#region -- Protocol implementation --------------------------------------------

		private readonly Dictionary<int, ReturnWait> waits = new Dictionary<int, ReturnWait>();

		/// <summary></summary>
		/// <param name="socket"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		protected sealed override async Task OnConnectedAsync(ClientWebSocket socket, CancellationToken cancellationToken)
		{
			if (currentUsePath != null && currentUsePath != "/" && currentUsePath.Length > 0)
				currentUseToken = (int)await SendAsync(socket, GetUseMessage(currentUsePath), false, cancellationToken);
			else
				CurrentUsePath = "/";
			await base.OnConnectedAsync(socket, cancellationToken);
		} // proc OnConnectedAsync

		/// <summary></summary>
		/// <param name="recvBuffer"></param>
		/// <param name="offset"></param>
		/// <param name="count"></param>
		/// <returns></returns>
		protected sealed override Task OnProcessMessageAsync(byte[] recvBuffer, int offset, int count)
		{
			OnProcessAnswer(XElement.Parse(Encoding.UTF8.GetString(recvBuffer, offset, count)));
			return Task.CompletedTask;
		} // proc OnProcessMessageAsync

		private void OnProcessAnswer(XElement x)
		{
			var token = x.GetAttribute("token", 0);
			DebugPrint($"[Client] Receive Message: {token}");
			if (token != 0) // answer
			{
				if (currentUseToken == token) // was the use command successful
				{
					if (x.Name == "exception")
						CurrentUsePath = "/";
					else
						CurrentUsePath = GetUsePathFromReturn(x);
					currentUseToken = -1;
				}
				else // other messages
				{
					var w = GetWait(token);
					if (w != null && !w.Source.Task.IsCompleted)
					{
						if (x.Name == "exception")
							ThreadPool.QueueUserWorkItem(s => w.Source.SetException(new DebugSocketException(x)), null);
						else
							ThreadPool.QueueUserWorkItem(s => w.Source.SetResult(x), null);
					}
				}
			}
			else // notify
			{
				if (x.Name == "log")
				{
					var t = x.Attribute("type")?.Value;
					OnMessage(String.IsNullOrEmpty(t) ? 'D' : Char.ToUpper(t[0]), x.Value);
				}
				else if (x.Name == "script")
					OnStartScript(new DebugRunScriptResult.Script(x, false), x.GetAttribute("message", String.Empty));
				else if (x.Name == "test")
					OnTestResult(new DebugRunScriptResult.Test(null, x, false), x.GetAttribute("message", String.Empty));
			}
		} // proc OnProcessAnswer

		private ReturnWait GetWait(int token)
		{
			lock (waits)
			{
				if (waits.TryGetValue(token, out var w))
				{
					waits.Remove(token);
					return w;
				}
				else
					return null;
			}
		} // func GetWait

		private TaskCompletionSource<XElement> RegisterAnswer(int token, CancellationToken cancellationToken)
		{
			var w = new ReturnWait(token, cancellationToken);
			lock (waits)
				waits[token] = w;
			return w.Source;
		} // proc RegisterAnswer

		private void UnregisterAnswer(int token)
		{
			lock (waits)
				waits.Remove(token);
		} // proc UnregisterAnswer

		/// <summary></summary>
		/// <param name="type"></param>
		/// <param name="message"></param>
		protected abstract void OnMessage(char type, string message);

		/// <summary></summary>
		/// <param name="script"></param>
		/// <param name="message"></param>
		protected virtual void OnStartScript(DebugRunScriptResult.Script script, string message)
			=> OnMessage('I', $">> Script: {script.ScriptId} | {(script.Success ? "OK" : "Err")} <<");

		/// <summary></summary>
		/// <param name="test"></param>
		/// <param name="message"></param>
		protected virtual void OnTestResult(DebugRunScriptResult.Test test, string message)
			=> OnMessage('I', $">> Test: {test.Duration} | {(test.Success ? "OK" : message ?? "Err")} <<");

		/// <summary>Subprotocol is always dedbg</summary>
		protected sealed override string SubProtocol => "dedbg";

		#endregion

		#region -- Send Answer --------------------------------------------------------

		/// <summary></summary>
		/// <param name="xMessage"></param>
		/// <returns></returns>
		protected Task<XElement> SendAsync(XElement xMessage)
			=> SendAsync(xMessage, CancellationToken.None);

		/// <summary></summary>
		/// <param name="xMessage"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		protected async Task<XElement> SendAsync(XElement xMessage, CancellationToken cancellationToken)
		{
			var socket = GetSocket();

			// send and wait for answer
			var cancellationTokenSource = GetSendCancellationTokenSource(defaultTimeout, cancellationToken);

			var completionSource = (TaskCompletionSource<XElement>)await SendAsync(socket, xMessage, true, cancellationToken);
			if (cancellationTokenSource == null)
				return await completionSource.Task;
			else
			{
				return await completionSource.Task.ContinueWith(
					t =>
					{
						cancellationTokenSource.Dispose();
						return t.Result;
					}
				);
			}
		} // proc Send

		private async Task<object> SendAsync(ClientWebSocket socket, XElement xMessage, bool registerAnswer, CancellationToken cancellationToken)
		{
			// add token for the answer
			var token = random.Next(1, Int32.MaxValue);
			xMessage.SetAttributeValue("token", token);
			var cancellationSource = (TaskCompletionSource<XElement>)null;

			if (registerAnswer)
				cancellationSource = RegisterAnswer(token, cancellationToken);

			// send message to server
			try
			{
				var messageBytes = Encoding.UTF8.GetBytes(xMessage.ToString(SaveOptions.None));
				Debug.Print("[Client] Send Message: {0}", token);
				await socket.SendAsync(new ArraySegment<byte>(messageBytes, 0, messageBytes.Length), WebSocketMessageType.Text, true, cancellationToken);
			}
			catch
			{
				UnregisterAnswer(token);
				throw;
			}

			return (object)cancellationSource ?? token;
		} // proc SendAsync

		#endregion

		#region -- GetMemberValue, ParseReturn ----------------------------------------

		private static DebugMemberValue GetMemberValue(XElement x, int index)
		{
			// get the member name or index
			var member = x.GetAttribute("n", String.Empty);
			if (String.IsNullOrEmpty(member))
				member = "$" + x.GetAttribute("i", index).ToString();

			// get type
			var typeString = x.GetAttribute("t", "object");
			var contentType = x.GetAttribute("ct", typeString);
			var type = typeString == "table" ? null : DebugMemberValue.GetType(typeString);

			// check if the value is convertible (only convert core types)
			object value;
			if (contentType == "table" || contentType == "row") // table, row
				value = ParseReturn(x, 1).ToArray();
			else if (contentType == "rows")
			{
				var xFields = x.Element("f");
				if (xFields != null)
				{
					var j = 0;
					var columns = ( // elementName, fieldName, typeString, type
						from xField in xFields.Elements()
						let n = xField.GetAttribute("n", (j++).ToString())
						let t = xField.GetAttribute("t", "object")
						select new Tuple<string, string, string, Type>(xField.Name.LocalName, n, t, DebugMemberValue.GetType(t))
					).ToArray();

					var rows = new List<DebugMemberValue[]>();
					foreach (var xRow in x.Elements("r"))
					{
						var values = new DebugMemberValue[columns.Length];

						for (var i = 0; i < columns.Length; i++)
						{
							var col = columns[i];
							var xValue = xRow.Element(col.Item1);
							values[i] = new DebugMemberValue(col.Item2, col.Item3, col.Item4, xValue == null || xValue.IsEmpty ? null : xValue.Value);
						}

						rows.Add(values);
					}

					value = rows.ToArray();
				}
				else
					value = null;
			}
			else
				value = x.IsEmpty ? null : x.Value;

			return new DebugMemberValue(member, typeString, type, value);
		} // func GetMemberValue

		private static IEnumerable<DebugMemberValue> ParseReturn(XElement r, int index = 0)
			 => from c in r.Elements("v")
				select GetMemberValue(c, index++);

		#endregion

		#region -- RunScript ----------------------------------------------------------

		/// <summary>Run test script.</summary>
		/// <param name="scriptFilter">Filter scripts, that should be run.</param>
		/// <param name="methodFilter">Method filter</param>
		/// <returns></returns>
		public Task<DebugRunScriptResult> RunScriptAsync(string scriptFilter = null, string methodFilter = null)
			=> RunScriptAsync(scriptFilter, methodFilter, CancellationToken.None);

		/// <summary>Run test script.</summary>
		/// <param name="scriptFilter">Filter scripts, that should be run.</param>
		/// <param name="methodFilter">Method filter</param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<DebugRunScriptResult> RunScriptAsync(string scriptFilter, string methodFilter, CancellationToken cancellationToken)
		{
			var x = await SendAsync(new XElement("run",
				new XAttribute("script", scriptFilter ?? "*"),
				new XAttribute("method", methodFilter ?? "*"))
			);
			return new DebugRunScriptResult(x);
		} // func RunScriptAsync

		#endregion

		#region -- Recompile ----------------------------------------------------------

		/// <summary>Recompile all scripts.</summary>
		/// <returns></returns>
		public Task<IEnumerable<(string scriptId, bool failed)>> RecompileAsync()
			=> RecompileAsync(CancellationToken.None);

		/// <summary>Recompile all scripts.</summary>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<IEnumerable<(string scriptId, bool failed)>> RecompileAsync(CancellationToken cancellationToken)
		{
			var x = await SendAsync(new XElement("recompile"));
			return
				from c in x.Elements("r")
				let scriptId = c.GetAttribute("id", "error")
				let failed = c.GetAttribute("failed", false)
				select (scriptId, failed);
		} // func RecompileAsync

		#endregion

		#region -- Use ----------------------------------------------------------------

		/// <summary>Change current node</summary>
		/// <param name="node"></param>
		/// <returns></returns>
		public Task<string> UseAsync(string node)
			=> UseAsync(node, CancellationToken.None);

		/// <summary>Change current node</summary>
		/// <param name="node"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<string> UseAsync(string node, CancellationToken cancellationToken)
		{
			// send the use command
			var r = await SendAsync(
				GetUseMessage(node),
				cancellationToken
			);

			// get the new use path
			CurrentUsePath = GetUsePathFromReturn(r);
			return CurrentUsePath;
		} // proc UseAsync

		private static XElement GetUseMessage(string node)
			=> new XElement("use", new XAttribute("node", node));

		private static string GetUsePathFromReturn(XElement r)
			=> r.GetAttribute("node", "/");

		/// <summary>Called if the use path is changed.</summary>
		protected virtual void OnCurrentUsePathChanged()
			=> CurrentUsePathChanged?.Invoke(this, EventArgs.Empty);

		/// <summary>Current active path.</summary>
		public string CurrentUsePath
		{
			get => currentUsePath;
			private set
			{
				if (currentUsePath != value)
				{
					currentUsePath = value;
					OnCurrentUsePathChanged();
				}
			}
		} // prop CurrentUsePath

		#endregion

		#region -- Execute ------------------------------------------------------------

		/// <summary>Execute a lua scriptlet on the server.</summary>
		/// <param name="command"></param>
		/// <returns></returns>
		public Task<DebugExecuteResult> ExecuteAsync(string command)
			=> ExecuteAsync(command, CancellationToken.None);

		/// <summary>Execute a lua scriptlet on the server.</summary>
		/// <param name="command"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<DebugExecuteResult> ExecuteAsync(string command, CancellationToken cancellationToken)
		{
			var r = await SendAsync(
				new XElement("execute",
					new XText(command)
				),
				cancellationToken
			);

			return new DebugExecuteResult(
				r.GetAttribute("compileTime", -1L),
				r.GetAttribute("runTime", -1L),
				ParseReturn(r)
			);
		} // proc CommandAsync

		#endregion

		#region -- GlobalVars ---------------------------------------------------------

		/// <summary>Return all members</summary>
		/// <param name="memberPath"></param>
		/// <returns></returns>
		public Task<IEnumerable<DebugMemberValue>> MembersAsync(string memberPath)
			=> MembersAsync(memberPath, CancellationToken.None);

		/// <summary>Return all members</summary>
		/// <param name="memberPath"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<IEnumerable<DebugMemberValue>> MembersAsync(string memberPath, CancellationToken cancellationToken)
			=> ParseReturn(await SendAsync(new XElement("member"), cancellationToken));

		#endregion

		#region -- List ---------------------------------------------------------------

		/// <summary>List nodes of the crrent path.</summary>
		/// <param name="recursive"></param>
		/// <returns></returns>
		public Task<XElement> ListAsync(bool recursive = false)
			=> ListAsync(recursive, CancellationToken.None);

		/// <summary>List nodes of the crrent path.</summary>
		/// <param name="recursive"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public async Task<XElement> ListAsync(bool recursive, CancellationToken cancellationToken)
			=> await SendAsync(new XElement("list", new XAttribute("r", recursive)), cancellationToken);

		#endregion

		#region -- Scope --------------------------------------------------------------

		private string GetScopeUserName(XElement x)
			=> x.GetAttribute<string>("user", "none");

		/// <summary>Begin new transaction scope.</summary>
		/// <returns>The scope user name.</returns>
		public Task<string> BeginScopeAsync()
			=> BeginScopeAsync(CancellationToken.None);

		/// <summary>Begin new transaction scope.</summary>
		/// <param name="cancellationToken"></param>
		/// <returns>The scope user name.</returns>
		public async Task<string> BeginScopeAsync(CancellationToken cancellationToken)
			=> GetScopeUserName(await SendAsync(new XElement("scopeBegin")));

		/// <summary>Rollback current scope.</summary>
		/// <returns>The scope user name.</returns>
		public Task<string> RollbackScopeAsync()
			=> RollbackScopeAsync(CancellationToken.None);

		/// <summary>Rollback current scope.</summary>
		/// <param name="cancellationToken"></param>
		/// <returns>The scope user name.</returns>
		public async Task<string> RollbackScopeAsync(CancellationToken cancellationToken)
			=> GetScopeUserName(await SendAsync(new XElement("scopeRollback")));

		/// <summary>Commit current scope.</summary>
		/// <returns>The scope user name.</returns>
		public Task<string> CommitScopeAsync()
			=> CommitScopeAsync(CancellationToken.None);

		/// <summary>Commit current scope.</summary>
		/// <param name="cancellationToken"></param>
		/// <returns>The scope user name.</returns>
		public async Task<string> CommitScopeAsync(CancellationToken cancellationToken)
			=> GetScopeUserName(await SendAsync(new XElement("scopeCommit")));

		#endregion

		/// <summary>Default timeout for socket send requests</summary>
		public int DefaultTimeout
		{
			get { return defaultTimeout; }
			set { defaultTimeout = value < 0 ? 0 : value; }
		} // prop DefaultTimeout
	} // class DebugSocket

	#endregion
}
